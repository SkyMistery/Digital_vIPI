using System.Text.RegularExpressions;

namespace Vipi.Application.Media;

/// <summary>
/// Trova i riferimenti a un allegato dentro un testo: JSON di un blocco, corpo di una sezione, payload di una
/// release. È il gemello di <see cref="MediaReferenceScanner"/> e serve alla stessa domanda, posta al
/// contrario: non «questo asset lo usa ancora qualcuno?» ma <b>«chi cita questa voce di biblioteca?»</b>.
///
/// <para><b>Un token solo, quindi una regex sola.</b> Il blocco cita
/// <c>{"ref":"allegato:loa-lirr-lfmm"}</c> e la prosa <c>[LoA Marseille](allegato:loa-lirr-lfmm)</c>: due
/// forme, un formato di riferimento. Se fossero due, questo file sarebbe due file — e il giorno che se ne
/// aggiunge una terza uno dei due resterebbe indietro senza dirlo.</para>
///
/// <para>⚠️ <b>Perché non basta l'occhio.</b> Un allegato citato solo da una release pubblicata non compare
/// in nessuna bozza: cancellarlo dalla biblioteca lascerebbe un link morto dentro un documento che nessuno
/// sta guardando, e lo scoprirebbe un lettore mesi dopo. Per questo si guardano <b>tutte</b> le sorgenti,
/// non solo quelle che si stanno scrivendo adesso.</para>
/// </summary>
public static class AttachmentReferenceScanner
{
    /// <summary>Separatore che sostituisce gli escape: non sta nell'alfabeto di uno slug, quindi delimita.</summary>
    private const string Separatore = "|";

    /// <summary>
    /// Gli escape JSON si neutralizzano <b>prima</b> di cercare. È la stessa trappola già pagata dallo scanner
    /// delle immagini: dentro un payload di release il JSON di un blocco è una stringa <i>annidata</i>, e le
    /// sue virgolette sono scritte come sequenze di escape. Toglierle salderebbe due pezzi di testo; lasciarle
    /// spezzerebbe un riferimento a metà.
    /// </summary>
    private static readonly Regex EscapeJson = new(
        @"\\u[0-9a-fA-F]{4}|\\.",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Il token, delimitato: <c>allegato:</c> seguito da uno slug.
    /// <para>⚠️ Il confine a destra c'è perché uno slug non deve <b>mangiarsi</b> quel che gli sta attaccato:
    /// senza, <c>allegato:loa-lirr</c> dentro <c>allegato:loa-lirr-bis</c> non si distinguerebbe — e la
    /// guardia della cancellazione direbbe che la voce sbagliata è citata.</para>
    /// <para>Il confine a sinistra impedisce che <c>vecchio-allegato:x</c> passi per una citazione.</para>
    /// </summary>
    private static readonly Regex Token = new(
        @"(?<![A-Za-z0-9_-])allegato:([a-z0-9]+(?:-[a-z0-9]+)*)(?![a-z0-9-])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Slug citati nel testo, senza duplicati. Testo vuoto ⇒ nessuno.</summary>
    public static IEnumerable<string> Scan(string? text)
    {
        if (string.IsNullOrEmpty(text)) yield break;

        // Come per le immagini: ogni escape diventa un separatore neutro, non sparisce.
        var pulito = EscapeJson.Replace(text, Separatore);

        var visti = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Token.Matches(pulito))
        {
            var slug = m.Groups[1].Value;
            if (visti.Add(slug)) yield return slug;
        }
    }

    /// <summary>Slug citati da una sequenza di testi, uniti in un insieme solo.</summary>
    public static HashSet<string> ScanAll(IEnumerable<string?> texts)
    {
        var tutti = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in texts)
            foreach (var slug in Scan(t))
                tutti.Add(slug);
        return tutti;
    }
}
