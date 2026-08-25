namespace Vipi.Application.Abstractions;

/// <summary>Che movimento è, per l'aeroporto che lo racconta.</summary>
public enum AirportMovementKind { Inbound, Outbound, Overflight }

/// <summary>
/// Un movimento su un aeroporto in una finestra di tempo, come lo racconta la sorgente. DTO neutro.
/// </summary>
/// <param name="PilotCallsign">Callsign del volo.</param>
/// <param name="PilotUserId">VID del pilota.</param>
/// <param name="FlightPlanId">Id del piano di volo, se c'è: identità forte della tratta.</param>
/// <param name="ConnectedUtc">
/// Quando il pilota si è collegato (<c>createdAt</c> della sorgente). Per una <b>partenza</b> è l'istante
/// più vicino al movimento che la sorgente sappia dare: il decollo non lo dichiara nessuno.
/// </param>
/// <param name="LastSeenUtc">
/// Ultimo tracciato del volo (<c>lastTrack.timestamp</c>). Per un <b>arrivo</b> è il momento in cui era su
/// quel campo.
/// </param>
public sealed record SourceAirportMovement(
    AirportMovementKind Kind,
    string PilotCallsign,
    int PilotUserId,
    long? FlightPlanId,
    string? DepIcao,
    string? ArrIcao,
    string? AircraftIcao,
    DateTimeOffset? ConnectedUtc = null,
    DateTimeOffset? LastSeenUtc = null);

/// <summary>
/// Porta neutra verso i <b>movimenti di un aeroporto</b> in una finestra di tempo.
///
/// <para>È il solo modo di ricostruire il traffico delle sessioni <b>già passate</b>: il campionamento
/// dell'area funziona da qui in avanti, mentre questo racconta anche ieri. ⚠️ Copre gli aeroporti e basta:
/// per gli ACC il passato resta senza traffico e si popola vivendo.</para>
/// </summary>
public interface IAirportTrafficSource
{
    Task<IReadOnlyList<SourceAirportMovement>> GetMovementsAsync(
        string icao, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
