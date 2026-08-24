using Vipi.Application.Stats;
using Vipi.Domain;
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
/// <param name="HandoffTo">Callsign di chi l'ha preso dopo di noi; <c>null</c> = nessuno, o sessione potata.</param>
/// <param name="HandoffFrom">Callsign di chi ce l'ha passato.</param>
public sealed record StatsTrafficRow(
    string PilotCallsign, int LegOrdinal, string? DepIcao, string? ArrIcao, string? AircraftIcao,
    DateTimeOffset FirstSeenUtc, DateTimeOffset LastSeenUtc, int SeenMinutes,
    bool SawMovement, bool HasObservationGap, TrafficOrigin Origin,
    FlightPhase? FirstPhase = null, FlightPhase? LastPhase = null, bool SawAirborne = false,
    int? EntryAltitudeFt = null, int? ExitAltitudeFt = null, int? MaxAltitudeFt = null,
    string? HandoffTo = null, string? HandoffFrom = null);

/// <summary>
/// Dove sta una persona nella classifica di divisione, senza mostrarle la classifica.
/// </summary>
/// <param name="Position">1 = prima. Zero se in questo periodo non ha turni.</param>
/// <param name="Total">Quanti controllori hanno almeno un turno nel periodo.</param>
public sealed record StatsRank(int Position, int Total)
{
    /// <summary>Percentuale di controllori che stanno sotto o insieme a te; 0 se non sei in classifica.</summary>
    public int TopPercent => Position <= 0 || Total <= 0 ? 0 : (int)Math.Ceiling(Position * 100.0 / Total);
}

/// <summary>Una configurazione di pista, con l'istante da cui vale.</summary>
public sealed record StatsRunwayRow(DateTimeOffset FromUtc, string Arrival, string Departure);

/// <summary>Il dettaglio di una sessione: la riga, i suoi aeroplani e le piste che si sono succedute.</summary>
public sealed record StatsSessionDetail(
    StatsSessionRow Session,
    IReadOnlyList<StatsTrafficRow> Traffic,
    IReadOnlyList<StatsRunwayRow> Runways);

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

    /// <summary>
    /// La griglia ora × giorno. <paramref name="userId"/> null = copertura della <b>divisione</b> (quando
    /// c'era qualcuno), valorizzato = quando controlla quella persona.
    ///
    /// <para>⚠️ Gli intervalli si <b>uniscono</b> prima di contare (<see cref="Vipi.Application.Stats.CoverageGrid"/>):
    /// tre controllori insieme fanno un'ora coperta, non tre.</para>
    /// </summary>
    Task<IReadOnlyList<CoverageCell>> CoverageAsync(int? userId, DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default);

    /// <summary>Gli aeroporti del traffico gestito (partenze e arrivi insieme), dal più frequente.</summary>
    Task<IReadOnlyList<StatsByKey>> TopAirportsAsync(int? userId, DateTimeOffset from, DateTimeOffset to,
        int limit = 15, CancellationToken ct = default);

    /// <summary>I tipi di aeromobile gestiti, dal più frequente.</summary>
    Task<IReadOnlyList<StatsByKey>> TopAircraftAsync(int? userId, DateTimeOffset from, DateTimeOffset to,
        int limit = 15, CancellationToken ct = default);

    /// <summary>Settimane consecutive con almeno un turno (<see cref="Vipi.Application.Stats.ControllerStreak"/>).</summary>
    Task<StatsStreak> StreakAsync(int userId, DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default);

    /// <summary>
    /// Posizione in classifica di una persona. ⚠️ Si legge <b>anche a classifica spenta</b>: dire «sei nel
    /// primo 12%» non svela le ore di nessun altro, ed è l'unico confronto che la pagina personale può
    /// mostrare finché lo staff non ha deciso di aprire l'elenco.
    /// </summary>
    Task<StatsRank> RankAsync(int userId, DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default);

    /// <summary>
    /// La prima connessione in archivio (di tutti, o di una persona); <c>null</c> se non ce n'è nessuna.
    /// Serve a non promettere periodi che l'archivio non ha.
    /// </summary>
    Task<DateTimeOffset?> ArchiveStartAsync(int? userId, CancellationToken ct = default);
}
