using Vipi.Domain.Entities;

namespace Vipi.Application.Abstractions;

/// <summary>I numeri in cima a una pagina di statistiche.</summary>
/// <param name="Shifts">Turni, non connessioni: gli spezzoni di una caduta di linea contano per uno.</param>
/// <param name="Movements">Tratte che si sono <b>mosse</b>: è il numero da mettere in evidenza.</param>
/// <param name="Presences">Tutte le tratte viste, parcheggiati compresi.</param>
public sealed record StatsTotals(int Sessions, int Shifts, long Seconds, int Movements, int Presences);

/// <summary>Una riga di raggruppamento: per postazione, per mese, per aeroporto.</summary>
public sealed record StatsByKey(string Key, int Sessions, long Seconds, int Movements);

/// <summary>Una sessione nell'elenco.</summary>
public sealed record StatsSessionRow(
    long SessionId, int UserId, string Callsign, string? Position, string? Frequency,
    DateTimeOffset StartUtc, DateTimeOffset? EndUtc, int DurationSeconds,
    int TrafficCount, int MovementCount, int TrafficMinutes, long ShiftKey);

/// <summary>Un aeroplano gestito, come si legge nel dettaglio di una sessione.</summary>
public sealed record StatsTrafficRow(
    string PilotCallsign, int LegOrdinal, string? DepIcao, string? ArrIcao, string? AircraftIcao,
    DateTimeOffset FirstSeenUtc, DateTimeOffset LastSeenUtc, int SeenMinutes,
    bool SawMovement, bool HasObservationGap, TrafficOrigin Origin);

/// <summary>Il dettaglio di una sessione: la riga e i suoi aeroplani.</summary>
public sealed record StatsSessionDetail(StatsSessionRow Session, IReadOnlyList<StatsTrafficRow> Traffic);

/// <summary>Una riga di classifica.</summary>
public sealed record ControllerRanking(int UserId, int Shifts, long Seconds, int Movements);

/// <summary>
/// Le letture delle pagine statistiche. Porta di sola lettura: nessun metodo scrive.
///
/// <para>⚠️ Tutte le letture <b>escludono le connessioni sotto il minuto</b>
/// (<see cref="Vipi.Application.Stats.StatsCounting"/>): sono il 32% delle sessioni italiane vere, entrate e
/// uscite, e contarle gonfierebbe ogni numero di un terzo. Restano in archivio — servono a ricostruire i
/// turni — ma non fanno numero, e il filtro sta qui una volta per tutte invece che in ogni pagina.</para>
/// </summary>
public interface IAtcStatsQueries
{
    /// <param name="userId">VID di cui si vogliono i numeri; <c>null</c> = tutta la divisione.</param>
    Task<StatsTotals> TotalsAsync(int? userId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);

    /// <summary>Ore e movimenti per postazione, dalla più frequentata.</summary>
    Task<IReadOnlyList<StatsByKey>> ByPositionAsync(int? userId, DateTimeOffset from, DateTimeOffset to,
        int limit = 20, CancellationToken ct = default);

    /// <summary>Ore e movimenti per mese (<c>yyyy-MM</c>), dal più vecchio.</summary>
    Task<IReadOnlyList<StatsByKey>> ByMonthAsync(int? userId, DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default);

    /// <summary>Le sessioni, dalla più recente.</summary>
    Task<IReadOnlyList<StatsSessionRow>> SessionsAsync(int? userId, DateTimeOffset from, DateTimeOffset to,
        int limit = 50, CancellationToken ct = default);

    /// <summary>Una sessione col suo traffico; <c>null</c> se non esiste.</summary>
    Task<StatsSessionDetail?> SessionAsync(long sessionId, CancellationToken ct = default);

    /// <summary>Classifica per ore, dalla più alta.</summary>
    Task<IReadOnlyList<ControllerRanking>> TopControllersAsync(DateTimeOffset from, DateTimeOffset to,
        int limit = 20, CancellationToken ct = default);
}
