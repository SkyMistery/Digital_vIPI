using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le regole pure della biblioteca allegati: lo slug che i documenti citeranno, e l'id del file estratto dal
/// link che lo staffista incolla.
///
/// <para>Sono la parte che si può sbagliare in silenzio. Uno slug malformato produce un riferimento che a
/// volte si scrive giusto e a volte no; un id estratto male produce un link che <b>sembra</b> salvato e
/// porta a una pagina di Google che dice «file non trovato» — a chi legge il documento, non a chi l'ha
/// caricato.</para>
/// </summary>
public class AttachmentRulesTests
{
    // ---- slug ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("loa-lirr-lfmm")]
    [InlineData("circolare-2026-01")]
    [InlineData("ab")]
    public void Uno_slug_ben_formato_passa(string slug) => Assert.True(AttachmentRules.SlugValido(slug));

    [Theory]
    [InlineData("")]
    [InlineData("a")]                    // troppo corto: un carattere non è un nome
    [InlineData("LoA-LIRR")]             // maiuscole
    [InlineData("loa lirr")]             // spazi
    [InlineData("loa--lirr")]            // trattino doppio
    [InlineData("-loa")]                 // trattino al bordo
    [InlineData("loa-")]
    [InlineData("loa_lirr")]             // il trattino basso non è il nostro separatore
    [InlineData("loa.lirr")]
    [InlineData("forlì")]                // accenti: si scrivono in due modi e il link muore in uno dei due
    public void Uno_slug_malformato_viene_rifiutato(string slug) => Assert.False(AttachmentRules.SlugValido(slug));

    [Fact]
    public void Lo_slug_troppo_lungo_non_passa()
    {
        var lungo = new string('a', AttachmentRules.SlugMaxLength + 1);
        Assert.False(AttachmentRules.SlugValido(lungo));
    }

    [Theory]
    [InlineData("LoA Roma–Marseille", "loa-roma-marseille")]
    [InlineData("Circolare 01/2026", "circolare-01-2026")]
    [InlineData("  spazi   in   mezzo  ", "spazi-in-mezzo")]
    public void Lo_slug_si_propone_dal_titolo(string titolo, string atteso) =>
        Assert.Equal(atteso, AttachmentRules.SlugDa(titolo));

    /// <summary>
    /// ⚠️ Gli accenti si <b>traslitterano</b>, non si buttano: buttarli darebbe <c>forl</c>, che è un nome
    /// che nessuno riconosce più — e nessuno se ne accorge, perché uno slug non si rilegge.
    /// </summary>
    [Fact]
    public void Gli_accenti_diventano_la_lettera_non_il_nulla() =>
        Assert.Equal("aeroporto-di-forli", AttachmentRules.SlugDa("Aeroporto di Forlì"));

    /// <summary>La proposta è sempre valida: altrimenti la pagina proporrebbe qualcosa che poi rifiuta.</summary>
    [Theory]
    [InlineData("LoA Roma–Marseille")]
    [InlineData("Circolare 01/2026 — «rivista»")]
    [InlineData("Città di Forlì / Cesena")]
    public void Quel_che_si_propone_e_sempre_accettato(string titolo) =>
        Assert.True(AttachmentRules.SlugValido(AttachmentRules.SlugDa(titolo)));

    /// <summary>Un titolo senza nemmeno una lettera non produce uno slug finto: produce il vuoto, che la
    /// pagina rifiuta dicendolo.</summary>
    [Fact]
    public void Un_titolo_senza_lettere_non_produce_uno_slug() =>
        Assert.Equal("", AttachmentRules.SlugDa("— / —"));

    [Fact]
    public void Lo_slug_proposto_si_taglia_alla_lunghezza_della_colonna()
    {
        var slug = AttachmentRules.SlugDa(new string('a', 100) + " " + new string('b', 100));
        Assert.True(slug.Length <= AttachmentRules.SlugMaxLength);
        Assert.True(AttachmentRules.SlugValido(slug));
    }

    // ---- link di Drive ------------------------------------------------------------------------------

    private const string Id = "1A2b3C4d5E6f7G8h9I0jKlMnOpQrStUvW";

