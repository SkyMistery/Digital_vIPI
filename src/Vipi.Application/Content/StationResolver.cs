using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <summary>Informazioni di un ACC per la navigazione documentale (derivato dalle ACC nel DB).</summary>
public sealed record AccInfo(string Code, string Name);

/// <summary>Aeroporto con l'ACC di competenza (per mappare i callsign d'aeroporto al loro ACC).</summary>
public sealed record AirportStation(string Icao, string AccCode);

/// <summary>
/// Risolve la navigazione per ACC dalle ACC esistenti nel DB (via <see cref="IStationDirectory"/>).
/// </summary>
public interface IStationResolver
{
    IReadOnlyList<AccInfo> Accs { get; }
    AccInfo? Resolve(string accCode);

    /// <summary>ACC di competenza di un callsign ATC: per testa = codice ACC (es. LIRR_NE_CTR → LIRR),
    /// oppure per testa = ICAO di un aeroporto (es. LIRP_TWR → l'ACC di LIRP). Null se non riconosciuto.</summary>
    AccInfo? ResolveByCallsign(string callsign);

    /// <summary>Forza il caricamento delle cache (ACC + mappa aeroporti→ACC) FUORI dal render. Va chiamato dal
    /// ciclo di vita async della pagina prima di usare <see cref="ResolveByCallsign"/> nel render: evita che il
    /// lazy-load colpisca il DbContext condiviso durante il render (crash "second operation" su Postgres).</summary>
    void Prewarm();
}

/// <inheritdoc cref="IStationResolver"/>
public sealed class StationResolver : IStationResolver
{
    private readonly IStationDirectory _dir;
    private IReadOnlyList<AccInfo>? _cache;
    private Dictionary<string, string>? _airportToAcc;   // ICAO → codice ACC

    public StationResolver(IStationDirectory dir) => _dir = dir;

    // Scoped: una sola lettura per richiesta, poi cache.
    public IReadOnlyList<AccInfo> Accs => _cache ??= _dir.ListAccs();

    private Dictionary<string, string> AirportToAcc => _airportToAcc ??= _dir.ListAirports()
        .GroupBy(a => a.Icao, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First().AccCode, StringComparer.OrdinalIgnoreCase);

    public AccInfo? Resolve(string accCode) =>
        Accs.FirstOrDefault(a => a.Code.Equals(accCode, StringComparison.OrdinalIgnoreCase));

    // Scalda entrambe le cache in una volta (chiamata dal ciclo di vita async, context libero e sequenziale).
    public void Prewarm() { _ = Accs; _ = AirportToAcc; }

    public AccInfo? ResolveByCallsign(string callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return null;
        var i = callsign.IndexOf('_');
        var head = i > 0 ? callsign[..i] : callsign;
        return Resolve(head)                                                   // testa = codice ACC
            ?? (AirportToAcc.TryGetValue(head, out var acc) ? Resolve(acc) : null);   // testa = ICAO aeroporto → suo ACC
    }
}
