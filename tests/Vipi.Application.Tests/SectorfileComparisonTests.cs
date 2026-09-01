using Vipi.Application.Abstractions;
using Vipi.Application.Diagnostics;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il confronto fra i cataloghi IVAO e il sectorfile Aurora.
///
/// <para>⚠️ <b>Metà di questi test difende dal RUMORE, non dai difetti.</b> La misura del 1 settembre 2026
/// sui dati veri ha mostrato che un confronto ingenuo produce ~300 righe di cui quasi nessuna vera: 142
/// callsign esteri, 25 ATIS, 40 piste che differiscono per uno zero iniziale, 96 pseudo-piste, 115 QFU. Ogni
/// filtro qui sotto corrisponde a una di quelle famiglie, e toglierlo «per prudenza» rende la pagina
/// illeggibile — che è il solo modo in cui questa funzione può fallire davvero.</para>
///
/// <para>Carta: <c>docs/design/piano-coerenza-sectorfile.md</c>.</para>
/// </summary>
public class SectorfileComparisonTests
{
    private static SectorfileFacts Sf(
        IEnumerable<SectorfilePosition>? pos = null,
        IEnumerable<SectorfileAirport>? apt = null,
        IEnumerable<SectorfileRunwayEnd>? rwy = null) =>
        new(pos?.ToList() ?? new List<SectorfilePosition>(),
            apt?.ToList() ?? new List<SectorfileAirport>(),
            rwy?.ToList() ?? new List<SectorfileRunwayEnd>());

    private static SectorfileComparisonDataset Casa(
        IEnumerable<VipiAtcPosition>? pos = null,
        IEnumerable<VipiAirport>? apt = null,
        IEnumerable<VipiRunwayEnd>? rwy = null,
        params string[] acc) => new()
        {
            Positions = pos?.ToList() ?? new List<VipiAtcPosition>(),
            Airports = apt?.ToList() ?? new List<VipiAirport>(),
            RunwayEnds = rwy?.ToList() ?? new List<VipiRunwayEnd>(),
            AccCodes = acc.ToHashSet(StringComparer.Ordinal),
        };

    // ---------------------------------------------------------------------------------- la sorgente muta

    /// <summary>
    /// ⚠️ Sorgente che non risponde ⇒ <b>zero rilievi</b>, non «tutto divergente». Confrontare contro il
    /// vuoto aprirebbe una riga su ogni posizione, aeroporto e pista che abbiamo — centinaia — e sarebbe
    /// anche il modo più efficace di far ignorare la pagina per sempre.
    /// </summary>
    [Fact]
    public void Se_la_sorgente_non_risponde_non_si_rileva_niente()
    {
        var d = Casa(pos: new[] { new VipiAtcPosition("LIRF_TWR", "118.700", false) });
        Assert.Empty(SectorfileComparison.Analyze(null, d));
    }

    // -------------------------------------------------------------------------------- A. le frequenze

    [Fact]
    public void Una_frequenza_diversa_e_un_rilievo()
    {
        var f = SectorfileComparison.Analyze(
            Sf(pos: new[] { new SectorfilePosition("LIRM_APP", "135.255") }),
            Casa(pos: new[] { new VipiAtcPosition("LIRM_APP", "132.255", false) }));

        var r = Assert.Single(f);
        Assert.Equal(SectorfileComparison.CatFrequenza, r.CategoryKey);
        Assert.Contains("132.255", r.DetailArgs!.Select(a => a.ToString()));
        Assert.Contains("135.255", r.DetailArgs!.Select(a => a.ToString()));
    }

    /// <summary>
    /// ⚠️ Cinque kHz <b>non</b> sono una divergenza: nella spaziatura 8.33 lo stesso canale si scrive
    /// <c>118.955</c> o <c>118.950</c>. Misurato: senza questa tolleranza uscivano 7 rilievi invece di 6, e
    /// quello in più era un canale scritto nei due modi.
    /// </summary>
    [Theory]
    [InlineData("118.955", "118.950", false)]
    [InlineData("118.180", "118.175", false)]
    [InlineData("119.995", "119.955", true)]
    [InlineData("118.700", "118.700", false)]
    [InlineData("118.5", "118.500", false)]
    public void La_tolleranza_di_canale_833_non_si_confonde_con_una_divergenza(
        string casa, string sectorfile, bool atteso)
    {
        var f = SectorfileComparison.Analyze(
            Sf(pos: new[] { new SectorfilePosition("LIRF_TWR", sectorfile) }),
            Casa(pos: new[] { new VipiAtcPosition("LIRF_TWR", casa, false) }));

        Assert.Equal(atteso, f.Any(x => x.CategoryKey == SectorfileComparison.CatFrequenza));
    }