    [Theory]
    [InlineData("https://drive.google.com/file/d/{0}/view?usp=sharing")]   // il tasto «Condividi»
    [InlineData("https://drive.google.com/file/d/{0}/view")]
    [InlineData("https://drive.google.com/file/d/{0}/preview")]
    [InlineData("https://drive.google.com/open?id={0}")]
    [InlineData("https://drive.google.com/uc?id={0}&export=download")]
    [InlineData("https://docs.google.com/document/d/{0}/edit")]
    [InlineData("{0}")]                                                    // l'id nudo, incollato a mano
    public void Dalle_forme_vere_di_drive_si_ricava_lid(string forma) =>
        Assert.Equal(Id, AttachmentRules.ExternalIdDa(string.Format(forma, Id)));

    [Fact]
    public void Lo_spazio_intorno_al_link_non_conta() =>
        Assert.Equal(Id, AttachmentRules.ExternalIdDa($"  https://drive.google.com/file/d/{Id}/view  "));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("non un link")]
    [InlineData("https://drive.google.com/")]
    [InlineData("https://drive.google.com/file/d/corto/view")]     // troppo corto per essere un id
    [InlineData("https://drive.google.com/open?id=")]
    public void Da_un_link_senza_id_non_si_ricava_niente(string link) =>
        Assert.Null(AttachmentRules.ExternalIdDa(link));

    /// <summary>
    /// ⚠️ Uno schema che non è http(s) non passa. Il valore finisce dentro un <c>href</c> costruito da noi:
    /// accettare <c>javascript:</c> qui vorrebbe dire farlo entrare in un documento editoriale.
    /// </summary>
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/loa.pdf")]
    [InlineData("ftp://esempio.it/loa.pdf")]
    public void Uno_schema_che_non_e_http_non_passa(string link) =>
        Assert.Null(AttachmentRules.ExternalIdDa(link));

    /// <summary>Si tiene l'<b>id</b>, non l'URL: la stessa voce salvata da due forme diverse dello stesso
    /// link deve dare la stessa riga, o la biblioteca avrebbe due voci per un file solo.</summary>
    [Fact]
    public void Due_forme_dello_stesso_link_danno_lo_stesso_id() =>
        Assert.Equal(
            AttachmentRules.ExternalIdDa($"https://drive.google.com/file/d/{Id}/view?usp=sharing"),
            AttachmentRules.ExternalIdDa($"https://drive.google.com/open?id={Id}"));

    /// <summary>L'indirizzo si ricostruisce dall'id, in un posto solo, ed è la forma <c>/preview</c>: è
    /// l'unica che funziona dentro un iframe, cioè quella su cui poggerà il modo «incorporato».</summary>
    [Fact]
    public void Lindirizzo_esterno_e_la_preview() =>
        Assert.Equal($"https://drive.google.com/file/d/{Id}/preview",
            AttachmentRules.UrlEsterno(AttachmentProvider.Drive, Id));

    // ---- ambito -------------------------------------------------------------------------------------

    /// <summary>⚠️ La divisione non ha una chiave, e se qualcuno ne batte una si butta: una chiave su un
    /// perimetro che non ne ha una è un filtro che un giorno non trova più la riga.</summary>
    [Fact]
    public void La_divisione_non_tiene_una_chiave()
    {
        Assert.Null(AttachmentRules.ScopeKeyNorm(AttachmentScope.Division, "LIRR"));
        Assert.True(AttachmentRules.ScopeValido(AttachmentScope.Division, "LIRR"));
        Assert.True(AttachmentRules.ScopeValido(AttachmentScope.Division, null));
    }

    [Fact]
    public void Un_acc_o_uno_scalo_la_pretendono()
    {
        Assert.False(AttachmentRules.ScopeValido(AttachmentScope.Acc, null));
        Assert.False(AttachmentRules.ScopeValido(AttachmentScope.Airport, "  "));
        Assert.True(AttachmentRules.ScopeValido(AttachmentScope.Acc, "LIRR"));
    }

    [Fact]
    public void La_chiave_si_scrive_maiuscola_comunque_la_si_batta() =>
        Assert.Equal("LIMC", AttachmentRules.ScopeKeyNorm(AttachmentScope.Airport, " limc "));

    [Theory]
    [InlineData("LI RR")]
    [InlineData("LIRR!")]
    [InlineData("L")]
    public void Una_chiave_che_non_e_un_codice_non_passa(string chiave) =>
        Assert.False(AttachmentRules.ScopeValido(AttachmentScope.Acc, chiave));
}
