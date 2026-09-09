using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components;
using Vipi.Ui.Components.Blocks;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Rete bUnit sui componenti di rendering dei blocchi: cattura le regressioni Blazor "silenziose coi test verdi"
/// (dispatch del BlockRenderer sbagliato; attributo dinamico reso come letterale perché scritto senza @).
/// </summary>
public class BlockRenderingTests : TestContext
{
    /// <summary>Localizzatore che rende la chiave stessa: dal 31 agosto 2026 anche l'etichetta di un callout
    /// («Nota», «Attenzione») viene dai resx invece che da un letterale italiano, perché sta DENTRO il
    /// documento e in una pagina inglese restava italiana.</summary>
    private sealed class ChiaveComeValore : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public BlockRenderingTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeValore());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    private static BlockView Block(BlockFormat format, string? body = null, string? bodyJson = null,
        CalloutKind? callout = null) => new()
    {
        Id = 1,
        Format = format,
        State = RenderState.Expanded,
        Body = body,
        BodyJson = bodyJson,
        CalloutKind = callout,
    };

    [Fact]
    public void BlockRenderer_routes_prose_to_prose_block()
    {
        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block, Block(BlockFormat.Prose, body: "ciao")));
        Assert.Contains("ciao", cut.Markup);
        Assert.DoesNotContain("callout", cut.Markup);   // non deve finire sul ramo sbagliato
    }

    [Fact]
    public void BlockRenderer_routes_tip_variant_to_tip_block()
    {
        var json = "{\"variant\":\"tip\",\"title\":\"Suggerimento\",\"lines\":[\"riga uno\"]}";
        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block, Block(BlockFormat.Prose, bodyJson: json)));
        Assert.Contains("tip", cut.Markup);
        Assert.Contains("Suggerimento", cut.Markup);
        Assert.Contains("riga uno", cut.Markup);
    }

    [Fact]
    public void BlockRenderer_routes_callout_to_callout_block()
    {
        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block,
            Block(BlockFormat.Callout, body: "occhio", callout: CalloutKind.Warning)));
        Assert.Contains("callout", cut.Markup);
        Assert.Contains("occhio", cut.Markup);
    }

    // Trappola nota (memoria dev-process-gates): un attributo dinamico scritto come class="Kind" invece di
    // class="@Kind" renderebbe la stringa letterale "Kind". Questo test morde se qualcuno rompe l'interpolazione.
    [Theory]
    [InlineData(CalloutKind.Danger, "danger", "octagon")]
    [InlineData(CalloutKind.Success, "success", "check-circle")]
    [InlineData(CalloutKind.Info, "info", "info")]
    public void CalloutBlock_renders_dynamic_kind_class_and_icon(CalloutKind kind, string cssClass, string icon)
    {
        var cut = RenderComponent<CalloutBlock>(p => p.Add(x => x.Block, Block(BlockFormat.Callout, callout: kind)));
        var callout = cut.Find("div.callout");
        Assert.Contains(cssClass, callout.ClassList);           // classe = valore dinamico, non "Kind" letterale
        Assert.Contains($"data-icon=\"{icon}\"", cut.Markup);   // icona SVG per tipo (Icon.razor, U2)
    }

    [Fact]
    public void CalloutBlock_html_encodes_body_no_xss()
    {
        var cut = RenderComponent<CalloutBlock>(p => p.Add(x => x.Block,
            Block(BlockFormat.Callout, body: "<script>alert(1)</script>", callout: CalloutKind.Info)));
        Assert.DoesNotContain("<script>", cut.Markup);          // MarkdownLite encoda
        Assert.Contains("&lt;script&gt;", cut.Markup);
    }

    // --- Blocco immagine ---

    private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void BlockRenderer_routes_image_to_the_figure()
    {
        var json = MediaRef.Serialize(new MediaRef(Sha, "Torre di controllo", 1600, 900));

        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block,
            Block(BlockFormat.Image, body: "Vista da nord", bodyJson: json)));

        var img = cut.Find("figure.doc-img img");
        Assert.Equal("/vsop/media/" + Sha, img.GetAttribute("src"));
        Assert.Equal("Torre di controllo", img.GetAttribute("alt"));
        // width/height nativi: senza, il testo salta mentre l'immagine arriva.
        Assert.Equal("1600", img.GetAttribute("width"));
        Assert.Equal("900", img.GetAttribute("height"));
        Assert.Contains("Vista da nord", cut.Markup);
    }

    [Fact]
    public void La_larghezza_scelta_arriva_al_documento_come_percentuale_della_colonna()
    {
        // Una percentuale e non dei pixel: la stessa immagine si legge su un monitor, su un telefono e su un A4.
        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block,
            Block(BlockFormat.Image, bodyJson: MediaRef.Serialize(new MediaRef(Sha, null, 1600, 900, 40)))));

        Assert.Contains("width:40%", cut.Find("figure.doc-img").GetAttribute("style"));
    }

    [Fact]
    public void Senza_larghezza_scelta_la_figura_non_porta_nessuno_stile()
    {
        // Le release congelate prima di questo campo devono rendersi ESATTAMENTE come prima.
        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block,
            Block(BlockFormat.Image, bodyJson: MediaRef.Serialize(new MediaRef(Sha, null, 1600, 900)))));

        Assert.True(string.IsNullOrEmpty(cut.Find("figure.doc-img").GetAttribute("style")));
    }

    [Fact]
    public void Image_block_without_reference_shows_the_placeholder_not_a_broken_image()
    {
        // Blocco appena creato (o JSON rotto): esiste prima della sua foto.
        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block, Block(BlockFormat.Image, bodyJson: "{oops")));

        Assert.Empty(cut.FindAll("img"));
        Assert.Single(cut.FindAll("figure.img-ph"));
    }

    [Fact]
    public void Image_caption_is_html_encoded_no_xss()
    {
        var json = MediaRef.Serialize(new MediaRef(Sha, "<img onerror=alert(1)>"));

        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block,
            Block(BlockFormat.Image, body: "<script>alert(1)</script>", bodyJson: json)));

        Assert.DoesNotContain("<script>", cut.Markup);
        // L'alt arriva da chi carica: dev'essere un attributo, non markup interpretato.
        Assert.Single(cut.FindAll("figure.doc-img img"));
    }

    /// <summary>
    /// ⚠️ <b>Un blocco tabella con una VARIANTE sconosciuta non è contenuto: è il payload di una sezione resa
    /// dalla pagina</b>, e non si rende.
    ///
    /// <para>Trovato dal vivo il 30 agosto 2026, ed era un <b>500 sull'intera pagina</b>: appena le sezioni
    /// del vSOP militare hanno cominciato a tenere i propri blocchi, il payload di «Nominativi» —
    /// <c>rows</c> fatto di <i>array di stringhe</i> — è finito nella tabella generica, che legge le righe
    /// come oggetti con <c>cells</c>. <c>TryGetProperty</c> su un array alza <c>InvalidOperationException</c>,
    /// che non è una <c>JsonException</c> e quindi passava indenne il <c>catch</c>.</para>
    /// </summary>
    [Theory]
    [InlineData("""{"variant":"milcallsigns","rows":[["13° Gruppo","IBIS","IAM 1234","QRA 01"]]}""")]
    [InlineData("""{"variant":"milparkings","rows":[["Piazzale Nord","1-12",""]]}""")]
    [InlineData("""{"variant":"milnavaids","rows":[{"code":"MNL","kind":"VOR"}]}""")]
    [InlineData("""{"variant":"mildiversion","rows":[{"icao":"LIMC"}]}""")]
    public void BlockRenderer_non_rende_il_payload_di_una_sezione(string json)
    {
        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block, Block(BlockFormat.Table, bodyJson: json)));

        Assert.DoesNotContain("<table", cut.Markup);
        Assert.DoesNotContain("Gruppo", cut.Markup);
    }

    /// <summary>Le tabelle generiche scritte a mano NON hanno variante, e restano contenuto: la regola sopra
    /// non deve portarsele via.</summary>
    [Fact]
    public void BlockRenderer_rende_ancora_la_tabella_generica()
    {
        var json = """{"columns":["Colonna 1"],"rows":[{"cells":["valore"]}]}""";

        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block, Block(BlockFormat.Table, bodyJson: json)));

        Assert.Contains("Colonna 1", cut.Markup);
        Assert.Contains("valore", cut.Markup);
    }

    /// <summary>⚠️ Una radice ARRAY — la forma legacy della selezione delle aree — non deve far esplodere
    /// niente: `TryGetProperty` su un array non alza una `JsonException`, e una sola riga vecchia in archivio
    /// mandava in 500 la pagina che la mostra.</summary>
    [Fact]
    public void Un_payload_con_radice_array_non_esplode()
    {
        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block,
            Block(BlockFormat.Table, bodyJson: """["1029","1032"]""")));

        Assert.DoesNotContain("<table", cut.Markup);
    }

    /// <summary>
    /// 🔴 <b>Il payload che NON ha una variante è payload lo stesso.</b> La selezione delle aree
    /// regolamentate è <c>{"OwnAuto":…,"OwnIds":[…]}</c> — un blocco <c>Table</c> senza <c>variant</c> — e
    /// fino all'8 settembre 2026 questa regola chiedeva proprio la variante: il payload finiva nella tabella
    /// generica, l'editor gli disegnava accanto il tasto <b>«+ riga»</b>, e premerlo ci scriveva sopra
    /// <c>{"columns":…,"rows":…}</c>. <b>Tutte le aree scelte cancellate</b>, senza un errore.
    ///
    /// <para>Segnalato dal committente su 1.16.0 in produzione, riprodotto a schermo su LIBG: nella sezione
    /// «Aree di lavoro» c'erano DUE tabelle, la sua e una <c>cfg-table</c> vuota con l'intestazione «＋».</para>
    ///
    /// <para>⚠️ Il caso col <c>notes</c> è la forma di 1.16.0, quello senza è la forma che sta in archivio
    /// dai documenti scritti prima: valgono tutt'e due, e nessuna delle due ha una variante.</para>
    /// </summary>
    [Theory]
    [InlineData("""{"OwnAuto":false,"OwnIds":["10478","735"],"ExtraIds":[],"activities":{"735":"CAS"}}""")]
    [InlineData("""{"OwnAuto":false,"OwnIds":["10478"],"ExtraIds":[],"activities":{},"notes":{"10478":"prosa"}}""")]
    [InlineData("""[{"Key":"cfg:bbbb0611","Name":"Conf 1","OpenCallsigns":["LIMF_WW0_APP"]}]""")]
    public void Il_payload_senza_variante_non_si_rende(string json)
    {
        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block, Block(BlockFormat.Table, bodyJson: json)));

        Assert.DoesNotContain("<table", cut.Markup);
        Assert.DoesNotContain("OwnIds", cut.Markup);
        Assert.DoesNotContain("prosa", cut.Markup);
    }

    /// <summary>
    /// ⚠️ La domanda è UNA SOLA, e questa è la rete che lo tiene vero: quel che l'editor mostra come
    /// contenuto è esattamente quel che <see cref="SectionPayload"/> <b>non</b> legge come payload. Due
    /// regole per la stessa domanda sono il modo in cui si perde un payload — o il contenuto di qualcun
    /// altro — senza che nessuno protesti.
    /// </summary>
    [Theory]
    [InlineData("""{"OwnAuto":false,"OwnIds":["1"]}""", false)]                       // payload aree
    [InlineData("""{"variant":"milcallsigns","rows":[]}""", false)]                   // payload militare
    [InlineData("""["1029"]""", false)]                                               // payload legacy
    [InlineData("""{"columns":["A"],"rows":[{"cells":["x"]}]}""", true)]              // tabella a mano
    [InlineData("""{"mediaId":"abc","alt":null}""", true)]                            // immagine
    [InlineData("""{"ref":"allegato-1"}""", true)]                                    // allegato
    public void L_editor_e_il_payload_rispondono_alla_STESSA_domanda(string json, bool eContenuto)
    {
        // Quel che il viewer/editor considerano contenuto...
        var reso = !Vipi.Ui.BlockJson.EStruttura(json);
        // ...e quel che la lettura del payload salta.
        var saltato = SectionPayload.EEditoriale(json);

        Assert.Equal(eContenuto, reso);
        Assert.Equal(reso, saltato);
    }

    /// <summary>
    /// ⚠️ La rete sul posto dove il difetto è stato SEGNALATO (9 settembre 2026): un callout in una sezione
    /// custom della vIPI di LIMC, «il testo va tutto su una riga sola». Il renderer l'a capo lo rendeva
    /// — provato — ma nessuno controllava che arrivasse fin qui: fra il corpo e lo schermo ci sono il
    /// dispatch del <see cref="BlockRenderer"/> e il markup del callout, e un difetto in mezzo avrebbe
    /// avuto la stessa faccia. Adesso la riga di questo file dice dove guardare.
    /// </summary>
    [Fact]
    public void Un_callout_manda_a_capo_come_lo_scrive_chi_redige()
    {
        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block,
            Block(BlockFormat.Callout, body: "Test\nVEst", callout: CalloutKind.Info)));

        Assert.Contains("Test<br>VEst", cut.Markup);
    }

    /// <summary>Lo stesso per la prosa, e per gli elenchi: fra il corpo e il <c>&lt;li&gt;</c> non deve
    /// esserci niente che li appiattisca.</summary>
    [Fact]
    public void La_prosa_manda_a_capo_e_rende_gli_elenchi()
    {
        var acapo = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block,
            Block(BlockFormat.Prose, body: "uno\ndue")));
        Assert.Contains("uno<br>due", acapo.Markup);

        var elenco = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block,
            Block(BlockFormat.Prose, body: "- uno\n- due")));
        Assert.Contains("<li>uno</li><li>due</li>", elenco.Markup);
    }
}
