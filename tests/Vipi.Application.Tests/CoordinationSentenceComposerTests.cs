using System.Collections.Generic;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Composizione della frase di coordinamento: stato ↑/↓/exact, livello a parole
/// («a livello N o livello inferiore/superiore»), parità («dispari»/«pari»), punto «tutti i punti»
/// (CoP ALL/vuoto) e «tutti i punti verso X» (CoP «ALL to X»), omissione codice per APP, fallback nomi,
/// livello speciale, aeroporto assente.</summary>
public class CoordinationSentenceComposerTests
{
    private static readonly CoordinationSentenceTemplate Tpl = CoordinationSentenceTemplate.Default;

    private static readonly IReadOnlyDictionary<string, SectorType> Types =
        new Dictionary<string, SectorType>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["LIRR_NE_CTR"] = SectorType.Ctr,
            ["LIMM_WS2"] = SectorType.Ctr,
            ["LIRP_APP"] = SectorType.App,
        };
    // Sector.Name: i CTR proiettati = callsign (nome nice via AtcCallsign); gli APP hanno il nome IVAO.
    private static readonly IReadOnlyDictionary<string, string> Names =
        new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["LIRR_NE_CTR"] = "LIRR_NE_CTR",
            ["LIMM_WS2"] = "LIMM_WS2",
            ["LIRP_APP"] = "LIRP Approach",
        };
    private static readonly IReadOnlyDictionary<string, string> Codes =
        new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["LIRR_NE_CTR"] = "NE",
            ["LIMM_WS2"] = "WS2",
            ["LIRP_APP"] = "US0",
        };
    private static readonly IReadOnlyDictionary<string, string> Airports =
        new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["LIRF"] = "Fiumicino",
            ["LIRP"] = "Pisa - San Giusto",
        };
    // Sector.Name dei CTR = callsign (proiezione); il nome nice arriva da AtcCallsign + MiddleIdentifier.
    private static readonly IReadOnlyDictionary<string, string> Atc =
        new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["LIRR_NE_CTR"] = "Roma Radar",
            ["LIMM_WS2"] = "Milano Radar",
            ["LIRP_APP"] = "Pisa Approach",
        };

    private static string? Compose(string owner, string target, string? icao, LevelConstraint c,
        int? value, string cop, TransferFlowKind kind = TransferFlowKind.Arrival,
        LevelParity parity = LevelParity.Any, string? special = null, LevelUnit unit = LevelUnit.Fl)
        => CoordinationSentences.Compose(Tpl, Types, Names, Codes, Airports, Atc, owner, target, icao,
            c, value, unit, special, parity, cop, kind);

    [Fact]
    public void Ctr_target_includes_code_and_descent()
    {
        var s = Compose("LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.AtOrBelow, 130, "VALMA");
        Assert.Equal("Roma Radar NE trasferisce a Milano Radar WS2 il traffico con destinazione Fiumicino LIRF in discesa a livello 130 o livello inferiore su VALMA.", s);
    }

    [Fact]
    public void Departure_uses_origin_wording_not_destination()
    {
        var s = Compose("LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.AtOrAbove, 280, "VALMA", TransferFlowKind.Departure);
        Assert.Equal("Roma Radar NE trasferisce a Milano Radar WS2 il traffico in partenza da Fiumicino LIRF in salita a livello 280 o livello superiore su VALMA.", s);
        Assert.DoesNotContain("destinazione", s);
    }

    [Fact]
    public void App_target_with_identifier_includes_code()
    {
        // APP consolidato (fornito dall'ACC) con MiddleIdentifier di posizione (es. US0): l'identifier va mostrato
        // per disambiguare dal nome generico. L'omissione del codice vale solo per i terminali SENZA identifier.
        var s = Compose("LIRR_NE_CTR", "LIRP_APP", "LIRP", LevelConstraint.AtOrBelow, 120, "MAREL");
        Assert.Equal("Roma Radar NE trasferisce a Pisa Approach US0 il traffico con destinazione Pisa - San Giusto LIRP in discesa a livello 120 o livello inferiore su MAREL.", s);
    }

    [Fact]
    public void App_target_without_identifier_omits_code()
    {
        // Stesso APP ma senza MiddleIdentifier noto: nessun codice nella frase.
        var codes = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase) { ["LIRR_NE_CTR"] = "NE" };
        var s = CoordinationSentences.Compose(Tpl, Types, Names, codes, Airports, Atc,
            "LIRR_NE_CTR", "LIRP_APP", "LIRP", LevelConstraint.AtOrBelow, 120, LevelUnit.Fl, null, LevelParity.Any, "MAREL");
        Assert.Equal("Roma Radar NE trasferisce a Pisa Approach il traffico con destinazione Pisa - San Giusto LIRP in discesa a livello 120 o livello inferiore su MAREL.", s);
    }

    [Fact]
    public void Climb_and_level_state_words()
    {
        var up = Compose("LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.AtOrAbove, 280, "VALMA");
        Assert.Contains("in salita a livello 280 o livello superiore", up);
        var lvl = Compose("LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.Exact, 240, "VALMA");
        Assert.Contains("stabile a livello 240 su", lvl);
    }

    [Fact]
    public void Parity_appended_as_word_with_value()
    {
        var s = Compose("LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.AtOrBelow, 150, "TIGRA", parity: LevelParity.Odd);
        Assert.Contains("in discesa a livello 150 o livello inferiore dispari su TIGRA.", s);
    }

    [Fact]
    public void Parity_without_value_reads_un_livello()
    {
        // Sorvolo «stabile» senza valore numerico ma con parità → «per un livello dispari».
        var s = Compose("LIRR_NE_CTR", "LIMM_WS2", null, LevelConstraint.Exact, null, "TIGRA",
            TransferFlowKind.Overflight, parity: LevelParity.Odd);
        Assert.Equal("Roma Radar NE trasferisce a Milano Radar WS2 il traffico stabile per un livello dispari su TIGRA.", s);
    }

    [Theory]
    [InlineData("ALL")]
    [InlineData("all")]
    [InlineData("")]
    public void Cop_all_or_blank_reads_all_points(string cop)
    {
        var s = Compose("LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.AtOrBelow, 150, cop);
        Assert.EndsWith("su tutti i punti.", s);
    }

    [Theory]
    [InlineData("ALL to GR", "verso GR")]
    [InlineData("all to gr", "verso gr")]      // dest reso come scritto (nessuna mappa codice→nome)
    [InlineData("ALL  to  LFFF", "verso LFFF")] // spazi extra tollerati
    public void Cop_all_toward_reads_all_points_toward_dest(string cop, string tail)
    {
        // «stabile a livello 260 pari su tutti i punti verso X»: ALL to X → tutti i punti verso una nazione/FIR.
        var s = Compose("LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.Exact, 260, cop, parity: LevelParity.Even);
        Assert.Contains("stabile a livello 260 pari su tutti i punti " + tail + ".", s);
    }

    [Fact]
    public void Empty_cop_distinct_from_all_when_missing_point_customised()
    {
        // Config reale: FallbackMissingPoint = «—» (CoP vuoto = non compilato). «ALL»/«ALL to X» NON devono
        // ereditare quel «—»: usano FallbackAllPoints/FallbackAllToward. Regressione del bug «su —» per CoP ALL.
        var tpl = new CoordinationSentenceTemplate { FallbackMissingPoint = "—" };
        string? C(string cop) => CoordinationSentences.Compose(tpl, Types, Names, Codes, Airports, Atc,
            "LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.AtOrBelow, 150, LevelUnit.Fl, null, LevelParity.Any, cop);
        Assert.EndsWith("su —.", C(""));
        Assert.EndsWith("su tutti i punti.", C("ALL"));
        Assert.EndsWith("su tutti i punti verso GR.", C("ALL to GR"));
    }

    [Fact]
    public void English_template_composes_english_sentence()
    {
        // Template inglese (vLOA): stato/livello/parità/punto tutti in EN.
        var s = CoordinationSentences.Compose(CoordinationSentenceTemplate.English, Types, Names, Codes, Airports, Atc,
            "LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.AtOrBelow, 150, LevelUnit.Fl, null, LevelParity.Odd, "ALL");
        Assert.Equal("Roma Radar NE transfers to Milano Radar WS2 the traffic inbound to Fiumicino LIRF descending at level 150 or below odd over all points.", s);
    }

    [Fact]
    public void Special_level_has_no_livello_body()
    {
        var s = Compose("LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.Special, null, "ELB", special: "per aerovia");
        Assert.DoesNotContain("a livello", s);
        Assert.Contains("destinazione Fiumicino LIRF per aerovia su ELB.", s);
    }

    [Fact]
    public void Missing_airport_returns_null_for_arrivals_and_departures()
    {
        // Arrivi/partenze senza aeroporto = relazione aeroporto orfana → nessuna frase.
        Assert.Null(Compose("LIRR_NE_CTR", "LIMM_WS2", null, LevelConstraint.AtOrBelow, 130, "VALMA"));
        Assert.Null(Compose("LIRR_NE_CTR", "LIMM_WS2", "", LevelConstraint.AtOrBelow, 130, "VALMA"));
        Assert.Null(Compose("LIRR_NE_CTR", "LIMM_WS2", null, LevelConstraint.AtOrAbove, 280, "VALMA", TransferFlowKind.Departure));
    }

    [Fact]
    public void Overflight_without_airport_composes_neutral_sentence()
    {
        // Sorvolo senza aeroporto: relazione neutra, nessun «con destinazione», frase valida.
        var s = Compose("LIRR_NE_CTR", "LIMM_WS2", null, LevelConstraint.Special, null, "ELB",
            TransferFlowKind.Overflight, special: "per aerovia");
        Assert.Equal("Roma Radar NE trasferisce a Milano Radar WS2 il traffico per aerovia su ELB.", s);
        Assert.DoesNotContain("destinazione", s!);
        Assert.DoesNotContain("partenza", s!);
    }

    [Fact]
    public void Overflight_with_airport_shows_airport_neutrally()
    {
        // Sorvolo con aeroporto valorizzato: mostra il nome aeroporto senza relazione arrivo/partenza.
        var s = Compose("LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.AtOrBelow, 130, "VALMA", TransferFlowKind.Overflight);
        Assert.Contains("Fiumicino LIRF", s!);
        Assert.DoesNotContain("destinazione", s!);
    }

    [Fact]
    public void Unknown_names_fall_back_to_callsign_and_icao()
    {
        var s = Compose("LFOO_CTR", "LFBB_XX", "LFPG", LevelConstraint.AtOrBelow, 130, "ABC");
        Assert.Contains("LFOO_CTR trasferisce a LFBB_XX", s);   // niente codice noto → solo callsign
        Assert.Contains("destinazione LFPG LFPG", s);            // ICAO come nome di fallback
    }

    private static string? ComposeCond(string? runway = null, string? area = null, string? custom = null) =>
        CoordinationSentences.Compose(Tpl, Types, Names, Codes, Airports, Atc,
            "LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.AtOrBelow, 195, LevelUnit.Fl, null, LevelParity.Any,
            "VALMA", TransferFlowKind.Arrival, runway, area, custom);

    [Fact]
    public void Runway_condition_appends_clause_before_period()
    {
        // Variante editoriale: clausola condizione appesa a fine frase, prima del punto.
        var s = ComposeCond(runway: "RWY 16");
        Assert.EndsWith("su VALMA con pista RWY 16 in uso.", s);
    }

    [Fact]
    public void Area_condition_uses_active_wording()
    {
        var s = ComposeCond(area: "R41");
        Assert.EndsWith("su VALMA con R41 attiva.", s);
    }

    [Fact]
    public void Custom_condition_uses_generic_wording()
    {
        var s = ComposeCond(custom: "traffico intenso");
        Assert.EndsWith("su VALMA in condizione traffico intenso.", s);
    }

    [Fact]
    public void No_condition_appends_no_clause()
    {
        Assert.EndsWith("su VALMA.", ComposeCond());
        Assert.EndsWith("su VALMA.", ComposeCond(runway: "  "));   // label vuota → niente clausola
    }

    [Fact]
    public void English_template_composes_english_condition_clause()
    {
        var s = CoordinationSentences.Compose(CoordinationSentenceTemplate.English, Types, Names, Codes, Airports, Atc,
            "LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.AtOrBelow, 195, LevelUnit.Fl, null, LevelParity.Any,
            "VALMA", TransferFlowKind.Arrival, "RWY 16");
        Assert.EndsWith("over VALMA with runway RWY 16 in use.", s);
    }

    [Fact]
    public void Multi_runway_label_lists_runways_in_one_clause()
    {
        // Stessa condizione su più piste: l'etichetta le elenca, una sola clausola.
        var s = ComposeCond(runway: "16R / 16L");
        Assert.EndsWith("su VALMA con pista 16R / 16L in uso.", s);
    }

    [Fact]
    public void Runway_and_area_combine_with_and_wording()
    {
        // Pista + area insieme: forma dedicata «con pista X in uso e Y attiva».
        var s = ComposeCond(runway: "16R", area: "R41");
        Assert.EndsWith("su VALMA con pista 16R in uso e R41 attiva.", s);
    }

    [Fact]
    public void Runway_and_area_english_wording()
    {
        var s = CoordinationSentences.Compose(CoordinationSentenceTemplate.English, Types, Names, Codes, Airports, Atc,
            "LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.AtOrBelow, 195, LevelUnit.Fl, null, LevelParity.Any,
            "VALMA", TransferFlowKind.Arrival, "16R", "R41");
        Assert.EndsWith("over VALMA with runway 16R in use and R41 active.", s);
    }

    [Fact]
    public void All_three_conditions_joined_with_e()
    {
        // Tre dimensioni indipendenti insieme: pista+area (forma dedicata) « e » personalizzata.
        var s = ComposeCond(runway: "16R", area: "R41", custom: "traffico intenso");
        Assert.EndsWith("su VALMA con pista 16R in uso e R41 attiva e in condizione traffico intenso.", s);
    }

    [Fact]
    public void Area_and_custom_without_runway_joined()
    {
        var s = ComposeCond(area: "R41", custom: "notte");
        Assert.EndsWith("su VALMA con R41 attiva e in condizione notte.", s);
    }
}
