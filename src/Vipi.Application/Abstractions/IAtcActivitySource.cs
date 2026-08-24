namespace Vipi.Application.Abstractions;

/// <summary>
/// Una connessione ATC vista dalla sorgente, in un istante. DTO <b>neutro</b>: nessun nome della rete
/// esterna, come vuole il disaccoppiamento dalla sorgente (doc sorgenti, 27 giugno 2026).
/// </summary>
/// <param name="SessionId">Id di sessione della sorgente: è anche la chiave con cui lo storico ritrova
/// la stessa connessione, quindi non va reinventato.</param>
/// <param name="UserId">VID del controllore.</param>
/// <param name="Callsign">Callsign in frequenza, es. <c>LIRF_TWR</c>.</param>
/// <param name="Position">Suffisso di posizione (TWR/GND/APP/CTR…), se la sorgente lo espone.</param>
/// <param name="Frequency">Frequenza in MHz come testo, se esposta.</param>
/// <param name="Rating">Rating ATC.</param>
/// <param name="StartUtc">Inizio della connessione.</param>
/// <param name="ConnectedSeconds">Secondi di connessione dichiarati dalla sorgente: è la durata
/// autorevole, più affidabile di una differenza calcolata da noi fra due istanti.</param>
/// <param name="AtisLines">Righe dell'ATIS trasmesso, se ce n'è uno: da lì si leggono le piste in uso
/// (misurato: la fotografia le porta per ogni ATC, e 48 su 71 nominano una pista).</param>
public sealed record SourceAtcConnection(
    long SessionId,
    int UserId,
    string Callsign,
    string? Position,
    string? Frequency,
    int Rating,
    DateTimeOffset StartUtc,
    int ConnectedSeconds,
    IReadOnlyList<string>? AtisLines = null);

/// <summary>
/// La posizione di un aeroplano in un istante, più quel che serve a capire <b>chi</b> è e <b>cosa</b> sta
/// facendo. DTO neutro.
/// </summary>
/// <param name="SessionId">Id di sessione del pilota. ⚠️ Non usarlo come identità del volo: alla
/// riconnessione cambia, e un volo caduto e ripreso verrebbe contato due volte.</param>
/// <param name="UserId">VID del pilota.</param>
/// <param name="Callsign">Callsign del volo.</param>
/// <param name="Latitude">Gradi.</param>
/// <param name="Longitude">Gradi.</param>
/// <param name="AltitudeFt">Quota in PIEDI (verificata sul dato reale: un Concorde a 60 119 ft con
/// piano di volo <c>F600</c>).</param>
/// <param name="GroundSpeed">Nodi.</param>
/// <param name="OnGround">Vero se il simulatore lo dichiara al suolo.</param>
/// <param name="State">Stato dichiarato dalla sorgente (<c>Boarding</c>, <c>Departing</c>, <c>En Route</c>,
/// <c>On Blocks</c>, <c>Landed</c>…): serve a distinguere una partenza ferma da un arrivo ai blocchi.</param>
/// <param name="DepartureDistanceNm">Distanza dal campo di partenza, se nota: è ciò che smaschera un
/// <c>On Blocks</c> a destinazione (misurato: 453 NM dalla partenza = è arrivato, non deve partire).</param>
/// <param name="FlightPlanId">Id del piano di volo: identità forte della tratta.</param>
/// <param name="DepIcao">Aeroporto di partenza dal piano di volo.</param>
/// <param name="ArrIcao">Aeroporto di arrivo dal piano di volo.</param>
/// <param name="AircraftIcao">Tipo di aeromobile dal piano di volo.</param>
public sealed record SourcePilotFix(
    long SessionId,
    int UserId,
    string Callsign,
    double Latitude,
    double Longitude,
    double AltitudeFt,
    double GroundSpeed,
    bool OnGround,
    string? State,
    double? DepartureDistanceNm,
    long? FlightPlanId,
    string? DepIcao,
    string? ArrIcao,
    string? AircraftIcao);

/// <summary>
/// Fotografia della rete in un istante: chi controlla e chi vola. Immutabile.
///
/// <para>⚠️ Gli ATC sono <b>già filtrati alla divisione</b> (prefissi ICAO configurati); i piloti <b>no</b>,
/// e non è una svista: un volo attribuito a un settore italiano può trovarsi ovunque dentro quel volume, e
/// filtrare per callsign non avrebbe senso. Il filtro dei piloti è geometrico e lo fa l'attribuzione.</para>
/// </summary>
public sealed class NetworkSnapshot
{
    public required IReadOnlyList<SourceAtcConnection> Atc { get; init; }
    public required IReadOnlyList<SourcePilotFix> Pilots { get; init; }
    public required DateTimeOffset AsOf { get; init; }

    public static readonly NetworkSnapshot Empty = new()
    {
        Atc = Array.Empty<SourceAtcConnection>(),
        Pilots = Array.Empty<SourcePilotFix>(),
        AsOf = DateTimeOffset.MinValue,
    };
}

/// <summary>
/// Porta neutra verso l'attività della rete in tempo reale: chi è in frequenza e dove sono gli aerei.
///
/// <para>Sostituisce, per il poller, la sola lista degli ATC online: le statistiche hanno bisogno anche dei
/// piloti, e la sorgente li dà <b>nella stessa chiamata</b> — quindi il costo non cambia (misurato:
/// 119 KB compressi, 0,2 s, una chiamata al minuto, nessun token).</para>
///
/// <para>L'implementazione concreta vive in Infrastructure e si sceglie col seam <c>DataSource:Provider</c>:
/// qui dentro non deve comparire nessun nome di rete.</para>
/// </summary>
public interface IAtcActivitySource
{
    /// <summary>Fotografia corrente della rete. Lancia se la sorgente non risponde: il chiamante decide.</summary>
    Task<NetworkSnapshot> GetSnapshotAsync(CancellationToken ct = default);
}
