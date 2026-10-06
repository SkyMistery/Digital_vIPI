using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Un solo motore di copertura: la catena di ripiego (righe con la fascia, poi il padre) la leggono anche la
/// mappa AoR, la tabella delle configurazioni e chi cede un trasferimento — non più i soli trasferimenti dal
/// lato di chi riceve. Carta <c>docs/feature/2026-10-04-copertura-unica.md</c>.
///
/// <para>Il banco è Milano com'è in produzione: WS2 radice, ES2 e WS5 figli di WS2, <b>ES5 figlio di ES2</b>,
/// e una riga sola — «ES5, FL325–UNL → WS5». Le due configurazioni che il committente non riusciva a far
/// uscire giuste insieme (4 ottobre 2026) sono «WS2 + ES2 + WS5» (sopra FL325 il solo WS5) e «WS2 + ES2»
/// (tutti e due fino a UNL): con i soli padri una delle due è sbagliata per forza, qualunque padre si dia a
/// ES5.</para>
/// </summary>
public class CoperturaUnicaTests
{
    private const string Ws2 = "LIMM_WS2_CTR", Es2 = "LIMM_ES2_CTR", Ws5 = "LIMM_WS5_CTR", Es5 = "LIMM_ES5_CTR";
    private const int Split = 32500;

    private static readonly StringComparer OIC = StringComparer.OrdinalIgnoreCase;

    private static Topology Milano() => new()
    {
        Sectors = new[] { Ws2, Es2, Ws5, Es5 },
        Parent = new Dictionary<string, string>(OIC) { [Es2] = Ws2, [Ws5] = Ws2, [Es5] = Es2 },
        Fallbacks = new Dictionary<string, IReadOnlyList<FallbackRow>>(OIC)
        {
            [Es5] = new[] { new FallbackRow(Ws5, BaseFeet: Split, TopFeet: null) },
        },
        Bands = new Dictionary<string, (int? BaseFeet, int? TopFeet)>(OIC)
        {
            [Ws2] = (0, Split), [Es2] = (0, Split), [Ws5] = (Split, null), [Es5] = (Split, null),
        },
    };

    private static IReadOnlySet<string> Online(params string[] cs) => new HashSet<string>(cs, OIC);

    // ---- Il cuore: chi tiene il cielo di un settore, fascia per fascia ----------------------------------

    private static IReadOnlyList<FallbackHolding> Tiene(Topology t, string settore, params string[] online)
    {
        var aperti = Online(online);
        var banda = t.Bands.TryGetValue(settore, out var b) ? b : (null, null);
        return FallbackChain.Holders(settore, banda.BaseFeet, banda.TopFeet, t.Fallbacks, t.ParentOf, aperti.Contains);
    }

    [Fact]
    public void ES5_chiuso_con_WS5_aperto_e_di_WS5_per_intero()
    {
        // La riga «FL325–UNL» copre tutta la banda di ES5, che parte da FL325: una fascia sola, non due.
        var fasce = Tiene(Milano(), Es5, Ws2, Es2, Ws5);

        var f = Assert.Single(fasce);
        Assert.Equal(Ws5, f.Holder);
    }

    [Fact]
    public void ES5_chiuso_con_WS5_chiuso_torna_al_padre_ES2()
    {
        var f = Assert.Single(Tiene(Milano(), Es5, Ws2, Es2));
        Assert.Equal(Es2, f.Holder);
    }

    [Fact]
    public void Un_settore_aperto_tiene_se_stesso()
    {
        var f = Assert.Single(Tiene(Milano(), Es5, Ws2, Es2, Ws5, Es5));
        Assert.Equal(Es5, f.Holder);
    }

    [Fact]
    public void Nessuno_della_catena_in_frequenza_nessuno_lo_tiene()
    {
        var f = Assert.Single(Tiene(Milano(), Es5));
        Assert.Null(f.Holder);
    }

    /// <summary>Un settore SFC–UNL con una riga che vale solo sopra FL325: il suo cielo si divide in due mani.</summary>
    private static Topology Diviso(int? piede = 0, int? tetto = null) => new()
    {
        Sectors = new[] { "XX_CTR", "ALTO_CTR", "PADRE_CTR" },
        Parent = new Dictionary<string, string>(OIC) { ["XX_CTR"] = "PADRE_CTR" },
        Fallbacks = new Dictionary<string, IReadOnlyList<FallbackRow>>(OIC)
        {
            ["XX_CTR"] = new[] { new FallbackRow("ALTO_CTR", BaseFeet: Split, TopFeet: null) },
        },
        Bands = piede is null && tetto is null
            ? new Dictionary<string, (int? BaseFeet, int? TopFeet)>(OIC)
            : new Dictionary<string, (int? BaseFeet, int? TopFeet)>(OIC) { ["XX_CTR"] = (piede, tetto) },
    };

