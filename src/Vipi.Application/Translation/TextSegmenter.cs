using System.Text.Json;
using System.Text.Json.Nodes;

namespace Vipi.Application.Translation;

/// <summary>
/// Taglia un campo editoriale nei <b>segmenti</b> che si traducono, e sa rimetterli insieme (carta
/// <c>docs/feature/2026-08-27-documenti-bilingue.md</c> §2).
///
/// <para>
/// ⚠️ <b>Non si manda mai un campo intero al motore.</b> Un blocco tabella è JSON: spedirlo così com'è
/// tornerebbe con le virgolette tradotte e la struttura distrutta. E un blocco di prosa lungo costringerebbe
/// a ritradurre tutto per una virgola cambiata.
/// </para>
///
/// <para>
/// <b>Il taglio è misurato, non scelto a naso.</b> Sul <c>vipi.db</c> reale (27 agosto 2026): 72 blocchi con
/// prosa, di cui <b>4</b> contengono un a-capo e <b>zero</b> contengono un paragrafo vuoto o un elenco. La
/// prosa dei nostri documenti è fatta di frasi singole. Quindi il paragrafo è l'unità giusta — quasi sempre
/// coincide col blocco — e non serve la macchina per gli elenchi che il formato non ha:
/// <c>MarkdownLite</c> conosce grassetto, corsivo e a-capo, e nient'altro.
/// </para>
///
/// <para>
/// ⚠️ <b>Il segmentatore è muto.</b> Restituisce <b>tutti</b> i segmenti, anche quelli senza niente da
/// tradurre, perché è ciò che rende il rimontaggio un giro completo. A decidere che cosa vale la pena
/// spedire pensano <c>TranslationText.HasSomethingToTranslate</c> e il protettore, che vengono dopo.
/// </para>
/// </summary>
public static class TextSegmenter
{
    /// <summary>Separatore di paragrafo, dopo la normalizzazione (che riduce a due gli a-capo di fila).</summary>
    private const string Paragrafo = "\n\n";

    /// <summary>
    /// Chiavi JSON il cui valore è <b>testo da leggere</b> e quindi da tradurre. È un elenco di ciò che si
    /// traduce, non di ciò che si salta: una chiave nuova che nessuno ha classificato resta <b>intatta</b>.
    /// <para>⚠️ Fuori restano di proposito <c>tableId</c>, <c>mediaId</c>, <c>r</c>, <c>width</c>,
    /// <c>height</c>, <c>unified</c>, <c>primary</c>, <c>star</c>: sono identificatori e interruttori, e
    /// tradurli romperebbe il documento invece di renderlo bilingue.</para>
    /// </summary>
    private static readonly IReadOnlySet<string> ChiaviDiTesto =
        new HashSet<string>(StringComparer.Ordinal) { "title", "alt", "group", "columns", "cells" };

    // ---- Prosa ---------------------------------------------------------------------------------------

    /// <summary>
    /// I paragrafi di un campo di prosa, nell'ordine. Normalizza per primo: senza, «due a-capo» sarebbe una
    /// nozione diversa a seconda di chi ha battuto il testo, e lo stesso blocco si taglierebbe in due modi.
    /// </summary>
    public static IReadOnlyList<string> SplitProse(string? markdown)
    {
        var norm = TranslationText.Normalize(markdown);
        if (norm.Length == 0) return Array.Empty<string>();
        return norm.Split(Paragrafo, StringSplitOptions.None);
    }

    /// <summary>
    /// Rimette insieme i paragrafi. Con i segmenti originali ridà esattamente il normalizzato — è una
    /// proprietà provata, ed è ciò che permette di sostituire un paragrafo alla volta senza perdere il resto.
    /// </summary>
    public static string JoinProse(IReadOnlyList<string> paragrafi) => string.Join(Paragrafo, paragrafi);

    // ---- JSON dei blocchi (tabelle, immagini) --------------------------------------------------------

    /// <summary>
    /// Ogni stringa traducibile dentro il <c>BodyJson</c> di un blocco, in ordine stabile: intestazioni di
    /// colonna, celle, etichette di gruppo, titolo, testo alternativo dell'immagine.
    /// <para>JSON illeggibile o vuoto → nessun segmento. Non è un errore: un blocco può avere un corpo che
    /// questo formato non descrive, e il traduttore deve lasciarlo stare, non protestare.</para>
    /// </summary>
    public static IReadOnlyList<string> SplitJson(string? bodyJson)
    {
        var trovati = new List<string>();
        MapJson(bodyJson, s => { trovati.Add(s); return s; });
        return trovati;
    }

    /// <summary>
    /// Riscrive lo stesso JSON passando ogni stringa traducibile per <paramref name="traduci"/>. Tutto il
    /// resto — chiavi, numeri, booleani, identificatori — resta byte per byte quello che era.
    /// <para>Con <c>traduci = s =&gt; s</c> il risultato è equivalente all'originale: è la proprietà che
    /// prova che la struttura non si perde per strada.</para>
    /// <para>JSON illeggibile → torna <b>l'originale intatto</b>. Un corpo che non capiamo non si tocca.</para>
    /// </summary>
    public static string? MapJson(string? bodyJson, Func<string, string> traduci)
    {
        if (string.IsNullOrWhiteSpace(bodyJson)) return bodyJson;

        JsonNode? radice;
        try
        {
            radice = JsonNode.Parse(bodyJson);
        }
        catch (JsonException)
        {
            return bodyJson;   // non è JSON nostro: si lascia com'è
        }
        if (radice is null) return bodyJson;

        Percorri(radice, traducibile: false, traduci);
        return radice.ToJsonString();
    }

    /// <summary>
    /// Scende nell'albero. <paramref name="traducibile"/> vale per i valori <b>dentro</b> un array che una
    /// chiave di testo ha aperto (<c>columns</c>, <c>cells</c>): lì le stringhe sono tutte da leggere, e non
    /// hanno una chiave propria da cui riconoscerle.
    /// </summary>
    private static void Percorri(JsonNode nodo, bool traducibile, Func<string, string> traduci)
    {
        switch (nodo)
        {
            case JsonObject oggetto:
                // ToList(): si riscrivono i valori mentre si itera, e senza la copia l'enumeratore protesta.
                foreach (var (chiave, valore) in oggetto.ToList())
                {
                    if (valore is null) continue;
                    var chiaveDiTesto = ChiaviDiTesto.Contains(chiave);
                    if (chiaveDiTesto && valore is JsonValue vs && vs.TryGetValue<string>(out var testo))
                        oggetto[chiave] = traduci(testo);
                    else
                        Percorri(valore, chiaveDiTesto, traduci);
                }
                break;

            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    var elemento = array[i];
                    if (elemento is null) continue;
                    if (traducibile && elemento is JsonValue ev && ev.TryGetValue<string>(out var cella))
                        array[i] = traduci(cella);
                    else
                        Percorri(elemento, traducibile, traduci);
                }
                break;
        }
    }
}
