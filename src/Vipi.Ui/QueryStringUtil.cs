namespace Vipi.Ui;

/// <summary>
/// Lettura della querystring per le pagine che tengono il proprio stato nell'URL (filtri, selezione, vista).
/// <para>Scrivere l'URL lo fa già <c>NavigationManager.GetUriWithQueryParameters</c>; rileggerlo no, e ogni
/// pagina che ci prova finisce per riscrivere lo stesso ciclo su <c>&amp;</c> e <c>=</c>. Era già in due posti
/// — <c>VersioniPage</c> e l'editor trasferimenti — ed è il momento in cui diventa uno.</para>
/// </summary>
public static class QueryStringUtil
{
    /// <summary>
    /// I parametri di una querystring («?a=1&amp;b=2» o «a=1&amp;b=2»), con i nomi confrontati senza distinzione
    /// di maiuscole: un link scritto a mano non deve fallire per una lettera.
    /// </summary>
    public static Dictionary<string, string> Parse(string? query)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in (query ?? string.Empty).TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var i = part.IndexOf('=');
            if (i <= 0) continue;
            d[Uri.UnescapeDataString(part[..i])] = Uri.UnescapeDataString(part[(i + 1)..]);
        }
        return d;
    }

    /// <summary>Il valore di un parametro, o <c>null</c> se manca o è vuoto.</summary>
    public static string? Value(this IReadOnlyDictionary<string, string> q, string name) =>
        q.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

    /// <summary>Il valore come intero positivo, o <c>null</c>: gli id di questa applicazione partono da 1, e
    /// uno zero in un link è la stessa cosa di un parametro assente.</summary>
    public static int? Id(this IReadOnlyDictionary<string, string> q, string name) =>
        q.Value(name) is { } v && int.TryParse(v, out var n) && n > 0 ? n : null;

    /// <summary>Il valore come enum, o <c>null</c> se manca o non è un nome valido.</summary>
    public static T? Enum<T>(this IReadOnlyDictionary<string, string> q, string name) where T : struct, Enum =>
        q.Value(name) is { } v && System.Enum.TryParse<T>(v, ignoreCase: true, out var e) ? e : null;

    /// <summary>Il valore come interruttore: presente e diverso da «0» = acceso. Serve ai filtri che nell'URL
    /// si scrivono «&amp;norx=1» e che quando sono spenti non si scrivono affatto.</summary>
    public static bool Flag(this IReadOnlyDictionary<string, string> q, string name) =>
        q.Value(name) is { } v && v != "0";
}
