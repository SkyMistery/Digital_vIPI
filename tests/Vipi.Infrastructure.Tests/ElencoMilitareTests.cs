using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// L'elenco dei vSOP militari e la creazione del primo (carta
/// <c>2026-08-27-vsop-militari.md</c> §5).
///
/// <para>
/// ⚠️ <b>Il test che conta è quello sul catch-22.</b> L'elenco pubblico mostra solo ciò che ha una release
/// effettiva, quindi il <b>primo</b> documento non sarebbe raggiungibile da nessuna parte: non c'è, e per
/// farlo esistere bisognerebbe già poterci arrivare. È successo davvero con l'elenco APP, e la risposta è
/// la stessa — allo staff si mostrano anche i candidati senza documento.
/// </para>
/// </summary>
public class ElencoMilitareTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    /// <summary>Autorizzazione permissiva: qui si prova l'elenco, non i permessi.</summary>
    private sealed class AllowAuthz : IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public VipiRole Role => IsAdmin ? VipiRole.Admin : VipiRole.User;
        public int? CurrentUserId => 42;
        public string? CurrentName => "test";
        public void EnsureAdmin() { }
    }

    private EfMilitaryDocumentService Servizio() =>
        new(_db, new AiracService(), new AllowAuthz(),
            new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db)),
            new EfSpecialAreaRepository(_db), new EfNavaidCatalog(_db));

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();

        var acc = new Acc { Code = "LIPP", Name = "Padova" };
        _db.Accs.Add(acc);
        _db.Airports.AddRange(
            // Rivolto: campo solo militare, il caso tipico.
            new Airport { Icao = "LIPI", Name = "Rivolto", Acc = acc, HasMilitaryPresence = true, IsMilitaryOnly = true },
            // ⚠️ Pisa: presenza militare MA scalo civile. La sorgente dice `military` anche per lui, ed è
            // giusto che compaia — il suo SOP è fra i quindici PDF veri.
            new Airport { Icao = "LIRP", Name = "Pisa", Acc = acc, HasMilitaryPresence = true, IsMilitaryOnly = false },
            // Venezia: nessuna presenza militare, non è un candidato.
            new Airport { Icao = "LIPZ", Name = "Venezia", Acc = acc, HasMilitaryPresence = false });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    // ---- Il catch-22 dell'ingresso --------------------------------------------------------------------

    [Fact]
    public async Task Al_PUBBLICO_non_si_mostra_niente_finche_non_c_e_una_release()
    {
        Assert.Empty(await Servizio().ListAsync(perStaff: false));
    }

    [Fact]
    public async Task Allo_STAFF_si_mostrano_anche_i_candidati_SENZA_documento()
    {
        // ⚠️ È l'unica cosa che rende creabile il PRIMO vSOP militare. Se questo test cade, la funzione
        // esiste e nessuno può cominciare a usarla.
        var righe = await Servizio().ListAsync(perStaff: true);
        Assert.Equal(new[] { "LIPI", "LIRP" }, righe.Select(r => r.Icao));
        Assert.All(righe, r => Assert.Null(r.DocumentId));
    }

    [Fact]
    public async Task Un_campo_senza_presenza_militare_non_e_un_candidato()
    {
        var righe = await Servizio().ListAsync(perStaff: true);
        Assert.DoesNotContain("LIPZ", righe.Select(r => r.Icao));
    }

    [Fact]
    public async Task Uno_scalo_CIVILE_con_sedime_militare_e_un_candidato_e_si_vede_che_lo_e()
    {
        // ⚠️ `HasMilitaryPresence` è vero anche su Linate, Pisa e Ciampino, e non vuol dire «militare»: la
        // riga lo dice com'è, o il lettore crederebbe a un'informazione che non abbiamo.
        var pisa = (await Servizio().ListAsync(perStaff: true)).Single(r => r.Icao == "LIRP");
        Assert.False(pisa.SoloMilitare);

        var rivolto = (await Servizio().ListAsync(perStaff: true)).Single(r => r.Icao == "LIPI");
        Assert.True(rivolto.SoloMilitare);
    }

    // ---- La creazione ---------------------------------------------------------------------------------

    [Fact]
    public async Task Creare_lega_il_documento_all_aeroporto_e_lo_marca_MILITARE()
    {
        var id = await Servizio().CreaAsync("LIPI");

        var campo = await _db.Airports.AsNoTracking().SingleAsync(a => a.Icao == "LIPI");
        var doc = await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == id);

        Assert.Equal(id, campo.MilDocumentId);
        Assert.Null(campo.DocumentId);                      // il civile resta un'altra cosa
        Assert.Equal(DocumentEdition.Military, doc.Edition);
    }

    [Fact]
    public async Task Il_documento_militare_nasce_in_ITALIANO()
    {
        // ⚠️ Carta §1d: la lingua sorgente è quella in cui si REDIGE, non quella dei quindici PDF di
        // partenza. Se questo test cade, qualcuno ha rimesso in piedi la premessa vecchia.
        var id = await Servizio().CreaAsync("LIPI");
        Assert.Equal(Language.It, (await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == id)).Language);
    }

    [Fact]
    public async Task Il_documento_nasce_in_BOZZA_come_le_altre_famiglie()
    {
        var id = await Servizio().CreaAsync("LIPI");
        var doc = await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == id);
        Assert.Equal(DocumentStatus.Draft, doc.Status);
        // ⚠️ CurrentVersionId vuol dire «la versione PUBBLICATA corrente»: un documento mai pubblicato che
        // dichiarasse di averne una direbbe il falso. Lo scrive PublishAsync.
        Assert.Null(doc.CurrentVersionId);
    }

    [Fact]
    public async Task Il_documento_nasce_con_le_sezioni_del_profilo_ANNIDATE()
    {
        var id = await Servizio().CreaAsync("LIPI");
        var sezioni = await _db.DocumentSections.AsNoTracking()
            .Where(s => s.DocumentVersion!.DocumentId == id).ToListAsync();

        // ⚠️ I numeri li dice il CATALOGO, non questa riga: scritti a mano invecchiano — è già successo col
        // commento «ventiquattro sezioni» rimasto indietro di due. Dal 3 settembre 2026 sono trentadue, con
        // «Carte aeroportuali» e le sue cinque raccolte.
        static IEnumerable<SectionDescriptor> Tutte(IEnumerable<SectionDescriptor> d) =>
            d.SelectMany(x => new[] { x }.Concat(Tutte(x.Children ?? Array.Empty<SectionDescriptor>())));
        var profilo = SectionCatalog.For(SectionProfile.AirportMil);

        Assert.Equal(Tutte(profilo).Count(), sezioni.Count);
        Assert.Equal(profilo.Count, sezioni.Count(s => s.Depth == 0));
        Assert.Equal(Tutte(profilo).Count() - profilo.Count, sezioni.Count(s => s.Depth == 1));
        Assert.Contains(sezioni, s => s.SectionKey == "qra");
        // Le carte nascono ANNIDATE: il contenitore in cima e le cinque raccolte dentro.
        var carte = sezioni.Single(s => s.SectionKey == "charts");
        Assert.Equal(5, sezioni.Count(s => s.ParentSectionId == carte.Id));
    }

    [Fact]
    public async Task Creare_due_volte_non_crea_due_documenti()
    {
        var primo = await Servizio().CreaAsync("LIPI");
        var secondo = await Servizio().CreaAsync("LIPI");
        Assert.Equal(primo, secondo);
        Assert.Single(_db.Documents);
    }

    [Fact]
    public async Task Non_si_crea_un_vSOP_militare_su_un_campo_che_militare_non_e()
    {
        // Il documento resterebbe lì, vuoto, in un elenco dove nessuno saprebbe perché c'è.
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => Servizio().CreaAsync("LIPZ"));
    }

    [Fact]
    public async Task Un_ICAO_inesistente_lo_dice_invece_di_tacere()
    {
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => Servizio().CreaAsync("ZZZZ"));
    }

    // ---- Dopo la creazione ----------------------------------------------------------------------------

    [Fact]
    public async Task Creato_ma_non_pubblicato_resta_invisibile_al_pubblico()
    {
        await Servizio().CreaAsync("LIPI");

        var staff = await Servizio().ListAsync(perStaff: true);
        Assert.NotNull(staff.Single(r => r.Icao == "LIPI").DocumentId);
        Assert.False(staff.Single(r => r.Icao == "LIPI").Pubblicato);

        // ⚠️ Il gate sta nel SERVIZIO e non nella pagina: una pagina che filtra è una pagina che può
        // dimenticarsene, e qui la dimenticanza sarebbe una bozza militare pubblicata per sbaglio.
        Assert.Empty(await Servizio().ListAsync(perStaff: false));
    }

    [Fact]
    public async Task Con_una_release_effettiva_compare_anche_al_pubblico()
    {
        await Servizio().CreaAsync("LIPI");
        _db.DocReleases.Add(new DocRelease
        {
            TargetType = ReleaseTargetType.AirportMil, TargetKey = "LIPI", VersionNumber = 1,
            ReleaseAiracCycle = "2609", ReleaseEffectiveUtc = DateTime.UtcNow.AddDays(-1),
            Status = ReleaseStatus.Effective, PayloadJson = "{}", CreatedByUserId = 1,
            CreatedUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var pubblico = await Servizio().ListAsync(perStaff: false);
        Assert.Equal(new[] { "LIPI" }, pubblico.Select(r => r.Icao));

        // La stessa domanda, per UN aeroporto: è quella che si fa la pagina civile per il ponte fra le due
        // edizioni, e deve rispondere come l'elenco — o il collegamento comparirebbe dove non c'è niente.
        Assert.True(await Servizio().HasPublishedAsync("LIPI"));
        Assert.False(await Servizio().HasPublishedAsync("LIRP"));
    }

    [Fact]
    public async Task Una_release_FUTURA_non_rende_pubblico_niente()
    {
        // Una release programmata per il ciclo prossimo non è ancora in vigore: mostrarla adesso vorrebbe
        // dire pubblicare in anticipo un documento che dichiara un altro AIRAC.
        await Servizio().CreaAsync("LIPI");
        _db.DocReleases.Add(new DocRelease
        {
            TargetType = ReleaseTargetType.AirportMil, TargetKey = "LIPI", VersionNumber = 1,
            ReleaseAiracCycle = "2610", ReleaseEffectiveUtc = DateTime.UtcNow.AddDays(20),
            Status = ReleaseStatus.Scheduled, PayloadJson = "{}", CreatedByUserId = 1,
            CreatedUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        Assert.Empty(await Servizio().ListAsync(perStaff: false));
        Assert.False(await Servizio().HasPublishedAsync("LIPI"));
    }
}
