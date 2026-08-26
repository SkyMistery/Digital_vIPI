using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Import ACC dalla sorgente: upsert ACC (dedup per centerId) + settori CTR, idempotente,
/// con preservazione di IsHidden. Verifica anche che EfStationDirectory escluda gli ACC nascosti.
/// </summary>
public class AccImportTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAccAdminRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _repo = new EfAccAdminRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private static IReadOnlyList<SourceCenter> Sample() => new[]
    {
        new SourceCenter("LIRR_N_CTR", "LIRR", "Roma Control", false, "124.000"),
        new SourceCenter("LIRR_S_CTR", "LIRR", "Roma Control", false, "125.000"),
        new SourceCenter("LIMM_CTR",   "LIMM", "Milano Control", false, "133.250"),
        new SourceCenter("LIRM_CTR",   "LIRM", "Martina Control", true, "130.000"),  // militare
    };

    private static IReadOnlyList<SourceSubcenter> Subs() => new[]
    {
        new SourceSubcenter("LIRR_N_CTR", "LIRR", "CTR", "N", "124.000", "{\"poly\":1}"),
        new SourceSubcenter("LIRR_S_CTR", "LIRR", "CTR", "S", "125.000", null),
        new SourceSubcenter("LIRR_FSS",   "LIRR", "FSS", null, "123.000", null),   // FSS → default GND-19000
        new SourceSubcenter("LIBB_X_CTR", "LIBB", "CTR", "X", "126.000", null),   // ACC inesistente → skip
    };

    [Fact]
    public async Task Import_Creates_One_Acc_Per_CenterId()
    {
        var r = await _repo.ImportAsync(Sample());

        Assert.Equal(3, r.Created);       // LIRR (dedup da 2 righe), LIMM, LIRM

        var accs = await _repo.ListAccsAsync();
        Assert.Equal(3, accs.Count);
        Assert.Single(accs, a => a.Code == "LIRR");
        Assert.True(Assert.Single(accs, a => a.Code == "LIRM").IsMilitary);
        Assert.All(accs, a => Assert.False(a.IsHidden));   // tutti attivi di default
    }

    [Fact]
    public async Task Reimport_Is_Idempotent_And_Preserves_Hidden()
    {
        await _repo.ImportAsync(Sample());
        var limm = (await _repo.ListAccsAsync()).Single(a => a.Code == "LIMM");
        await _repo.SetHiddenAsync(limm.Id, true);

        var r = await _repo.ImportAsync(Sample());   // secondo import

        Assert.Equal(0, r.Created);
        Assert.Equal(3, r.Updated);

        Assert.Equal(3, (await _repo.ListAccsAsync()).Count);    // nessun duplicato
        Assert.True((await _repo.ListAccsAsync()).Single(a => a.Code == "LIMM").IsHidden);  // hide preservato
    }

    [Fact]
    public async Task Import_Subcenters_Skips_Unknown_Acc_And_Preserves_Admin_Limits()
    {
        await _repo.ImportAsync(Sample());        // crea LIRR, LIMM, LIRM (non LIBB)
        var r = await _repo.ImportSubcentersAsync(Subs());

        Assert.Equal(3, r.Created);               // LIRR_N/S/FSS; LIBB_X scartato (nessun ACC LIBB)
        var subs = await _repo.ListSubcentersAsync();
        Assert.Equal(3, subs.Count);
        Assert.True(subs.Single(s => s.ComposePosition == "LIRR_N_CTR").HasPolygon);
        // default limiti CTR: inferiore 0 (GND), superiore null = UNL
        var s2 = subs.Single(s => s.ComposePosition == "LIRR_S_CTR");
        Assert.Equal(0, s2.LowerLimit);
        Assert.Null(s2.UpperLimit);
        // default limiti FSS: GND (0) → 19000
        var fss = subs.Single(s => s.ComposePosition == "LIRR_FSS");
        Assert.Equal(0, fss.LowerLimit);
        Assert.Equal(19000, fss.UpperLimit);

        // admin imposta limiti + nasconde
        var n = subs.Single(s => s.ComposePosition == "LIRR_N_CTR");
        await _repo.SetSubcenterLimitsAsync(n.Id, 0, 24500);
        await _repo.SetSubcenterHiddenAsync(n.Id, true);

        // re-import (sorgente senza limiti) preserva i valori admin
        var r2 = await _repo.ImportSubcentersAsync(Subs());
        Assert.Equal(0, r2.Created);
        Assert.Equal(3, r2.Updated);
        var after = (await _repo.ListSubcentersAsync()).Single(s => s.ComposePosition == "LIRR_N_CTR");
        Assert.Equal(0, after.LowerLimit);
        Assert.Equal(24500, after.UpperLimit);
        Assert.True(after.IsHidden);
    }

    [Fact]
    public async Task Hiding_Acc_Marks_Its_Sectors_AccHidden()
    {
        await _repo.ImportAsync(Sample());
        await _repo.ImportSubcentersAsync(Subs());
        var lirr = (await _repo.ListAccsAsync()).Single(a => a.Code == "LIRR");

        await _repo.SetHiddenAsync(lirr.Id, true);

        var subs = await _repo.ListSubcentersAsync();
        Assert.All(subs.Where(s => s.CenterId == "LIRR"), s => Assert.True(s.AccHidden));
    }

    [Fact]
    public async Task StationDirectory_Excludes_Hidden_Accs()
    {
        await _repo.ImportAsync(Sample());
        var lirm = (await _repo.ListAccsAsync()).Single(a => a.Code == "LIRM");
        await _repo.SetHiddenAsync(lirm.Id, true);

        var nav = new EfStationDirectory(_db).ListAccs();

        Assert.DoesNotContain(nav, a => a.Code == "LIRM");
        Assert.Contains(nav, a => a.Code == "LIRR");
    }

    // ---- L'identità della sorgente, e la rinomina che ne consegue ------------------------------------

    private static IReadOnlyList<SourceSubcenter> SubsConId() => new[]
    {
        new SourceSubcenter("LIRR_N_CTR", "LIRR", "CTR", "N", "124.000", null, IvaoId: 1171),
        new SourceSubcenter("LIRR_S_CTR", "LIRR", "CTR", "S", "125.000", null, IvaoId: 1172),
    };

    [Fact]
    public async Task L_import_registra_l_identita_della_sorgente()
    {
        await _repo.ImportAsync(Sample());
        await _repo.ImportSubcentersAsync(SubsConId());

        var righe = await _db.AccSectors.AsNoTracking().ToDictionaryAsync(x => x.ComposePosition, x => x.IvaoId);
        Assert.Equal(1171, righe["LIRR_N_CTR"]);
        Assert.Equal(1172, righe["LIRR_S_CTR"]);
    }

    /// <summary>
    /// L'archivio di prima non ha id: il backfill glieli dà senza inventarsi nessuna rinomina. È il primo giro
    /// dopo il deploy, ed è l'unico momento in cui questo può andare storto per tutti insieme.
    /// </summary>
    [Fact]
    public async Task Il_primo_giro_riempie_gli_id_senza_rinominare_niente()
    {
        await _repo.ImportAsync(Sample());
        await _repo.ImportSubcentersAsync(Subs());                    // senza id: com'era prima
        Assert.All(await _db.AccSectors.AsNoTracking().ToListAsync(), s => Assert.Null(s.IvaoId));

        await _repo.ImportSubcentersAsync(SubsConId());               // ora la sorgente li manda

        Assert.Equal(1171, (await _db.AccSectors.AsNoTracking()
            .SingleAsync(x => x.ComposePosition == "LIRR_N_CTR")).IvaoId);
        Assert.Empty(await _db.CallsignAliases.AsNoTracking().ToListAsync());
    }

    /// <summary>
    /// Stesso id, nominativo nuovo: la riga si RINOMINA, non si sdoppia. Prima di questa carta l'archivio
    /// finiva con due righe e il documento sulla vecchia.
    /// </summary>
    [Fact]
    public async Task Stesso_id_con_nome_nuovo_rinomina_la_riga_invece_di_aggiungerne_una()
    {
        await _repo.ImportAsync(Sample());
        await _repo.ImportSubcentersAsync(SubsConId());

        await _repo.ImportSubcentersAsync(new[]
        {
            new SourceSubcenter("LIRR_NE_CTR", "LIRR", "CTR", "NE", "124.000", null, IvaoId: 1171),
            new SourceSubcenter("LIRR_S_CTR", "LIRR", "CTR", "S", "125.000", null, IvaoId: 1172),
        });

        var righe = await _db.AccSectors.AsNoTracking().Where(x => x.CenterId == "LIRR").ToListAsync();
        Assert.Equal(2, righe.Count);                                        // due, non tre
        Assert.Equal("LIRR_NE_CTR", righe.Single(x => x.IvaoId == 1171).ComposePosition);
        Assert.DoesNotContain(righe, x => x.ComposePosition == "LIRR_N_CTR");

        var alias = Assert.Single(await _db.CallsignAliases.AsNoTracking().ToListAsync());
        Assert.Equal("LIRR_N_CTR", alias.OldCallsign);
        Assert.Equal("LIRR_NE_CTR", alias.NewCallsign);
    }

    /// <summary>
    /// Il caso vero del 22 agosto 2026: <c>LIRR_NE1_CTR</c> è nato ACCANTO a <c>LIRR_NE_CTR</c>, con la stessa
    /// frequenza e lo stesso nome IVAO. È uno sdoppiamento, e le righe devono restare due.
    /// </summary>
    [Fact]
    public async Task Uno_sdoppiamento_aggiunge_una_riga_e_non_ne_rinomina_nessuna()
    {
        await _repo.ImportAsync(Sample());
        await _repo.ImportSubcentersAsync(SubsConId());

        await _repo.ImportSubcentersAsync(new[]
        {
            new SourceSubcenter("LIRR_N_CTR", "LIRR", "CTR", "N", "124.000", null, IvaoId: 1171),
            new SourceSubcenter("LIRR_S_CTR", "LIRR", "CTR", "S", "125.000", null, IvaoId: 1172),
            new SourceSubcenter("LIRR_N1_CTR", "LIRR", "CTR", "N1", "124.000", null, IvaoId: 3916),
        });

        var righe = await _db.AccSectors.AsNoTracking().Where(x => x.CenterId == "LIRR").ToListAsync();
        Assert.Equal(3, righe.Count);
        Assert.Contains(righe, x => x.ComposePosition == "LIRR_N_CTR");     // il vecchio è ancora lì
        Assert.Contains(righe, x => x.ComposePosition == "LIRR_N1_CTR");
        Assert.Empty(await _db.CallsignAliases.AsNoTracking().ToListAsync());
    }

    /// <summary>
    /// ⚠️ Il gemello del caso sugli aeroporti: quando IVAO torna a mandare i poligoni — guasto loro
    /// confermato il 26 agosto 2026 — l'anagrafica deve riprendere il comando per intero, o il gate AIRAC
    /// continuerebbe ad applicarsi a una shape che non ne ha bisogno.
    /// </summary>
    [Fact]
    public async Task Quando_l_anagrafica_torna_a_mandare_la_shape_riprende_il_comando()
    {
        await _repo.ImportAsync(Sample());
        await _repo.ImportSubcentersAsync(SubsConId());

        var riga = await _db.AccSectors.SingleAsync(x => x.ComposePosition == "LIRR_N_CTR");
        riga.RegionMapPolygon = "[[9.0,45.0],[9.5,45.0],[9.5,45.5]]";
        riga.RegionMapPolygonInForce = "[[8.0,44.0],[8.5,44.0],[8.5,44.5]]";
        riga.ShapeAiracCycle = "2610";
        riga.ShapeSource = ShapeSource.Sectorfile;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        const string Vero = "[[12.0,42.0],[12.9,42.0],[12.9,42.9]]";
        await _repo.ImportSubcentersAsync(new[]
        {
            new SourceSubcenter("LIRR_N_CTR", "LIRR", "CTR", "N", "124.000", Vero, IvaoId: 1171),
        });

        var dopo = await _db.AccSectors.AsNoTracking().SingleAsync(x => x.ComposePosition == "LIRR_N_CTR");
        Assert.Equal(Vero, dopo.RegionMapPolygon);
        Assert.Equal(ShapeSource.Source, dopo.ShapeSource);
        Assert.Null(dopo.ShapeAiracCycle);
        Assert.Null(dopo.RegionMapPolygonInForce);
    }
}
