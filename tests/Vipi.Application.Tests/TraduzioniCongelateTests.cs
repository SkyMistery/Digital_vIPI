using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Translation;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Application.Tests;

/// <summary>
/// Il congelamento della traduzione nello snapshot di release (carta
/// <c>2026-08-27-documenti-bilingue.md</c> §6).
///
/// <para>
/// ⚠️ <b>Congelare non è cautela: è l'unico modo di limitare il raggio d'azione di una correzione.</b> La
/// memoria è indicizzata sulla FRASE, quindi senza fotografia chi corregge una resa su un documento
/// cambierebbe l'inglese <b>già pubblicato</b> di ogni altro documento che contiene quella frase — sotto gli
/// occhi di chi lo sta leggendo, e senza che il suo editor abbia pubblicato niente.
/// </para>
/// </summary>
public class TraduzioniCongelateTests
{
    private static DocumentView Vista(
        string titolo, string corpo,
        Dictionary<string, Dictionary<string, FrozenTranslation>>? congelate = null,
        Language? lingua = Language.En) => new()
    {
        Title = titolo,
        AiracCycle = "2609",
        Language = lingua,
        Translations = congelate,
        Sections = new[]
        {
            new SectionView
            {
                Id = "s-1", Title = "Purpose", Depth = 0, SectionKey = "purpose",
                Blocks = new[]
                {
                    new BlockView { Id = 1, Format = BlockFormat.Prose, State = RenderState.Expanded, Body = corpo },
                },
                Children = Array.Empty<SectionView>(),
            },
        },
    };

    /// <summary>Congelate <b>senza timbro</b>: è quello che portano le release pubblicate prima che il
    /// timbro esistesse, e resta il caso normale di una release mai revisionata.</summary>
    private static Dictionary<string, Dictionary<string, FrozenTranslation>> Congelate(
        params (string Da, string A)[] coppie) => Congelate(rilette: false, coppie);

