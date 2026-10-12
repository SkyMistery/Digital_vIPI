using System.Globalization;
using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.Application.Content;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il riquadro dei NOTAM in testa ai documenti pubblici (carta
/// <c>docs/feature/2026-10-12-notam-sui-documenti.md</c>): che cosa disegna, e che le quattro sedi dei documenti lo montano solo
/// sulla vista pubblica.
/// </summary>
public class NotamBoxTests : TestContext
{
    public NotamBoxTests() => Services.AddSingleton<StringheDelSito>();

    /// <summary>Il testo vero di IT-B002/25, accorciato.</summary>
    private const string TestoFirenze =
        "Q) LIXX/QSPXX/IV/BO /E /000/185/4344N01107E034\nA) LIMM LIRR B) 2511270000 C) PERM\n" +
        "E) FIRENZE CTR FIRENZE CTA. APPROACH CONTROL CHG AS FOLLOWS:\n- LIMM-LIRQ VLOA\n" +
        "REF https://it.ivao.aero/atc/briefing-loa";

    private static NotamAvviso Firenze(bool inArrivo = false, DateTime? fine = null, string? testo = null) => new(
        4845, "IT-B002/25", new DateTime(2025, 11, 27, 0, 0, 0, DateTimeKind.Utc),
        fine ?? new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), testo ?? TestoFirenze, inArrivo,
        new[] { "LIRQ" }, new[] { "LIRR" });

    private IRenderedComponent<NotamBox> Disegna(string cultura, params NotamAvviso[] avvisi)
    {
        var prima = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultura);
            return RenderComponent<NotamBox>(p => p.Add(x => x.Avvisi, avvisi));
        }
        finally { CultureInfo.CurrentUICulture = prima; }
    }

    /// <summary>Senza NOTAM il riquadro non c'è: non un riquadro vuoto, niente.</summary>
    [Fact]
    public void Senza_NOTAM_non_disegna_niente() =>
        Assert.Equal("", Disegna("it").Markup.Trim());

    /// <summary>La riga sempre in vista porta numero, stato e VALIDITÀ per esteso — è quello che il committente
    /// ha chiesto in evidenza — e la validità è in UTC, con l'ora.</summary>
    [Fact]
    public void La_riga_in_vista_dice_numero_stato_e_validita()
    {
        var riga = Disegna("it", Firenze()).Find("details.notam > summary");

        Assert.Equal("IT-B002/25", riga.QuerySelector(".notam-num")!.TextContent);
        Assert.Equal("In vigore", riga.QuerySelector(".pill")!.TextContent);
        var date = riga.QuerySelectorAll(".notam-val time");
        Assert.Equal(2, date.Length);
        Assert.Equal("27 nov 2025 00:00Z", date[0].TextContent);
        Assert.Equal("2025-11-27T00:00:00Z", date[0].GetAttribute("datetime"));
        Assert.Equal("1 gen 2027 00:00Z", date[1].TextContent);
        Assert.Matches(@"dal\s+27 nov 2025 00:00Z\s+al\s+1 gen 2027 00:00Z",
            Regex.Replace(riga.QuerySelector(".notam-val")!.TextContent, @"\s+", " ").Trim());
    }

    [Fact]
    public void In_inglese_parla_inglese()
    {
        var riga = Disegna("en", Firenze()).Find("details.notam > summary");

        Assert.Equal("In force", riga.QuerySelector(".pill")!.TextContent);
        Assert.Matches(@"from\s+27 Nov 2025 00:00Z\s+to\s+1 Jan 2027 00:00Z",
            Regex.Replace(riga.QuerySelector(".notam-val")!.TextContent, @"\s+", " ").Trim());
    }

    /// <summary>Un NOTAM non ancora in vigore lo dice, e si distingue a colpo d'occhio.</summary>
    [Fact]
    public void Un_NOTAM_in_arrivo_lo_dice()
    {
        var c = Disegna("it", Firenze(inArrivo: true));

        Assert.NotNull(c.Find("details.notam.soon"));
        Assert.Equal("In arrivo", c.Find("summary .pill").TextContent);
        Assert.Contains("future", c.Find("summary .pill").ClassList);
    }

    [Fact]
    public void Senza_fine_dice_senza_scadenza()
    {
        var avviso = Firenze() with { FineUtc = null };

        var validita = Disegna("it", avviso).Find(".notam-val");

        Assert.Single(validita.QuerySelectorAll("time"));
        Assert.Contains("senza scadenza", validita.TextContent);
    }

    /// <summary>Per chi non apre: la riga <c>E)</c>, che è quella che dice di che cosa parla. Le righe prima sono
    /// codici e date che il riquadro dice già.</summary>
    [Fact]
    public void La_prima_riga_e_il_campo_E()
    {
        Assert.Equal("FIRENZE CTR FIRENZE CTA. APPROACH CONTROL CHG AS FOLLOWS:", NotamBox.PrimaRiga(TestoFirenze));
        Assert.Equal("TSA 'DONALD WEST' ACTIVE", NotamBox.PrimaRiga("TSA 'DONALD WEST' ACTIVE\nSECONDA RIGA"));
        Assert.Equal("", NotamBox.PrimaRiga(""));

        Assert.Equal("FIRENZE CTR FIRENZE CTA. APPROACH CONTROL CHG AS FOLLOWS:",
            Disegna("it", Firenze()).Find(".notam-first").TextContent);
    }

    /// <summary>Il testo intero sta nel <c>details</c>, riga per riga com'è alla sorgente, e chiuso all'apertura
    /// della pagina: in cima al documento deve restare una riga per NOTAM, non quindici.</summary>
    [Fact]
    public void Il_testo_intero_sta_dietro_il_gesto()
    {
        var c = Disegna("it", Firenze());

        Assert.False(c.Find("details.notam").HasAttribute("open"));
        Assert.Equal(TestoFirenze, c.Find("pre.notam-text").TextContent);
        Assert.Contains("LIRQ", c.Find(".notam-scope").TextContent);
        Assert.Contains("LIRR", c.Find(".notam-scope").TextContent);
    }

    /// <summary>Gli indirizzi diventano collegamenti; la punteggiatura in coda resta fuori.</summary>
    [Fact]
    public void Gli_indirizzi_si_possono_cliccare()
    {
        var link = Disegna("it", Firenze()).Find("pre.notam-text a");

        Assert.Equal("https://it.ivao.aero/atc/briefing-loa", link.GetAttribute("href"));
        Assert.Equal("noopener noreferrer", link.GetAttribute("rel"));

        var pezzi = NotamBox.Pezzi("VEDI https://it.ivao.aero/loa. POI (http://x.it/a?b=1), FINE");
        Assert.Equal(new[]
        {
            ("VEDI ", false), ("https://it.ivao.aero/loa", true), (". POI (", false),
            ("http://x.it/a?b=1", true), ("), FINE", false),
        }, pezzi);
    }

    /// <summary>
    /// ⚠️ Il testo viene da fuori e si scrive come TESTO: un NOTAM con dentro del markup non deve diventare
    /// markup nella nostra pagina. Solo <c>http(s)://</c> diventa collegamento — <c>javascript:</c> no.
    /// </summary>
    [Fact]
    public void Il_testo_della_sorgente_non_diventa_markup()
    {
        var c = Disegna("it", Firenze(testo: "E) <script>alert(1)</script> <b>GRASSETTO</b> javascript:alert(2)"));

        Assert.Empty(c.FindAll("script"));
        Assert.Empty(c.FindAll("pre.notam-text b"));
        Assert.Empty(c.FindAll("pre.notam-text a"));
        Assert.Contains("<script>alert(1)</script>", c.Find("pre.notam-text").TextContent);
        Assert.Contains("&lt;script&gt;", c.Markup);
    }

    /// <summary>Nella vista live il riquadro compare dopo che qualcuno ha premuto la chip per leggere: i NOTAM
    /// nascono aperti. Nei documenti no.</summary>
    [Fact]
    public void Su_richiesta_i_NOTAM_nascono_aperti()
    {
        var prima = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it");
            var c = RenderComponent<NotamBox>(p => p.Add(x => x.Avvisi, new[] { Firenze() }).Add(x => x.Aperti, true));

            Assert.True(c.Find("details.notam").HasAttribute("open"));
        }
        finally { CultureInfo.CurrentUICulture = prima; }
    }

    [Fact]
    public void Un_NOTAM_per_scheda_e_il_conto_in_testa()
    {
        var c = Disegna("it", Firenze(), Firenze(inArrivo: true) with { IvaoId = 2, Nome = "IT-A009/26" });

        Assert.Equal(2, c.FindAll("details.notam").Count);
        Assert.Equal("2", c.Find(".notam-head .notam-n").TextContent);
        Assert.Equal(new[] { "IT-B002/25", "IT-A009/26" }, c.FindAll(".notam-num").Select(e => e.TextContent));
    }

    // ---- Le sedi dei documenti --------------------------------------------------------------------------------

    /// <summary>
    /// Guardie sul sorgente, e di proposito: una pagina che smette di chiedere i NOTAM non dà nessun errore —
    /// semplicemente il riquadro non c'è, come quando NOTAM non ce ne sono.
    /// <para>Ogni sede li chiede <b>una volta</b>, <b>solo sulla vista pubblica</b> (bozza e anteprima di release
    /// mostrano il documento, non l'oggi) e con l'ambito della sua famiglia.</para>
    /// </summary>
    [Theory]
    [InlineData("Pages/AeroportoPage.razor", "AmbitoNotam.Aeroporti(ScaliPerNotam.Di(_icao, _altri))")]
    [InlineData("Pages/MilDocumentPage.razor", "AmbitoNotam.Aeroporti(ScaliPerNotam.Di(_icao, _altri))")]
    [InlineData("Pages/AppnPage.razor", "AmbitoNotam.Aeroporti(ScaliPerNotam.Di(_app, _altri))")]
    [InlineData("Pages/AccVipiPage.razor", "AmbitoNotam.Acc(_acc.Code,")]
    public void Ogni_sede_chiede_i_NOTAM_solo_sulla_vista_pubblica(string relativo, string ambito)
    {
        var righe = Leggi(relativo).Split('\n');
        var chiamate = righe.Select((r, i) => (r, i)).Where(x => x.r.Contains("Notam.PerAsync(")).ToList();

        var (riga, indice) = Assert.Single(chiamate);
        Assert.Contains(ambito, riga, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"_notam = .*[Mm]ode\.Kind == PreviewKind\.Public"), righe[indice - 1]);
    }

    /// <summary>Il riquadro è montato una volta per sede.</summary>
    [Theory]
    [InlineData("Pages/AeroportoPage.razor", "<NotamBox Avvisi=\"_notam\" />")]
    [InlineData("Pages/MilDocumentPage.razor", "<NotamBox Avvisi=\"_notam\" />")]
    [InlineData("Pages/AppnPage.razor", "<NotamBox Avvisi=\"_notam\" />")]
    [InlineData("Pages/AccVipiPage.razor", "<NotamBox Avvisi=\"_notam\" />")]
    [InlineData("Pages/LivePage.razor", "<NotamBox Avvisi=\"_view.Notam\" Aperti=\"true\" />")]
    public void Ogni_sede_monta_il_riquadro_una_volta(string relativo, string montaggio) =>
        Assert.Single(Regex.Matches(Leggi(relativo), Regex.Escape(montaggio)));

    /// <summary>
    /// Le vLOA NO, ed è una decisione del committente (12 ottobre 2026: «per le vLOA non prevediamo nulla per
    /// ora»), non una sede dimenticata: chi le aggiunge «per completezza» lo trova scritto qui.
    /// </summary>
    [Theory]
    [InlineData("Pages/VloaListPage.razor")]
    [InlineData("Components/VloaDocumentView.razor")]
    public void Le_vLOA_non_mostrano_NOTAM(string relativo) =>
        Assert.DoesNotContain("Notam", Leggi(relativo), StringComparison.Ordinal);

    /// <summary>Il riquadro sta SOPRA il corpo del documento: «in cima», come chiesto.</summary>
    [Theory]
    [InlineData("Pages/AeroportoPage.razor", "<AirportDocumentBody ")]
    [InlineData("Pages/AppnPage.razor", "<AppDocumentBody ")]
    [InlineData("Pages/AccVipiPage.razor", "@foreach (var block in _data.Blocks)")]
    public void Il_riquadro_sta_sopra_il_corpo(string relativo, string corpo)
    {
        var sorgente = Leggi(relativo);

        var riquadro = sorgente.IndexOf("<NotamBox ", StringComparison.Ordinal);
        Assert.InRange(riquadro, 0, sorgente.IndexOf(corpo, StringComparison.Ordinal));
    }

    // ---- La vista live --------------------------------------------------------------------------------

    /// <summary>
    /// Ogni tipo di postazione dichiara il SUO ambito, con la stessa regola del suo documento: lo scalo per le
    /// postazioni d'aeroporto e per gli APP, l'ACC (più gli scali dei suoi APP) per i settori d'area. Guardia sul
    /// sorgente: un descrittore che se ne dimentica non dà errore — la sua postazione resta senza chip, come
    /// quando NOTAM non ce ne sono.
    /// </summary>
    [Theory]
    [InlineData("AirportLiveStation.cs", "AmbitoNotam = icao is null ? AmbitoNotam.Nessuno : AmbitoNotam.Aeroporti(new[] { icao })")]
    [InlineData("ApproachLiveStation.cs", "AmbitoNotam = AmbitoNotam.Aeroporti(new[] { AmbitoNotam.AeroportoDi(chiave) })")]
    [InlineData("AreaLiveStation.cs", "AmbitoNotam = AmbitoNotam.Acc(ctx.Acc.Code,")]
    public void Ogni_tipo_di_postazione_dichiara_il_suo_ambito(string file, string dichiarazione) =>
        Assert.Single(Regex.Matches(LeggiApplication("Live/" + file), Regex.Escape(dichiarazione)));

    /// <summary>I NOTAM li legge il servizio, una volta, con l'ambito dichiarato dal descrittore: la pagina riceve
    /// il modello già pronto e non interroga l'archivio per conto suo.</summary>
    [Fact]
    public void La_vista_live_riceve_i_NOTAM_dal_modello()
    {
        Assert.Single(Regex.Matches(LeggiApplication("Live/LiveViewService.cs"),
            Regex.Escape("Notam = await _notam.PerAsync(view.AmbitoNotam, ct)")));
        Assert.DoesNotContain("INotamDeiDocumenti", Leggi("Pages/LivePage.razor"), StringComparison.Ordinal);
    }

    private static string LeggiApplication(string relativo) =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(PercorsoUi("x")))!,
            "Vipi.Application", relativo.Replace('/', Path.DirectorySeparatorChar)));

    private static string Leggi(string relativo) => File.ReadAllText(PercorsoUi(relativo));

    private static string PercorsoUi(string relativo)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Vipi.Ui")))
            dir = dir.Parent;
        var radice = dir?.FullName ?? throw new DirectoryNotFoundException("Radice del repo non trovata.");
        return Path.Combine(radice, "src", "Vipi.Ui", relativo.Replace('/', Path.DirectorySeparatorChar));
    }
}
