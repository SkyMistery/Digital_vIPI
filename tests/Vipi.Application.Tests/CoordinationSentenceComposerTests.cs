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
        LevelParity parity = LevelParity.Any, string? special = null, LevelUnit unit = LevelUnit.Fl,
        TransferVerticalState vstate = TransferVerticalState.Unspecified,
        TransferHandoffFacet? facet = null)
        => CoordinationSentences.Compose(Tpl, Types, Names, Codes, Airports, Atc, owner, target, icao,
            c, value, unit, special, parity, cop, kind, verticalState: vstate, facet: facet);

    /// <summary>Faccetta trasferimento con i soli campi che il caso in prova usa: il resto è «non c'è».</summary>
    private static TransferHandoffFacet Facet(
        TransferHandoffKind kind = TransferHandoffKind.AorBoundary, string? label = null,
        int? level = null, LevelConstraint levelConstraint = LevelConstraint.Exact,
        TransferHandoffKind commsKind = TransferHandoffKind.Unspecified, string? commsLabel = null,
        int? speed = null, SpeedConstraint speedConstraint = SpeedConstraint.Unspecified,
        bool otherwise = false)
        => new(kind, label, level, LevelUnit.Fl, levelConstraint, commsKind, commsLabel, speed, speedConstraint, otherwise);

    [Fact]
    public void Ctr_target_includes_code_and_descent()
    {
        // Stato verticale «in discesa» scelto a mano: dimensione indipendente dal vincolo di livello (≤).
        var s = Compose("LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.AtOrBelow, 130, "VALMA",
            vstate: TransferVerticalState.Descending);
        Assert.Equal("Roma Radar NE trasferisce a Milano Radar WS2 il traffico con destinazione Fiumicino LIRF in discesa a livello 130 o livello inferiore su VALMA.", s);
    }

    [Fact]
    public void Constraint_alone_has_no_vertical_state_word()
    {
        // Regressione: il vincolo di livello (≤/≥) NON implica più «in discesa/salita». Senza stato verticale scelto,
        // la frase riporta solo il bound di livello. (Richiesta operativa: «a 130 o inferiore» non è una discesa.)
        var s = Compose("LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.AtOrBelow, 130, "PISIP");
        Assert.Equal("Roma Radar NE trasferisce a Milano Radar WS2 il traffico con destinazione Fiumicino LIRF a livello 130 o livello inferiore su PISIP.", s);
        Assert.DoesNotContain("in discesa", s);
    }

    [Fact]
    public void Departure_uses_origin_wording_not_destination()
    {
        var s = Compose("LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.AtOrAbove, 280, "VALMA", TransferFlowKind.Departure,
            vstate: TransferVerticalState.Climbing);
        Assert.Equal("Roma Radar NE trasferisce a Milano Radar WS2 il traffico in partenza da Fiumicino LIRF in salita a livello 280 o livello superiore su VALMA.", s);
        Assert.DoesNotContain("destinazione", s);
    }

    [Fact]
    public void App_target_with_identifier_includes_code()
    {
        // APP consolidato (fornito dall'ACC) con MiddleIdentifier di posizione (es. US0): l'identifier va mostrato
        // per disambiguare dal nome generico. L'omissione del codice vale solo per i terminali SENZA identifier.
        var s = Compose("LIRR_NE_CTR", "LIRP_APP", "LIRP", LevelConstraint.AtOrBelow, 120, "MAREL",
            vstate: TransferVerticalState.Descending);
        Assert.Equal("Roma Radar NE trasferisce a Pisa Approach US0 il traffico con destinazione Pisa - San Giusto LIRP in discesa a livello 120 o livello inferiore su MAREL.", s);
    }

    [Fact]
    public void App_target_without_identifier_omits_code()
    {
        // Stesso APP ma senza MiddleIdentifier noto: nessun codice nella frase.
        var codes = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase) { ["LIRR_NE_CTR"] = "NE" };
        var s = CoordinationSentences.Compose(Tpl, Types, Names, codes, Airports, Atc,
            "LIRR_NE_CTR", "LIRP_APP", "LIRP", LevelConstraint.AtOrBelow, 120, LevelUnit.Fl, null, LevelParity.Any, "MAREL",
            verticalState: TransferVerticalState.Descending);
        Assert.Equal("Roma Radar NE trasferisce a Pisa Approach il traffico con destinazione Pisa - San Giusto LIRP in discesa a livello 120 o livello inferiore su MAREL.", s);
    }

    [Fact]
    public void Climb_and_level_state_words()
    {
        var up = Compose("LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.AtOrAbove, 280, "VALMA",
            vstate: TransferVerticalState.Climbing);
        Assert.Contains("in salita a livello 280 o livello superiore", up);
        var lvl = Compose("LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.Exact, 240, "VALMA",
            vstate: TransferVerticalState.Level);
        Assert.Contains("stabile a livello 240 su", lvl);
    }

    [Fact]
    public void Parity_appended_as_word_with_value()
    {
        var s = Compose("LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.AtOrBelow, 150, "TIGRA", parity: LevelParity.Odd,
            vstate: TransferVerticalState.Descending);
        Assert.Contains("in discesa a livello 150 o livello inferiore dispari su TIGRA.", s);
    }

    [Fact]
    public void Parity_without_value_reads_un_livello()
    {
        // Sorvolo «stabile» senza valore numerico ma con parità → «per un livello dispari».
        var s = Compose("LIRR_NE_CTR", "LIMM_WS2", null, LevelConstraint.Exact, null, "TIGRA",
            TransferFlowKind.Overflight, parity: LevelParity.Odd, vstate: TransferVerticalState.Level);
        Assert.Equal("Roma Radar NE trasferisce a Milano Radar WS2 il traffico stabile per un livello dispari su TIGRA.", s);
    }

    /// <summary>
    /// La parità in <b>inglese</b> non si attacca come in italiano, e le vLOA sono documenti che leggono i
    /// vicini: ricalcando l'ordine italiano uscivano «at level 260 even» e «for a level odd», che nessuno
    /// scriverebbe. Trovato leggendo una vLOA resa (verifica live D2), non da un test: il compositore era
    /// corretto, era la lingua a non entrarci. Ora l'ordine sta nel template, che è il posto della lingua.
    /// </summary>
    [Fact]
    public void English_template_puts_parity_where_english_wants_it()
    {
        var en = CoordinationSentenceTemplate.English;

        var conValore = CoordinationSentences.Compose(en, Types, Names, Codes, Airports, Atc,
            "LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.Exact, 260, LevelUnit.Fl, null,
            LevelParity.Even, "ALL to GR", TransferFlowKind.Departure, verticalState: TransferVerticalState.Level);
        Assert.Contains("at level 260 (even)", conValore);
        Assert.DoesNotContain("level 260 even ", conValore);

        var senzaValore = CoordinationSentences.Compose(en, Types, Names, Codes, Airports, Atc,
            "LIRR_NE_CTR", "LIMM_WS2", null, LevelConstraint.Exact, null, LevelUnit.Fl, null,
            LevelParity.Odd, "TIGRA", TransferFlowKind.Overflight, verticalState: TransferVerticalState.Level);
        Assert.Contains("for an odd level", senzaValore);
        Assert.DoesNotContain("for a level odd", senzaValore);
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
        var s = Compose("LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.Exact, 260, cop, parity: LevelParity.Even,
            vstate: TransferVerticalState.Level);
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
        // ⚠️ L'attesa è cambiata il 9 agosto 2026: diceva «or below odd», che era la parità attaccata
        // all'ordine italiano. Il test fotografava il difetto invece di impedirlo — è saltato fuori
        // leggendo una vLOA vera, non da qui.
        var s = CoordinationSentences.Compose(CoordinationSentenceTemplate.English, Types, Names, Codes, Airports, Atc,
            "LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.AtOrBelow, 150, LevelUnit.Fl, null, LevelParity.Odd, "ALL",
            verticalState: TransferVerticalState.Descending);
        Assert.Equal("Roma Radar NE transfers to Milano Radar WS2 the traffic inbound to Fiumicino LIRF descending at level 150 or below (odd) over all points.", s);
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

    // ---- Faccetta trasferimento (ACC→APP): autorizzazione e trasferimento sono due eventi ----

    [Fact]
    public void Without_handoff_the_sentence_keeps_the_historic_form()
    {
        // L'invariante che rende sicura tutta la faccetta: senza, non cambia una parola.
        var conFaccettaVuota = Compose("LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.AtOrBelow, 195, "VALMA",
            facet: TransferHandoffFacet.None);
        var senzaFaccetta = Compose("LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.AtOrBelow, 195, "VALMA");
        Assert.Equal(senzaFaccetta, conFaccettaVuota);
        Assert.StartsWith("Roma Radar NE trasferisce a", conFaccettaVuota);
    }

    [Fact]
    public void Handoff_switches_the_verb_and_says_both_levels()
    {
        // Il caso portato dal committente: autorizzato a un livello, trasferito passandone un altro.
        var s = Compose("LIRR_NE_CTR", "LIRP_APP", "LIRP", LevelConstraint.AtOrAbove, 160, "CHI",
            vstate: TransferVerticalState.Descending,
            facet: Facet(TransferHandoffKind.AorBoundary, level: 110));

        Assert.Equal(
            "Roma Radar NE autorizza il traffico con destinazione Pisa - San Giusto LIRP via CHI a livello 160 " +
            "o livello superiore e lo trasferisce a Pisa Approach US0 al confine dell'AoR passando FL110 in discesa.", s);
    }

    [Fact]
    public void Handoff_on_a_point_names_it()
    {
        var s = Compose("LIRR_NE_CTR", "LIRP_APP", "LIRP", LevelConstraint.Exact, 160, "CHI",
            facet: Facet(TransferHandoffKind.Point, label: "AVN", level: 110));
        Assert.Contains("e lo trasferisce a Pisa Approach US0 su AVN passando FL110.", s);
    }

    [Fact]
    public void Handoff_level_constraint_changes_the_wording()
    {
        var s = Compose("LIRR_NE_CTR", "LIRP_APP", "LIRP", LevelConstraint.Exact, 160, "CHI",
            facet: Facet(level: 110, levelConstraint: LevelConstraint.AtOrBelow));
        Assert.Contains("al confine dell'AoR a FL110 o inferiore.", s);
    }

    [Fact]
    public void Speed_restriction_is_appended_after_a_comma()
    {
        var s = Compose("LIRR_NE_CTR", "LIRP_APP", "LIRP", LevelConstraint.Exact, 160, "CHI",
            facet: Facet(level: 110, speed: 250, speedConstraint: SpeedConstraint.AtOrBelow));
        Assert.EndsWith("passando FL110, a 250 kt o inferiore.", s);
    }

    [Fact]
    public void Comms_are_said_only_when_they_pass_elsewhere()
    {
        // Stesso posto del controllo: ripeterlo allunga la frase e non aggiunge niente.
        var stessoPosto = Compose("LIRR_NE_CTR", "LIRP_APP", "LIRP", LevelConstraint.Exact, 160, "CHI",
            facet: Facet(TransferHandoffKind.AorBoundary, level: 110, commsKind: TransferHandoffKind.AorBoundary));
        Assert.EndsWith("al confine dell'AoR passando FL110.", stessoPosto);

        var altrove = Compose("LIRR_NE_CTR", "LIRP_APP", "LIRP", LevelConstraint.Exact, 160, "CHI",
            facet: Facet(TransferHandoffKind.AorBoundary, level: 110,
                commsKind: TransferHandoffKind.Point, commsLabel: "AVN"));
        Assert.EndsWith("passando FL110, comunicazioni su AVN.", altrove);
    }

    [Fact]
    public void Handoff_point_without_label_says_nothing_rather_than_something_broken()
    {
        var s = Compose("LIRR_NE_CTR", "LIRP_APP", "LIRP", LevelConstraint.Exact, 160, "CHI",
            facet: Facet(TransferHandoffKind.Point, label: null, level: 110));
        Assert.Contains("e lo trasferisce a Pisa Approach US0 passando FL110.", s);
    }

    [Fact]
    public void Otherwise_row_replaces_the_condition_clause()
    {
        var s = CoordinationSentences.Compose(Tpl, Types, Names, Codes, Airports, Atc,
            "LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.AtOrBelow, 130, LevelUnit.Fl, null, LevelParity.Any,
            "BIRSU", TransferFlowKind.Arrival,
            // Anche se qualcuno ci mettesse una condizione, «negli altri casi» vince: è ciò che quella riga è.
            conditionLabel: "16R",
            facet: Facet(TransferHandoffKind.Unspecified, otherwise: true));
        Assert.EndsWith("su BIRSU negli altri casi.", s);
    }

    [Fact]
    public void English_template_says_the_handoff_in_english()
    {
        var s = CoordinationSentences.Compose(CoordinationSentenceTemplate.English, Types, Names, Codes, Airports, Atc,
            "LIRR_NE_CTR", "LIRP_APP", "LIRP", LevelConstraint.AtOrAbove, 160, LevelUnit.Fl, null, LevelParity.Any,
            "CHI", TransferFlowKind.Arrival, verticalState: TransferVerticalState.Descending,
            facet: Facet(TransferHandoffKind.AorBoundary, level: 110,
                commsKind: TransferHandoffKind.Point, commsLabel: "AVN",
                speed: 250, speedConstraint: SpeedConstraint.AtOrBelow));

        Assert.Equal(
            "Roma Radar NE clears the traffic inbound to Pisa - San Giusto LIRP via CHI at level 160 or above " +
            "and transfers it to Pisa Approach US0 at the AoR boundary passing FL110 descending, " +
            "at 250 kt or less, communications over AVN.", s);
    }
}
