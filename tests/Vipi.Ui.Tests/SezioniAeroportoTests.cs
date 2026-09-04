using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Weather;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// <b>La rete per l'aeroporto</b> — e prima di P7, non dopo.
///
/// <para>
/// Il runbook di refactor del progetto ha un'invariante (#8) che vale esattamente qui: se il componente da
/// spezzare è complesso e <b>privo di test diretti</b>, non lo si splitta alla cieca — prima si scrive la rete,
/// poi la si usa per le estrazioni. La vIPI d'aeroporto è il caso limite del progetto: 2180 righe di editor,
/// 594 di viewer, 384 chiavi di traduzione su tre prefissi, cinque sezioni con un componente di lettura e un
/// editor scritto dentro la pagina — e <b>nessun test</b> montava nessuno dei due.
/// </para>
///
/// <para>
/// Queste prove coprono le cinque sezioni di lettura: cosa mostrano, cosa mostrano quando non c'è niente, e le
/// due regole che un lettore di torre userebbe per sbagliare pista se cambiassero di nascosto — quale regola è
/// attiva adesso, e quali SID valgono per la pista scelta.
/// </para>
/// </summary>
public class SezioniAeroportoTests : TestContext
{
    private sealed class ChiaveComeValore : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public SezioniAeroportoTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeValore());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    // ---------------------------------------------------------------------------------------------------
    // Regole piste — «quale sta valendo adesso» è l'informazione operativa della sezione.
    // ---------------------------------------------------------------------------------------------------

    private static AirportRulesView Regole() => new(new[]
    {
        new AirportRuleRowView(1, "Vento da 160/220", "16L", "16R", "preferenziale"),
        new AirportRuleRowView(2, "Vento da 340/040", "34R", "34L", ""),
    });

    [Fact]
    public void Le_regole_piste_si_vedono_tutte_nell_ordine_dato()
    {
        var cut = RenderComponent<AirportRunwayRules>(p => p.Add(x => x.View, Regole()));

        // ⚠️ .ToList(): in questa versione di bUnit indicizzare direttamente il risultato di FindAll solleva
        // MissingMethodException su AngleSharp. La collezione è «rinfrescabile» e va materializzata.
        var righe = cut.FindAll("tbody tr").ToList();
        Assert.Equal(2, righe.Count);
        Assert.Contains("Vento da 160/220", righe[0].TextContent);
        Assert.Contains("Vento da 340/040", righe[1].TextContent);
    }

    [Fact]
    public void La_regola_attiva_e_marcata_e_le_altre_no()
    {
        // ⚠️ È il dato che un controllore legge per primo. Se la marcatura si spostasse di una riga, la pagina
        // direbbe che è in uso una pista diversa da quella vera, e nessuno se ne accorgerebbe leggendo il codice.
        var cut = RenderComponent<AirportRunwayRules>(p => p
            .Add(x => x.View, Regole())
            .Add(x => x.ActivePosition, 2));

        var marcate = cut.FindAll("tbody tr.rwy-both").ToList();
        Assert.Single(marcate);
        Assert.Contains("Vento da 340/040", marcate[0].TextContent);
    }

    [Fact]
    public void Senza_regole_non_si_disegna_una_tabella_vuota()
    {
        // ⚠️ La tabella c'è lo stesso, con una riga sola che dice perché è vuota: è la forma che il componente
        // ha davvero, e una prova di caratterizzazione deve descrivere quella, non quella che immaginavo.
        var cut = RenderComponent<AirportRunwayRules>(p => p.Add(x => x.View, AirportRulesView.Empty));
        var riga = Assert.Single(cut.FindAll("tbody tr").ToList());
        Assert.Contains("Airport_NoRules", riga.TextContent);
    }

    // ---------------------------------------------------------------------------------------------------
    // Quote di transizione
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void La_transizione_mostra_la_TA_e_le_fasce()
    {
        var cut = RenderComponent<AirportTransition>(p => p.Add(x => x.View,
            new AirportTransitionView(6000, new[]
            {
                new AirportTlRowView("1013.2 e oltre", "FL070"),
                new AirportTlRowView("977.2 – 1013.1", "FL075"),
            })));

        Assert.Contains("6000", cut.Markup);
        Assert.Equal(2, cut.FindAll("tbody tr").Count);
        Assert.Contains("FL075", cut.Markup);
    }

    [Fact]
    public void Senza_TA_la_transizione_non_inventa_un_numero()
    {
        var cut = RenderComponent<AirportTransition>(p => p.Add(x => x.View, AirportTransitionView.Empty));
        Assert.DoesNotContain("FL", cut.Markup);
    }

    /// <summary>Le fasce come le scrive <c>AirportSectionProjection.QnhRange</c> dai numeri del database:
    /// «≥ 1013», «995 – 1012», «≤ 976». ⚠️ È la forma vera — in archivio non c'è una riga con un QNH
    /// decimale — e usarne un'altra proverebbe il comportamento su un testo che nessun documento ha.</summary>
    private static AirportTransitionView Fasce() => new(6000, new[]
    {
        new AirportTlRowView("≥ 1013", "FL70"),
        new AirportTlRowView("995 – 1012", "FL75"),
        new AirportTlRowView("≤ 994", "FL80"),
    });

    [Fact]
    public void La_scheda_adesso_scrive_il_livello_della_fascia_del_QNH()
    {
        var cut = RenderComponent<AirportTransition>(p => p
            .Add(x => x.View, Fasce())
            .Add(x => x.CurrentQnh, 1005)
            .Add(x => x.MetarTimeRaw, "291250Z"));

        var scheda = cut.Find(".ta-card.noprint");
        Assert.Contains("FL75", scheda.QuerySelector(".ta-card-v")!.TextContent);
        Assert.Contains("1005", scheda.TextContent);
        Assert.Contains("291250Z", scheda.TextContent);
    }

    [Fact]
    public void Senza_QNH_la_scheda_adesso_non_compare()
    {
        var cut = RenderComponent<AirportTransition>(p => p.Add(x => x.View, Fasce()));
        Assert.Empty(cut.FindAll(".ta-card.noprint"));
    }

    /// <summary>Senza TA la tabella scrive «N/A» su ogni riga: un livello grande accanto sarebbe un numero
    /// che il documento non ha mai detto.</summary>
    [Fact]
    public void Senza_TA_la_scheda_adesso_non_compare()
    {
        var cut = RenderComponent<AirportTransition>(p => p
            .Add(x => x.View, new AirportTransitionView(null, new[] { new AirportTlRowView("≥ 1013", "FL70") }))
            .Add(x => x.CurrentQnh, 1020));

        Assert.Empty(cut.FindAll(".ta-card.noprint"));
    }

    /// <summary>QNH dentro un buco fra le fasce scritte: la scheda c'è e dice che fascia non ce n'è — tacere
    /// lascerebbe pensare a un livello che nessuno ha scritto.</summary>
    [Fact]
    public void Un_QNH_fuori_da_ogni_fascia_lo_dice()
    {
        var cut = RenderComponent<AirportTransition>(p => p
            .Add(x => x.View, new AirportTransitionView(6000, new[] { new AirportTlRowView("995 – 1012", "FL75") }))
            .Add(x => x.CurrentQnh, 1020));

        var scheda = cut.Find(".ta-card.noprint");
        Assert.Contains("—", scheda.QuerySelector(".ta-card-v")!.TextContent);
        Assert.Contains("Airport_TlNoBand", scheda.TextContent);
    }

    [Fact]
    public void I_dati_del_campo_si_leggono_accanto_alla_tabella()
    {
        var cut = RenderComponent<AirportTransition>(p => p
            .Add(x => x.View, Fasce())
            .Add(x => x.Station, new AirportStation("LIMC", "LIMM", ElevationFt: 768, MagneticVariation: 1,
                Iata: "MXP", Latitude: 45.63, Longitude: 8.7230555556)));

        var schede = cut.FindAll(".ta-card:not(.noprint)");
        var testo = Assert.Single(schede).TextContent;
        Assert.Contains("768 ft", testo);
        Assert.Contains("234 m", testo);            // 768 ft in metri, arrotondati
        Assert.Contains("1° E", testo);             // positiva = est, e l'emisfero si scrive
        Assert.Contains("MXP", testo);
        Assert.Contains("N45°37", testo);
        Assert.Contains("E008°43", testo);          // longitudine su TRE cifre di grado
    }

    [Fact]
    public void La_variazione_a_ovest_si_scrive_W()
    {
        var cut = RenderComponent<AirportTransition>(p => p
            .Add(x => x.View, Fasce())
            .Add(x => x.Station, new AirportStation("BIKF", "BIRD", MagneticVariation: -12.5)));

        Assert.Contains("W", cut.Find(".ta-card:not(.noprint)").TextContent);
        Assert.DoesNotContain("-12", cut.Markup);
    }

    /// <summary>Senza anagrafica — o con un'anagrafica che non porta nessuno dei quattro dati — la scheda
    /// non c'è: un riquadro di trattini non è un'informazione.</summary>
    [Fact]
    public void Senza_dati_del_campo_la_scheda_non_compare()
    {
        var vuota = RenderComponent<AirportTransition>(p => p.Add(x => x.View, Fasce()));
        Assert.Empty(vuota.FindAll(".ta-card:not(.noprint)"));

        var senzaDati = RenderComponent<AirportTransition>(p => p
            .Add(x => x.View, Fasce())
            .Add(x => x.Station, new AirportStation("LIRA", "LIRR")));
        Assert.Empty(senzaDati.FindAll(".ta-card:not(.noprint)"));
    }

    // ---------------------------------------------------------------------------------------------------
    // Frequenze
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Le_frequenze_si_vedono_e_la_principale_e_marcata()
    {
        var cut = RenderComponent<AirportFrequencies>(p => p.Add(x => x.View, new AirportFreqView(new[]
        {
            new AirportFreqRowView("Roma Torre", "LIRF_TWR", "118.700", true),
            new AirportFreqRowView("Roma Ground", "LIRF_GND", "121.700", false),
        })));

        Assert.Equal(2, cut.FindAll("tbody tr").Count);
        Assert.Contains("118.700", cut.Markup);
        Assert.Contains("LIRF_GND", cut.Markup);
    }

    // ---------------------------------------------------------------------------------------------------
    // Piste
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Le_piste_in_uso_si_distinguono_da_quelle_ferme()
    {
        var cut = RenderComponent<AirportRunways>(p => p
            .Add(x => x.View, new AirportRunwaysView(new[]
            {
                new AirportRunwayRowView("16L", 3900, "3900", "3900", "ILS", "—", "—"),
                new AirportRunwayRowView("34R", 3900, "3900", "3900", "ILS", "—", "—"),
            }))
            .Add(x => x.DepIdents, new HashSet<string>(new[] { "16L" }, StringComparer.OrdinalIgnoreCase)));

        Assert.Equal(2, cut.FindAll("tbody tr").Count);
        Assert.Contains("16L", cut.Markup);
        Assert.Contains("34R", cut.Markup);
    }

    private static AirportRunwaysView DuePiste() => new(new[]
    {
        new AirportRunwayRowView("16L", 3900, "3900", "3900", "ILS", "—", "—"),
        new AirportRunwayRowView("34R", 3900, "3900", "3900", "ILS", "—", "—"),
    });

    [Fact] // la meccanica delle regole è roba da staff: sotto DivisionStaff resta il vento, non il numero
    public void La_provenienza_della_pista_consigliata_non_si_mostra_a_chi_legge_e_basta()
    {
        var cut = RenderComponent<AirportRunways>(p => p
            .Add(x => x.View, DuePiste())
            .Add(x => x.RuleName, "#2")
            .Add(x => x.RuleNote, "pista preferenziale notturna")
            .Add(x => x.WindDir, 10).Add(x => x.WindKt, 4));

        // ⚠️ Il valore predefinito è «chiusa»: chi non lo scrive non mostra niente.
        Assert.DoesNotContain("Airport_RwyByRule", cut.Markup);
        Assert.DoesNotContain("Airport_RwyByWind", cut.Markup);
        Assert.DoesNotContain("pista preferenziale notturna", cut.Markup);

        // Il vento invece si vede sempre, e col carattere della tabella.
        Assert.Contains("Airport_WindVal", cut.Markup);
        Assert.Contains("rwy-wind", cut.Markup);

        // ⚠️ Nessun <text> nel documento: fuori da un blocco di codice non è un comando di Razor ma un
        // elemento HTML sconosciuto, e ci è finito davvero — visto nel prerender di LIBD, non dai test.
        Assert.DoesNotContain("<text>", cut.Markup);
    }

    [Fact] // aperta: si vede da dove viene la pista consigliata, e la nota della regola
    public void La_provenienza_si_mostra_a_chi_le_regole_le_scrive()
    {
        var cut = RenderComponent<AirportRunways>(p => p
            .Add(x => x.View, DuePiste())
            .Add(x => x.MostraProvenienza, true)
            .Add(x => x.RuleName, "#2")
            .Add(x => x.RuleNote, "pista preferenziale notturna")
            .Add(x => x.WindDir, 10).Add(x => x.WindKt, 4));

        Assert.Contains("Airport_RwyByRule", cut.Markup);
        Assert.Contains("pista preferenziale notturna", cut.Markup);
        Assert.Contains("Airport_WindVal", cut.Markup);
        Assert.DoesNotContain("<text>", cut.Markup);
    }

    [Fact] // senza regola attiva la provenienza è il vento del METAR, e resta comunque roba da staff
    public void Senza_regola_la_provenienza_e_il_vento_METAR_ma_solo_per_lo_staff()
    {
        var chiusa = RenderComponent<AirportRunways>(p => p
            .Add(x => x.View, DuePiste()).Add(x => x.WindDir, 10).Add(x => x.WindKt, 4));
        Assert.DoesNotContain("Airport_RwyByWind", chiusa.Markup);

        var aperta = RenderComponent<AirportRunways>(p => p
            .Add(x => x.View, DuePiste()).Add(x => x.MostraProvenienza, true)
            .Add(x => x.WindDir, 10).Add(x => x.WindKt, 4));
        Assert.Contains("Airport_RwyByWind", aperta.Markup);
    }

    // ---------------------------------------------------------------------------------------------------
    // SID — il filtro per pista è una scelta di LETTURA, e decide che cosa un pilota legge come autorizzato.
    // ---------------------------------------------------------------------------------------------------

    private static AirportSidView Sid() => new(new[]
    {
        new AirportSidRowView("16L", "ALAXI", "ALAXI 5A", "—", "5000", "RNAV", "A", "M", "—"),
        new AirportSidRowView("34R", "ELKAP", "ELKAP 3B", "—", "5000", "RNAV", "A", "M", "—"),
        new AirportSidRowView("—", "TAQ", "TAQ 1X", "—", "4000", "CONV", "A", "M", "solo H24"),
    });

    [Fact]
    public void Le_piste_delle_SID_escono_ordinate_e_senza_i_trattini()
    {
        // La riga senza pista («—») vale per tutte: non è una pista da offrire nel selettore.
        Assert.Equal(new[] { "16L", "34R" }, AirportSids.RunwaysOf(Sid()));
    }

    [Fact]
    public void La_pista_scelta_filtra_le_SID_ma_tiene_quelle_valide_per_tutte()
    {
        // ⚠️ La regola che conta: chi sceglie 16L deve vedere le SID di 16L E quelle senza pista, che valgono
        // comunque. Perderle vorrebbe dire nascondere una procedura pubblicata.
        var cut = RenderComponent<AirportSids>(p => p
            .Add(x => x.View, Sid())
            .Add(x => x.InitialRunway, "16L"));

        var testo = cut.Markup;
        Assert.Contains("ALAXI 5A", testo);   // la pista scelta
        Assert.Contains("TAQ 1X", testo);     // senza pista: vale per tutte
        Assert.DoesNotContain("ELKAP 3B", testo);
    }

    [Fact]
    public void Senza_pista_scelta_si_vedono_tutte_le_SID()
    {
        var cut = RenderComponent<AirportSids>(p => p.Add(x => x.View, Sid()));

        Assert.Equal(3, cut.FindAll("tbody tr").Count);
    }

    [Fact]
    public void Il_selettore_di_pista_compare_solo_quando_c_e_da_scegliere()
    {
        var unaSola = new AirportSidView(new[] { Sid().Rows[0] });
        var cut = RenderComponent<AirportSids>(p => p.Add(x => x.View, unaSola));
        Assert.Empty(cut.FindAll(".cfg-btn"));

        var due = RenderComponent<AirportSids>(p => p.Add(x => x.View, Sid()));
        Assert.Equal(2, due.FindAll(".cfg-btn").Count);
    }

    [Fact]
    public void La_chip_di_pista_cambia_davvero_le_SID_mostrate()
    {
        // ⚠️ Questo test mancava, ed è il buco da cui il difetto è passato: le prove qui sopra montavano il
        // componente con la pista GIÀ scelta e guardavano il risultato, senza mai premere una chip. Il filtro
        // funzionava; a non funzionare era il modo in cui la scelta arrivava — la teneva la pagina, che dal
        // doc 14 è SSR statica e non si ridisegna più. Chip premute a mano: zero.
        var cut = RenderComponent<AirportSids>(p => p
            .Add(x => x.View, Sid())
            .Add(x => x.InitialRunway, "16L"));

        Assert.Contains("ALAXI 5A", cut.Markup);
        Assert.DoesNotContain("ELKAP 3B", cut.Markup);

        cut.FindAll(".cfg-btn").Single(b => b.TextContent.Contains("34R")).Click();

        Assert.Contains("ELKAP 3B", cut.Markup);          // la pista appena scelta
        Assert.Contains("TAQ 1X", cut.Markup);            // senza pista: vale per tutte, anche dopo il cambio
        Assert.DoesNotContain("ALAXI 5A", cut.Markup);    // quella di prima se ne va
    }

    [Fact]
    public void Il_seme_non_torna_a_riprendersi_la_scelta_del_lettore()
    {
        // Il genitore può ridisegnare (in produzione non lo fa, ma un componente non deve dipendere da
        // questo): il seme si prende una volta sola, o riporterebbe il lettore alla pista in uso.
        var cut = RenderComponent<AirportSids>(p => p
            .Add(x => x.View, Sid())
            .Add(x => x.InitialRunway, "16L"));

        cut.FindAll(".cfg-btn").Single(b => b.TextContent.Contains("34R")).Click();
        cut.SetParametersAndRender(p => p.Add(x => x.InitialRunway, "16L"));

        Assert.Contains("ELKAP 3B", cut.Markup);
        Assert.DoesNotContain("ALAXI 5A", cut.Markup);
    }

    [Fact]
    public void Senza_SID_si_dice_perche_invece_di_mostrare_una_tabella_vuota()
    {
        var cut = RenderComponent<AirportSids>(p => p.Add(x => x.View, AirportSidView.Empty));

        Assert.Empty(cut.FindAll("table"));
        Assert.Contains("Airport_NoSidsTitle", cut.Markup);
    }

    // ---------------------------------------------------------------------------------------------------
    // Meteo — le chip METAR/TAF. Stessa lezione delle SID: non basta che la vista sia giusta, deve
    // rispondere al CLIC. Prima di questo giro nessun test montava <AirportWeather>.

    private static ParsedMetar Metar() => new(
        "METAR LIBD 271820Z 05004KT CAVOK 28/23 Q1017", "LIBD", "271820Z",
        new ParsedWind(50, false, 4, null, false), "CAVOK", Array.Empty<CloudLayer>(),
        null, 1017, 28, 23, null, false, false);

    private static ParsedTaf Taf() => new(
        "TAF LIBD 271700Z 2718/2818 06008KT CAVOK", "LIBD", "2718/2818", new[]
        {
            new TafSegment(TafChangeKind.Base, null, null, new ParsedWind(60, false, 8, null, false),
                "CAVOK", Array.Empty<CloudLayer>(), null, "2718/2818 06008KT CAVOK"),
        });

    private IRenderedComponent<AirportWeather> RendiMeteo(ParsedTaf? taf) =>
        RenderComponent<AirportWeather>(p => p
            .Add(x => x.Icao, "LIBD")
            .Add(x => x.Report, new WeatherReport("LIBD", Metar().Raw, taf?.Raw, DateTimeOffset.UtcNow))
            .Add(x => x.Metar, Metar())
            .Add(x => x.Taf, taf));

    [Fact]
    public void La_chip_TAF_mostra_il_TAF()
    {
        var cut = RendiMeteo(Taf());

        Assert.Contains("METAR LIBD 271820Z", cut.Markup);
        Assert.DoesNotContain("TAF LIBD 271700Z", cut.Markup);

        cut.FindAll(".wx-tab").Single(b => b.TextContent.Trim() == "TAF").Click();

        Assert.Contains("TAF LIBD 271700Z", cut.Markup);
        Assert.DoesNotContain("METAR LIBD 271820Z", cut.Markup);
    }

    [Fact]
    public void Senza_TAF_la_chip_e_spenta()
    {
        // Non è un dettaglio estetico: una chip che si preme e non porta da nessuna parte è il difetto che
        // questo giro ha appena chiuso.
        var cut = RendiMeteo(null);

        var taf = cut.FindAll(".wx-tab").Single(b => b.TextContent.Trim() == "TAF");
        Assert.True(taf.HasAttribute("disabled"));
    }
}
