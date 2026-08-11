namespace Vipi.AuroraBridge.Contracts;

/// <summary>
/// Contesto del volo selezionato in Aurora, così come il tool riesce a ricavarlo dal protocollo 3rd-party
/// (piano §5). Nessun dato personale: il callsign dell'aeromobile non serve alla risoluzione e non si manda.
/// </summary>
public sealed class TransferResolveRequest
{
    /// <summary>Callsign della postazione che sta chiedendo, da <c>#CONN</c> (o campo 12 di <c>#TRPOS</c>).</summary>
    public string OwnerCallsign { get; set; } = "";

    /// <summary>ICAO di partenza (campo 1 del Flight Plan Record).</summary>
    public string? Departure { get; set; }

    /// <summary>ICAO di destinazione (campo 2 del Flight Plan Record).</summary>
    public string? Arrival { get; set; }

    /// <summary>Livello di crociera in FL, già normalizzato dal formato ICAO (<c>F330</c> → 330).</summary>
    public int? CruiseLevel { get; set; }

    /// <summary>Rotta grezza del piano di volo (campo 14). Serve per le AEROVIE, che <see cref="RouteFixes"/> non porta.</summary>
    public string? Route { get; set; }

    /// <summary>Fix della rotta già risolti da Aurora (<c>#TRPATHL</c>), in ordine di sorvolo. Fonte preferita
    /// per il CoP: contiene ciò che Aurora ha davvero capito della rotta, non il testo del piano.</summary>
    public IList<RouteFix> RouteFixes { get; set; } = new List<RouteFix>();

    /// <summary>Quota corrente in piedi (campo 3 di <c>#TRPOS</c>).</summary>
    public int? CurrentAltitudeFt { get; set; }

    /// <summary>Rateo verticale in ft/min (campo 20 di <c>#TRPOS</c>): positivo = salita.</summary>
    public int? VerticalSpeedFpm { get; set; }

    /// <summary>Traffico al suolo (campo 14 di <c>#TRPOS</c>).</summary>
    public bool OnGround { get; set; }

    /// <summary>Ente successivo già impostato dal controllore in Aurora (campo 13 di <c>#TRPOS</c>), se c'è.</summary>
    public string? NextStation { get; set; }

    /// <summary>Configurazione piste per ICAO, da <c>#CTRLRWY</c>. Alimenta le condizioni «pista in uso».</summary>
    public IDictionary<string, RunwayConfig> RunwaysInUse { get; set; } = new Dictionary<string, RunwayConfig>();
}

/// <summary>Un fix della rotta con l'orario stimato di sorvolo. <paramref name="Eto"/> è <c>HHMM</c>, oppure
/// <c>-</c> per i punti già passati (<c>#TRPATHA</c>).</summary>
public sealed record RouteFix(string Fix, string? Eto);

/// <summary>Piste in uso di un aeroporto: <c>#CTRLRWY</c> le dà separate da «:» quando sono più d'una.</summary>
public sealed class RunwayConfig
{
    public IList<string> Departure { get; set; } = new List<string>();
    public IList<string> Arrival { get; set; } = new List<string>();
}

/// <summary>Esito della risoluzione: i candidati sono ordinati, il primo è il migliore ma la scelta resta umana.</summary>
public sealed class TransferResolveResponse
{
    /// <summary>Istante della risposta (UTC).</summary>
    public DateTimeOffset AsOf { get; set; }

    /// <summary>Freschezza della cache ATC online: il tool la mostra, così il controllore sa quanto è vecchio il dato.</summary>
    public DateTimeOffset OnlineAsOf { get; set; }

    /// <summary>Settore riconosciuto a partire da <see cref="TransferResolveRequest.OwnerCallsign"/>; null se ignoto.</summary>
    public string? ResolvedOwner { get; set; }

    /// <summary>ACC di competenza del richiedente; null se il callsign non è riconducibile a nessuna ACC.</summary>
    public string? AccCode { get; set; }

    public IList<TransferCandidate> Candidates { get; set; } = new List<TransferCandidate>();

    /// <summary>Avvisi non bloccanti (condizioni non verificabili, dati mancanti, callsign ignoto…).</summary>
    public IList<string> Warnings { get; set; } = new List<string>();
}

/// <summary>Un punto di trasferimento candidato, col livello pronto da scrivere e il perché è stato scelto.</summary>
public sealed class TransferCandidate
{
    public int FlowId { get; set; }
    public int PointId { get; set; }

    /// <summary>Arrival | Departure | Overflight | Vfr | Other.</summary>
    public string FlowKind { get; set; } = "";
    public string? AirportIcao { get; set; }

