namespace Vipi.Application.Abstractions;

/// <summary>Aeroporto dall'anagrafica della sorgente esterna. <see cref="AccCode"/> = centerId (ACC di competenza).
/// Senza coordinate: quelle del riferimento aeroporto arrivano da <see cref="IAirportDetailProvider"/>
/// (<c>AirportLatitude</c>/<c>AirportLongitude</c> del blocco ATCPositions), usate dal fallback shape TWR.
/// <para><paramref name="HasMilitaryPresence"/> si chiama cosi' e non <c>IsMilitary</c> perche' e' cio' che la
/// sorgente dice davvero: il suo campo <c>military</c> e' vero anche per Linate, Pisa, Ciampino, Catania, Elmas,
/// Lamezia e Rimini, che sono scali civili con sedime militare. «Solo militare» e' un giudizio che la sorgente non
/// esprime, e sta sull'aeroporto in archivio (<c>Airport.IsMilitaryOnly</c>), deciso da un amministratore.</para></summary>
public sealed record SourceAirport(string Icao, string Name, string? AccCode, string? City, int? TransitionAltitude = null,
    bool HasMilitaryPresence = false, string? Iata = null, int? ElevationFt = null, double? MagneticVariation = null);

/// <summary>
/// Porta verso l'anagrafica aeroporti della sorgente esterna, usata dall'editor struttura per scegliere
/// un aeroporto reale invece di digitarne ICAO/nome a mano. L'implementazione attiva è scelta via DataSource:Provider
/// (oggi IVAO: IvaoAirportClient con cache di processo — dati di riferimento, cambiano di rado).
/// </summary>
public interface IAirportDirectory
{
    /// <summary>Tutti gli aeroporti del paese configurato (default IT), normalizzati e ordinati per ICAO.</summary>
    Task<IReadOnlyList<SourceAirport>> GetAirportsAsync(CancellationToken ct = default);

    /// <summary>Dettaglio di un SINGOLO aeroporto per ICAO — anche fuori dal paese configurato (estero). Sola
    /// lettura dalla sorgente: NON viene inserito nell'anagrafica/catalogo locale (serve solo a riempire il nome
    /// di un aeroporto fuori DB, es. nei trasferimenti). null = non trovato o sorgente non configurata.</summary>
    Task<SourceAirport?> GetByIcaoAsync(string icao, CancellationToken ct = default);
}
