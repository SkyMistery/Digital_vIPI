using System.Text.Json.Serialization;

namespace Vipi.Infrastructure.Ivao.Dtos;

// DTO permissivi: i campi non noti vengono ignorati. Forma verificata sul whazzup reale del 24 agosto 2026
// (467 piloti, 71 ATC): vedi docs/feature/2026-08-24-servizio-statistiche-atc.md §3.

internal sealed record WhazzupDto(
    [property: JsonPropertyName("clients")] WhazzupClientsDto? Clients,
    // Quando la sorgente ha GENERATO la fotografia (U-131): è la data della fotografia, non l'ora d'arrivo.
    [property: JsonPropertyName("updatedAt")] DateTimeOffset? UpdatedAt = null);

internal sealed record WhazzupClientsDto(
    [property: JsonPropertyName("atcs")] List<WhazzupAtcDto>? Atcs,
    [property: JsonPropertyName("pilots")] List<WhazzupPilotDto>? Pilots);

internal sealed record WhazzupAtcDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("userId")] int UserId,
    [property: JsonPropertyName("callsign")] string? Callsign,
    [property: JsonPropertyName("rating")] int Rating,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("time")] int Time,
    [property: JsonPropertyName("atcSession")] WhazzupAtcSessionDto? AtcSession,
    [property: JsonPropertyName("atis")] WhazzupAtisDto? Atis,
    // Il centro della postazione (10 ottobre 2026). Verificato sul whazzup vero di quel giorno: tutte e 79 le
    // postazioni online lo portano, nessuna a 0,0. Fino ad allora si leggeva solo per i piloti.
    [property: JsonPropertyName("lastTrack")] WhazzupAtcTrackDto? LastTrack = null);

/// <summary>
/// Dove sta una postazione ATC: del suo <c>lastTrack</c> servono solo le coordinate. ⚠️ Non è
/// <see cref="WhazzupTrackDto"/>: lì latitudine e longitudine sono obbligatorie perché un pilota senza posizione
/// si scarta; qui mancare è ammesso, e la postazione si archivia lo stesso.
/// </summary>
internal sealed record WhazzupAtcTrackDto(
    [property: JsonPropertyName("latitude")] double? Latitude,
    [property: JsonPropertyName("longitude")] double? Longitude);

internal sealed record WhazzupAtisDto(
    [property: JsonPropertyName("lines")] List<string>? Lines);

internal sealed record WhazzupAtcSessionDto(
    [property: JsonPropertyName("position")] string? Position,
    [property: JsonPropertyName("frequency")] double? Frequency);

internal sealed record WhazzupPilotDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("userId")] int UserId,
    [property: JsonPropertyName("callsign")] string? Callsign,
    // ⚠️ Può mancare: misurato 1 pilota su 468 senza tracciato. Guardia obbligatoria.
    [property: JsonPropertyName("lastTrack")] WhazzupTrackDto? LastTrack,
    [property: JsonPropertyName("flightPlan")] WhazzupFlightPlanDto? FlightPlan);

internal sealed record WhazzupTrackDto(
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    /// <summary>Piedi (verificato: Concorde a 60 119 con piano di volo F600).</summary>
    [property: JsonPropertyName("altitude")] double Altitude,
    [property: JsonPropertyName("groundSpeed")] double GroundSpeed,
    [property: JsonPropertyName("onGround")] bool OnGround,
    [property: JsonPropertyName("state")] string? State,
    /// <summary>NM dal campo di partenza: smaschera un «On Blocks» che in realtà è arrivato.</summary>
    [property: JsonPropertyName("departureDistance")] double? DepartureDistance);

internal sealed record WhazzupFlightPlanDto(
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("departureId")] string? DepartureId,
    [property: JsonPropertyName("arrivalId")] string? ArrivalId,
    [property: JsonPropertyName("aircraftId")] string? AircraftId);
