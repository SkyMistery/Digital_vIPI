using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Translation;

/// <summary>
/// La vIPI ACC nella lingua di chi legge (carta <c>docs/feature/2026-08-27-documenti-bilingue.md</c> §7).
///
/// <para>
/// ⚠️ <b>Perché una classe a parte e non <see cref="DocumentTranslator"/>.</b> La vIPI ACC è l'unica delle
/// cinque famiglie che <b>non</b> arriva alla pagina come <see cref="DocumentView"/>: vive come blocchi
/// (<see cref="AccVipiData"/>), che sono la stessa cosa vista da un'altra angolazione — un titolo di blocco,
/// e per ogni sezione un titolo più la sua parte editoriale, che invece è una <see cref="SectionView"/> e si
/// traduce con la macchina di sempre. Qui c'è solo la passeggiata sull'albero dei blocchi: la memoria, il
/// conteggio della copertura e la preferenza per le traduzioni congelate restano una implementazione sola.
/// </para>
///
/// <para>
/// ⚠️ <b>Traduce SUL POSTO.</b> Non ricostruisce gli <see cref="AccBlock"/>, li modifica: un blocco porta
/// quindici campi che non c'entrano niente con la lingua (settori membri, configurazioni, colori dell'AoR,
/// override delle frequenze) e ricopiarli a mano è esattamente il guasto già pagato sulle sezioni — un campo
/// dimenticato torna al suo default e la pagina continua a rendersi, in silenzio. Il modello è di una
/// richiesta sola, quindi modificarlo non si vede da nessun'altra parte; ma va chiamato <b>dopo</b> aver
/// risolto le sezioni derivate, che di quei campi si servono.
/// </para>
/// </summary>
public sealed class AccVipiTranslator
{
    /// <summary>La lingua in cui nasce una vIPI ACC, per i documenti salvati prima che il campo esistesse.</summary>
    private const Language Predefinita = Language.It;

    private readonly DocumentTranslator _traduttore;

    public AccVipiTranslator(DocumentTranslator traduttore) => _traduttore = traduttore;

    /// <summary>
    /// Traduce i blocchi nella lingua di lettura e dice quanto ne è coperto. Se le due lingue coincidono non
    /// tocca né il modello né il database.
    /// </summary>
    /// <param name="data">I blocchi della vIPI ACC: modificati sul posto (vedi la nota sulla classe).</param>
    /// <param name="lingua">La lingua sorgente del documento; nulla sui documenti vecchi.</param>
    /// <param name="targetLang">La lingua di chi legge.</param>
    /// <param name="congelate">Le traduzioni congelate dalla release, per lingua di lettura: se ci sono
    /// vincono sulla memoria viva, come per le altre famiglie.</param>
    public async Task<TranslationCoverage> TranslateAsync(
        AccVipiData data, Language? lingua, string targetLang,
        IReadOnlyDictionary<string, Dictionary<string, FrozenTranslation>>? congelate = null, CancellationToken ct = default)
    {
        var sourceLang = DocumentTranslator.CodiceSorgente(lingua, Predefinita);
        var passata = await _traduttore
            .PreparaAsync(Segmenti(data), sourceLang, targetLang,
                DocumentTranslator.Congelate(congelate, targetLang), ct)
            .ConfigureAwait(false);

        if (passata.Coverage.Segmenti == 0) return TranslationCoverage.Nessuna;

        foreach (var blocco in data.Blocks)
        {
            // ⚠️ Il titolo del blocco Aerovia la pagina non lo mostra (usa una stringa di risorsa): tradurlo
            // costa niente e vale per i gruppi APP, che il titolo se lo scrive lo staff.
            blocco.Title = passata.Testo(blocco.Title) ?? blocco.Title;
            blocco.Sections = blocco.Sections
                .Select(s => s with
                {
                    Title = passata.Testo(s.Title) ?? s.Title,
                    Editorial = s.Editorial is null ? null : passata.Sezione(s.Editorial),
                })
                .ToList();
        }

        return passata.Coverage;
    }

    /// <summary>Ogni testo traducibile dei blocchi: i titoli, e la parte editoriale delle sezioni.</summary>
    private static IEnumerable<string> Segmenti(AccVipiData data)
    {
        foreach (var blocco in data.Blocks)
        {
            foreach (var s in DocumentTranslator.Aggiungi(blocco.Title)) yield return s;

            foreach (var sezione in blocco.Sections)
            {
                foreach (var s in DocumentTranslator.Aggiungi(sezione.Title)) yield return s;
                if (sezione.Editorial is null) continue;
                foreach (var s in DocumentTranslator.SegmentiSezione(sezione.Editorial)) yield return s;
            }
        }
    }
}