    /// <summary>
    /// ⚠️ I confinanti esteri stanno nei nostri cataloghi (misurato: <b>142</b> callsign su 345) e il
    /// sectorfile italiano non ha nessuna ragione di elencarli. Senza questo filtro la famiglia «posizioni»
    /// nasce con 142 rilievi falsi e nessuno la guarda mai più.
    /// </summary>
    [Fact]
    public void I_callsign_esteri_restano_fuori_dal_confronto()
    {
        var f = SectorfileComparison.Analyze(
            Sf(pos: new[] { new SectorfilePosition("LIRF_TWR", "118.700") }),
            Casa(pos: new[]
            {
                new VipiAtcPosition("LIRF_TWR", "118.700", false),
                new VipiAtcPosition("LDZO_CTR", "134.075", false),
                new VipiAtcPosition("DAAA_N_CTR", "120.100", false),
            }));

        Assert.Empty(f);
    }

    /// <summary>⚠️ In vIPI gli ATIS sono posizioni; nel sectorfile stanno nei file <c>.atis</c>, che è un
    /// altro posto. Misurato: 25 callsign, tutti falsi positivi.</summary>
    [Fact]
    public void Gli_atis_restano_fuori_dal_confronto()
    {
        var f = SectorfileComparison.Analyze(
            Sf(),
            Casa(pos: new[] { new VipiAtcPosition("LIRF_ATIS", "126.550", false) }));

        Assert.Empty(f);
    }

    /// <summary>Un ente estero catalogato a mano: la sorgente non l'ha mai mandato, il sectorfile non deve
    /// averlo.</summary>
    [Fact]
    public void Le_righe_manuali_restano_fuori_dal_confronto()
    {
        var f = SectorfileComparison.Analyze(
            Sf(),
            Casa(pos: new[] { new VipiAtcPosition("LIPP_XX_CTR", "121.000", IsManual: true) }));

        Assert.Empty(f);
    }

    /// <summary>
    /// ⚠️ <b>Mai un rilievo su un dato che manca a noi.</b> Un campo vuoto di casa è «non lo so», e «non lo
    /// so» non è «le due sorgenti non concordano». La regola vale in tutte e tre le famiglie.
    /// </summary>
    [Fact]
    public void Un_dato_mancante_di_casa_non_e_una_divergenza()
    {
        var f = SectorfileComparison.Analyze(
            Sf(pos: new[] { new SectorfilePosition("LIRF_TWR", "118.700") }),
            Casa(pos: new[] { new VipiAtcPosition("LIRF_TWR", null, false) }));

        Assert.Empty(f);
    }

    [Fact]
    public void Le_posizioni_presenti_da_una_parte_sola_si_dicono_nei_due_versi()
    {
        var f = SectorfileComparison.Analyze(
            Sf(pos: new[] { new SectorfilePosition("LIDA_I_TWR", "119.000") }),
            Casa(pos: new[] { new VipiAtcPosition("LIRR_NE1_CTR", "128.800", false) }));

        Assert.Contains(f, x => x.CategoryKey == SectorfileComparison.CatPosSoloSf);
        Assert.Contains(f, x => x.CategoryKey == SectorfileComparison.CatPosSoloVipi);
    }

    // -------------------------------------------------------------------------------- B. gli aeroporti

    [Fact]
    public void Una_ta_diversa_e_un_rilievo()
    {
        var f = SectorfileComparison.Analyze(
            Sf(apt: new[] { new SectorfileAirport("LIMF", 909, 7000, null, null, "TORINO") }),
            Casa(apt: new[] { new VipiAirport("LIMF", 6000, 909, null, null) }));

        var r = Assert.Single(f);
        Assert.Equal(SectorfileComparison.CatTa, r.CategoryKey);
    }

