namespace Vipi.Domain.Entities;

/// <summary>Da dove è arrivata la riga di sessione: dal poller (dal vivo) o dallo storico IVAO (backfill).</summary>
public enum AtcSessionSource { Live, Backfill }

/// <summary>Come è stato attribuito un traffico: campionando l'AoR dal vivo, o dai movimenti d'aeroporto dell'API.</summary>
public enum TrafficOrigin { Aor, AirportApi }

/// <summary>
/// Una connessione ATC, come la conta IVAO. Chiave primaria = l'<b>id di sessione IVAO</b>: è lo stesso
/// numero nel whazzup e nello storico <c>/v2/tracker/sessions/{id}</c>, quindi il poller e il backfill
/// scrivono sulla stessa riga senza doversi accoppiare per (callsign, ora).
///
/// <para>Carta: <c>docs/feature/2026-08-24-servizio-statistiche-atc.md</c>.</para>
/// </summary>
public class AtcSession
{
    /// <summary>Id di sessione IVAO (chiave primaria, non generata da noi).</summary>
    public long SessionId { get; set; }

    /// <summary>VID del controllore.</summary>
    public int UserId { get; set; }

    /// <summary>Callsign usato, es. <c>LIRF_TWR</c>.</summary>
    public string Callsign { get; set; } = default!;

    /// <summary>Suffisso di posizione (TWR/GND/APP/CTR…), dal dettaglio sessione o dal callsign.</summary>
    public string? Position { get; set; }

    /// <summary>Frequenza in MHz, dal dettaglio sessione IVAO (la lista non la porta).</summary>
    public string? Frequency { get; set; }

    public DateTime StartUtc { get; set; }

    /// <summary>Fine della connessione; <c>null</c> = ancora in corso.</summary>
    public DateTime? EndUtc { get; set; }

    /// <summary>Secondi di connessione (campo <c>time</c> di IVAO): è la durata autorevole, non End−Start.</summary>
    public int DurationSeconds { get; set; }

    /// <summary>Rating ATC al momento della connessione.</summary>
    public int? Rating { get; set; }

    public AtcSessionSource Source { get; set; }

    /// <summary>
    /// Id della <b>prima</b> sessione del turno: raccoglie gli spezzoni lasciati da una caduta di linea
    /// (stesso VID, stesso callsign, ripresa entro 15 minuti — <c>AtcShiftGrouper</c>).
    ///
    /// <para>⚠️ Non è un vezzo: misurato su 1316 sessioni italiane vere di 30 giorni, <b>501 (38%)</b>
    /// riprendono entro un quarto d'ora dalla precedente. Contando le sessioni invece dei turni, i due
    /// quinti dei numeri sarebbero doppioni — e lo stesso aereo comparirebbe in ogni spezzone.</para>
    /// </summary>
    public long ShiftKey { get; set; }

    /// <summary>Quante tratte distinte sono state viste (<b>presenze</b>, parcheggiati compresi).</summary>
    public int TrafficCount { get; set; }

    /// <summary>Quante di quelle tratte si sono <b>mosse</b> almeno una volta: è il numero da mettere in evidenza.</summary>
    public int MovementCount { get; set; }

    /// <summary>Somma dei minuti in cui c'era del traffico dentro l'area (contati per giro, non per differenza).</summary>
    public int TrafficMinutes { get; set; }

    /// <summary>
    /// Quando il traffico di questa sessione è stato ricostruito <b>a posteriori</b> dai movimenti
    /// d'aeroporto della sorgente; <c>null</c> = mai provato.
    ///
    /// <para>Serve a non riprovarci all'infinito: una sessione senza traffico e senza questa data è «da
    /// riempire», una con la data e zero traffico è «riempita, non c'era nessuno» — due cose che senza una
    /// marca sarebbero indistinguibili.</para>
    /// </summary>
    public DateTime? TrafficFilledUtc { get; set; }

    /// <summary>Ultima scrittura, per il checkpoint del poller e la diagnostica.</summary>
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<AtcSessionTraffic> Traffic { get; set; } = new List<AtcSessionTraffic>();

    /// <summary>Le configurazioni di pista che si sono succedute durante la sessione.</summary>
    public ICollection<AtcSessionRunway> Runways { get; set; } = new List<AtcSessionRunway>();
}

