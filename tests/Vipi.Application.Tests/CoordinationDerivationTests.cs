using System.Collections.Generic;
using System.Linq;
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
    public void Variant_group_and_otherwise_travel_to_the_row()
    {
        TransferPointRow V(int? level, string? runway, bool otherwise, int order) => new()
        {
            Id = order, Cop = "BIRSU", LevelValue = level, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow,
            LevelText = $"FL{level}-", NextSectorCallsign = "LIRR_TS_CTR", Order = order,
            ConditionLabel = runway, VariantGroup = 1, IsOtherwise = otherwise,
        };
        var flows = new[] { Flow("LIBB_ES_CTR", TransferFlowKind.Arrival, "LIRN", V(80, "16R", false, 1), V(110, null, true, 2)) };

        var rows = Build(flows, "LIBB_ES_CTR").Select(x => x.Row).ToList();
        Assert.All(rows, r => Assert.Equal(1, r.VariantGroup));
        Assert.EndsWith("con pista 16R in uso.", rows[0].Sentence);
        Assert.True(rows[1].IsOtherwise);
        Assert.EndsWith("negli altri casi.", rows[1].Sentence);
        // La cella condizione dice la stessa cosa della frase, e la dice nella lingua del template: sono a due
        // centimetri di distanza nella stessa schermata.
        Assert.Equal("negli altri casi", rows[1].ConditionLabel);
    }
}