    /// <param name="rilette">Il timbro che viaggia nello snapshot: se una persona le aveva riviste al
    /// momento della pubblicazione.</param>
    private static Dictionary<string, Dictionary<string, FrozenTranslation>> Congelate(
        bool rilette, params (string Da, string A)[] coppie) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["it"] = coppie.ToDictionary(
                c => TranslationText.Hash(c.Da),
                c => new FrozenTranslation(c.A, rilette),
                StringComparer.Ordinal),
        };

    // ---- Il congelato vince -------------------------------------------------------------------------

    [Fact]
    public async Task Se_il_congelato_copre_TUTTO_la_memoria_non_si_legge()
    {
        // La memoria viva dice una cosa, lo snapshot un'altra: deve vincere lo snapshot. E se lo snapshot
        // copre ogni segmento, la memoria non si deve nemmeno interrogare — sarebbe una query per
        // documento pubblicato, per niente.
        var memoria = new MemoriaDiTraduzioneFinta()
            .Nota("Purpose", "SCOPO DALLA MEMORIA VIVA")
            .Nota("This LoA applies.", "DALLA MEMORIA VIVA");

        var vista = Vista("Letter of Agreement", "This LoA applies.",
            Congelate(("Purpose", "Scopo"),
                      ("This LoA applies.", "La presente lettera si applica."),
                      ("Letter of Agreement", "Lettera d'accordo")));

        var esito = await new DocumentTranslator(memoria).TranslateAsync(vista, "en", "it");

        // ⚠️ Il TITOLO no: e' il nome del documento e non si traduce (regole-lingua R4), nemmeno se una
        // release vecchia se l'era congelato tradotto. Lo snapshot puo' portarsela dietro, la pagina non
        // la usa.
        Assert.Equal("Letter of Agreement", esito.View.Title);
        Assert.Equal("Scopo", esito.View.Sections[0].Title);
        Assert.Equal("La presente lettera si applica.", esito.View.Sections[0].Blocks[0].Body);
        Assert.Equal(0, memoria.Letture);
    }

    [Fact]
    public async Task Una_correzione_fatta_dopo_NON_cambia_un_documento_gia_pubblicato()
    {
        // È il caso che dà senso a tutta la slice: qualcuno corregge la frase su un altro documento, e
        // questo — pubblicato ieri — deve continuare a leggersi come ieri.
        var congelate = Congelate(("This LoA applies.", "La presente lettera si applica."));
        var vista = Vista("T", "This LoA applies.", congelate);

        var memoriaCorretta = new MemoriaDiTraduzioneFinta().Nota("This LoA applies.", "RESA CORRETTA OGGI");
        var esito = await new DocumentTranslator(memoriaCorretta).TranslateAsync(vista, "en", "it");

        Assert.Equal("La presente lettera si applica.", esito.View.Sections[0].Blocks[0].Body);
    }

    [Fact]
    public async Task Senza_congelato_si_ricade_sulla_memoria_viva()
    {
        // È il comportamento delle release pubblicate prima di questa funzione, ed è quello giusto per una
        // bozza: chi sta scrivendo vuole vedere la traduzione di adesso, non quella dell'ultima release.
        var memoria = new MemoriaDiTraduzioneFinta().Nota("This LoA applies.", "La presente lettera si applica.");
        var vista = Vista("T", "This LoA applies.", congelate: null);

        var esito = await new DocumentTranslator(memoria).TranslateAsync(vista, "en", "it");

        Assert.Equal("La presente lettera si applica.", esito.View.Sections[0].Blocks[0].Body);
        Assert.Equal(1, memoria.Letture);
    }

    [Fact]
    public async Task Il_congelato_di_UN_ALTRA_lingua_non_serve_a_questa()
    {
        // Snapshot con l'inglese congelato, lettore che chiede il francese: si ricade sulla memoria viva
        // invece di mostrare l'inglese come se fosse francese.
        var congelate = new Dictionary<string, Dictionary<string, FrozenTranslation>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new(StringComparer.Ordinal) { [TranslationText.Hash("Testo")] = new("Text", false) },
        };
        var memoria = new MemoriaDiTraduzioneFinta();
        var vista = Vista("T", "Testo", congelate, lingua: Language.It);

        await new DocumentTranslator(memoria).TranslateAsync(vista, "it", "fr");
        Assert.Equal(1, memoria.Letture);
    }

    // ---- Quel che il congelato NON copre --------------------------------------------------------------
    //
    // ⚠️ È il difetto che si vedeva solo a regime, e che nessun test prendeva. La fotografia la scatta
    // `ReleaseService` nell'ISTANTE della pubblicazione, e il giro che riempie la memoria passa ogni
    // QUARTO D'ORA: chi scriveva prosa nuova e pubblicava subito — il caso normale, non quello raro —
    // congelava una traduzione incompleta. Il motore traduceva il resto dieci minuti dopo, la memoria ce
    // l'aveva, e nessuno andava più a prenderla: quel documento restava a chiazze FINO ALLA
    // RIPUBBLICAZIONE, con l'avviso «mancano N frasi su M» acceso per sempre.

    [Fact]
    public async Task Quel_che_il_congelato_NON_copre_lo_riempie_la_memoria_viva()
    {
        // Lo snapshot ha fotografato solo il titolo di sezione: il corpo, scritto poco prima di pubblicare,
        // il motore l'ha tradotto dopo.
        var memoria = new MemoriaDiTraduzioneFinta().Nota("This LoA applies.", "La presente lettera si applica.");
        var vista = Vista("T", "This LoA applies.", Congelate(("Purpose", "Scopo")));

        var esito = await new DocumentTranslator(memoria).TranslateAsync(vista, "en", "it");

        Assert.Equal("Scopo", esito.View.Sections[0].Title);                                  // dal congelato
        Assert.Equal("La presente lettera si applica.", esito.View.Sections[0].Blocks[0].Body); // dalla memoria
        Assert.True(esito.Coverage.Completa);
        Assert.Equal(0, esito.Coverage.Mancanti);
    }

    [Fact]
    public async Task La_memoria_si_interroga_SOLO_per_le_impronte_scoperte()
    {
        // ⚠️ Non basta contare le letture: una lettura che chiede tutto e butta via metà costerebbe come
        // prima e riaprirebbe la porta a una correzione arrivata dopo. Si chiede solo quel che manca.
        var memoria = new MemoriaDiTraduzioneFinta().Nota("This LoA applies.", "Si applica.");
        var vista = Vista("T", "This LoA applies.", Congelate(("Purpose", "Scopo")));

        await new DocumentTranslator(memoria).TranslateAsync(vista, "en", "it");

        Assert.Equal(1, memoria.Letture);
        Assert.True(memoria.HaChiesto("This LoA applies."));
        Assert.False(memoria.HaChiesto("Purpose"));
    }

    [Fact]
    public async Task Il_congelato_vince_ANCHE_quando_e_parziale()
    {
        // La riparazione non deve aprire una porta: sulle frasi che lo snapshot PORTA continua a vincere
        // lui, anche se il resto lo riempie la memoria. È tutta la ragione per cui si congela.
        var memoria = new MemoriaDiTraduzioneFinta()
            .Nota("Purpose", "RESA CORRETTA DOPO")
            .Nota("This LoA applies.", "Si applica.");
        var vista = Vista("T", "This LoA applies.", Congelate(("Purpose", "Scopo")));

        var esito = await new DocumentTranslator(memoria).TranslateAsync(vista, "en", "it");

        Assert.Equal("Scopo", esito.View.Sections[0].Title);
    }

    [Fact]
    public async Task Un_congelato_VUOTO_non_cancella_la_frase()
    {
        // ⚠️ Uno snapshot troncato, o una forma che il lettore non riconosce, arriva come testo vuoto. Se
        // valesse come «congelata», la frase sparirebbe dalla pagina invece di restare nella sua lingua —
        // e sparire è l'unico esito che un documento operativo non può avere. Vuoto = scoperto.
        var memoria = new MemoriaDiTraduzioneFinta().Nota("This LoA applies.", "Si applica.");
        var congelate = new Dictionary<string, Dictionary<string, FrozenTranslation>>(StringComparer.OrdinalIgnoreCase)
        {
            ["it"] = new(StringComparer.Ordinal)
            {
                [TranslationText.Hash("This LoA applies.")] = new("", false),
                [TranslationText.Hash("Purpose")] = new("", false),
            },
        };
        var vista = Vista("T", "This LoA applies.", congelate);

        var esito = await new DocumentTranslator(memoria).TranslateAsync(vista, "en", "it");

        Assert.Equal("Si applica.", esito.View.Sections[0].Blocks[0].Body);   // ripescata dalla memoria
        Assert.Equal("Purpose", esito.View.Sections[0].Title);                // né tradotta né cancellata
    }

    // ---- La copertura sul congelato ------------------------------------------------------------------

    [Fact]
    public async Task Il_congelato_SENZA_timbro_si_dichiara_non_riletto()
    {
        // ⚠️ Sbagliare per eccesso di cautela qui vuol dire un avviso di troppo; sbagliare al contrario
        // vuol dire dichiarare riletta una frase che nessuno ha mai guardato — su un documento operativo.
        // Senza timbro sono le release pubblicate prima del 28 agosto 2026: restano marcate finché non si
        // ripubblica, che è la regola di ogni altra correzione editoriale.
        var vista = Vista("T", "This LoA applies.",
            Congelate(("T", "T"), ("Purpose", "Scopo"), ("This LoA applies.", "Si applica.")));

        var esito = await new DocumentTranslator(new MemoriaDiTraduzioneFinta()).TranslateAsync(vista, "en", "it");

        Assert.True(esito.Coverage.Completa);
        Assert.True(esito.Coverage.DaRileggere);
        Assert.Equal(0, esito.Coverage.Riletti);
    }

    [Fact]
    public async Task Col_timbro_l_avviso_SI_SPEGNE()
    {
        // ⚠️ È il difetto che rendeva il giro di revisione un vicolo cieco: lo snapshot portava il testo e
        // non chi l'aveva scritto, quindi il viewer non poteva che dichiarare tutto «non revisionato».
        // Lo staff correggeva nel pannello, ripubblicava, e l'avviso restava acceso — su un documento in
        // cui ogni frase era stata riletta. Un giro di revisione senza uscita è un giro che nessuno fa una
        // seconda volta.
        var vista = Vista("T", "This LoA applies.",
            Congelate(rilette: true, ("Purpose", "Scopo"), ("This LoA applies.", "Si applica.")));

        var esito = await new DocumentTranslator(new MemoriaDiTraduzioneFinta()).TranslateAsync(vista, "en", "it");

        Assert.True(esito.Coverage.Completa);
        Assert.False(esito.Coverage.DaRileggere);
        Assert.Equal(2, esito.Coverage.Riletti);
    }

    [Fact]
    public async Task Una_sola_frase_senza_timbro_tiene_acceso_l_avviso()
    {
        // Il timbro è per FRASE, non per documento: basta che una non l'abbia guardata nessuno perché il
        // lettore debba saperlo. È l'avviso giusto, non un avviso di troppo.
        var congelate = Congelate(rilette: true, ("Purpose", "Scopo"));
        congelate["it"][TranslationText.Hash("This LoA applies.")] = new("Si applica.", false);
        var vista = Vista("T", "This LoA applies.", congelate);

        var esito = await new DocumentTranslator(new MemoriaDiTraduzioneFinta()).TranslateAsync(vista, "en", "it");

        Assert.True(esito.Coverage.Completa);
        Assert.True(esito.Coverage.DaRileggere);
        Assert.Equal(1, esito.Coverage.Riletti);
    }

    [Fact]
    public async Task La_vista_tradotta_ricorda_ancora_la_lingua_di_partenza()
    {
        // Resta una vista dello STESSO documento: chi la riceve deve poter sapere da che lingua si parte.
        var vista = Vista("T", "Testo", Congelate(("Testo", "Testo tradotto")));
        var esito = await new DocumentTranslator(new MemoriaDiTraduzioneFinta()).TranslateAsync(vista, "en", "it");

        Assert.Equal(Language.En, esito.View.Language);
        Assert.NotNull(esito.View.Translations);
    }

    // ---- La lingua sorgente sconosciuta ---------------------------------------------------------------

    [Fact]
    public async Task Uno_snapshot_vecchio_senza_lingua_non_dichiara_niente()
    {
        // ⚠️ Gli snapshot pubblicati prima del 28 agosto 2026 non portano la lingua. Un default farebbe
        // dire a una vLOA — che nasce in inglese — di essere italiana, e il viewer tradurrebbe testo
        // inglese come se fosse italiano. null = non si sa.
        var vista = Vista("T", "Testo", congelate: null, lingua: null);
        Assert.Null(vista.Language);

        // Il traduttore lavora lo stesso: la lingua sorgente gliela dice il chiamante. Quel che cambia è
        // che il chiamante, senza lingua nello snapshot, non deve chiedere una traduzione a caso.
        var esito = await new DocumentTranslator(new MemoriaDiTraduzioneFinta()).TranslateAsync(vista, "en", "it");
        Assert.Null(esito.View.Language);
    }
}