    /// <summary>⚠️ Nel file <c>itap.ap</c> la TA a zero significa «non dichiarata» — 24 aeroporti su 130 — e
    /// il parser la porta a null: qui si verifica che non diventi «divergente da 6000».</summary>
    [Fact]
    public void Una_ta_non_dichiarata_non_e_divergente()
    {
        var f = SectorfileComparison.Analyze(
            Sf(apt: new[] { new SectorfileAirport("LIMF", 909, null, null, null, "TORINO") }),
            Casa(apt: new[] { new VipiAirport("LIMF", 6000, 909, null, null) }));

        Assert.Empty(f);
    }

    /// <summary>
    /// ⚠️ Il sectorfile elenca 44 scali che non documentiamo, più voci che aeroporti non sono
    /// (<c>LIZZ … AIR DEFENCE</c>): «solo nel sectorfile» è la normalità, non un rilievo. Il verso che conta
    /// è l'altro.
    /// </summary>
    [Fact]
    public void Solo_il_verso_che_conta_apre_un_rilievo_sugli_aeroporti()
    {
        var f = SectorfileComparison.Analyze(
            Sf(apt: new[] { new SectorfileAirport("LIZZ", 0, null, null, null, "AIR DEFENCE") }),
            Casa(apt: new[] { new VipiAirport("LIQV", null, null, null, null) }));

        var r = Assert.Single(f);
        Assert.Equal(SectorfileComparison.CatAptSoloVipi, r.CategoryKey);
        Assert.Equal("LIQV", r.EntityArgs![0]);
    }

    /// <summary>⚠️ Nella tabella degli aeroporti di vIPI ci sono anche i codici ACC. Senza escluderli, il
    /// confronto direbbe «il sectorfile non ha l'aeroporto LIRR» — che non è un aeroporto.</summary>
    [Fact]
    public void I_codici_acc_non_sono_aeroporti()
    {
        var f = SectorfileComparison.Analyze(
            Sf(),
            Casa(apt: new[] { new VipiAirport("LIRR", null, null, null, null) }, acc: "LIRR"));

        Assert.Empty(f);
    }

    [Fact]
    public void Elevazione_e_coordinate_hanno_una_tolleranza()
    {
        var uguali = SectorfileComparison.Analyze(
            Sf(apt: new[] { new SectorfileAirport("LIRF", 20, null, 41.8005, 12.2389, "FIUMICINO") }),
            Casa(apt: new[] { new VipiAirport("LIRF", null, 14, 41.8003, 12.2388) }));
        Assert.Empty(uguali);

        var diversi = SectorfileComparison.Analyze(
            Sf(apt: new[] { new SectorfileAirport("LIRF", 300, null, 42.5, 12.2389, "FIUMICINO") }),
            Casa(apt: new[] { new VipiAirport("LIRF", null, 14, 41.8003, 12.2388) }));
        Assert.Contains(diversi, x => x.CategoryKey == SectorfileComparison.CatElevazione);
        Assert.Contains(diversi, x => x.CategoryKey == SectorfileComparison.CatCoordinate);
    }

    // ------------------------------------------------------------------------------------- C. le piste

    /// <summary>
    /// La rinumerazione applicata da una parte sola — misurata su una dozzina di aeroporti veri, fra cui
    /// <c>LIRP</c> (3L/3R/21L/21R contro 4L/4R/22L/22R).
    ///
    /// <para>⚠️ <b>Un rilievo per aeroporto, non uno per pista.</b> Quattro righe che dicono la stessa cosa
    /// sono quattro modi di non farla leggere.</para>
    /// </summary>
    [Fact]
    public void I_designatori_divergenti_fanno_un_rilievo_per_aeroporto()
    {
        var f = SectorfileComparison.Analyze(
            Sf(rwy: new[]
            {
                new SectorfileRunwayEnd("LIRP", "3L", null, null, null),
                new SectorfileRunwayEnd("LIRP", "3R", null, null, null),
                new SectorfileRunwayEnd("LIRP", "21L", null, null, null),
                new SectorfileRunwayEnd("LIRP", "21R", null, null, null),
            }),
            Casa(rwy: new[]
            {
                new VipiRunwayEnd("LIRP", "4L", null, null),
                new VipiRunwayEnd("LIRP", "4R", null, null),
                new VipiRunwayEnd("LIRP", "22L", null, null),
                new VipiRunwayEnd("LIRP", "22R", null, null),
            }));

        var r = Assert.Single(f);
        Assert.Equal(SectorfileComparison.CatPiste, r.CategoryKey);
        Assert.Contains("4L", r.DetailArgs![1].ToString());
        Assert.Contains("21R", r.DetailArgs![2].ToString());
    }