    /// <summary>Punto di trasferimento come scritto nella vIPI: un fix, «ALL», «ALL to GR», un range di aerovie…</summary>
    public string Cop { get; set; } = "";

    /// <summary>ETO del CoP se compare fra i <see cref="TransferResolveRequest.RouteFixes"/>. Ordina i CoP nel tempo.</summary>
    public string? CopEto { get; set; }

    public CandidateLevel Level { get; set; } = new();

    /// <summary>Ente nominale del punto (come da vIPI).</summary>
    public string? NextSectorCallsign { get; set; }

    /// <summary>Chi prende davvero il traffico ORA, risalendo la gerarchia; «UNICOM» se nessuno è online.</summary>
    public string? ResolvedHandler { get; set; }
    public bool HandlerOnline { get; set; }

    public CandidateCondition Condition { get; set; } = new();

    /// <summary>Stringa da passare a <c>#LBALT</c>. Null quando il livello non è scrivibile.
    /// È una stringa e non un intero: Aurora accetta testo libero (piano §11.2).</summary>
    public string? AuroraValue { get; set; }

    public bool Writable { get; set; }

    /// <summary>0..1. Ordina i candidati; non è una probabilità, è una graduatoria.</summary>
    public double Score { get; set; }

    /// <summary>Perché questo candidato sta qui, in italiano e leggibile: va mostrato accanto al livello.</summary>
    public IList<string> Reasons { get; set; } = new List<string>();
}

/// <summary>
/// Livello del punto, nelle sue componenti più il testo già formattato della vIPI.
/// <para><b>Attenzione: qui i livelli possono essere due.</b> <see cref="Value"/> e <see cref="Text"/> sono il
/// livello <b>autorizzato</b>; <see cref="TransferValue"/> è quello <b>al trasferimento</b>, che esiste solo
/// negli accordi in cui i due eventi non coincidono (tipicamente ACC→APP: «autorizzato a FL160, trasferito
/// passando FL110»). Quando <see cref="TransferValue"/> è null i due coincidono, che è il caso di tutte le
/// righe scritte prima dell'11 agosto 2026.</para>
/// <para>L'etichetta quota di Aurora (<c>AuroraValue</c>) porta il livello <b>al trasferimento</b> quando c'è:
/// è il livello che il traffico ha nel momento in cui passa di mano, cioè quello che il controllore scrive
/// nel tag.</para>
/// </summary>
public sealed class CandidateLevel
{
    public int? Value { get; set; }
    /// <summary>Fl | Feet.</summary>
    public string Unit { get; set; } = "";
    /// <summary>AtOrAbove | AtOrBelow | Exact | Special.</summary>
    public string Constraint { get; set; } = "";
    public string? Special { get; set; }
    /// <summary>Any | Even | Odd.</summary>
    public string Parity { get; set; } = "";
    /// <summary>Unspecified | Level | Descending | Climbing.</summary>
    public string VerticalState { get; set; } = "";
    /// <summary>Resa testuale ufficiale del livello AUTORIZZATO (es. «FL210- (dispari)»).</summary>
    public string Text { get; set; } = "";

    /// <summary>Dove passa il controllo: Unspecified | Point | AorBoundary | Custom. Unspecified = coincide
    /// col punto d'ingresso, e allora il livello è uno solo.</summary>
    public string HandoffKind { get; set; } = "Unspecified";
    /// <summary>Etichetta del punto/testo di trasferimento; vuota per il confine dell'AoR.</summary>
    public string? HandoffLabel { get; set; }

    /// <summary>Livello AL TRASFERIMENTO. Null = coincide con quello autorizzato.</summary>
    public int? TransferValue { get; set; }
    /// <summary>Fl | Feet.</summary>
    public string? TransferUnit { get; set; }
    /// <summary>AtOrAbove | AtOrBelow | Exact.</summary>
    public string? TransferConstraint { get; set; }
    /// <summary>Resa testuale del livello al trasferimento (es. «FL110»); vuota se non c'è.</summary>
    public string TransferText { get; set; } = "";

    /// <summary>Restrizione di velocità al trasferimento già formattata («≤250 kt»); vuota se assente.</summary>
    public string Speed { get; set; } = "";
}

/// <summary>Esito della verifica delle condizioni operative del punto (pista, area, personalizzata).</summary>
public sealed class CandidateCondition
{
    /// <summary>Etichetta combinata da mostrare; null se il punto non ha condizioni.</summary>
    public string? Display { get; set; }

    /// <summary>matched | unmatched | unknown | none — «unknown» = condizione non verificabile in automatico.</summary>
    public string Match { get; set; } = "none";
}
