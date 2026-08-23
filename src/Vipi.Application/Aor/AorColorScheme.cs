namespace Vipi.Application.Aor;

/// <summary>
/// Colori di default degli anelli AoR per <b>tipo di ente</b>, dedotti dal suffisso del callsign (l'ultimo token dopo
/// l'ultimo <c>_</c>: es. <c>LIRP_TWR</c> → <c>TWR</c>). Tutti gli enti dello stesso tipo condividono il colore
/// (es. ogni <c>_TWR</c> è rosso), salvo override manuale per singolo settore. Ha sostituito la vecchia ciclatura
/// per indice su una palette fissa, rimossa perché senza più consumatori.
/// </summary>
public static class AorColorScheme
{
    // ⚠️ Questi colori NON seguono la palette di brand IVAO, ed è voluto (verificato il 2026-08-22 contro
    // ivaoaero/atmosphere). Sono colori CARTOGRAFICI, non chrome: gli anelli AoR si sovrappongono e si
    // riempiono al 16%, e i passi del brand a piena saturazione a quell'opacità diventano indistinguibili
    // fra loro. In più finiscono in un <input type=color> come override manuale, quindi devono restare
    // stringhe esadecimali vere: un var(--token) qui non sarebbe né selezionabile né disegnabile
    // (Leaflet li scrive in attributi SVG, che non sostituiscono var()).
    // Tre combaciano già col brand: CTR = atmos-700, APP/Fallback = ocean-600, ATIS = semantic-blue-500.
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

    /// <summary>Colore risolto: override manuale (se presente e <b>valido</b>) altrimenti default per tipo.</summary>
    public static string Resolve(string callsign, IReadOnlyDictionary<string, string>? overrides) =>
        overrides is not null && overrides.TryGetValue(callsign, out var hex) && IsHex(hex)
            ? hex
            : DefaultForCallsign(callsign);

    /// <summary>
    /// Un esadecimale CSS scritto per intero: <c>#rgb</c>, <c>#rrggbb</c> e le due forme con l'alfa.
    ///
    /// <para><b>Perché serve.</b> Fino al 23 agosto 2026 <see cref="Resolve"/> prometteva «se presente e
    /// valido» nel commento e restituiva la stringa <b>verbatim</b>. Quel valore finisce dritto in
    /// <c>style="background:{colore}33;border-color:{colore}"</c> e nell'attributo <c>fill</c> di un SVG:
    /// Blazor codifica l'attributo, quindi non si esce dalle virgolette, ma dentro il valore un <c>;</c>
    /// apre un'altra dichiarazione CSS. Oggi l'unica sorgente è un <c>&lt;input type="color"&gt;</c>, che il
    /// browser vincola — ma non lo sono un import, una migrazione o una riga corretta a mano nel DB.</para>
    ///
    /// <para>⚠️ Non accetta i nomi CSS (<c>red</c>) né <c>rgb(...)</c>: gli override nascono tutti dal
    /// selettore di colore, che emette solo <c>#rrggbb</c>. Un elenco più largo sarebbe superficie in più
    /// per nessun caso d'uso reale. Chi ne avesse bisogno lo allarga qui, in un posto solo.</para>
    /// </summary>
    private static bool IsHex(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return false;
        var s = v.Trim();
        if (s.Length is not (4 or 7 or 5 or 9) || s[0] != '#') return false;
        for (var i = 1; i < s.Length; i++)
            if (!Uri.IsHexDigit(s[i])) return false;
        return true;
    }

    /// <summary>Suffisso di tipo del callsign (ultimo token dopo <c>_</c>), maiuscolo. Vuoto se assente.</summary>
    public static string SuffixOf(string? callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return "";
        var i = callsign.LastIndexOf('_');
        var suffix = i >= 0 && i < callsign.Length - 1 ? callsign[(i + 1)..] : callsign;
        return suffix.Trim().ToUpperInvariant();
    }
}
