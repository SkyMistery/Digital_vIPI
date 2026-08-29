using Vipi.Ui;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// L'unico link che <see cref="MarkdownLite"/> conosce: <c>[testo](allegato:slug)</c>.
///
/// <para>⚠️ <b>Questa classe è soprattutto un elenco di cose che NON devono diventare link.</b> Il renderer
/// encoda e poi sostituisce con delle regex: aprirlo a <c>[testo](url)</c> qualunque significherebbe far
/// entrare nel contenuto editoriale un indirizzo arbitrario — <c>javascript:</c> compreso — dentro un
/// <c>href</c> che costruiamo noi. Il perimetro è la difesa, quindi il perimetro va presidiato.</para>
/// </summary>
public class MarkdownLiteLinkTests
{
    private static string Html(string? markdown) => MarkdownLite.Render(markdown).Value;

    // ---- quel che diventa link -------------------------------------------------------------------------

    [Fact]
    public void Il_link_a_un_allegato_diventa_unancora()
    {
        var html = Html("Vedi la [LoA Marseille](allegato:loa-lirr-lfmm) per i dettagli.");

        Assert.Contains("<a href=\"/vsop/files/loa-lirr-lfmm\"", html);
        Assert.Contains(">LoA Marseille</a>", html);
        Assert.DoesNotContain("allegato:", html);
    }

    /// <summary>Come nel blocco: si apre una scheda nuova, perché un documento aperto per consultarlo non
    /// deve sparire da sotto chi lo stava leggendo.</summary>
    [Fact]
    public void Il_link_apre_una_scheda_nuova()
    {
        var html = Html("[LoA](allegato:loa-lirr-lfmm)");

        Assert.Contains("target=\"_blank\"", html);
        Assert.Contains("rel=\"noopener\"", html);
    }

    /// <summary>L'indirizzo lo componiamo NOI dallo slug: nel testo del documento c'è un nome, non un URL.
    /// È la stessa ragione per cui il blocco porta il token e non la rotta.</summary>
    [Fact]
    public void Lindirizzo_lo_componiamo_noi_e_non_e_quello_del_deposito()
    {
        var html = Html("[LoA](allegato:loa-lirr-lfmm)");

        Assert.DoesNotContain("drive.google.com", html);
        Assert.Contains("/vsop/files/loa-lirr-lfmm", html);
    }

    [Fact]
    public void Due_link_nella_stessa_frase_escono_tutti_e_due()
    {
        var html = Html("[una](allegato:loa-uno) e [due](allegato:loa-due)");

        Assert.Contains("/vsop/files/loa-uno", html);
        Assert.Contains("/vsop/files/loa-due", html);
    }

    /// <summary>Il testo del link può portare grassetto e corsivo: le due sostituzioni girano prima.</summary>
    [Fact]
    public void Il_testo_del_link_puo_essere_in_grassetto()
    {
        var html = Html("[**LoA** Marseille](allegato:loa-lirr-lfmm)");

        Assert.Contains("<a href=\"/vsop/files/loa-lirr-lfmm\"", html);
        Assert.Contains("<strong>LoA</strong>", html);
    }

    // ---- quel che NON deve diventare link ---------------------------------------------------------------

    /// <summary>
    /// ⚠️ Il caso per cui esiste il perimetro. Se il renderer accettasse un URL qualunque, questa riga
    /// diventerebbe un link eseguibile scritto da chi compila un documento.
    /// </summary>
    [Theory]
    [InlineData("[clicca](javascript:alert(1))")]
    [InlineData("[clicca](https://esempio.it)")]
    [InlineData("[clicca](http://esempio.it)")]
    [InlineData("[clicca](/services/vsop/admin/permissions)")]
    [InlineData("[clicca](data:text/html;base64,PHNjcmlwdD4=)")]
    [InlineData("[clicca](vbscript:msgbox)")]
    public void Nessun_altro_schema_diventa_un_link(string markdown)
    {
        var html = Html(markdown);

        Assert.DoesNotContain("<a ", html);
        Assert.DoesNotContain("href", html);
    }

    /// <summary>Uno «slug» che non ha la forma di uno slug non passa: senza il vincolo,
    /// <c>allegato:../../qualcosa</c> finirebbe dentro l'indirizzo che componiamo.</summary>
    [Theory]
    [InlineData("[x](allegato:../../qualcosa)")]
    [InlineData("[x](allegato:LOA-LIRR)")]
    [InlineData("[x](allegato:loa lirr)")]
    [InlineData("[x](allegato:)")]
    [InlineData("[x](allegato:loa/lirr)")]
    public void Uno_slug_malformato_non_diventa_un_link(string markdown)
    {
        var html = Html(markdown);

        Assert.DoesNotContain("<a ", html);
        Assert.DoesNotContain("href", html);
    }

    /// <summary>Il testo del link è già encodato quando la sostituzione gira: dentro l'ancora resta testo.</summary>
    [Fact]
    public void Il_testo_del_link_resta_encodato()
    {
        var html = Html("[<script>alert(1)</script>](allegato:loa-lirr-lfmm)");

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    /// <summary>⚠️ Un apice nel testo non deve poter chiudere l'attributo <c>href</c> che scriviamo: se
    /// l'encoding non girasse prima, questa riga aprirebbe un attributo nuovo dentro l'ancora.</summary>
    [Fact]
    public void Un_apice_nel_testo_non_esce_dallattributo()
    {
        var html = Html("[\" onmouseover=\"alert(1)](allegato:loa-lirr-lfmm)");

        Assert.DoesNotContain("onmouseover=\"alert", html);
        Assert.Contains("&quot;", html);
    }

    /// <summary>Un link a metà resta testo: le parentesi non sono un invito a indovinare.</summary>
    [Theory]
    [InlineData("[senza parentesi] allegato:loa-lirr-lfmm")]
    [InlineData("[x](allegato loa-lirr-lfmm)")]
    [InlineData("allegato:loa-lirr-lfmm da solo")]
    public void Cio_che_non_e_un_link_resta_testo(string markdown) =>
        Assert.DoesNotContain("<a ", Html(markdown));

    /// <summary>Le vecchie righe non cambiano: grassetto, corsivo e a capo funzionano come prima.</summary>
    [Fact]
    public void Il_resto_del_renderer_non_e_cambiato()
    {
        var html = Html("**forte** e *lieve*\nnuova riga");

        Assert.Contains("<strong>forte</strong>", html);
        Assert.Contains("<em>lieve</em>", html);
        Assert.Contains("<br>", html);
    }
}
