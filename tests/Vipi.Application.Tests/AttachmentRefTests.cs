using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il formato con cui un blocco cita il suo allegato. È la <b>fonte unica</b>: la usano i due editor, il
/// viewer, la ricerca e lo scanner dei riferimenti.
/// </summary>
public class AttachmentRefTests
{
    [Fact]
    public void Legge_il_riferimento_dal_json()
    {
        var r = AttachmentRef.Parse("""{"ref":"allegato:loa-lirr-lfmm","titolo":"LoA Roma-Marseille"}""");

        Assert.NotNull(r);
        Assert.Equal("loa-lirr-lfmm", r!.Slug);
        Assert.Equal("LoA Roma-Marseille", r.Title);
    }

    /// <summary>Nel blocco finisce il <b>token</b>, non l'URL: nemmeno il nostro. Se ci finisse la rotta,
    /// spostarla domani vorrebbe dire riscrivere il JSON di ogni blocco già pubblicato.</summary>
    [Fact]
    public void Nel_json_finisce_il_token_non_lurl()
    {
        var json = AttachmentRef.Serialize(new AttachmentRef("loa-lirr-lfmm", "LoA"));

        Assert.Contains("allegato:loa-lirr-lfmm", json);
        Assert.DoesNotContain("/vsop/files/", json);
        Assert.DoesNotContain("drive.google.com", json);
    }

    [Fact]
    public void Il_giro_completo_torna_uguale()
    {
        var originale = new AttachmentRef("loa-lirr-lfmm", "LoA con Marsiglia");

        Assert.Equal(originale, AttachmentRef.Parse(AttachmentRef.Serialize(originale)));
    }

    /// <summary>La rotta si ricava dallo slug, e resta la nostra: è l'identità del link.</summary>
    [Fact]
    public void Lurl_e_la_nostra_rotta() =>
        Assert.Equal("/vsop/files/loa-lirr-lfmm", new AttachmentRef("loa-lirr-lfmm").Url);

    /// <summary>
    /// ⚠️ <b>Solo lo schema <c>allegato:</c></b>. Un <c>ref</c> con un URL qualunque non è un riferimento a un
    /// allegato: accettarlo vorrebbe dire far entrare un indirizzo arbitrario — <c>javascript:</c> compreso —
    /// dentro un <c>href</c> che poi costruiamo noi, e per giunta dentro contenuto editoriale.
    /// </summary>
    [Theory]
    [InlineData("""{"ref":"javascript:alert(1)"}""")]
    [InlineData("""{"ref":"https://drive.google.com/file/d/abc/view"}""")]
    [InlineData("""{"ref":"/vsop/files/loa-lirr-lfmm"}""")]
    [InlineData("""{"ref":"loa-lirr-lfmm"}""")]
    public void Uno_schema_diverso_non_e_un_riferimento(string json) =>
        Assert.Null(AttachmentRef.Parse(json));

    /// <summary>Uno slug malformato non passa: il token da solo non basta, o basterebbe scrivere
    /// <c>allegato:</c> seguito da qualunque cosa per ottenere un link.</summary>
    [Theory]
    [InlineData("""{"ref":"allegato:LOA-LIRR"}""")]
    [InlineData("""{"ref":"allegato:loa lirr"}""")]
    [InlineData("""{"ref":"allegato:"}""")]
    public void Uno_slug_malformato_non_passa(string json) =>
        Assert.Null(AttachmentRef.Parse(json));

    /// <summary>Un JSON rotto si comporta come un blocco senza allegato: si vede il segnaposto, non
    /// un'eccezione in mezzo a un documento.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("non json")]
    [InlineData("{")]
    [InlineData("""{"altro":"x"}""")]
    public void Un_json_rotto_non_esplode(string? json) => Assert.Null(AttachmentRef.Parse(json));

