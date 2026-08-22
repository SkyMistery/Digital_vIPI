using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <summary>
/// Compone la sezione «Minime di vettoramento» a partire dalla sorgente sectorfile. Sta qui e non nei due service
/// che la usano (vIPI ACC e APP standalone) perché la regola è una sola: <b>una carta per file <c>.mva</c></b>, e
/// il file lo sceglie l'ente. Duplicarla darebbe, prima o poi, due sezioni «minime» che non mostrano le stesse
/// carte pur descrivendo lo stesso aeroporto.
/// </summary>
public static class MinimaCharts
{
    /// <summary>
    /// ICAO dell'aeroporto di una posizione, dal prefisso del callsign (<c>LIRN_APP</c> → <c>LIRN</c>).
    /// </summary>
    /// <remarks>Il prefisso e non <c>Sector.AirportIcao</c>: misurato sui 64 settori APP del DB, i due coincidono
    /// ovunque tranne che sui 5 esteri (LGKR, LDSP, LDDU, LYTV, LATI), dove <c>AirportIcao</c> è <b>nullo</b> e il
    /// prefisso è invece corretto. Per quei cinque il file non esiste comunque nel sectorfile italiano, e una
    /// carta assente è l'esito giusto.</remarks>
    public static string? IcaoOf(string? callsign)
    {
        var cs = (callsign ?? "").Trim();
        if (cs.Length == 0) return null;
        var icao = cs.Split('_')[0].Trim().ToUpperInvariant();
        return icao.Length == 4 ? icao : null;
    }

    /// <summary>La carta enroute di un ACC (<c>ENRMVA/{acc}.mva</c>). Vuota se il file non c'è.</summary>
    public static async Task<MinimaView> ForAccAsync(
        IVectoringMinimaSource source, string accCode, string? accName, CancellationToken ct)
    {
        var code = (accCode ?? "").Trim().ToUpperInvariant();
        if (code.Length == 0) return MinimaView.Empty;

        var chart = await source.GetAccChartAsync(code, ct);
        return chart.IsEmpty
            ? MinimaView.Empty
            : new MinimaView(new[] { new MinimaChart(code, Title(code, accName), chart) });
    }

    /// <summary>
    /// Una carta per aeroporto delle posizioni indicate, nell'ordine dato e senza ripetizioni (due posizioni dello
    /// stesso scalo condividono il file). Gli aeroporti senza file semplicemente non compaiono: nel sectorfile
    /// italiano sono la maggioranza — 24 file per 49 APP — e l'assenza non è un guasto.
    /// </summary>
    public static async Task<MinimaView> ForPositionsAsync(
        IVectoringMinimaSource source, IEnumerable<string> callsigns,
        IReadOnlyDictionary<string, string>? airportNames, CancellationToken ct)
    {
        var icaos = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cs in callsigns)
            if (IcaoOf(cs) is { } icao && seen.Add(icao)) icaos.Add(icao);
        if (icaos.Count == 0) return MinimaView.Empty;

        var charts = new List<MinimaChart>();
        foreach (var icao in icaos)
        {
            var chart = await source.GetAirportChartAsync(icao, ct);
            if (chart.IsEmpty) continue;
            charts.Add(new MinimaChart(icao, Title(icao, Name(airportNames, icao)), chart));
        }
        return charts.Count == 0 ? MinimaView.Empty : new MinimaView(charts);
    }

    private static string? Name(IReadOnlyDictionary<string, string>? names, string icao) =>
        names is not null && names.TryGetValue(icao, out var n) && !string.IsNullOrWhiteSpace(n) ? n : null;

    private static string Title(string code, string? name) =>
        string.IsNullOrWhiteSpace(name) ? code : $"{code} — {name}";
}
