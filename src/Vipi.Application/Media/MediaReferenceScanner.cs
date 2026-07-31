using System.Text.RegularExpressions;

namespace Vipi.Application.Media;

/// <summary>
/// Trova i riferimenti a un'immagine dentro un testo (JSON di un blocco, corpo di una sezione extra, payload di una
/// release). Serve alla pulizia degli asset non più usati: un'immagine si cancella solo se il suo sha non compare
/// da nessuna parte.
/// <para>
/// Cerca <b>qualunque</b> sequenza isolata di 64 esadecimali, non solo <c>"mediaId":"…"</c>. È deliberatamente largo
/// perché i due errori possibili non si equivalgono: riconoscere di più lascia in vita un asset orfano (spazio
/// sprecato, cioè il problema che stiamo già tollerando), riconoscere di meno cancella un'immagine ancora in uso e
/// rompe in silenzio un documento pubblicato. Il pattern largo regge anche un formato futuro che citasse lo sha in
/// un campo con un altro nome.
/// </para>
/// </summary>
public static class MediaReferenceScanner
{
    /// <summary>Separatore che sostituisce gli escape: fuori dall'alfabeto esadecimale, quindi delimita.</summary>
    private const string Separatore = "|";

    // Gli escape JSON vanno neutralizzati PRIMA di cercare, o il riferimento sfugge: System.Text.Json scrive le
    // virgolette di una stringa annidata con la sequenza di sei caratteri che comincia per backslash-u e finisce
    // per 22: le sue due cifre finali sono esadecimali, quindi lo sha si ritrova incollato a «22» da entrambe
    // le parti. Pretendere un confine non lo trova più; prendere 64 caratteri qualsiasi lo legge spostato di due.
    // In tutti e due i casi l'esito è lo stesso: una foto
    // ancora in uso finisce fra le orfane. Preso da un test sul corpo vero di una sezione extra d'aeroporto.
    private static readonly Regex EscapeJson = new(
        @"\\u[0-9a-fA-F]{4}|\\.",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Delimitata: dentro una stringa esadecimale più lunga non c'è uno sha, c'è altro.
    private static readonly Regex Sha256 = new(
        @"(?<![0-9a-fA-F])[0-9a-fA-F]{64}(?![0-9a-fA-F])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Sha citati nel testo (minuscoli, senza duplicati). Testo vuoto ⇒ nessuno.</summary>
    public static IEnumerable<string> Scan(string? text)
    {
        if (string.IsNullOrEmpty(text)) yield break;

        // Ogni escape diventa un separatore neutro, non sparisce: togliendolo, due sequenze esadecimali ai suoi lati
        // si salderebbero in una più lunga di 64, che il confine poi scarterebbe.
        var pulito = EscapeJson.Replace(text, Separatore);

        var visti = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Sha256.Matches(pulito))
        {
            var sha = m.Value.ToLowerInvariant();
            if (visti.Add(sha)) yield return sha;
        }
    }

    /// <summary>Sha citati da una sequenza di testi, uniti in un insieme solo.</summary>
    public static HashSet<string> ScanAll(IEnumerable<string?> texts)
    {
        var tutti = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in texts)
            foreach (var sha in Scan(t))
                tutti.Add(sha);
        return tutti;
    }
}
