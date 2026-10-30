using System.Text.Json;
using System.Text.Json.Nodes;
using Vipi.Application.Translation;

namespace Vipi.Application.Content;

/// <summary>Che cosa è successo a una riga di testo fra la copia di confronto e quella in esame.</summary>
public enum TextDiffKind { Removed, Added }

/// <summary>Una riga di testo tolta o aggiunta in una sezione (carta 2026-10-28-stato-dei-documenti §2.1).</summary>
public sealed record TextDiffLine(TextDiffKind Kind, string Text);

/// <summary>
/// Il testo di una sezione come lo legge il confronto fra due copie di un documento, e le righe che cambiano fra due
/// testi (carta <c>docs/feature/2026-10-28-stato-dei-documenti.md</c> §2.1, S133).
///
/// <para><b>Perché il testo e non il numero di blocchi.</b> Fino al 28 ottobre 2026 il confronto contava i blocchi di
/// ogni sezione: una frase riscritta dentro un paragrafo che c'era già non era un cambiamento, e la copia pubblicata
/// poteva dire una cosa vecchia senza che nessuna riga chiedesse di ripubblicarla.</para>
///
/// <para>Una <b>riga</b> è quel che si legge: un paragrafo di prosa, l'intestazione di una tabella, una riga di
/// tabella con le sue celle in fila (« · »), un testo di un blocco JSON che non è una tabella (titolo, testo
/// alternativo, nota). Le stesse stringhe che si traducono (<see cref="TextSegmenter"/>), normalizzate allo stesso
/// modo (<see cref="TranslationText.Normalize"/>): lo stesso testo battuto su Windows o incollato da altrove è la
/// stessa riga.</para>
/// <para>⚠️ Il titolo della sezione NON è testo: quello di una sezione di catalogo lo risolve la vista e cambia
/// senza che cambi il documento (U-249), e quello di una sezione libera è già la sua identità.</para>
/// </summary>
public static class TestoDelConfronto
{
    /// <summary>Separatore delle celle di una riga di tabella.</summary>
    public const string FraLeCelle = " · ";

    /// <summary>Oltre questo prodotto di righe il confronto non cerca l'allineamento: tolte tutte, aggiunte tutte.</summary>
    private const int TettoAllineamento = 250_000;

    /// <summary>Le righe di testo dei blocchi di una sezione, nell'ordine; senza quelle delle sottosezioni.</summary>
    public static IReadOnlyList<string> Righe(IEnumerable<RawBlock> blocchi)
    {
        var righe = new List<string>();
        foreach (var b in blocchi.OrderBy(b => b.Order))
        {
            foreach (var p in TextSegmenter.SplitProse(b.Body))
                Aggiungi(righe, p);
            RigheJson(b.BodyJson, righe);
        }
        return righe;
    }

    private static void Aggiungi(List<string> righe, string? testo)
    {
        var norm = TranslationText.Normalize(testo);
        if (norm.Length > 0) righe.Add(norm);
    }

    private static string Cucite(IEnumerable<string> pezzi) =>
        string.Join(FraLeCelle, pezzi.Select(TranslationText.Normalize).Where(p => p.Length > 0));

    /// <summary>
    /// Il testo di un corpo JSON. Una tabella (<c>{"columns":[…],"rows":[{"cells":[…]}]}</c>) dà una riga per
    /// l'intestazione e una per ogni riga, con le celle in fila: una cella sola fuori dalla sua riga non dice niente a
    /// chi legge il confronto. Tutto il resto dà una riga per stringa di testo.
    /// </summary>
    private static void RigheJson(string? bodyJson, List<string> righe)
    {
        if (string.IsNullOrWhiteSpace(bodyJson)) return;
        JsonNode? radice;
        try { radice = JsonNode.Parse(bodyJson); }
        catch (JsonException) { return; }

        if (radice is JsonObject oggetto && oggetto["rows"] is JsonArray file)
        {
            foreach (var (chiave, valore) in oggetto)
            {
                if (chiave == "rows" || valore is null) continue;
                var pezzo = new JsonObject { [chiave] = valore.DeepClone() };
                var riga = Cucite(TextSegmenter.SplitJson(pezzo.ToJsonString()));
                if (riga.Length > 0) righe.Add(riga);
            }
            foreach (var fila in file)
            {
                if (fila is null) continue;
                var riga = Cucite(TextSegmenter.SplitJson(fila.ToJsonString()));
                if (riga.Length > 0) righe.Add(riga);
            }
            return;
        }

        foreach (var s in TextSegmenter.SplitJson(bodyJson))
            Aggiungi(righe, s);
    }

    /// <summary>
    /// Le righe tolte e aggiunte per passare da <paramref name="prima"/> a <paramref name="dopo"/>, nell'ordine in cui
    /// si leggono: l'allineamento è la sottosequenza comune più lunga, quindi una riga spostata di posto conta come tolta
    /// e aggiunta, e le righe rimaste uguali non compaiono.
    /// </summary>
    public static IReadOnlyList<TextDiffLine> Differenze(IReadOnlyList<string> prima, IReadOnlyList<string> dopo)
    {
        if (prima.SequenceEqual(dopo, StringComparer.Ordinal)) return Array.Empty<TextDiffLine>();

        var n = prima.Count;
        var m = dopo.Count;
        if ((long)n * m > TettoAllineamento)
            return prima.Select(r => new TextDiffLine(TextDiffKind.Removed, r))
                .Concat(dopo.Select(r => new TextDiffLine(TextDiffKind.Added, r))).ToList();

        // lcs[i, j] = lunghezza della sottosequenza comune fra prima[i..] e dopo[j..].
        var lcs = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
            for (var j = m - 1; j >= 0; j--)
                lcs[i, j] = string.Equals(prima[i], dopo[j], StringComparison.Ordinal)
                    ? lcs[i + 1, j + 1] + 1
                    : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

        var esito = new List<TextDiffLine>();
        int a = 0, b = 0;
        while (a < n && b < m)
        {
            if (string.Equals(prima[a], dopo[b], StringComparison.Ordinal)) { a++; b++; }
            else if (lcs[a + 1, b] >= lcs[a, b + 1]) esito.Add(new TextDiffLine(TextDiffKind.Removed, prima[a++]));
            else esito.Add(new TextDiffLine(TextDiffKind.Added, dopo[b++]));
        }
        while (a < n) esito.Add(new TextDiffLine(TextDiffKind.Removed, prima[a++]));
        while (b < m) esito.Add(new TextDiffLine(TextDiffKind.Added, dopo[b++]));
        return esito;
    }
}