    /// <summary>Il testo cercabile è il titolo e la nota, <b>mai</b> il JSON: cercare «Marseille» deve trovare
    /// la LoA, cercare un pezzo di slug non deve mostrare una riga di JSON nel risultato.</summary>
    [Fact]
    public void Il_testo_cercabile_e_il_titolo_e_la_nota()
    {
        var json = AttachmentRef.Serialize(new AttachmentRef("loa-lirr-lfmm", "LoA Roma-Marseille"));

        var testo = AttachmentRef.TextOf(json, "Firmata il 3 giugno");

        Assert.Contains("LoA Roma-Marseille", testo);
        Assert.Contains("Firmata il 3 giugno", testo);
        Assert.DoesNotContain("allegato:", testo);
        Assert.DoesNotContain("ref", testo);
    }

    // ---- il blocco dentro le sezioni extra --------------------------------------------------------

    /// <summary>Il giro completo attraverso l'envelope delle sezioni extra: è la stessa stringa che nel
    /// documento sta in <c>BodyJson</c>, quindi il travaso non deve tradurre niente.</summary>
    [Fact]
    public void Un_blocco_allegato_sopravvive_alla_serializzazione_degli_extra()
    {
        var json = AttachmentRef.Serialize(new AttachmentRef("loa-lirr-lfmm", "LoA"));
        var body = ExtraBlocks.Serialize(new[]
        {
            new ExtraBlock { Format = BlockFormat.Attachment, AttachmentJson = json, Text = "nota" },
        });

        var riletto = Assert.Single(ExtraBlocks.Parse(body));

        Assert.Equal(BlockFormat.Attachment, riletto.Format);
        Assert.Equal(json, riletto.AttachmentJson);
        Assert.Equal("nota", riletto.Text);
    }

    /// <summary>⚠️ Il contenuto è il RIFERIMENTO, non il testo: un allegato senza nota si salva, una nota
    /// senza allegato no. È la stessa regola dell'immagine, e per la stessa ragione.</summary>
    [Fact]
    public void Una_nota_senza_allegato_non_si_salva()
    {
        var body = ExtraBlocks.Serialize(new[]
        {
            new ExtraBlock { Format = BlockFormat.Attachment, Text = "una nota e basta" },
        });

        Assert.Null(body);
    }

    [Fact]
    public void Un_allegato_senza_nota_si_salva()
    {
        var body = ExtraBlocks.Serialize(new[]
        {
            new ExtraBlock
            {
                Format = BlockFormat.Attachment,
                AttachmentJson = AttachmentRef.Serialize(new AttachmentRef("loa-lirr-lfmm", "LoA")),
            },
        });

        Assert.NotNull(body);
        Assert.Equal(BlockFormat.Attachment, Assert.Single(ExtraBlocks.Parse(body)).Format);
    }

    /// <summary>Il testo di anteprima di una sezione extra porta il titolo dell'allegato, non il suo JSON.</summary>
    [Fact]
    public void Lanteprima_degli_extra_mostra_il_titolo_non_il_json()
    {
        var body = ExtraBlocks.Serialize(new[]
        {
            new ExtraBlock
            {
                Format = BlockFormat.Attachment,
                AttachmentJson = AttachmentRef.Serialize(new AttachmentRef("loa-lirr-lfmm", "LoA Marsiglia")),
            },
        });

        var testo = ExtraBlocks.PlainText(body);

        Assert.Contains("LoA Marsiglia", testo);
        Assert.DoesNotContain("allegato:", testo);
    }

    /// <summary>
    /// ⚠️ <b>I valori dell'enum stanno in coda.</b> Nel payload di una release gli enum sono serializzati come
    /// ordinali: inserirne uno in mezzo reinterpreterebbe in silenzio ogni release già pubblicata — un blocco
    /// tabella diventerebbe un'immagine, e nessuno lo denuncerebbe.
    /// </summary>
    [Fact]
    public void I_valori_storici_di_blockformat_non_si_sono_spostati()
    {
        Assert.Equal(0, (int)BlockFormat.Table);
        Assert.Equal(1, (int)BlockFormat.Prose);
        Assert.Equal(2, (int)BlockFormat.Image);
        Assert.Equal(3, (int)BlockFormat.List);
        Assert.Equal(4, (int)BlockFormat.AorMap);
        Assert.Equal(5, (int)BlockFormat.Callout);
        Assert.Equal(6, (int)BlockFormat.Attachment);
    }
}
