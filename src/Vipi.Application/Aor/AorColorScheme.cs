namespace Vipi.Application.Aor;

/// <summary>
/// Colori di default degli anelli AoR per <b>tipo di ente</b>, dedotti dal suffisso del callsign (l'ultimo token dopo
/// l'ultimo <c>_</c>: es. <c>LIRP_TWR</c> → <c>TWR</c>). Tutti gli enti dello stesso tipo condividono il colore
/// (es. ogni <c>_TWR</c> è rosso), salvo override manuale per singolo settore. Rimpiazza la ciclatura per indice di
/// <see cref="AorPalette"/> come default; la palette resta per usi che vogliono colori distinti a prescindere dal tipo.
/// </summary>
public static class AorColorScheme
{
    /// <summary>Colore usato quando il suffisso non è riconosciuto.</summary>
    public const string Fallback = "#3C55AC";

    /// <summary>Mappa tipo-ente (suffisso callsign) → colore di default.</summary>
    public static readonly IReadOnlyDictionary<string, string> Defaults =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CTR"] = "#0D2C99",   // blu IVAO
            ["APP"] = "#3C55AC",   // blu medio
            ["DEP"] = "#C77D3C",   // arancio
            ["TWR"] = "#B0413E",   // rosso
            ["GND"] = "#5B8C5A",   // verde
            ["DEL"] = "#8E5BA6",   // viola
            ["ATIS"] = "#7EA2D6",  // azzurro chiaro
            ["FSS"] = "#5B8C5A",   // verde
        };

    /// <summary>Colore di default per un callsign, in base al suffisso di tipo. <see cref="Fallback"/> se ignoto.</summary>
    public static string DefaultForCallsign(string? callsign) =>
        Defaults.TryGetValue(SuffixOf(callsign), out var c) ? c : Fallback;

    /// <summary>Colore risolto: override manuale (se presente e valido) altrimenti default per tipo.</summary>
    public static string Resolve(string callsign, IReadOnlyDictionary<string, string>? overrides) =>
        overrides is not null && overrides.TryGetValue(callsign, out var hex) && !string.IsNullOrWhiteSpace(hex)
            ? hex
            : DefaultForCallsign(callsign);

    /// <summary>Suffisso di tipo del callsign (ultimo token dopo <c>_</c>), maiuscolo. Vuoto se assente.</summary>
    public static string SuffixOf(string? callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return "";
        var i = callsign.LastIndexOf('_');
        var suffix = i >= 0 && i < callsign.Length - 1 ? callsign[(i + 1)..] : callsign;
        return suffix.Trim().ToUpperInvariant();
    }
}
