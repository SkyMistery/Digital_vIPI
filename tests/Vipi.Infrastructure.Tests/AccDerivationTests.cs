using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Aor;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>vIPI ACC a blocchi (Aerovia + gruppi APP): roundtrip blocchi + derivazioni (frequenze, coordinamenti, AoR per config).</summary>
public class AccProfileTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAccDerivationRepository _repo = default!;
    private AccDerivationService _service = default!;

    private const string Acc = "LIRR";

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);

        // Catalogo APP dell'aeroporto LIRP (frequenze) + poligoni AoR per i CTR e l'APP.
        _db.AirportSectors.AddRange(
            ApSec("LIRP_ATIS", "ATIS", "125.000", null),
            ApSec("LIRP_TWR", "TWR", "118.300", null),
            ApSec("LIRP_APP", "APP", "124.500", "[[10.4,43.6],[10.5,43.6],[10.5,43.7],[10.4,43.7]]"));
        _db.AccSectors.AddRange(
            AcSec("LIRR_NE_CTR", "[[12.0,42.0],[13.0,42.0],[13.0,43.0],[12.0,43.0]]"),
            AcSec("LIRR_EW_CTR", "[[11.0,41.0],[12.0,41.0],[12.0,42.0],[11.0,42.0]]"));
        await _db.SaveChangesAsync();

        _repo = new EfAccDerivationRepository(_db);
        var authz = new AllowAuthz();
        var topo = new TopologyBuilder(_db);
        var transfers = new AgreementService(new EfAgreementRepository(_db), authz, topo);
        _service = new AccDerivationService(_repo, new EfSpecialAreaRepository(_db), transfers, topo,
            new Vipi.Application.Aor.AorService(), new StubCoordinationSentenceTemplate(), new NoMinimaSource());
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    // Nota: storage/load/save della vIPI ACC è ora su Document (AccDocumentService/AccDocumentServiceTests, doc 08e-acc).
    // Qui restano solo le DERIVAZIONI live di AccDerivationService (freq/coord/config-table), che i viewer/editor riusano.

    [Fact]
    public async Task Derive_Frequencies_Aerovia_Covers_Whole_Acc()
    {
        // Una sola vIPI per ACC: membri vuoti = TUTTI i CTR dell'ACC (tutti gli alberi insieme), a prescindere
        // dal rootCallsign passato (parametro non più scopante).
        var block = new AccBlock { Key = "aerovia", Kind = AccBlockKind.Aerovia };
        var all = await _service.DeriveFrequenciesAsync(Acc, block);
        Assert.Contains(all, f => f.Callsign == "LIRR_NE_CTR" && f.FrequencyMhz == "128.800");
        Assert.Contains(all, f => f.Callsign == "LIRR_EW_CTR");   // altro albero, ma stessa vIPI di ACC
    }

    [Fact]
    public async Task Derive_Frequencies_AppGroup_Expands_Airport_Catalog()
    {
        var block = new AccBlock { Key = "grp:1", Kind = AccBlockKind.AppGroup, MemberCallsigns = { "LIRP_APP" } };
        var freqs = await _service.DeriveFrequenciesAsync(Acc, block);
        Assert.Contains(freqs, f => f.Callsign == "LIRP_APP" && f.FrequencyMhz == "124.500");
        Assert.Contains(freqs, f => f.Callsign == "LIRP_TWR");     // catalogo aeroporto espanso
        Assert.Contains(freqs, f => f.Callsign == "LIRP_ATIS");
    }

    [Fact]
    public async Task Derive_Coordination_Classifies_Acc_App_Towers()
    {
        var tr = new EfAgreementRepository(_db);
        var ne = (await _db.Sectors.FirstAsync(s => s.Callsign == "LIRR_NE_CTR")).Id;
        var ew = (await _db.Sectors.FirstAsync(s => s.Callsign == "LIRR_EW_CTR")).Id;
        var app = (await _db.Sectors.FirstAsync(s => s.Callsign == "LIRP_APP")).Id;

        await Agreement(tr, ne, ew, TransferFlowKind.Departure, null, "VALMA", 250);   // NE → EW (CTR) = verso ACC
        await Agreement(tr, ne, app, TransferFlowKind.Arrival, null, "MAREL", 110);   // NE → LIRP_APP = verso APP

        var block = new AccBlock { Key = "aerovia", Kind = AccBlockKind.Aerovia };
        var coord = await _service.DeriveCoordinationAsync(Acc, block, "LIRR_NE_CTR");   // albero NE (owner NE)

        // Gerarchia unica Settore → ACC → Aeroporto → Arrivi/Partenze: NE con 1 partenza (→EW) e 1 arrivo (→LIRP_APP).
        var sector = Assert.Single(coord.Sectors);
        var accGroup = Assert.Single(sector.Accs);
        var airport = Assert.Single(accGroup.Airports);
        Assert.Single(airport.Arrivals);
        Assert.Single(airport.Departures);
    }

    [Fact]
    public async Task Owned_Flow_Sentence_Reads_Owner_As_Sender()
    {
        var tr = new EfAgreementRepository(_db);
        var ne = (await _db.Sectors.FirstAsync(s => s.Callsign == "LIRR_NE_CTR")).Id;
        var ew = (await _db.Sectors.FirstAsync(s => s.Callsign == "LIRR_EW_CTR")).Id;

        // Flusso POSSEDUTO da EW (settore del blocco) con next = NE: EW detiene il traffico e lo cede a NE, quindi
        // la frase deve avere EW come mittente e NE come destinatario (owner→next, come la pagina trasferimenti).
        await Agreement(tr, ew, ne, TransferFlowKind.Arrival, "LIRP", "PISIP", 140);

        var block = new AccBlock { Key = "aerovia", Kind = AccBlockKind.Aerovia };
        var coord = await _service.DeriveCoordinationAsync(Acc, block, "LIRR_NE_CTR");

        var row = coord.Sectors.SelectMany(s => s.Accs).SelectMany(a => a.Airports).SelectMany(ap => ap.Arrivals).Single();
        Assert.NotNull(row.Sentence);
        // Il template è "{owner} trasferisce a {target} …": il mittente (inizio frase) è il proprietario EW, non NE.
        Assert.StartsWith("Roma Radar EW", row.Sentence);
        Assert.Contains("Roma Radar NE", row.Sentence!);
        Assert.True(row.Sentence!.IndexOf("EW", StringComparison.Ordinal) < row.Sentence!.IndexOf("NE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Overflight_without_airport_appears_under_sorvoli_node()
    {
        var tr = new EfAgreementRepository(_db);
        var ew = (await _db.Sectors.FirstAsync(s => s.Callsign == "LIRR_EW_CTR")).Id;
        var ne = (await _db.Sectors.FirstAsync(s => s.Callsign == "LIRR_NE_CTR")).Id;

        // Sorvolo POSSEDUTO da EW, senza aeroporto, verso NE. Deve comparire nel nodo «Sorvoli», non fra gli aeroporti.
        var sec = await SectionAsync(tr, ew, ne, TransferFlowKind.Overflight, null);
        await tr.AddClauseAsync(Acc, sec, new AgreementClauseInput
        {
            Cops = "ELB", LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.Special, LevelSpecial = "per aerovia",
        });

        var block = new AccBlock { Key = "aerovia", Kind = AccBlockKind.Aerovia };
        var coord = await _service.DeriveCoordinationAsync(Acc, block, "LIRR_NE_CTR");

        var accGroup = coord.Sectors.SelectMany(s => s.Accs).Single(a => a.Extras.Count > 0);
        var sorvoli = Assert.Single(accGroup.Extras);
        Assert.Equal("Sorvoli", sorvoli.KindLabel);
        var row = Assert.Single(sorvoli.Rows);
        Assert.Equal(TransferFlowKind.Overflight, row.Kind);
        Assert.StartsWith("Roma Radar EW trasferisce a Roma Radar NE", row.Sentence);
        Assert.DoesNotContain("destinazione", row.Sentence!);
        // Nessun aeroporto «—» spurio: gli Extras non stanno fra gli Airports.
        Assert.Empty(accGroup.Airports);
    }

    [Fact]
    public async Task Config_Table_Derives_Accorpamento()
    {
        // Albero NE = {NE, TS} (TS figlio di NE). Config apre solo NE → NE assorbe NE + TS.
        var block = new AccBlock
        {
            Key = "aerovia", Kind = AccBlockKind.Aerovia,
            Configurations =
            {
                new AccConfiguration { Key = "c1", Name = "NE unificato",
                    Open = { new AccConfigOpen { Callsign = "LIRR_NE_CTR", CenterPoint = "GINEL", Range = "140" } } },
            },
        };

        var tables = await _service.DeriveConfigTableAsync(Acc, block, "LIRR_NE_CTR");
        var t = Assert.Single(tables);
        var row = Assert.Single(t.Rows);                      // un solo settore unificato (NE)
        Assert.Equal("LIRR_NE_CTR", row.UnifiedCallsign);
        Assert.Equal("GINEL", row.CenterPoint);
        Assert.Equal("140", row.Range);
        Assert.Equal(2, row.Absorbed.Count);                 // accorpa NE + TS
    }

    [Fact]
    public async Task DeriveAorView_Appends_Extra_Shapes_After_Sectors_Dedup()
    {
        // Blocco Aerovia (membri = tutti i CTR). Config "all" apre i CTR; aggiungo shape extra:
        //  - LIRP_APP (APP d'aeroporto, con poligono) → appesa come anello extra;
        //  - LIRR_NE_CTR → già presente tra i settori → NON duplicata.
        var block = new AccBlock
        {
            Key = "aerovia", Kind = AccBlockKind.Aerovia,
            Configurations =
            {
                new AccConfiguration { Key = "c1", Name = "Tutti",
                    Open = { new AccConfigOpen { Callsign = "LIRR_NE_CTR" }, new AccConfigOpen { Callsign = "LIRR_EW_CTR" } } },
            },
            ExtraAorCallsigns = { "LIRP_APP", "LIRR_NE_CTR" },
        };

        var view = await _service.DeriveAorViewAsync(Acc, block);

        Assert.Contains(view.Sectors, s => s.Callsign == "LIRP_APP");    // shape extra appesa
        Assert.Single(view.Sectors, s => s.Callsign == "LIRR_NE_CTR");   // no duplicati per l'extra già presente
    }

    [Fact]
    public async Task DeriveAorView_Colors_Default_By_Type_And_Honor_Override()
    {
        var block = new AccBlock
        {
            Key = "aerovia", Kind = AccBlockKind.Aerovia,
            Configurations =
            {
                new AccConfiguration { Key = "c1", Name = "Tutti",
                    Open = { new AccConfigOpen { Callsign = "LIRR_NE_CTR" }, new AccConfigOpen { Callsign = "LIRR_EW_CTR" } } },
            },
            ExtraAorCallsigns = { "LIRP_APP" },
            AorColorOverrides = { ["LIRR_NE_CTR"] = "#123456" },
        };

        var view = await _service.DeriveAorViewAsync(Acc, block);
        Assert.Equal("#123456", view.Sectors.Single(s => s.Callsign == "LIRR_NE_CTR").Color);                         // override
        Assert.Equal(Vipi.Application.Aor.AorColorScheme.Defaults["CTR"], view.Sectors.Single(s => s.Callsign == "LIRR_EW_CTR").Color);  // default CTR
        Assert.Equal(Vipi.Application.Aor.AorColorScheme.Defaults["APP"], view.Sectors.Single(s => s.Callsign == "LIRP_APP").Color);     // default APP (extra)
    }

    [Fact]
    public async Task Regulated_Aerovia_Auto_Returns_All_Own_Acc_Areas()
    {
        _db.Accs.Add(new Acc { Code = "LIMM", Name = "Milano" });
        _db.SpecialAreas.AddRange(Area("a1", Acc, "R14A"), Area("a2", Acc, "R99"), Area("x1", "LIMM", "Other"));
        await _db.SaveChangesAsync();

        // Aerovia con Regulated di default (OwnAuto=true) → tutte e sole le aree del proprio ACC (ordinate per nome).
        var block = new AccBlock { Key = "aerovia", Kind = AccBlockKind.Aerovia };
        var views = await _service.GetAttachedSpecialAreasAsync(Acc, block);
        Assert.Equal(new[] { "a1", "a2" }, views.Select(v => v.IvaoId));
    }

    [Fact]
    public async Task Regulated_Aerovia_Manual_Subset_Plus_Extra_Other_Acc()
    {
        _db.Accs.Add(new Acc { Code = "LIMM", Name = "Milano" });
        _db.SpecialAreas.AddRange(Area("a1", Acc, "AAA"), Area("a2", Acc, "BBB"), Area("x1", "LIMM", "XXX"));
        await _db.SaveChangesAsync();

        var block = new AccBlock
        {
            Key = "aerovia", Kind = AccBlockKind.Aerovia,
            Regulated = new RegulatedSelection { OwnAuto = false, OwnIds = { "a2" }, ExtraIds = { "x1" } },
        };
        var views = await _service.GetAttachedSpecialAreasAsync(Acc, block);
        Assert.Equal(new[] { "a2", "x1" }, views.Select(v => v.IvaoId));   // sottoinsieme proprio poi extra
    }

    [Fact]
    public async Task Regulated_AppGroup_Ignores_Auto_And_Uses_OwnIds_Only()
    {
        _db.SpecialAreas.Add(Area("a1", Acc, "AAA"));
        await _db.SaveChangesAsync();

        // Gruppo APP con OwnAuto=true (default): l'automatico vale solo per Aerovia → nessuna area.
        var block = new AccBlock { Key = "grp:1", Kind = AccBlockKind.AppGroup };
        var views = await _service.GetAttachedSpecialAreasAsync(Acc, block);
        Assert.Empty(views);
    }

    [Fact]
    public async Task ListOtherAccSpecialAreas_Excludes_Own_Acc()
    {
        _db.Accs.AddRange(new Acc { Code = "LIMM", Name = "Milano" }, new Acc { Code = "LIBB", Name = "Brindisi" });
        _db.SpecialAreas.AddRange(Area("a1", Acc, "AAA"), Area("x1", "LIMM", "XXX"), Area("x2", "LIBB", "YYY"));
        await _db.SaveChangesAsync();

        var others = await _service.ListOtherAccSpecialAreasAsync(Acc);
        Assert.DoesNotContain(others, p => p.Centers.Contains(Acc));
        Assert.Contains(others, p => p.IvaoId == "x1" && p.Centers.SequenceEqual(new[] { "LIMM" }));
        Assert.Contains(others, p => p.IvaoId == "x2" && p.Centers.SequenceEqual(new[] { "LIBB" }));
    }

    [Fact]
    public async Task Area_listed_by_two_accs_is_own_for_both()
    {
        _db.Accs.Add(new Acc { Code = "LIZZ", Name = "Legion" });
        _db.SpecialAreas.Add(Area("8870", Acc, "LI R49 - Zita", "LIZZ"));   // civile + militare, come sulla sorgente
        await _db.SaveChangesAsync();

        var mine = await _service.ListSpecialAreasByAccAsync(Acc);
        var military = await _service.ListSpecialAreasByAccAsync("LIZZ");

        Assert.Contains(mine, p => p.IvaoId == "8870");
        Assert.Contains(military, p => p.IvaoId == "8870");
        Assert.DoesNotContain(await _service.ListOtherAccSpecialAreasAsync(Acc), p => p.IvaoId == "8870");
    }

    // ---- helper ----

    private static SpecialArea Area(string ivaoId, string acc, string name, params string[] alsoAccs) => new()
    {
        IvaoId = ivaoId,
        Name = name,
        Centers = new[] { acc }.Concat(alsoAccs)
            .Select(a => new SpecialAreaCenter { IvaoId = ivaoId, CenterId = a }).ToList(),
    };

    private static AirportSector ApSec(string compose, string position, string freq, string? poly) => new()
    {
        ComposePosition = compose, AirportIcao = "LIRP", AccCode = "LIRR", Position = position,
        Frequency = freq, RegionMapPolygon = poly,
    };

    private static AccSector AcSec(string compose, string poly) => new()
    {
        ComposePosition = compose, CenterId = "LIRR", RegionMapPolygon = poly,
    };

    /// <summary>Un accordo con una clausola sola: la forma piu' corta di un caso di prova, adesso che il
    /// ricevente e' dell'accordo e non della riga.</summary>
    private static async Task Agreement(EfAgreementRepository tr, int from, int to, TransferFlowKind kind,
        string? icao, string cops, int level)
    {
        var sec = await SectionAsync(tr, from, to, kind, icao);
        await tr.AddClauseAsync(Acc, sec, new AgreementClauseInput
        {
            Cops = cops, LevelValue = level, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrAbove,
        });
    }

    /// <summary>
    /// La sezione dove finiranno le clausole: l'accordo fra i due enti (riusato se c'è già — ne esiste UNO solo
    /// per coppia) e dentro una sezione col traffico e lo scalo.
    /// <para>⚠️ Il verso si sceglie a mano perché <paramref name="from"/> dice chi cede, e dopo la canonizzazione
    /// dei lati «chi cede» può essere finito su A o su B: leggerlo dall'accordo salvato è l'unico modo di non
    /// scrivere il contrario di ciò che il caso di prova intende.</para>
    /// </summary>
    private static async Task<int> SectionAsync(EfAgreementRepository tr, int from, int to,
        TransferFlowKind kind, string? icao)
    {
        var id = await tr.FindByPairAsync(Acc, from, to)
                 ?? await tr.AddAgreementAsync(Acc, new AgreementInput { SideASectorId = from, SideBSectorId = to });

        var a = (await tr.ListByAccAsync(Acc)).First(x => x.Id == id);
        var direction = a.SideA.SectorId == from ? AgreementDirection.AtoB : AgreementDirection.BtoA;

        return await tr.AddSectionAsync(Acc, id, new AgreementSectionInput
        {
            Kind = kind,
            Direction = direction,
            Airports = icao is null ? Array.Empty<AgreementAirportInput>() : new[] { new AgreementAirportInput(icao) },
        });
    }

    private sealed class AllowAuthz : IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public VipiRole Role => IsAdmin ? VipiRole.Admin : VipiRole.User;
        public int? CurrentUserId => 1;
        public string? CurrentName => "test";
        public void EnsureAdmin() { }
    }
}
