using Vipi.Application.Content;
using Vipi.Application.Translation;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>
/// Un documento che si legge in <b>una lingua sola</b>, dentro un sito che resta bilingue (carta
/// <c>docs/feature/2026-08-31-lingua-bloccata.md</c>).
///
/// <para>
/// ⚠️ <b>Il blocco spegne la traduzione, non la fa.</b> È la decisione del committente, ed è quella che
/// rende questa funzione a costo zero: nessun carattere pagato al motore, nessuna resa plausibile-e-sbagliata
/// da rileggere. Un documento scritto in italiano non diventa inglese bloccandolo.
/// </para>
/// </summary>
public class LinguaBloccataTests
{
    private static SectionView Sezione(string titolo, string? corpo = null) => new()
    {
        Id = "s-1",
        Title = titolo,
        Depth = 0,
        SectionKey = "custom:abc",
        Blocks = corpo is null
            ? Array.Empty<BlockView>()
            : new[] { new BlockView { Id = 1, Format = BlockFormat.Prose, State = RenderState.Expanded, Body = corpo } },
        Children = Array.Empty<SectionView>(),
    };

    private static DocumentView Documento(Language lingua, bool bloccata) => new()
    {
        Title = "vSOP — LIPA Aviano",
        AiracCycle = "2609",
        Language = lingua,
        LanguageLocked = bloccata,
        Sections = new[] { Sezione("General data", "Contact the tower.") },
    };

    // ---- Il cuore: bloccato ⇒ non si traduce, e non si LEGGE nemmeno ----------------------------------

    [Fact]
    public void Documento_bloccato_si_legge_nella_sua_lingua_anche_a_chi_guarda_il_sito_nell_altra()
    {
        var doc = Documento(Language.En, bloccata: true);

        // Quel che farebbe la pagina: la lingua la decide LinguaDiLettura, non la cultura di chi guarda.
        var lettore = LinguaDiLettura.PerIlDocumento(doc.LanguageLocked, doc.Language, Language.It);

        Assert.Equal("en", lettore);
    }

    [Fact]
    public async Task Bloccato_la_memoria_di_traduzione_non_si_legge_affatto()
    {
        // ⚠️ La memoria porta la resa italiana di ogni frase: se il traduttore la interrogasse, il documento
        // uscirebbe tradotto e il test lo vedrebbe. È il modo di provare che non ci si ARRIVA nemmeno.
        var memoria = new MemoriaDiTraduzioneFinta()
            .Nota("General data", "Dati generali")
            .Nota("Contact the tower.", "Contatta la torre.");

        var doc = Documento(Language.En, bloccata: true);
        var lettore = LinguaDiLettura.PerIlDocumento(doc.LanguageLocked, doc.Language, Language.It);

        var esito = await new DocumentTranslator(memoria).TranslateAsync(doc, Language.It, lettore);

        Assert.Equal(0, memoria.Letture);
        Assert.Equal("General data", esito.View.Sections[0].Title);
        Assert.Equal("Contact the tower.", esito.View.Sections[0].Blocks[0].Body);
    }

    [Fact]
    public async Task Bloccato_la_copertura_e_zero_segmenti_cosi_l_avviso_di_traduzione_a_macchina_tace()
    {
        // ⚠️ Non è un dettaglio di conteggio: `TranslationNotice` si mostra a `Segmenti > 0`, quindi è
        // QUESTA riga a spegnere l'avviso «pagina tradotta automaticamente» su un documento che tradotto
        // non è. Con una copertura «completa» l'avviso resterebbe muto per caso, non per costruzione.
        var doc = Documento(Language.En, bloccata: true);
        var lettore = LinguaDiLettura.PerIlDocumento(doc.LanguageLocked, doc.Language, Language.It);

        var esito = await new DocumentTranslator(new MemoriaDiTraduzioneFinta())
            .TranslateAsync(doc, Language.It, lettore);

        Assert.Equal(0, esito.Coverage.Segmenti);
        Assert.False(esito.Coverage.DaRileggere);
    }

    [Fact]
    public async Task Il_blocco_sopravvive_alla_traduzione()
    {
        // ⚠️ Il traduttore RICOSTRUISCE la vista, e ogni campo che non ricopia lo azzera in silenzio — il
        // default è quello «buono», quindi la pagina si rende lo stesso e nessun altro test cade. È già
        // successo con `Audience`: la chip non compariva mai su un documento tradotto, e il filtro non
        // filtrava. Qui vorrebbe dire dire «non bloccato» di un documento bloccato, subito dopo averlo letto.
        var doc = new DocumentView
        {
            Title = "vIPI — LIBC Crotone",
            AiracCycle = "2609",
            Language = Language.It,
            LanguageLocked = true,
            Sections = new[] { Sezione("Separazioni", "Contatta la torre.") },
        };

        var esito = await new DocumentTranslator(new MemoriaDiTraduzioneFinta().Nota("Separazioni", "Separations"))
            .TranslateAsync(doc, "it", "en");

        Assert.True(esito.View.LanguageLocked);
    }

    // ---- Non bloccato: tutto come ieri ----------------------------------------------------------------

    [Fact]
    public async Task Non_bloccato_si_traduce_come_prima()
    {
        // La metà che conta di più: una funzione spenta deve somigliare a una funzione spenta.
        var memoria = new MemoriaDiTraduzioneFinta().Nota("Separazioni", "Separations");
        var doc = new DocumentView
        {
            Title = "vIPI — LIBC Crotone",
            AiracCycle = "2609",
            Language = Language.It,
            Sections = new[] { Sezione("Separazioni") },
        };

        // Fuori da un blocco la lingua è quella di chi guarda; qui la si chiede esplicitamente al traduttore.
        var esito = await new DocumentTranslator(memoria).TranslateAsync(doc, "it", "en");

        Assert.Equal(1, memoria.Letture);
        Assert.Equal("Separations", esito.View.Sections[0].Title);
    }

    // ---- La lingua di lettura, in un posto solo -------------------------------------------------------

    [Theory]
    [InlineData(Language.It, "it")]
    [InlineData(Language.En, "en")]
    public void Bloccato_la_lingua_e_quella_del_documento(Language sorgente, string atteso) =>
        Assert.Equal(atteso, LinguaDiLettura.PerIlDocumento(bloccato: true, sorgente, Language.It));

    [Fact]
    public void Bloccato_senza_lingua_vale_quella_in_cui_la_famiglia_nasce()
    {
        // I documenti salvati prima che il campo esistesse arrivano con la lingua nulla. Bloccare uno di
        // quelli non deve dare «italiano» per default a una vLOA, che nasce inglese.
        Assert.Equal("en", LinguaDiLettura.PerIlDocumento(bloccato: true, sorgente: null, Language.En));
        Assert.Equal("it", LinguaDiLettura.PerIlDocumento(bloccato: true, sorgente: null, Language.It));
    }
}