    [Fact]
    public void Una_riga_che_vale_per_una_parte_della_banda_divide_il_settore()
    {
        var fasce = Tiene(Diviso(), "XX_CTR", "PADRE_CTR", "ALTO_CTR");

        Assert.Equal(2, fasce.Count);
        Assert.Equal(new FallbackHolding(0, Split, "PADRE_CTR"), fasce[0]);
        Assert.Equal(new FallbackHolding(Split, null, "ALTO_CTR"), fasce[1]);
    }

    [Fact]
    public void Due_fasce_nella_stessa_mano_sono_una_fascia_sola()
    {
        // ALTO chiuso: sopra e sotto FL325 raccoglie il padre, e il taglio non si vede più.
        var f = Assert.Single(Tiene(Diviso(), "XX_CTR", "PADRE_CTR"));

        Assert.Equal(new FallbackHolding(0, null, "PADRE_CTR"), f);
    }

    [Fact]
    public void Senza_una_banda_nota_il_taglio_della_riga_si_vede_lo_stesso()
    {
        // «Non so dove comincia e finisce» non autorizza a ignorare la fascia scritta: sotto il padre, sopra la riga.
        var fasce = Tiene(Diviso(piede: null), "XX_CTR", "PADRE_CTR", "ALTO_CTR");

        Assert.Equal(new[] { "PADRE_CTR", "ALTO_CTR" }, fasce.Select(f => f.Holder));
        Assert.Null(fasce[0].BaseFeet);
        Assert.Equal(Split, fasce[0].TopFeet);
    }

    [Fact]
    public void Una_riga_fuori_dalla_banda_del_settore_non_lo_divide()
    {
        // Il settore finisce a FL195: la riga «sopra FL325» non lo riguarda, e resta tutto al padre.
        var f = Assert.Single(Tiene(Diviso(piede: 0, tetto: 19500), "XX_CTR", "PADRE_CTR", "ALTO_CTR"));

        Assert.Equal("PADRE_CTR", f.Holder);
    }

    // ---- AoR: le due configurazioni di Milano, con UN albero -------------------------------------------

    private readonly AorService _aor = new();

    [Fact]
    public void AoR_WS2_ES2_WS5_aperti_ES5_e_di_WS5()
    {
        var r = _aor.Resolve(Milano(), Ws2, Online(Ws2, Es2, Ws5));

        Assert.Equal(Ws5, r.Ownership[Es5]);
        Assert.Equal(SectorState.Online, r.State[Es5]);
    }

    [Fact]
    public void AoR_WS2_ES2_aperti_ES5_e_di_ES2_e_WS5_di_WS2()
    {
        var r = _aor.Resolve(Milano(), Ws2, Online(Ws2, Es2));

        Assert.Equal(Es2, r.Ownership[Es5]);
        Assert.Equal(Ws2, r.Ownership[Ws5]);
    }

    [Fact]
    public void AoR_un_settore_diviso_porta_le_due_fasce_e_la_voce_sola_e_P_se_ne_tiene_una()
    {
        var r = _aor.Resolve(Diviso(), "PADRE_CTR", Online("PADRE_CTR", "ALTO_CTR"));

        Assert.Equal(2, r.Holdings["XX_CTR"].Count);
        Assert.Equal("PADRE_CTR", r.Ownership["XX_CTR"]);
        Assert.Equal(SectorState.Covered, r.State["XX_CTR"]);
    }

    [Fact]
    public void AoR_senza_righe_e_senza_bande_resta_la_risalita_dei_padri()
    {
        // La rete: a tabella dei ripieghi vuota il risultato è quello di sempre.
        var soliPadri = new Topology
        {
            Sectors = Milano().Sectors, Parent = Milano().Parent,
        };

        var r = _aor.Resolve(soliPadri, Ws2, Online(Ws2, Es2, Ws5));

        Assert.Equal(Es2, r.Ownership[Es5]);
    }

    // ---- La tabella delle configurazioni della vIPI ----------------------------------------------------

    private static AccConfiguration Config(string nome, params string[] aperti) => new()
    {
        Key = "cfg:" + nome, Name = nome,
        Open = aperti.Select(a => new AccConfigOpen { Callsign = a }).ToList(),
    };

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Tabella(Topology t, string radice,
        IEnumerable<string> pool, AccConfiguration cfg)
    {
        var viste = ConfigTableProjector.Build(new AorService(), t, new[] { radice },
            new HashSet<string>(pool, OIC), new[] { cfg });
        return Assert.Single(viste).Rows.ToDictionary(r => r.UnifiedCallsign, r => r.Absorbed, OIC);
    }

    [Fact]
    public void Le_due_configurazioni_di_Milano_escono_giuste_tutte_e_due()
    {
        var t = Milano();

        var conWs5 = Tabella(t, Ws2, t.Sectors, Config("ES2/WS2 fino a FL325, sopra WS5", Ws2, Es2, Ws5));
        Assert.Equal(new[] { Es5, Ws5 }, conWs5[Ws5]);
        Assert.Equal(new[] { Es2 }, conWs5[Es2]);
        Assert.Equal(new[] { Ws2 }, conWs5[Ws2]);

        var senza = Tabella(t, Ws2, t.Sectors, Config("ES2/WS2 fino a UNL", Ws2, Es2));
        Assert.Equal(new[] { Es2, Es5 }, senza[Es2]);
        Assert.Equal(new[] { Ws2, Ws5 }, senza[Ws2]);
    }

