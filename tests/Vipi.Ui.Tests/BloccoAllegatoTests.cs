using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il blocco «Allegato»: la resa nel documento e l'editor condiviso dai due editor di blocchi.
///
/// <para>Presidia le tre cose che si sbagliano in silenzio: l'href è <b>sempre</b> la nostra rotta (nel
/// documento non entra mai un indirizzo del deposito); un riferimento illeggibile è un <b>posto vuoto</b> e
/// non un'eccezione in mezzo a un documento; e l'editor <b>sceglie</b> da un elenco invece di far incollare
/// un link, che è ciò che tiene onesto il registro «chi cita cosa».</para>
/// </summary>
public class BloccoAllegatoTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class BibliotecaFinta : IAttachmentLibrary
    {
        private readonly AttachmentRow[] _righe;
        public BibliotecaFinta(params AttachmentRow[] righe) => _righe = righe;

        public Task<IReadOnlyList<AttachmentRow>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AttachmentRow>>(_righe);
        public Task<AttachmentRow?> BySlugAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult(_righe.FirstOrDefault(r => r.Slug == slug));
        public Task<(AttachmentCreate Esito, AttachmentRow? Riga)> CreateAsync(
            AttachmentDraft draft, int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(AttachmentReplace Esito, AttachmentRow? Riga)> ReplaceAsync(
            string slug, string link, string? note, int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AttachmentDelete> DeleteAsync(string slug, int userId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private static AttachmentRow Voce(string slug, string titolo) =>
        new(1, slug, titolo, AttachmentKind.Loa, AttachmentScope.Division, null, null, 1, 1,
            AttachmentProvider.Drive, "1A2b3C4d5E6f7G8h9I0jKlMnOpQrStUvW",
            DateTime.UnixEpoch, DateTime.UnixEpoch);

    private static BlockView Block(string? bodyJson, string? body = null) => new()
    {
        Id = 1, Format = BlockFormat.Attachment, State = RenderState.Expanded, Body = body, BodyJson = bodyJson,
    };

    private void Localizzatore() =>
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());

    // ---- resa ------------------------------------------------------------------------------------------

    /// <summary>
    /// ⚠️ L'href è la <b>nostra</b> rotta. Nel documento non finisce mai un indirizzo del deposito: è ciò che
    /// rende reversibile un vincolo che non controlliamo — cambiare deposito domani non tocca un documento.
    /// </summary>
    [Fact]
    public void Il_blocco_linka_la_nostra_rotta_non_il_deposito()
    {
        Localizzatore();
        var json = AttachmentRef.Serialize(new AttachmentRef("loa-lirr-lfmm", "LoA Roma-Marseille"));

        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block, Block(json)));

        var a = cut.Find("p.att-link a");
        Assert.Equal("/vsop/files/loa-lirr-lfmm", a.GetAttribute("href"));
        Assert.Contains("LoA Roma-Marseille", a.TextContent);
        Assert.DoesNotContain("drive.google.com", cut.Markup);
    }

    /// <summary>Chi legge deve sapere <b>prima</b> del clic che il file sta fuori dal sito, e la scheda nuova
    /// serve a non far sparire il documento da sotto chi lo stava consultando.</summary>
    [Fact]
    public void Il_link_dice_che_porta_fuori_e_apre_una_scheda_nuova()
    {
        Localizzatore();
        var json = AttachmentRef.Serialize(new AttachmentRef("loa-lirr-lfmm", "LoA"));

        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block, Block(json)));

        Assert.Equal("_blank", cut.Find("p.att-link a").GetAttribute("target"));
        Assert.Contains("noopener", cut.Find("p.att-link a").GetAttribute("rel"));
        Assert.Contains("Att_External", cut.Markup);
    }

    /// <summary>Un riferimento illeggibile è un posto vuoto che si vede, non un'eccezione in mezzo a un
    /// documento pubblicato.</summary>
    [Theory]
    [InlineData("{oops")]
    [InlineData("""{"ref":"javascript:alert(1)"}""")]
    [InlineData(null)]
    public void Un_riferimento_illeggibile_mostra_il_segnaposto(string? json)
    {
        Localizzatore();

        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block, Block(json)));

        Assert.Empty(cut.FindAll("p.att-link a"));
        Assert.Contains("Att_Missing", cut.Markup);
    }

    /// <summary>La nota sotto il link passa da MarkdownLite, che encoda: niente HTML dal contenuto editoriale.</summary>
    [Fact]
    public void La_nota_e_encodata_niente_xss()
    {
        Localizzatore();
        var json = AttachmentRef.Serialize(new AttachmentRef("loa-lirr-lfmm", "LoA"));

        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block, Block(json, "<script>alert(1)</script>")));

        Assert.DoesNotContain("<script>", cut.Markup);
        Assert.Contains("&lt;script&gt;", cut.Markup);
    }

    /// <summary>Anche il titolo viene dal contenuto editoriale: se ci si scrive dentro del markup, si legge
    /// come testo. È il posto in cui un link finto sarebbe più credibile.</summary>
    [Fact]
    public void Il_titolo_e_encodato_niente_xss()
    {
        Localizzatore();
        var json = AttachmentRef.Serialize(new AttachmentRef("loa-lirr-lfmm", "<script>alert(1)</script>"));

        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block, Block(json)));

        Assert.DoesNotContain("<script>", cut.Markup);
    }

    // ---- modo incorporato --------------------------------------------------------------------------

    /// <summary>
    /// ⚠️ Anche l'iframe punta alla <b>nostra</b> rotta: il 302 vale anche dentro un riquadro, e l'indirizzo
    /// del deposito resta fuori dal documento esattamente come nel link. Nessuna eccezione.
    /// </summary>
    [Fact]
    public void Il_modo_incorporato_mette_liframe_sulla_nostra_rotta()
    {
        Localizzatore();
        var json = AttachmentRef.Serialize(new AttachmentRef("loa-lirr-lfmm", "LoA",
            AttachmentDisplayMode.Embedded, AttachmentEmbedHeight.Large));

        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block, Block(json)));

        var frame = cut.Find(".att-embed iframe");
        Assert.Equal("/vsop/files/loa-lirr-lfmm", frame.GetAttribute("src"));
        Assert.Contains("800px", cut.Find(".att-embed").GetAttribute("style")!);
        Assert.DoesNotContain("drive.google.com", cut.Markup);
    }

    /// <summary>Il riquadro ha un nome: per chi naviga a tastiera o con uno screen reader un iframe senza
    /// <c>title</c> è «frame», e basta.</summary>
    [Fact]
    public void Il_riquadro_ha_un_nome()
    {
        Localizzatore();
        var json = AttachmentRef.Serialize(new AttachmentRef("loa-lirr-lfmm", "LoA Roma-Marseille",
            AttachmentDisplayMode.Embedded));

        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block, Block(json)));

        Assert.Equal("LoA Roma-Marseille", cut.Find(".att-embed iframe").GetAttribute("title"));
    }

    /// <summary>
    /// ⚠️ <b>Il link sotto c'è LO STESSO</b>, e non è ridondanza: è il ripiego per il giorno che Google
    /// chiude l'incorporamento — è già successo col fondo mappa CARTO — ed è l'unica cosa che sopravvive
    /// alla stampa, dove l'iframe non esce.
    /// </summary>
    [Fact]
    public void Anche_da_incorporato_il_link_sotto_ce_sempre()
    {
        Localizzatore();
        var json = AttachmentRef.Serialize(new AttachmentRef("loa-lirr-lfmm", "LoA",
            AttachmentDisplayMode.Embedded));

        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block, Block(json)));

        Assert.Equal("/vsop/files/loa-lirr-lfmm", cut.Find("p.att-link a").GetAttribute("href"));
    }

    /// <summary>Nel modo link non c'è nessun riquadro: è il default, e deve restare leggero.</summary>
    [Fact]
    public void Nel_modo_link_non_ce_nessun_riquadro()
    {
        Localizzatore();
        var json = AttachmentRef.Serialize(new AttachmentRef("loa-lirr-lfmm", "LoA"));

        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block, Block(json)));

        Assert.Empty(cut.FindAll("iframe"));
    }

    // ---- editor ----------------------------------------------------------------------------------------

    /// <summary>
    /// ⚠️ Si <b>sceglie</b> da un elenco, non si incolla un link. La biblioteca è il posto in cui un allegato
    /// entra: se di qui si potesse incollare un URL, il registro «chi cita cosa» direbbe il falso il giorno
    /// dopo — ed è esattamente il difetto che questa feature esiste per chiudere.
    /// </summary>
    [Fact]
    public void Leditor_scrive_il_token_scegliendo_dalla_biblioteca()
    {
        Localizzatore();
        Services.AddSingleton<IAttachmentLibrary>(new BibliotecaFinta(Voce("loa-lirr-lfmm", "LoA Roma-Marseille")));

        string? scritto = null;
        var cut = RenderComponent<AttachmentBlockEditor>(p => p
            .Add(x => x.AttachmentJson, null)
            .Add(x => x.AttachmentJsonChanged, (string? j) => scritto = j));

        cut.Find("select").Change("loa-lirr-lfmm");

        var r = AttachmentRef.Parse(scritto);
        Assert.NotNull(r);
        Assert.Equal("loa-lirr-lfmm", r!.Slug);
        // Il titolo nasce da quello della biblioteca: è il punto di partenza sensato.
        Assert.Equal("LoA Roma-Marseille", r.Title);

        // Nessun campo per incollare un indirizzo: l'unico ingresso è l'elenco.
        Assert.Empty(cut.FindAll("input[type=url]"));
    }

    /// <summary>
    /// Il titolo si scrive nel blocco ed è una <b>decisione editoriale del documento</b>: «la LoA con
    /// Marsiglia» dentro una frase, un altro nome in una tabella. Prenderlo dalla biblioteca a ogni resa
    /// vorrebbe dire che rinominare una voce riscrive il testo dei documenti che la citano.
    /// </summary>
    [Fact]
    public void Il_titolo_del_blocco_si_puo_cambiare_senza_toccare_la_biblioteca()
    {
        Localizzatore();
        Services.AddSingleton<IAttachmentLibrary>(new BibliotecaFinta(Voce("loa-lirr-lfmm", "LoA Roma-Marseille")));

        string? scritto = null;
        var cut = RenderComponent<AttachmentBlockEditor>(p => p
            .Add(x => x.AttachmentJson, AttachmentRef.Serialize(new AttachmentRef("loa-lirr-lfmm", "LoA Roma-Marseille")))
            .Add(x => x.AttachmentJsonChanged, (string? j) => scritto = j));

        cut.Find("input.app-in").Change("la LoA con Marsiglia");

        var r = AttachmentRef.Parse(scritto);
        Assert.Equal("la LoA con Marsiglia", r!.Title);
        Assert.Equal("loa-lirr-lfmm", r.Slug);   // lo slug non si tocca: è l'identità
    }

    /// <summary>Scegliere «nessuno» toglie l'allegato: senza, un blocco messo per sbaglio non si svuota più.</summary>
    [Fact]
    public void Scegliere_nessuno_toglie_lallegato()
    {
        Localizzatore();
        Services.AddSingleton<IAttachmentLibrary>(new BibliotecaFinta(Voce("loa-lirr-lfmm", "LoA")));

        string? scritto = "non toccato";
        var cut = RenderComponent<AttachmentBlockEditor>(p => p
            .Add(x => x.AttachmentJson, AttachmentRef.Serialize(new AttachmentRef("loa-lirr-lfmm", "LoA")))
            .Add(x => x.AttachmentJsonChanged, (string? j) => scritto = j));

        cut.Find("select").Change("");

        Assert.Null(scritto);
    }

    /// <summary>⚠️ Il catch-22 al contrario: con la biblioteca vuota la tendina non dice niente, e chi edita
    /// non ha modo di sapere che si comincia da un'altra pagina.</summary>
    [Fact]
    public void Con_la_biblioteca_vuota_leditor_dice_da_dove_si_comincia()
    {
        Localizzatore();
        Services.AddSingleton<IAttachmentLibrary>(new BibliotecaFinta());

        var cut = RenderComponent<AttachmentBlockEditor>(p => p.Add(x => x.AttachmentJson, null));

        Assert.Contains("Att_BlockEmptyHint", cut.Markup);
    }

    /// <summary>Il modo si cambia dall'editor, e l'altezza compare solo dove conta: un campo che c'è ma non
    /// fa niente si compila lo stesso, e poi qualcuno si chiede perché non è cambiato niente.</summary>
    [Fact]
    public void Laltezza_compare_solo_nel_modo_incorporato()
    {
        Localizzatore();
        Services.AddSingleton<IAttachmentLibrary>(new BibliotecaFinta(Voce("loa-lirr-lfmm", "LoA")));

        string? scritto = null;
        var cut = RenderComponent<AttachmentBlockEditor>(p => p
            .Add(x => x.AttachmentJson, AttachmentRef.Serialize(new AttachmentRef("loa-lirr-lfmm", "LoA")))
            .Add(x => x.AttachmentJsonChanged, (string? j) => scritto = j));

        // In modo link ci sono due tendine: l'allegato e il modo. L'altezza no.
        Assert.Equal(2, cut.FindAll("select").Count);

        cut.FindAll("select").ToArray()[1].Change(nameof(AttachmentDisplayMode.Embedded));

        Assert.Equal(AttachmentDisplayMode.Embedded, AttachmentRef.Parse(scritto)!.Mode);
    }

    [Fact]
    public void Laltezza_scelta_finisce_nel_blocco()
    {
        Localizzatore();
        Services.AddSingleton<IAttachmentLibrary>(new BibliotecaFinta(Voce("loa-lirr-lfmm", "LoA")));

        string? scritto = null;
        var cut = RenderComponent<AttachmentBlockEditor>(p => p
            .Add(x => x.AttachmentJson, AttachmentRef.Serialize(new AttachmentRef("loa-lirr-lfmm", "LoA",
                AttachmentDisplayMode.Embedded)))
            .Add(x => x.AttachmentJsonChanged, (string? j) => scritto = j));

        var tendine = cut.FindAll("select").ToArray();
        Assert.Equal(3, tendine.Length);   // allegato, modo, altezza

        tendine[2].Change(nameof(AttachmentEmbedHeight.Small));

        Assert.Equal(AttachmentEmbedHeight.Small, AttachmentRef.Parse(scritto)!.Height);
    }

    /// <summary>L'anteprima nell'editor è il componente vero, non un facsimile: quel che si vede è quel che
    /// leggerà chi apre il documento.</summary>
    [Fact]
    public void Leditor_mostra_lanteprima_vera()
    {
        Localizzatore();
        Services.AddSingleton<IAttachmentLibrary>(new BibliotecaFinta(Voce("loa-lirr-lfmm", "LoA")));

        var cut = RenderComponent<AttachmentBlockEditor>(p => p
            .Add(x => x.AttachmentJson, AttachmentRef.Serialize(new AttachmentRef("loa-lirr-lfmm", "LoA"))));

        Assert.Equal("/vsop/files/loa-lirr-lfmm",
            cut.Find(".att-preview p.att-link a").GetAttribute("href"));
    }
}