/// <summary>
/// Una configurazione di pista durante una sessione, con l'istante da cui vale.
///
/// <para>⚠️ È una <b>sequenza</b>, non un valore: le piste cambiano durante il turno, e scrivere quella del
/// primo giro come «la pista della sessione» sarebbe falso per metà turno (nota del committente, 25 agosto
/// 2026). Una riga nuova nasce solo <b>quando la configurazione cambia</b>: un turno normale ne ha una.</para>
///
/// <para>Il testo viene dall'ATIS, che la fotografia della rete porta già per ogni ATC: nessuna chiamata in
/// più. La <b>lettera</b> dell'ATIS non si conserva — cambia a ogni aggiornamento del bollettino e non dice
/// niente sul lavoro fatto.</para>
/// </summary>
public class AtcSessionRunway
{
    public int Id { get; set; }

    public long SessionId { get; set; }
    public AtcSession? Session { get; set; }

    /// <summary>Da quando vale questa configurazione (primo giro in cui l'abbiamo vista).</summary>
    public DateTime FromUtc { get; set; }

    /// <summary>Piste d'arrivo, es. <c>16L/16R</c>.</summary>
    public string Arrival { get; set; } = "";

    /// <summary>Piste di partenza, es. <c>25</c>.</summary>
    public string Departure { get; set; } = "";
}

/// <summary>
/// Un aeroplano gestito durante una sessione ATC — <b>una riga per tratta, non per campione</b>: il poller
/// aggiorna la riga che c'è invece di aggiungerne una a ogni giro (una TWR di tre ore fa ~40 righe, non 180).
///
/// <para>La chiave è <c>(SessionId, PilotCallsign, LegOrdinal)</c>, e ognuno dei tre pezzi c'è per un difetto
/// misurato:</para>
/// <list type="bullet">
///   <item><description><b>niente id di sessione del pilota</b>: alla riconnessione IVAO gliene dà uno
///     nuovo, e un pilota che cade e rientra nello stesso volo verrebbe contato due volte;</description></item>
///   <item><description><b><see cref="LegOrdinal"/></b>: chi fa due voli senza disconnettersi
///     (LIRF→LIRN, poi LIRN→LIRF) sono due movimenti, e col solo callsign ne conteremmo uno;</description></item>
///   <item><description><b>nessun <c>Id</c> surrogato</b>: su mezzo milione di righe l'anno sarebbe una
///     colonna e un intero albero d'indice in più, per niente.</description></item>
/// </list>
/// </summary>
public class AtcSessionTraffic
{
    /// <summary>Sessione ATC di appartenenza (parte della chiave, e FK).</summary>
    public long SessionId { get; set; }
    public AtcSession? Session { get; set; }

    /// <summary>Callsign del pilota (parte della chiave).</summary>
    public string PilotCallsign { get; set; } = default!;

    /// <summary>Progressivo della tratta di quel pilota dentro questa sessione: 1, 2, 3… (parte della chiave).</summary>
    public int LegOrdinal { get; set; }

    /// <summary>VID del pilota.</summary>
    public int PilotUserId { get; set; }

    /// <summary>Id del piano di volo IVAO: è l'identità forte della tratta (<c>FlightLegResolver</c>).</summary>
    public long? FlightPlanId { get; set; }

