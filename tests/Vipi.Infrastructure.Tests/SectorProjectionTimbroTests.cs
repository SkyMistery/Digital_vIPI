using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il timbro d'import di un settore proiettato è quello della <b>riga di catalogo</b>, non l'ora in cui la
/// proiezione è girata.
///
/// <para><b>Il difetto, del 27 agosto 2026.</b> La proiezione scriveva <c>DateTime.UtcNow</c> su ogni
/// settore, e faceva due danni insieme.</para>
///
/// <para>Il primo è il costo: questa passata gira a <b>ogni avvio</b>, e un valore nuovo su ogni riga
/// significa che EF le marca tutte come modificate. Contate: <b>312 UPDATE su 465 query d'avvio</b>, ogni
/// volta, senza che nulla fosse cambiato. Dopo la correzione: 153 query e zero UPDATE.</para>
///
/// <para>Il secondo è il significato, ed è il più serio. Quel campo lo interroga la regola D8 delle
/// eliminazioni — «la sorgente lo manda ancora?» — e con <c>UtcNow</c> la risposta era «sì, perché abbiamo
/// riavviato»: un settore sparito dalla sorgente a luglio tornava fresco a ogni riavvio.
/// <c>EfDeletionRepository</c> lo sapeva e ci girava intorno, con scritto in chiaro che quel timbro «dice
/// quando è nato lo specchio, non quando la sorgente ha parlato». Adesso i due coincidono.</para>
/// </summary>
public class SectorProjectionTimbroTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfSectorProjectionService _proj = default!;

    private static readonly DateTime GiroDellaSorgente = new(2026, 8, 20, 3, 0, 0, DateTimeKind.Utc);

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _proj = new EfSectorProjectionService(_db);

        _db.Accs.Add(new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" });
        _db.AccSectors.AddRange(
            new AccSector { ComposePosition = "LIRR_NE_CTR", CenterId = "LIRR", Position = "CTR", ImportedAtUtc = GiroDellaSorgente },
            new AccSector { ComposePosition = "LIRR_TS_CTR", CenterId = "LIRR", Position = "CTR", ImportedAtUtc = GiroDellaSorgente });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    /// <summary>Il settore proiettato porta il timbro della riga da cui viene.</summary>
    [Fact]
    public async Task Il_settore_eredita_il_timbro_della_riga_di_catalogo()
    {
        await _proj.SyncFromCatalogsAsync();

        var settori = await _db.Sectors.AsNoTracking().ToListAsync();
        Assert.NotEmpty(settori);
        Assert.All(settori, s => Assert.Equal(GiroDellaSorgente, s.ImportedAtUtc));
    }

    /// <summary>
    /// <b>Il test che vale il lavoro.</b> Una seconda passata a catalogo immutato non deve scrivere niente.
    ///
    /// <para>⚠️ Si ascolta l'evento <c>SavingChanges</c>, e non si guarda il change-tracker DOPO la
    /// chiamata. Il primo giro di questo test faceva così ed era <b>verde anche col difetto</b>:
    /// <c>SyncFromCatalogsAsync</c> salva al proprio interno, quindi quando il test arriva a guardare le
    /// entità sono già tornate «Unchanged» — comunque siano andate le cose. Un test che non può fallire
    /// non è una guardia, è una decorazione. Qui si chiede a EF che cosa sta per mandare al database, nel
    /// momento in cui lo manda.</para>
    ///
    /// <para>Verificato al contrario: rimettendo <c>DateTime.UtcNow</c> nella proiezione, questo test
    /// diventa rosso con tutti e due i settori in elenco.</para>
    /// </summary>
    [Fact]
    public async Task Una_seconda_passata_a_catalogo_immutato_non_scrive_niente()
    {
        await _proj.SyncFromCatalogsAsync();
        _db.ChangeTracker.Clear();

        var daScrivere = new List<string>();
        void Ascolta(object? _, SavingChangesEventArgs __) =>
            daScrivere.AddRange(_db.ChangeTracker.Entries<Sector>()
                .Where(e => e.State is EntityState.Modified or EntityState.Added)
                .Select(e => e.Entity.Callsign));

        _db.SavingChanges += Ascolta;
        try { await _proj.SyncFromCatalogsAsync(); }
        finally { _db.SavingChanges -= Ascolta; }

        Assert.True(daScrivere.Count == 0,
            $"la proiezione riscriverebbe {daScrivere.Count} settori a catalogo immutato " +
            $"({string.Join(", ", daScrivere)}). È il difetto delle 312 UPDATE a ogni avvio: qualcuno ha " +
            "rimesso un valore che cambia da sé — quasi sempre un DateTime.UtcNow.");
    }

    /// <summary>Quando la sorgente parla davvero, il timbro nuovo arriva fino al settore.</summary>
    [Fact]
    public async Task Quando_il_catalogo_riceve_un_giro_nuovo_il_timbro_si_muove()
    {
        await _proj.SyncFromCatalogsAsync();
        var giroNuovo = GiroDellaSorgente.AddDays(1);

        foreach (var riga in await _db.AccSectors.ToListAsync()) riga.ImportedAtUtc = giroNuovo;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await _proj.SyncFromCatalogsAsync();

        var settori = await _db.Sectors.AsNoTracking().ToListAsync();
        Assert.All(settori, s => Assert.Equal(giroNuovo, s.ImportedAtUtc));
    }

    /// <summary>
    /// Una riga di catalogo che la sorgente non ha mai mandato — un settore estero aggiunto a mano dalla
    /// pagina Confinanti — non ha timbro, e il settore proiettato non deve inventarsene uno.
    ///
    /// <para>⚠️ Prima ne riceveva uno: l'ora del riavvio. Cioè la riga sembrava confermata dalla sorgente
    /// proprio nel caso in cui la sorgente non l'aveva mai vista. Chi decide le eliminazioni non si fa
    /// ingannare comunque (guarda <c>IsManual</c>), ma un campo che mente è un campo su cui qualcuno prima
    /// o poi si appoggerà.</para>
    /// </summary>
    [Fact]
    public async Task Una_riga_mai_mandata_dalla_sorgente_resta_senza_timbro()
    {
        _db.AccSectors.Add(new AccSector
        {
            ComposePosition = "LIRR_XX_CTR", CenterId = "LIRR", Position = "CTR",
            IsManual = true, ImportedAtUtc = null,
        });
        await _db.SaveChangesAsync();

        await _proj.SyncFromCatalogsAsync();

        var manuale = await _db.Sectors.AsNoTracking().SingleAsync(s => s.Callsign == "LIRR_XX_CTR");
        Assert.Null(manuale.ImportedAtUtc);
    }
}