    /// <summary>⚠️ Lo zero iniziale c'è da una parte sola (<c>09</c> contro <c>9</c>): normalizzato prima
    /// del confronto, o ~40 piste risultano assenti per una cifra e le dodici vere spariscono nel rumore.
    /// La normalizzazione è del parser, ed è la stessa sui due lati; qui si verifica l'effetto.</summary>
    [Fact]
    public void Lo_stesso_ident_normalizzato_non_diverge()
    {
        var f = SectorfileComparison.Analyze(
            Sf(rwy: new[] { new SectorfileRunwayEnd("LIAF", "17", null, null, null) }),
            Casa(rwy: new[] { new VipiRunwayEnd("LIAF", "17", null, null) }));

        Assert.Empty(f);
    }

    [Fact]
    public void Una_soglia_lontana_e_un_rilievo_e_una_vicina_no()
    {
        var vicina = SectorfileComparison.Analyze(
            Sf(rwy: new[] { new SectorfileRunwayEnd("LIRF", "16L", null, 41.8460, 12.2615) }),
            Casa(rwy: new[] { new VipiRunwayEnd("LIRF", "16L", 41.8461, 12.2615) }));
        Assert.Empty(vicina);

        var lontana = SectorfileComparison.Analyze(
            Sf(rwy: new[] { new SectorfileRunwayEnd("LIEO", "23", null, 40.9000, 9.5000) }),
            Casa(rwy: new[] { new VipiRunwayEnd("LIEO", "23", 40.9040, 9.5000) }));
        var r = Assert.Single(lontana);
        Assert.Equal(SectorfileComparison.CatSoglia, r.CategoryKey);
    }

    /// <summary>Un aeroporto che il sectorfile non ha non produce rilievi di pista: quello lo dice — se lo
    /// dice — la famiglia degli aeroporti, una volta sola.</summary>
    [Fact]
    public void Le_piste_di_un_aeroporto_non_confrontabile_non_si_giudicano()
    {
        var f = SectorfileComparison.Analyze(
            Sf(),
            Casa(rwy: new[] { new VipiRunwayEnd("LIQV", "9", null, null) }));

        Assert.Empty(f);
    }

    // -------------------------------------------------------------------------------- la forma dei rilievi

    /// <summary>
    /// ⚠️ <b>Tutti Warning, tutti dell'area Sectorfile, tutti senza link.</b> Un <c>Error</c> direbbe «qui
    /// c'è qualcosa di rotto <i>da noi</i>», e non lo sappiamo: chi ha ragione fra le due sorgenti è
    /// esattamente la domanda a cui questo confronto non risponde. E <c>Where</c> è null perché la
    /// riparazione non sta in questa applicazione — sta nel sectorfile, su GitHub.
    /// </summary>
    [Fact]
    public void Ogni_rilievo_e_un_avviso_dell_area_sectorfile_senza_link()
    {
        var f = TuttiIRilievi();

        Assert.NotEmpty(f);
        Assert.All(f, x =>
        {
            Assert.Equal(ConsistencySeverity.Warning, x.Severity);
            Assert.Equal(ConsistencyArea.Sectorfile, x.Area);
            Assert.Null(x.Where);
        });
    }

    /// <summary>
    /// ⚠️ Ogni rilievo porta <b>chiavi</b> di traduzione oltre al testo grezzo: il testo grezzo lo leggono
    /// l'health check e i log, dove una lingua d'interfaccia non esiste; le chiavi le legge chi lo mostra.
    /// Senza, la pagina inglese resta in italiano — è già successo, ed è la ragione per cui il modello ha
    /// due modi di dire la stessa cosa.
    /// </summary>
    [Fact]
    public void Ogni_rilievo_porta_le_chiavi_di_traduzione()
    {
        Assert.All(TuttiIRilievi(), x =>
        {
            Assert.False(string.IsNullOrWhiteSpace(x.CategoryKey));
            Assert.False(string.IsNullOrWhiteSpace(x.DetailKey));
            Assert.False(string.IsNullOrWhiteSpace(x.EntityKey));
            Assert.False(string.IsNullOrWhiteSpace(x.Category));
            Assert.False(string.IsNullOrWhiteSpace(x.Detail));
        });
    }

