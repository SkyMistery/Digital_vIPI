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
        Dictionary<string, Dictionary<string, string>>? congelate = null,
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

    private static Dictionary<string, Dictionary<string, string>> Congelate(params (string Da, string A)[] coppie) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["it"] = coppie.ToDictionary(c => TranslationText.Hash(c.Da), c => c.A, StringComparer.Ordinal),
        };

    // ---- Il congelato vince -------------------------------------------------------------------------

    [Fact]
    public async Task Se_la_release_ha_congelato_vince_il_congelato_e_la_memoria_NON_si_legge()
    {
        // La memoria viva dice una cosa, lo snapshot un'altra: deve vincere lo snapshot. E la memoria non
        // si deve nemmeno interrogare — sarebbe una query per documento pubblicato, per niente.
        var memoria = new MemoriaDiTraduzioneFinta()
            .Nota("Purpose", "SCOPO DALLA MEMORIA VIVA")
            .Nota("This LoA applies.", "DALLA MEMORIA VIVA");

        var vista = Vista("Letter of Agreement", "This LoA applies.",
            Congelate(("Purpose", "Scopo"),
                      ("This LoA applies.", "La presente lettera si applica."),
                      ("Letter of Agreement", "Lettera d'accordo")));

        var esito = await new DocumentTranslator(memoria).TranslateAsync(vista, "en", "it");

        Assert.Equal("Lettera d'accordo", esito.View.Title);
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
        var congelate = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new(StringComparer.Ordinal) { [TranslationText.Hash("Testo")] = "Text" },
        };
        var memoria = new MemoriaDiTraduzioneFinta();
        var vista = Vista("T", "Testo", congelate, lingua: Language.It);

        await new DocumentTranslator(memoria).TranslateAsync(vista, "it", "fr");
        Assert.Equal(1, memoria.Letture);
    }

    // ---- La copertura sul congelato ------------------------------------------------------------------

    [Fact]
    public async Task Il_congelato_si_dichiara_NON_riletto()
    {
        // ⚠️ Lo snapshot porta il TESTO, non chi lo ha scritto. Sbagliare per eccesso di cautela qui vuol
        // dire un avviso di troppo; sbagliare al contrario vuol dire dichiarare riletta una frase che
        // nessuno ha mai guardato — su un documento operativo.
        var vista = Vista("T", "This LoA applies.",
            Congelate(("T", "T"), ("Purpose", "Scopo"), ("This LoA applies.", "Si applica.")));

        var esito = await new DocumentTranslator(new MemoriaDiTraduzioneFinta()).TranslateAsync(vista, "en", "it");

        Assert.True(esito.Coverage.Completa);
        Assert.True(esito.Coverage.DaRileggere);
        Assert.Equal(0, esito.Coverage.Riletti);
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