    public string? DepIcao { get; set; }
    public string? ArrIcao { get; set; }
    public string? AircraftIcao { get; set; }

    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }

    /// <summary>
    /// Minuti in cui l'aeroplano è risultato dentro l'area, <b>contati per giro</b>.
    /// ⚠️ Non è <c>LastSeenUtc − FirstSeenUtc</c>: chi esce dal settore e rientra nella stessa tratta
    /// regalerebbe al controllore anche i minuti in cui non c'era.
    /// </summary>
    public int SeenMinutes { get; set; }

    /// <summary>
    /// Vero se l'aeroplano si è mosso almeno una volta (non è rimasto parcheggiato per tutta la sessione).
    /// Separa i <b>movimenti</b> dalle <b>presenze</b>: un ACC senza nessuno sotto gestisce anche il traffico
    /// a terra — e quindi la riga si scrive — ma il piazzale fermo non è traffico gestito.
    /// </summary>
    public bool SawMovement { get; set; }

    /// <summary>
    /// Fase del volo al <b>primo</b> avvistamento dentro l'area; <c>null</c> = mai osservata dal vivo
    /// (riga ricostruita dai movimenti d'aeroporto, che dicono CHE il volo c'è stato e nient'altro).
    ///
    /// <para>⚠️ Prima e ultima fase esistono per una domanda sola, e sono l'unico modo onesto di
    /// risponderle: <b>l'ho visto decollare? l'ho visto atterrare?</b> La fase la calcola già il recorder a
    /// ogni giro (<c>FlightPhases.Of</c>) e prima si buttava: quel che restava sulla riga era il solo
    /// <see cref="SawMovement"/>, cioè «si è mosso», che non distingue una partenza da un arrivo da un
    /// sorvolo.</para>
    /// </summary>
    public FlightPhase? FirstPhase { get; set; }

    /// <summary>Fase del volo all'<b>ultimo</b> avvistamento; <c>null</c> come <see cref="FirstPhase"/>.</summary>
    public FlightPhase? LastPhase { get; set; }

    /// <summary>
    /// Vero se almeno una volta l'abbiamo visto <b>in volo</b> dentro l'area. Serve a non spacciare per
    /// atterraggio un aeroplano che è solo rientrato al parcheggio rullando.
    /// </summary>
    public bool SawAirborne { get; set; }

    /// <summary>Quota (ft) al primo avvistamento; <c>null</c> se la riga non viene dal campionamento dal vivo.</summary>
    public int? EntryAltitudeFt { get; set; }

    /// <summary>Quota (ft) all'ultimo avvistamento.</summary>
    public int? ExitAltitudeFt { get; set; }

    /// <summary>Quota (ft) massima vista dentro l'area: per un CTR è il numero che racconta il volo.</summary>
    public int? MaxAltitudeFt { get; set; }

    /// <summary>
    /// Sessione ATC che ha preso in carico questo volo <b>subito dopo</b> di noi; <c>null</c> = nessuno
    /// (è uscito dalla rete, o non c'era nessun altro in frequenza).
    ///
    /// <para>Il dato è gratis: l'attribuzione sa già, al giro dopo, a chi va l'aeroplano. Si scrive solo se
    /// il passaggio avviene fra <b>due giri consecutivi</b> — a poller fermo per un'ora, «prima era mio e ora
    /// è suo» non è una consegna, è un buco.</para>
    ///
    /// <para>⚠️ Nessuna chiave esterna: la potatura del dettaglio (§5.1 della carta) cancellerà le righe
    /// vecchie a scaglioni, e una FK farebbe cadere la consegna insieme alla riga dell'altro. Un id che non
    /// risolve più si mostra senza collegamento.</para>
    /// </summary>
    public long? HandoffToSessionId { get; set; }

    /// <summary>Sessione ATC da cui abbiamo <b>ricevuto</b> questo volo; stesse regole di <see cref="HandoffToSessionId"/>.</summary>
    public long? HandoffFromSessionId { get; set; }

    /// <summary>
    /// Vero se fra due avvistamenti di questa tratta c'è stato un <b>buco di osservazione</b> (poller fermo,
    /// deploy, rete giù): i minuti e la traccia sono incompleti. Va mostrato accanto a <b>questo</b> volo,
    /// non in una nota generale a fondo pagina.
    /// </summary>
    public bool HasObservationGap { get; set; }

    public TrafficOrigin Origin { get; set; }
}

/// <summary>
/// Le scelte della divisione sulle statistiche: riga singola (<c>Id = 1</c>), come <c>ImportPolicy</c>.
/// </summary>
public class StatsSettings
{
    public int Id { get; set; }

    /// <summary>
    /// Classifica di divisione visibile a tutti i loggati. ⚠️ Default <b>false</b>, e qui è il valore giusto:
    /// esporre nome e ore degli altri è una scelta politica che deve essere presa, non ereditata da un
    /// default di colonna.
    /// </summary>
    public bool PublicLeaderboard { get; set; }

    public DateTime UpdatedUtc { get; set; }
    public int UpdatedByUserId { get; set; }
}