    /// <summary>
    /// ⚠️ Ogni categoria che il confronto sa produrre è <b>dichiarata</b> in <c>Categorie</c> e ha una
    /// famiglia. Serve perché la pagina raggruppa per famiglia: una categoria nuova non dichiarata finirebbe
    /// silenziosamente fra le posizioni — visibile, ma nel posto sbagliato, che è il modo peggiore di
    /// sbagliare.
    /// </summary>
    [Fact]
    public void Ogni_categoria_prodotta_e_dichiarata()
    {
        var prodotte = TuttiIRilievi().Select(x => x.CategoryKey!).Distinct().ToList();

        Assert.Equal(SectorfileComparison.Categorie.OrderBy(x => x, StringComparer.Ordinal),
            prodotte.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(SectorfileComparison.CatFrequenza, SectorfileComparison.Famiglia.Posizioni)]
    [InlineData(SectorfileComparison.CatPosSoloSf, SectorfileComparison.Famiglia.Posizioni)]
    [InlineData(SectorfileComparison.CatPosSoloVipi, SectorfileComparison.Famiglia.Posizioni)]
    [InlineData(SectorfileComparison.CatTa, SectorfileComparison.Famiglia.Aeroporti)]
    [InlineData(SectorfileComparison.CatElevazione, SectorfileComparison.Famiglia.Aeroporti)]
    [InlineData(SectorfileComparison.CatCoordinate, SectorfileComparison.Famiglia.Aeroporti)]
    [InlineData(SectorfileComparison.CatAptSoloVipi, SectorfileComparison.Famiglia.Aeroporti)]
    [InlineData(SectorfileComparison.CatPiste, SectorfileComparison.Famiglia.Piste)]
    [InlineData(SectorfileComparison.CatSoglia, SectorfileComparison.Famiglia.Piste)]
    public void Ogni_categoria_sta_nella_sua_famiglia(string categoria, SectorfileComparison.Famiglia attesa)
    {
        var finto = new ConsistencyFinding("x", ConsistencySeverity.Warning, "x", "x",
            ConsistencyArea.Sectorfile, CategoryKey: categoria);

        Assert.Equal(attesa, SectorfileComparison.FamigliaDi(finto));
    }

    /// <summary>Un dataset che tocca <b>tutte</b> e nove le famiglie di rilievo, una volta ciascuna.</summary>
    private static IReadOnlyList<ConsistencyFinding> TuttiIRilievi() => SectorfileComparison.Analyze(
        Sf(
            pos: new[]
            {
                new SectorfilePosition("LIRM_APP", "135.255"),      // frequenza divergente
                new SectorfilePosition("LIDA_I_TWR", "119.000"),    // solo nel sectorfile
            },
            apt: new[]
            {
                new SectorfileAirport("LIMF", 300, 7000, 42.5, 12.2, "TORINO"),   // TA + elev + coordinate
            },
            rwy: new[]
            {
                new SectorfileRunwayEnd("LIRP", "3L", null, 43.6800, 10.3900),    // designatori
                new SectorfileRunwayEnd("LIRP", "22R", null, 43.7000, 10.4000),   // soglia
            }),
        Casa(
            pos: new[]
            {
                new VipiAtcPosition("LIRM_APP", "132.255", false),
                new VipiAtcPosition("LIRR_NE1_CTR", "128.800", false),            // solo in vIPI
            },
            apt: new[]
            {
                new VipiAirport("LIMF", 6000, 909, 41.8, 12.2),
                new VipiAirport("LIQV", null, null, null, null),                  // solo in vIPI
            },
            rwy: new[]
            {
                new VipiRunwayEnd("LIRP", "4L", 43.6800, 10.3900),
                new VipiRunwayEnd("LIRP", "22R", 43.7040, 10.4000),
            }));
}
