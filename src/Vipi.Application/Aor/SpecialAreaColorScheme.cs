namespace Vipi.Application.Aor;

/// <summary>
/// Colori delle aree regolamentate sulla mappa, per <b>tipo di area</b> (<c>SpecialArea.Type</c>: R, D, P, TSA, TRA).
/// Gemello di <see cref="AorColorScheme"/>, che fa lo stesso lavoro per i tipi di ente: stessa ragione di esistere
/// — sulla stessa mappa possono esserci un centinaio di poligoni sovrapposti (LIRR ne ha 105), e il colore è
/// l'unica cosa che dice a colpo d'occhio di che natura sono.
/// </summary>
public static class SpecialAreaColorScheme
{
    // ⚠️ Colori CARTOGRAFICI, non di brand — stessa scelta e stesse ragioni di AorColorScheme: i poligoni si
    // riempiono al 16% e si sovrappongono, e i passi del brand a piena saturazione a quell'opacità diventano
    // indistinguibili. Sono esadecimali veri e non `var(--token)` perché Leaflet li scrive in attributi SVG,
    // che non sostituiscono `var()`.
    //
    // ⚠️ **R, D e P seguono la pratica cartografica corrente** (rosso il vietato/ristretto, giallo il pericoloso,
    // viola il proibito). **TSA e TRA no, e non possono**: non sono aree ICAO Annex 4, sono costrutti FUA, e per
    // loro un colore ufficiale NON ESISTE. Blu e verde sono la convenzione più diffusa sulle carte europee, non
    // uno standard: se la divisione ha una carta di riferimento che dice altro, si cambia QUI, in un posto solo.
    /// <summary>Colore usato quando il tipo non è fra quelli noti (o manca).</summary>
    public const string Fallback = "#5A6472";

    /// <summary>Mappa tipo area → colore.</summary>
    public static readonly IReadOnlyDictionary<string, string> Defaults =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["R"] = "#B0413E",     // rosso   — Restricted
            ["D"] = "#C9A227",     // giallo  — Danger
            ["P"] = "#7B4EA8",     // viola   — Prohibited
            ["TSA"] = "#2F6FB0",   // blu     — Temporary Segregated Area (convenzione, vedi sopra)
            ["TRA"] = "#3E8E5A",   // verde   — Temporary Reserved Area  (convenzione, vedi sopra)
        };

    /// <summary>Colore per un tipo di area. <see cref="Fallback"/> se ignoto o assente.</summary>
    public static string For(string? type) =>
        !string.IsNullOrWhiteSpace(type) && Defaults.TryGetValue(type.Trim(), out var c) ? c : Fallback;

    /// <summary>
    /// I tipi presenti in un elenco di aree, nell'ordine in cui il catalogo li elenca (<see cref="Defaults"/>),
    /// poi gli ignoti in ordine alfabetico. È l'ordine delle chip-preset per tipo: stabile fra un ACC e l'altro,
    /// così chi passa da LIRR a LIBB ritrova i tasti dove li aveva lasciati.
    /// </summary>
    public static IReadOnlyList<string> OrderTypes(IEnumerable<string?> types)
    {
        var noti = Defaults.Keys.ToList();
        var presenti = types
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return presenti
            .OrderBy(t => { var i = noti.FindIndex(n => string.Equals(n, t, StringComparison.OrdinalIgnoreCase)); return i < 0 ? int.MaxValue : i; })
            .ThenBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
