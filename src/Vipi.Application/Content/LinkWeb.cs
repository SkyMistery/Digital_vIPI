using System.Text.RegularExpressions;

namespace Vipi.Application.Content;

/// <summary>
/// Il link a un indirizzo web nel testo dei documenti, <c>[testo](https://…)</c> (S122, 21 ottobre 2026, committente:
/// «in qualunque posto si possa editare del testo, dei link»). Accanto al link a un allegato
/// (<see cref="AttachmentRules.LinkPattern"/>), con la stessa forma.
///
/// <para>🔴 <b>Solo <c>http</c> e <c>https</c></b>, assoluti, con un host. Fino al 21 ottobre 2026 il renderer non
/// conosceva link di nessun altro tipo apposta: aprirlo a «un indirizzo qualunque» avrebbe fatto entrare
/// <c>javascript:</c>, <c>data:</c> e gli indirizzi relativi del sito dentro un <c>href</c> che costruiamo noi. Qui
/// l'indirizzo passa due volte: la regex vuole lo schema, e <see cref="Sicuro"/> lo rilegge come <see cref="Uri"/>.</para>
///
/// <para>⚠️ L'indirizzo è un indirizzo, non un nome: nel registro «chi cita cosa» non entra, e se il sito di là
/// cambia, il link si rompe. Per un file che deve restare, la strada è la biblioteca degli allegati.</para>
/// </summary>
public static class LinkWeb
{
    /// <summary>
    /// <c>[testo](http(s)://…)</c>: il testo senza parentesi quadre né a capo, l'indirizzo senza spazi, parentesi
    /// tonde, virgolette o <c>&lt; &gt;</c>. Lo schema in maiuscolo vale lo stesso.
    /// </summary>
    public const string LinkPattern = @"\[([^\]\r\n]+)\]\(((?i:https?)://[^\s()<>""']+)\)";

    /// <summary>La regex di <see cref="LinkPattern"/>, col tetto di tempo delle altre del renderer.</summary>
    public static readonly Regex Link = new(LinkPattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>
    /// L'indirizzo come va scritto nell'<c>href</c>, o <c>null</c> se non è un indirizzo web assoluto: schema
    /// <c>http</c>/<c>https</c>, un host, niente credenziali (<c>https://utente:password@…</c> si legge come un sito
    /// e porta a un altro).
    /// </summary>
    public static string? Sicuro(string? indirizzo)
    {
        var s = (indirizzo ?? "").Trim();
        if (s.Length == 0 || s.Length > MaxIndirizzo) return null;
        if (!Uri.TryCreate(s, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
        if (string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo)) return null;
        return uri.AbsoluteUri;
    }

    /// <summary>Quanto può essere lungo un indirizzo: più di così non è un link scritto da una persona.</summary>
    public const int MaxIndirizzo = 2000;

    /// <summary>
    /// Il link come si scrive nel testo. Il testo perde le parentesi quadre e gli a capo, che chiuderebbero il link a
    /// metà; vuoto = l'indirizzo stesso. <c>null</c> se l'indirizzo non è sicuro.
    /// </summary>
    public static string? Scrivi(string? testo, string? indirizzo)
    {
        if (Sicuro(indirizzo) is not { } sicuro) return null;
        // ⚠️ Parentesi tonde e apostrofo codificati: `Uri` li lascia com'erano, e una «)» nell'indirizzo (Wikipedia
        // ne è piena) chiuderebbe il link a metà. Codificati, il sito di là legge lo stesso indirizzo.
        var url = sicuro.Replace("(", "%28").Replace(")", "%29").Replace("'", "%27");
        var t = (testo ?? "").Replace('[', '(').Replace(']', ')').Replace('\r', ' ').Replace('\n', ' ').Trim();
        return $"[{(t.Length == 0 ? url : t)}]({url})";
    }
}
