using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La proiezione che <b>racconta</b>: quando un callsign sparisce, viene nascosto o cambia padre, i documenti
/// che ne parlano se lo devono sentir dire. Carta <c>docs/feature/2026-08-25-documenti-da-rivedere.md</c> §6.
///
/// <para>Due dei casi qui sotto non riguardano quel che il meccanismo <b>fa</b>, ma quel che <b>non</b> fa: la
/// proiezione gira a ogni avvio dell'applicazione, prima e indipendentemente dagli import, e con un catalogo
/// vuoto o a metà segnalerebbe la sparizione di tutto ciò che esiste. Sono le due guardie che tengono la
/// casella leggibile il giorno in cui a monte va storto qualcosa.</para>
/// </summary>
public class SectorProjectionImpactTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfSectorProjectionService _proj = default!;
    private EfDocumentImpactRepository _impatti = default!;

    private int _docAcc;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        _impatti = new EfDocumentImpactRepository(_db);
        _proj = new EfSectorProjectionService(_db, new DocumentImpactService(_impatti, new AllowAuthz()));

        var lirr = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(lirr);
        _db.AccSectors.AddRange(
            new AccSector { ComposePosition = "LIRR_NE_CTR", CenterId = "LIRR", Position = "CTR" },
            new AccSector { ComposePosition = "LIRR_TS_CTR", CenterId = "LIRR", Position = "CTR", ParentCallsign = "LIRR_NE_CTR" },
            new AccSector { ComposePosition = "LIRR_SW_CTR", CenterId = "LIRR", Position = "CTR", ParentCallsign = "LIRR_NE_CTR" });
        await _db.SaveChangesAsync();

        // Prima proiezione: nasce tutto. Poi il documento ACC-wide sulla radice, come farebbe la generazione.
        await _proj.SyncFromCatalogsAsync();

        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = "vIPI Roma ACC", Language = Language.It,
            LastUpdatedAiracCycle = "2608",
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        _docAcc = doc.Id;

        var ne = await _db.Sectors.FirstAsync(s => s.Callsign == "LIRR_NE_CTR");
        ne.DocumentId = doc.Id;
        ne.IsPrimary = true;
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private async Task<IReadOnlyList<DocumentImpactRow>> ApertiAsync() => await _impatti.ListOpenAsync(_docAcc);

    // ---- Che cosa racconta ----

    [Fact]
    public async Task Un_Callsign_Sparito_Dal_Catalogo_Apre_SectorGone()
    {
        _db.AccSectors.Remove(await _db.AccSectors.FirstAsync(s => s.ComposePosition == "LIRR_TS_CTR"));
        await _db.SaveChangesAsync();

        await _proj.SyncFromCatalogsAsync();

        var riga = Assert.Single(await ApertiAsync());
        Assert.Equal(ImpactKind.SectorGone, riga.Kind);
        Assert.Equal("LIRR_TS_CTR", riga.SourceKey);
        Assert.Equal(new[] { "LIRR_TS_CTR" }, riga.ReasonArgs);
    }

    /// <summary>Nascosto e sparito sono due fatti diversi: il primo lo decide una persona, il secondo la
    /// sorgente. Il callsign è ancora in catalogo, quindi la riga deve dirlo.</summary>
    [Fact]
    public async Task Un_Callsign_Nascosto_Apre_SectorHidden_Non_SectorGone()
    {
        (await _db.AccSectors.FirstAsync(s => s.ComposePosition == "LIRR_TS_CTR")).IsHidden = true;
        await _db.SaveChangesAsync();

        await _proj.SyncFromCatalogsAsync();

        var riga = Assert.Single(await ApertiAsync());
        Assert.Equal(ImpactKind.SectorHidden, riga.Kind);
    }

    [Fact]
    public async Task Un_Cambio_Di_Padre_Apre_SectorReparented()
    {
        var ts = await _db.AccSectors.FirstAsync(s => s.ComposePosition == "LIRR_TS_CTR");
        ts.ParentCallsign = "LIRR_SW_CTR";   // prima stava sotto NE
        await _db.SaveChangesAsync();

        await _proj.SyncFromCatalogsAsync();

        var riga = Assert.Single(await ApertiAsync());
        Assert.Equal(ImpactKind.SectorReparented, riga.Kind);
        Assert.Equal("LIRR_TS_CTR", riga.SourceKey);
    }

    [Fact]
    public async Task Un_Giro_Che_Non_Cambia_Niente_Non_Apre_Niente()
    {
        await _proj.SyncFromCatalogsAsync();
        await _proj.SyncFromCatalogsAsync();

        Assert.Empty(await ApertiAsync());
    }

    /// <summary>Il callsign torna: la causa non c'è più, e la riga si chiude da sé. Nessuno l'ha «risolta»,
    /// quindi la chiusura porta l'utente 0.</summary>
    [Fact]
    public async Task Il_Callsign_Che_Torna_Chiude_La_Segnalazione()
    {
        var ts = await _db.AccSectors.FirstAsync(s => s.ComposePosition == "LIRR_TS_CTR");
        ts.IsHidden = true;
        await _db.SaveChangesAsync();
        await _proj.SyncFromCatalogsAsync();
        Assert.Single(await ApertiAsync());

        ts.IsHidden = false;
        await _db.SaveChangesAsync();
        await _proj.SyncFromCatalogsAsync();

        Assert.Empty(await ApertiAsync());
        var chiusa = await _db.DocumentImpacts.AsNoTracking().SingleAsync();
        Assert.Equal(0, chiusa.ClearedByUserId);          // l'ha chiusa il calcolo
        Assert.NotEqual(DocumentImpact.Aperto, chiusa.ClearedUtc);
    }

    // ---- Le due guardie ----

    /// <summary>⚠️ Catalogo vuoto: la proiezione gira a ogni avvio, e un database appena sostituito o un import
    /// fallito non sono «sono spariti tutti», sono «non lo sappiamo». Il settore si disattiva (comportamento
    /// invariato), ma nessuna riga finisce nella casella.</summary>
    [Fact]
    public async Task Catalogo_Vuoto_Non_Apre_Nessuna_Segnalazione()
    {
        _db.AccSectors.RemoveRange(await _db.AccSectors.ToListAsync());
        await _db.SaveChangesAsync();

        await _proj.SyncFromCatalogsAsync();

        Assert.Empty(await _db.DocumentImpacts.AsNoTracking().ToListAsync());
        Assert.All(await _db.Sectors.AsNoTracking().ToListAsync(), s => Assert.False(s.IsActive));
    }

    /// <summary>⚠️ Sparizione di massa: oltre un quarto dei settori attivi, il catalogo è sospetto. Sotto la
    /// soglia minima di cinque, invece, la quota non si applica — su tre settori uno solo che se ne va supera
    /// il 25% ed è del tutto normale.</summary>
    [Fact]
    public async Task Sparizione_Di_Massa_Non_Apre_Segnalazioni()
    {
        // Sei settori in più (nove in tutto), così togliendone sei si supera sia la quota sia il minimo.
        for (var i = 1; i <= 6; i++)
            _db.AccSectors.Add(new AccSector { ComposePosition = $"LIRR_X{i}_CTR", CenterId = "LIRR", Position = "CTR" });
        await _db.SaveChangesAsync();
        await _proj.SyncFromCatalogsAsync();

        _db.AccSectors.RemoveRange(await _db.AccSectors.Where(s => s.ComposePosition.Contains("_X")).ToListAsync());
        await _db.SaveChangesAsync();
        await _proj.SyncFromCatalogsAsync();

        Assert.Empty(await _db.DocumentImpacts.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Una_Sparizione_Sola_Passa_La_Guardia()
    {
        for (var i = 1; i <= 6; i++)
            _db.AccSectors.Add(new AccSector { ComposePosition = $"LIRR_X{i}_CTR", CenterId = "LIRR", Position = "CTR" });
        await _db.SaveChangesAsync();
        await _proj.SyncFromCatalogsAsync();

        _db.AccSectors.Remove(await _db.AccSectors.FirstAsync(s => s.ComposePosition == "LIRR_X1_CTR"));
        await _db.SaveChangesAsync();
        await _proj.SyncFromCatalogsAsync();

        Assert.NotEmpty(await _db.DocumentImpacts.AsNoTracking().ToListAsync());
    }

    private sealed class AllowAuthz : IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public VipiRole Role => IsAdmin ? VipiRole.Admin : VipiRole.User;
        public int? CurrentUserId => 1;
        public string? CurrentName => "test";
        public Task EnsureCanEditAccAsync(string accCode, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureCanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> CanEditAccAsync(string accCode, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> CanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> CanEditAnythingAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<GrantRow>> ListGrantsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GrantRow>>(Array.Empty<GrantRow>());
        public Task<int> AddGrantAsync(int UserId, string? displayName, string accCode, CancellationToken ct = default) => Task.FromResult(0);
        public Task RevokeGrantAsync(int grantId, CancellationToken ct = default) => Task.CompletedTask;
        public void EnsureAdmin() { }
    }
}
