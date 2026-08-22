using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Caratterizzazione del cuore condiviso <see cref="CoordinationDerivation"/>: direzione owner→next
/// (nessun invert), passo entranti, sorvolo airport-less. Rete per le estrazioni Acc/App.</summary>
public class CoordinationDerivationTests
{
    private static readonly CoordinationSentenceTemplate Tpl = CoordinationSentenceTemplate.Default;

    private static readonly IReadOnlyDictionary<string, SectorType> Types = new Dictionary<string, SectorType>(System.StringComparer.OrdinalIgnoreCase)
    {
        ["LIBB_ES_CTR"] = SectorType.Ctr,
        ["LIRR_TS_CTR"] = SectorType.Ctr,
        ["LIRR_NE_CTR"] = SectorType.Ctr,
        ["LIRN_US0_APP"] = SectorType.App,
    };
    // Sector.Name grezzo = callsign (proiezione); il nome nice arriva da AtcCallsign.
    private static readonly IReadOnlyDictionary<string, string> Names = Types.Keys.ToDictionary(k => k, k => k, System.StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyDictionary<string, string> Codes = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        ["LIBB_ES_CTR"] = "ES",
        ["LIRR_TS_CTR"] = "TS",
        ["LIRR_NE_CTR"] = "NE",
        ["LIRN_US0_APP"] = "US0",
    };
    private static readonly IReadOnlyDictionary<string, string> Airports = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        ["LIRN"] = "Napoli Capodichino",
    };
    private static readonly IReadOnlyDictionary<string, string> Atc = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        ["LIBB_ES_CTR"] = "Brindisi Radar",
        ["LIRR_TS_CTR"] = "Roma Radar",
        ["LIRR_NE_CTR"] = "Roma Radar",
        ["LIRN_US0_APP"] = "Roma Radar",
    };

    private static TransferPointRow Point(string cop, int? value, LevelConstraint c, string next,
        LevelParity parity = LevelParity.Any, string? special = null) => new()
    {
        Id = 0, Cop = cop, LevelValue = value, LevelUnit = LevelUnit.Fl, LevelConstraint = c,
        LevelSpecial = special, Parity = parity,
        LevelText = LevelFormatting.Format(value, LevelUnit.Fl, c, special, parity),
        NextSectorCallsign = next, Order = 1,
    };

    private static TransferFlowRow Flow(string owner, TransferFlowKind kind, string? apt, params TransferPointRow[] pts) => new()
    {
        Id = 0, AccCode = "LIBB", OwningSectorId = 0, OwningSectorCallsign = owner, Kind = kind, AirportIcao = apt, Order = 1, Points = pts,
    };

    private static IReadOnlyList<CoordinationEntry> Build(IReadOnlyList<TransferFlowRow> flows, params string[] owners) =>
        CoordinationDerivation.Build(flows, new HashSet<string>(owners, System.StringComparer.OrdinalIgnoreCase),
            Types, Names, Codes, Airports, Atc, Tpl);

    [Fact]
    public void Owned_arrival_to_ctr_keeps_owner_as_sender()
    {
        var flows = new[] { Flow("LIBB_ES_CTR", TransferFlowKind.Arrival, "LIRN", Point("NILTO", 260, LevelConstraint.AtOrBelow, "LIRR_TS_CTR")) };
        var e = Assert.Single(Build(flows, "LIBB_ES_CTR"));
        Assert.False(e.IsIncoming);
        Assert.Equal("LIBB_ES_CTR", e.OurSectorCallsign);
        Assert.Equal("LIRR_TS_CTR", e.CounterpartCallsign);
        Assert.StartsWith("Brindisi Radar ES trasferisce a Roma Radar TS", e.Row.Sentence);
    }

    [Fact]
    public void Owned_arrival_to_consolidated_app_shows_identifier()
    {
        var flows = new[] { Flow("LIBB_ES_CTR", TransferFlowKind.Arrival, "LIRN", Point("AMSOR", 200, LevelConstraint.AtOrBelow, "LIRN_US0_APP")) };
        var e = Assert.Single(Build(flows, "LIBB_ES_CTR"));
        Assert.Equal(SectorType.App, e.CounterpartType);
        Assert.StartsWith("Brindisi Radar ES trasferisce a Roma Radar US0", e.Row.Sentence);
    }

    [Fact]
    public void Owned_overflight_without_airport_composes_neutral()
    {
        var flows = new[] { Flow("LIRR_NE_CTR", TransferFlowKind.Overflight, null, Point("ELB", null, LevelConstraint.Special, "LIBB_ES_CTR", special: "per aerovia")) };
        var e = Assert.Single(Build(flows, "LIRR_NE_CTR"));
        Assert.Equal(TransferFlowKind.Overflight, e.Kind);
        Assert.Null(e.AirportIcao);
        Assert.Equal("Roma Radar NE trasferisce a Brindisi Radar ES il traffico per aerovia su ELB.", e.Row.Sentence);
    }

    [Fact]
    public void Overflight_to_foreign_confining_ctr_stays_owner_to_next()
    {
        // Sorvolo senza aeroporto che cede a un CTR estero confinante (presente nella mappa types perché
        // materializzato alla conferma del confinante): produce 1 entry, direzione owner→next, sezione sorvoli.
        var flows = new[] { Flow("LIRR_NE_CTR", TransferFlowKind.Overflight, null, Point("ELB", 350, LevelConstraint.AtOrAbove, "LIBB_ES_CTR", parity: LevelParity.Odd)) };
        var e = Assert.Single(Build(flows, "LIRR_NE_CTR"));
        Assert.False(e.IsIncoming);
        Assert.Equal("LIRR_NE_CTR", e.OurSectorCallsign);
        Assert.Equal("LIBB_ES_CTR", e.CounterpartCallsign);
        Assert.Equal(TransferFlowKind.Overflight, e.Kind);
    }

    [Fact]
    public void Point_with_unresolved_next_is_dropped()
    {
        // Causa radice del "non compare": un ricevente non risolto (non in types) fa scartare il punto →
        // il flusso non produce entry. Policy intenzionale; l'editor deve impedire/segnalare il caso.
        var flows = new[] { Flow("LIRR_NE_CTR", TransferFlowKind.Overflight, null, Point("ELB", null, LevelConstraint.Special, "LFPO_CTR", special: "per aerovia")) };
        Assert.Empty(Build(flows, "LIRR_NE_CTR"));
    }

    [Fact]
    public void Condition_label_propagates_to_row_and_sentence()
    {
        // Variante condizionata da pista: la label finisce sulla riga e la clausola nella frase.
        var pt = new TransferPointRow
        {
            Id = 0, Cop = "NILTO", LevelValue = 195, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow,
            Parity = LevelParity.Any, LevelText = "≤FL195", NextSectorCallsign = "LIRR_TS_CTR", Order = 1,
            ConditionLabel = "RWY 16",
        };
        var flows = new[] { Flow("LIBB_ES_CTR", TransferFlowKind.Arrival, "LIRN", pt) };
        var e = Assert.Single(Build(flows, "LIBB_ES_CTR"));
        Assert.Equal("RWY 16", e.Row.ConditionLabel);
        Assert.EndsWith("con pista RWY 16 in uso.", e.Row.Sentence);
    }

    [Fact]
    public void Incoming_arrival_from_neighbour_ctr_reads_neighbour_as_sender()
    {
        // Flusso posseduto da un CTR vicino (non membro) che consegna a un nostro settore del blocco.
        var flows = new[] { Flow("LIBB_ES_CTR", TransferFlowKind.Arrival, "LIRN", Point("NILTO", 260, LevelConstraint.AtOrBelow, "LIRR_TS_CTR")) };
        var e = Assert.Single(Build(flows, "LIRR_TS_CTR"));
        Assert.True(e.IsIncoming);
        Assert.Equal("LIRR_TS_CTR", e.OurSectorCallsign);
        Assert.Equal("LIBB_ES_CTR", e.CounterpartCallsign);
        Assert.StartsWith("Brindisi Radar ES trasferisce a Roma Radar TS", e.Row.Sentence);
    }

    // ---- La sezione estesa porta tutto ciò che entra o esce (11 agosto 2026) ----

    [Fact]
    public void Incoming_departure_from_an_app_reaches_the_acc()
    {
        // Prima del 11 agosto il passo «entranti» accettava solo Arrival da un Ctr: una partenza che un APP
        // consegna all'ACC non compariva da nessuna parte nel documento dell'ACC — l'accordo si vedeva da un
        // lato solo. È il caso che il committente ha chiesto di chiudere.
        var flows = new[] { Flow("LIRN_US0_APP", TransferFlowKind.Departure, "LIRN",
            Point("NILTO", 150, LevelConstraint.AtOrBelow, "LIRR_TS_CTR")) };

        var e = Assert.Single(Build(flows, "LIRR_TS_CTR"));
        Assert.True(e.IsIncoming);
        Assert.Equal("LIRR_TS_CTR", e.OurSectorCallsign);
        Assert.Equal("LIRN_US0_APP", e.CounterpartCallsign);
        Assert.Equal(SectorType.App, e.CounterpartType);
        Assert.Equal(TransferFlowKind.Departure, e.Kind);
        Assert.StartsWith("Roma Radar US0 trasferisce a Roma Radar TS", e.Row.Sentence);
    }

    [Fact]
    public void Both_sides_of_an_agreement_can_coexist_without_being_merged()
    {
        // Se ACC e APP hanno scritto ciascuno la propria riga per lo stesso accordo, compaiono entrambe:
        // sono due DICHIARAZIONI distinte, e fonderle nasconderebbe anche il caso in cui divergono.
        var flows = new[]
        {
            Flow("LIRR_TS_CTR", TransferFlowKind.Arrival, "LIRN", Point("NILTO", 150, LevelConstraint.AtOrBelow, "LIRN_US0_APP")),
            Flow("LIRN_US0_APP", TransferFlowKind.Departure, "LIRN", Point("NILTO", 150, LevelConstraint.AtOrBelow, "LIRR_TS_CTR")),
        };

        var entries = Build(flows, "LIRR_TS_CTR");
        Assert.Equal(2, entries.Count);
        Assert.Single(entries, x => !x.IsIncoming);
        Assert.Single(entries, x => x.IsIncoming);
    }

    // ---- Faccetta trasferimento nelle colonne ----

    [Fact]
    public void Handoff_columns_arrive_already_worded()
    {
        var pt = new TransferPointRow
        {
            Id = 0, Cop = "CHI", LevelValue = 160, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrAbove,
            LevelText = "FL160+", NextSectorCallsign = "LIRN_US0_APP", Order = 1,
            HandoffKind = TransferHandoffKind.AorBoundary,
            HandoffLevelValue = 110, HandoffLevelConstraint = LevelConstraint.Exact,
            CommsHandoffKind = TransferHandoffKind.Point, CommsHandoffLabel = "AVN",
            SpeedValue = 250, SpeedConstraint = SpeedConstraint.AtOrBelow,
        };
        var flows = new[] { Flow("LIBB_ES_CTR", TransferFlowKind.Arrival, "LIRN", pt) };

        var e = Assert.Single(Build(flows, "LIBB_ES_CTR"));
        // Le colonne arrivano alla vista GIÀ a parole: la lingua sta nel template, non nel markup.
        Assert.Equal("al confine dell'AoR", e.Row.Handoff);
        Assert.Equal("passando FL110", e.Row.HandoffLevel);
        Assert.Equal("su AVN", e.Row.CommsHandoff);
        Assert.Equal("a 250 kt o inferiore", e.Row.Speed);
        Assert.Contains("autorizza il traffico", e.Row.Sentence);
    }

    [Fact]
    public void Rows_without_the_facet_leave_the_new_columns_empty()
    {
        var flows = new[] { Flow("LIBB_ES_CTR", TransferFlowKind.Arrival, "LIRN", Point("NILTO", 260, LevelConstraint.AtOrBelow, "LIRR_TS_CTR")) };
        var e = Assert.Single(Build(flows, "LIBB_ES_CTR"));
        Assert.Equal("", e.Row.Handoff);
        Assert.Equal("", e.Row.HandoffLevel);
        Assert.Equal("", e.Row.CommsHandoff);
        Assert.Equal("", e.Row.Speed);
        Assert.Null(e.Row.VariantGroup);
    }

    [Fact]
    public void The_sentence_cumulates_the_chain_while_the_table_shows_the_delta()
    {
        TransferPointRow V(int? level, string? runway, string? area, int depth, int order) => new()
        {
            Id = order, Cop = "BIRSU", LevelValue = level, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow,
            LevelText = $"FL{level}-", NextSectorCallsign = "LIRR_TS_CTR", Order = order,
            ConditionLabel = runway, ConditionAreaLabel = area, VariantGroup = 1, VariantDepth = depth,
        };
        // Outline: pista 07 · sua eccezione con R403B attiva · pista 25 pari-grado alla 07.
        var flows = new[] { Flow("LIBB_ES_CTR", TransferFlowKind.Arrival, "LIRN",
            V(150, "07", null, 0, 1), V(130, null, "R403B", 1, 2), V(130, "25", null, 0, 3)) };

        var rows = Build(flows, "LIBB_ES_CTR").Select(x => x.Row).ToList();
        Assert.All(rows, r => Assert.Equal(1, r.VariantGroup));
        Assert.Equal(new[] { 0, 1, 0 }, rows.Select(r => r.VariantDepth));

        // La FRASE cumula la catena: l'eccezione vale solo dentro la pista 07, e viaggia da sola nella prosa.
        Assert.EndsWith("con pista 07 in uso.", rows[0].Sentence);
        Assert.EndsWith("con pista 07 in uso e R403B attiva.", rows[1].Sentence);
        Assert.EndsWith("con pista 25 in uso.", rows[2].Sentence);
        // In TABELLA invece si legge il solo delta: il rientro dà il contesto. («area » è il prefisso che
        // ConditionDisplay mette alle aree per distinguerle dalle piste in una cella breve.)
        Assert.Equal("area R403B", rows[1].ConditionLabel);
    }

    // ---- Padre nell'outline ----
    // La risalita posizionale serviva alla FRASE (ConditionChain) e ora serve anche alla TABELLA dell'editor,
    // che deve dire «eccezione di quale riga». Una sola risalita, altrimenti due letture dello stesso outline
    // possono raccontare due strutture diverse sullo stesso dato.

    private static TransferPointRow Node(int id, int? group, int depth, string? runway = null, bool wide = false) => new()
    {
        Id = id, Cop = "BIRSU", LevelValue = 150, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow,
        LevelText = "FL150-", NextSectorCallsign = "LIRR_TS_CTR", Order = id,
        ConditionLabel = runway, VariantGroup = group, VariantDepth = depth, IsGroupWide = wide,
    };

    [Fact]
    public void ParentOf_Walks_Back_To_The_First_Shallower_Row_Of_The_Same_Group()
    {
        // 1 capofila · 2 sua eccezione · 3 eccezione dell'eccezione · 4 seconda eccezione della capofila
        // · 5 riga di un ALTRO gruppo, annidata: la risalita non deve scavalcare il confine.
        var a = Node(1, 1, 0, "07");
        var b = Node(2, 1, 1, "25");
        var c = Node(3, 1, 2, "34");
        var d = Node(4, 1, 1, "16");
        var e = Node(5, 2, 1, "18");
        var pts = new[] { a, b, c, d, e };

        Assert.Same(a, CoordinationDerivation.ParentOf(pts, b));
        Assert.Same(b, CoordinationDerivation.ParentOf(pts, c));
        Assert.Same(a, CoordinationDerivation.ParentOf(pts, d));   // pari-grado saltata: non è un antenato
        Assert.Null(CoordinationDerivation.ParentOf(pts, e));      // il gruppo 2 non eredita dal gruppo 1
    }

    [Fact]
    public void ParentOf_Is_Null_For_Peers_And_For_Group_Wide_Rows()
    {
        var head = Node(1, 1, 0, "07");
        var peer = Node(2, 1, 0, "25");
        var wide = Node(3, 1, 0, wide: true);
        var alone = Node(4, null, 0, "34");
        var pts = new[] { head, peer, wide, alone };

        Assert.Null(CoordinationDerivation.ParentOf(pts, head));
        Assert.Null(CoordinationDerivation.ParentOf(pts, peer));   // pari-grado: nessuna è lo standard dell'altra
        Assert.Null(CoordinationDerivation.ParentOf(pts, wide));   // vale per tutte, quindi non sta dentro nessuna
        Assert.Null(CoordinationDerivation.ParentOf(pts, alone));  // fuori da un gruppo non c'è outline
    }

    [Fact]
    public void ConditionChain_Still_Returns_The_Same_Chain_After_Extraction()
    {
        // Caratterizzazione dell'estrazione: la catena della frase non cambia forma.
        var a = Node(1, 1, 0, "07");
        var b = Node(2, 1, 1, "25");
        var c = Node(3, 1, 2, "34");
        var pts = new[] { a, b, c };

        Assert.Equal(new[] { "07" }, CoordinationDerivation.ConditionChain(pts, a).Select(x => x.Runway));
        Assert.Equal(new[] { "07", "25" }, CoordinationDerivation.ConditionChain(pts, b).Select(x => x.Runway));
        Assert.Equal(new[] { "07", "25", "34" }, CoordinationDerivation.ConditionChain(pts, c).Select(x => x.Runway));
    }

    // ---- albero: etichetta della FIR e ordine degli ACC ----

    private static readonly IReadOnlyDictionary<string, AccRef> AccRefs = new Dictionary<string, AccRef>(System.StringComparer.OrdinalIgnoreCase)
    {
        ["LIBB_ES_CTR"] = new AccRef("Brindisi", "LIBB", false),
        ["LIRR_TS_CTR"] = new AccRef("Roma", "LIRR", false),
        ["LIRR_NE_CTR"] = new AccRef("Roma", "LIRR", false),
        ["LIRN_US0_APP"] = new AccRef("Roma", "LIRR", false),
        ["LIBB_CS_CTR"] = new AccRef("Brindisi", "LIBB", false),
        ["LGGG_W_CTR"] = new AccRef("Greece", "LGGG", true),
        ["LYBA_CTR"] = new AccRef("Beograd", "LYBA", true),
    };

    private static IReadOnlyList<AccSectorApps> Tree(IReadOnlyList<CoordinationEntry> entries) =>
        CoordinationDerivation.BuildAccTree(entries, Codes, Atc, Airports, AccRefs, TransferFlowKindLabels.Label);

    private static CoordinationEntry Entry(string ours, string counterpart) =>
        new(ours, counterpart, SectorType.Ctr, "LIRN", TransferFlowKind.Arrival, IsIncoming: false,
            new AppCoordRow("PIGOL", "FL200", counterpart, TransferFlowKind.Arrival));

    [Fact]
    public void AccLabel_Carries_The_Icao_Next_To_The_Fir_Name()
    {
        // «Beograd» e «Zagreb» sono LYBA e LDZO solo per chi le ha gia' in testa: il codice sta accanto al nome.
        var acc = Assert.Single(Assert.Single(Tree(new[] { Entry("LIBB_ES_CTR", "LYBA_CTR") })).Accs);
        Assert.Equal("Beograd-LYBA", acc.AccLabel);
    }

    [Fact]
    public void Acc_Without_A_Resolved_Reference_Keeps_The_Neutral_Label()
    {
        // Un counterpart che nessuna ACC rivendica non deve far sparire la riga dall'albero.
        var acc = Assert.Single(Assert.Single(Tree(new[] { Entry("LIBB_ES_CTR", "SCONOSCIUTO_CTR") })).Accs);
        Assert.Equal("ACC", acc.AccLabel);
    }

    [Fact]
    public void Accs_Are_Ordered_Home_Then_Italy_Then_Abroad()
    {
        // Dentro un settore l'ordine e' la distanza da chi legge, non l'alfabeto: alfabeticamente la propria ACC
        // — quella che si coordina a ogni volo — finiva in mezzo agli esteri.
        var tree = Tree(new[]
        {
            Entry("LIBB_ES_CTR", "LYBA_CTR"),      // estero
            Entry("LIBB_ES_CTR", "LIRR_TS_CTR"),   // altro italiano
            Entry("LIBB_ES_CTR", "LGGG_W_CTR"),    // estero
            Entry("LIBB_ES_CTR", "LIBB_CS_CTR"),   // casa
        });

        Assert.Equal(new[] { "Brindisi-LIBB", "Roma-LIRR", "Beograd-LYBA", "Greece-LGGG" },
                     Assert.Single(tree).Accs.Select(a => a.AccLabel));
    }

    [Fact]
    public void Home_Is_Read_From_Our_Own_Sector_Not_From_The_Document()
    {
        // Lo stesso albero visto da due settori di ACC diverse: «casa» cambia con il settore, non col documento.
        var tree = Tree(new[]
        {
            Entry("LIBB_ES_CTR", "LIRR_TS_CTR"),
            Entry("LIBB_ES_CTR", "LIBB_CS_CTR"),
            Entry("LIRR_NE_CTR", "LIRR_TS_CTR"),
            Entry("LIRR_NE_CTR", "LIBB_CS_CTR"),
        });

        var es = tree.Single(s => s.SectorLabel == "ES");
        var ne = tree.Single(s => s.SectorLabel == "NE");
        Assert.Equal(new[] { "Brindisi-LIBB", "Roma-LIRR" }, es.Accs.Select(a => a.AccLabel));
        Assert.Equal(new[] { "Roma-LIRR", "Brindisi-LIBB" }, ne.Accs.Select(a => a.AccLabel));
    }
}