    [Fact]
    public void Un_settore_diviso_sta_sotto_tutti_e_due_con_la_sua_fascia()
    {
        var t = Diviso();

        var righe = Tabella(t, "PADRE_CTR", t.Sectors, Config("due mani", "PADRE_CTR", "ALTO_CTR"));

        Assert.Contains("XX_CTR (SFC–FL325)", righe["PADRE_CTR"]);
        Assert.Contains("XX_CTR (FL325–UNL)", righe["ALTO_CTR"]);
    }

    [Theory]
    [InlineData(0, 32500, "SFC–FL325")]
    [InlineData(32500, null, "FL325–UNL")]
    [InlineData(null, 2500, "SFC–2500 ft")]
    [InlineData(2500, 19500, "2500 ft–FL195")]
    public void La_fascia_si_scrive_senza_parole_di_una_lingua(int? piede, int? tetto, string attesa) =>
        Assert.Equal(attesa, ConfigTableProjector.Fascia(piede, tetto));

    // ---- I trasferimenti: anche chi CEDE si risolve alla quota del punto -------------------------------

    private static TransferPointRow Punto(int id, string cop, int? fl, string ricevente) => new()
    {
        Id = id, Cop = cop, LevelValue = fl, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow,
        LevelText = fl is null ? "" : $"FL{fl}", NextSectorCallsign = ricevente, Order = id,
    };

    private static TransferFlowRow Flusso(string cedente, params TransferPointRow[] punti) => new()
    {
        Id = 1, AccCode = "LIMM", OwningSectorId = 1, OwningSectorCallsign = cedente,
        Kind = TransferFlowKind.Overflight, Order = 0, Points = punti,
    };

    private static IReadOnlyList<ResolvedTransferFlow> Risolvi(TransferFlowRow f, params string[] online) =>
        TransferResolution.Resolve(new[] { f }, Milano(), Online(online), CoverageFallbackContext.Nessuno);

    [Fact]
    public void Il_flusso_di_ES5_chiuso_a_FL350_lo_cede_WS5_non_ES2()
    {
        var r = Assert.Single(Risolvi(Flusso(Es5, Punto(1, "ABCDE", 350, "LIPP_CTR")), Ws2, Es2, Ws5));

        Assert.Equal(Ws5, r.ResolvedOwnerCallsign);
        Assert.True(r.OwnerOnline);
    }

    [Fact]
    public void Due_punti_dello_stesso_flusso_possono_avere_due_cedenti()
    {
        // Il punto senza quota non può valutare la riga con la fascia: resta al padre. Il flusso esce due volte,
        // ognuna coi suoi punti — non una volta sola col cedente del primo.
        var r = Risolvi(Flusso(Es5, Punto(1, "ALTO", 350, "LIPP_CTR"), Punto(2, "SENZA", null, "LIPP_CTR")),
            Ws2, Es2, Ws5);

        Assert.Equal(2, r.Count);
        Assert.Equal(Ws5, r[0].ResolvedOwnerCallsign);
        Assert.Equal("ALTO", Assert.Single(r[0].Points).Point.Cop);
        Assert.Equal(Es2, r[1].ResolvedOwnerCallsign);
        Assert.Equal("SENZA", Assert.Single(r[1].Points).Point.Cop);
    }

    [Fact]
    public void Cedente_aperto_il_flusso_resta_suo_e_intero()
    {
        var r = Assert.Single(Risolvi(
            Flusso(Es5, Punto(1, "ALTO", 350, "LIPP_CTR"), Punto(2, "SENZA", null, "LIPP_CTR")), Ws2, Es2, Ws5, Es5));

        Assert.Equal(Es5, r.ResolvedOwnerCallsign);
        Assert.Equal(2, r.Points.Count);
    }

    [Fact]
    public void Nessuno_della_catena_in_frequenza_resta_il_cedente_scritto_e_si_dice_che_e_chiuso()
    {
        var r = Assert.Single(Risolvi(Flusso(Es5, Punto(1, "ALTO", 350, "LIPP_CTR"))));

        Assert.Equal(Es5, r.ResolvedOwnerCallsign);
        Assert.False(r.OwnerOnline);
    }

    [Fact]
    public void Chi_riceve_si_risolve_come_prima()
    {
        var r = Assert.Single(Risolvi(Flusso("LIPP_CTR", Punto(1, "ALTO", 350, Es5)), Ws2, Es2, Ws5, "LIPP_CTR"));

        Assert.Equal(Ws5, Assert.Single(r.Points).ResolvedHandler);
    }
}
