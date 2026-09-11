using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Le quattro categorie d'aeroporto dal lato dell'<b>archivio</b> (carta
/// <c>docs/feature/2026-09-11-categorie-aeroporto.md</c>): il travaso d'avvio dal booleano in pensione, il comando
/// che tiene l'invariante, e la lettura della Diagnostica sui documenti fuori categoria.
///
/// <para>⚠️ Il travaso è la parte che non si può riparare dopo: gira da solo all'avvio in produzione, e fino al
/// 16 settembre 2026 nessuno può rimettere a posto il database. Si prova sui casi che l'archivio vero contiene
/// (misurati sul <c>vipi.db</c> di sviluppo: 6 solo militari, 2 con presenza e vSOP, 26 con presenza e basta).</para>
/// </summary>
public class CategorieAeroportoTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private Acc _acc = default!;

    private sealed class AllowAuthz : IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public VipiRole Role => VipiRole.Admin;
        public int? CurrentUserId => 42;
        public string? CurrentName => "test";
    }

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _acc = new Acc { Code = "LIPP", Name = "Padova" };
        _db.Accs.Add(_acc);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private async Task<Airport> Campo(string icao, bool presenza, AirportCategory categoria = AirportCategory.Civil)
    {
        var a = new Airport { Icao = icao, Name = icao, Acc = _acc, HasMilitaryPresence = presenza, Category = categoria };
        _db.Airports.Add(a);
        await _db.SaveChangesAsync();
        return a;
    }

    /// <summary>Un documento vero, legato allo scalo nel verso giusto: civile su <c>DocumentId</c>, militare su
    /// <c>MilDocumentId</c>. Nasce come nascono tutti (<see cref="Persistence.Seed.DocumentBirth"/>).</summary>
    private async Task<int> Documento(Airport a, DocumentEdition edizione)
    {
        var (doc, _) = Persistence.Seed.DocumentBirth.Crea(_db, new AiracService(), $"doc {a.Icao}", Language.It,
            edizione == DocumentEdition.Military ? Vipi.Application.Content.SectionProfile.AirportMil
                                                 : Vipi.Application.Content.SectionProfile.Airport,
            42, conSegnaposto: false);
        doc.Edition = edizione;
        await _db.SaveChangesAsync();
        if (edizione == DocumentEdition.Military) a.MilDocumentId = doc.Id; else a.DocumentId = doc.Id;
        await _db.SaveChangesAsync();
        return doc.Id;
    }

    /// <summary>
    /// Mette una riga nello stato in cui la lascia la migrazione: categoria al valore di nascita della colonna
    /// (<c>Civil</c>) e specchio in pensione com'era. Scritto in SQL apposta — il setter di <c>Category</c>
    /// riscriverebbe lo specchio, e la riga non sarebbe quella che la migrazione lascia.
    /// </summary>
    private async Task ComeDopoLaMigrazione(string icao, bool soloMilitare)
    {
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE Airports SET Category = 'Civil', IsMilitaryOnly = {0} WHERE Icao = {1}", soloMilitare, icao);
        _db.ChangeTracker.Clear();
    }

    private Task<Airport> Rileggi(string icao) => _db.Airports.AsNoTracking().SingleAsync(a => a.Icao == icao);

    // ---- Il travaso d'avvio --------------------------------------------------------------------------

    [Fact]
    public async Task Il_travaso_porta_ogni_riga_alla_sua_categoria()
    {
        await Campo("LIPA", presenza: true);
        var liml = await Campo("LIML", presenza: true);
        await Campo("LIMC", presenza: true);
        await Campo("LIPZ", presenza: false);
        await Documento(liml, DocumentEdition.Military);
        await ComeDopoLaMigrazione("LIPA", soloMilitare: true);
        await ComeDopoLaMigrazione("LIML", soloMilitare: false);
        await ComeDopoLaMigrazione("LIMC", soloMilitare: false);

        var cambiati = await new EfDocumentMaintenance(_db).ReconcileAirportCategoriesAsync();

        Assert.Equal(3, cambiati);
        Assert.Equal(AirportCategory.MilitaryOnly, (await Rileggi("LIPA")).Category);
        // ⚠️ Chi ha già un vSOP va in 4: nessun documento esistente finisce fuori categoria per il travaso.
        Assert.Equal(AirportCategory.MilitaryWithCivilPresence, (await Rileggi("LIML")).Category);
        Assert.Equal(AirportCategory.CivilWithMilitaryPresence, (await Rileggi("LIMC")).Category);
        Assert.Equal(AirportCategory.Civil, (await Rileggi("LIPZ")).Category);
    }

    /// <summary>Gira a ogni avvio: al secondo giro non deve toccare niente — né la scelta di una persona.</summary>
    [Fact]
    public async Task Il_travaso_e_idempotente_e_non_tocca_una_scelta_gia_fatta()
    {
        await Campo("LIRP", presenza: true, AirportCategory.MilitaryWithCivilPresence);
        await Campo("LIMC", presenza: true, AirportCategory.CivilWithMilitaryPresence);

        Assert.Equal(0, await new EfDocumentMaintenance(_db).ReconcileAirportCategoriesAsync());
        Assert.Equal(AirportCategory.MilitaryWithCivilPresence, (await Rileggi("LIRP")).Category);
    }

    /// <summary>Lo stato che nessuno digita: la presenza è caduta ma la categoria è rimasta militare.</summary>
    [Fact]
    public async Task Il_travaso_ripara_una_categoria_militare_senza_presenza()
    {
        var a = await Campo("LIPA", presenza: true, AirportCategory.MilitaryOnly);
        a.HasMilitaryPresence = false;
        await _db.SaveChangesAsync();

        Assert.Equal(1, await new EfDocumentMaintenance(_db).ReconcileAirportCategoriesAsync());
        var dopo = await Rileggi("LIPA");
        Assert.Equal(AirportCategory.Civil, dopo.Category);
        Assert.False(dopo.IsMilitaryOnly);
    }

    // ---- Il comando della pagina Aeroporti ------------------------------------------------------------

    [Fact]
    public async Task Il_comando_scrive_la_categoria_e_lo_specchio()
    {
        var a = await Campo("LIPA", presenza: true, AirportCategory.CivilWithMilitaryPresence);

        await new EfStructureEditingRepository(_db).SetAirportCategoryAsync("LIPP", a.Id, AirportCategory.MilitaryOnly);

        var dopo = await Rileggi("LIPA");
        Assert.Equal(AirportCategory.MilitaryOnly, dopo.Category);
        // Lo specchio in pensione: se si tornasse a 1.21.x, quella versione troverebbe il dato giusto.
        Assert.True(dopo.IsMilitaryOnly);
    }

    [Fact]
    public async Task Senza_presenza_militare_le_tre_categorie_militari_si_rifiutano()
    {
        var a = await Campo("LIPZ", presenza: false);

        foreach (var c in AirportCategories.Selectable)
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => new EfStructureEditingRepository(_db).SetAirportCategoryAsync("LIPP", a.Id, c));

        Assert.Equal(AirportCategory.Civil, (await Rileggi("LIPZ")).Category);
    }

    /// <summary>«Civile» non lo sceglie una persona: lo dice la sorgente, togliendo la presenza.</summary>
    [Fact]
    public async Task Con_presenza_militare_Civile_si_rifiuta()
    {
        var a = await Campo("LIPA", presenza: true, AirportCategory.MilitaryOnly);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new EfStructureEditingRepository(_db).SetAirportCategoryAsync("LIPP", a.Id, AirportCategory.Civil));
    }

    /// <summary>Cambiare categoria non tocca i documenti (decisione del 10 settembre, ribadita l'11).</summary>
    [Fact]
    public async Task Cambiare_categoria_non_tocca_i_documenti()
    {
        var a = await Campo("LIRP", presenza: true, AirportCategory.MilitaryWithCivilPresence);
        var civile = await Documento(a, DocumentEdition.Civil);
        var mil = await Documento(a, DocumentEdition.Military);

        await new EfStructureEditingRepository(_db).SetAirportCategoryAsync("LIPP", a.Id, AirportCategory.CivilWithMilitaryPresence);

        var dopo = await Rileggi("LIRP");
        Assert.Equal(civile, dopo.DocumentId);
        Assert.Equal(mil, dopo.MilDocumentId);
        Assert.Equal(2, await _db.Documents.CountAsync());
    }

    // ---- La lettura della Diagnostica ---------------------------------------------------------------

    /// <summary>
    /// I documenti fuori categoria, uno per edizione, e quelli a posto che NON compaiono. ⚠️ La query confronta
    /// le categorie per valore (un metodo non si traduce in SQL): questo test è ciò che la tiene allineata a
    /// <see cref="AirportCategories"/>.
    /// </summary>
    [Fact]
    public async Task La_Diagnostica_trova_i_documenti_fuori_categoria_e_solo_quelli()
    {
        // vIPI su solo militare: fuori. vSOP su civile con presenza: fuori, e con una release in vigore.
        var libg = await Campo("LIBG", presenza: true, AirportCategory.MilitaryOnly);
        await Documento(libg, DocumentEdition.Civil);
        var liml = await Campo("LIML", presenza: true, AirportCategory.CivilWithMilitaryPresence);
        await Documento(liml, DocumentEdition.Military);
        _db.DocReleases.Add(new DocRelease
        {
            TargetType = ReleaseTargetType.AirportMil, TargetKey = "LIML", VersionNumber = 1,
            ReleaseAiracCycle = "2609", ReleaseEffectiveUtc = DateTime.UtcNow.AddDays(-1),
            Status = ReleaseStatus.Effective, PayloadJson = "{}", CreatedByUserId = 1, CreatedUtc = DateTime.UtcNow,
        });
        // A posto: tutti e due i documenti su un campo che li ammette.
        var lirp = await Campo("LIRP", presenza: true, AirportCategory.MilitaryWithCivilPresence);
        await Documento(lirp, DocumentEdition.Civil);
        await Documento(lirp, DocumentEdition.Military);
        await _db.SaveChangesAsync();

        var righe = (await new EfConsistencyReportRepository(_db).LoadAsync()).DocumentiFuoriCategoria;

        Assert.Equal(new[] { "LIBG", "LIML" }, righe.Select(r => r.Icao));
        var vipi = righe.Single(r => r.Icao == "LIBG");
        Assert.Equal(DocumentEdition.Civil, vipi.Edizione);
        Assert.False(vipi.Visibile);   // nessuna release: non si vede dal web
        var vsop = righe.Single(r => r.Icao == "LIML");
        Assert.Equal(DocumentEdition.Military, vsop.Edizione);
        // ⚠️ Visibile per la release AirportMil: le due edizioni condividono la chiave, e la release va letta
        // col tipo del documento fuori categoria.
        Assert.True(vsop.Visibile);
    }
}
