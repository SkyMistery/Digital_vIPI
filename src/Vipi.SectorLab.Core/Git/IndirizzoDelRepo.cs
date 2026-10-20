namespace Vipi.SectorLab.Core.Git;

/// <summary>
/// L'indirizzo di un repo come lo dà l'AOD (20 ottobre, primo avvio): di solito il link copiato dal browser, che può
/// essere quello di una pagina dentro il repo. Per GitHub si tiene <c>proprietario/repo</c>; il resto (un altro server,
/// una cartella, un indirizzo ssh) passa com'è.
/// </summary>
public static class IndirizzoDelRepo
{
    /// <summary>
    /// L'indirizzo pulito: senza spazi e virgolette intorno; per <c>https://github.com/…</c> solo
    /// <c>https://github.com/proprietario/repo.git</c> (<c>…/tree/main</c>, <c>…/pulls</c>, la barra in fondo spariscono).
    /// Vuoto = null.
    /// </summary>
    public static string? Pulito(string? indirizzo)
    {
        if (string.IsNullOrWhiteSpace(indirizzo))
            return null;
        string pulito = indirizzo.Trim().Trim('"', '\'', '<', '>').Trim();
        if (pulito.Length == 0)
            return null;
        if (Uri.TryCreate(pulito, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            var parti = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parti.Length >= 2)
            {
                string repo = parti[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? parti[1][..^4] : parti[1];
                return $"https://github.com/{parti[0]}/{repo}.git";
            }
        }
        return pulito;
    }

    /// <summary>
    /// Perché git non deve ricevere quell'indirizzo, o null. 🔴 Un indirizzo che comincia con «-» per git sarebbe
    /// un'opzione (<c>--upload-pack=…</c> esegue un comando): si rifiuta, e il clone mette comunque <c>--</c> prima.
    /// </summary>
    public static string? PercheNonVa(string? indirizzo)
    {
        if (string.IsNullOrWhiteSpace(indirizzo))
            return "Manca l'indirizzo del repo.";
        if (indirizzo.TrimStart().StartsWith('-'))
            return "L'indirizzo del repo non può cominciare con «-».";
        return null;
    }
}
