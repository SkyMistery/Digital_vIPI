namespace Vipi.Domain;

/// <summary>Tipo di settore/postazione ATC (top-down DEL→GND→TWR→APP→CTR). <c>ITwr</c> = torre informativa (AFIS): stesso livello operativo della TWR ma servizio informazioni.</summary>
public enum SectorType { Del, Gnd, Twr, ITwr, App, Ctr }

/// <summary>Natura del settore: aeroportuale o di area (ACC). Determina l'API IVAO usata per le shape.</summary>
public enum SectorKind { Airport, Acc }

/// <summary>Per gli APP (<see cref="SectorType.App"/>): la doc vive nella vIPI di ACC (Remotized) o in un documento proprio (Standalone).</summary>
public enum ApproachKind { Remotized, Standalone }

/// <summary>vIPI (istruzioni di posizione) o vLOA (lettera di accordo).</summary>
public enum DocumentType { Vipi, Vloa }

/// <summary>Stato di un documento o di una sua versione.</summary>
public enum DocumentStatus { Draft, Published, Archived }

/// <summary>Come una sezione DERIVABILE si comporta nella copia pubblicata (doc 10 §3a). <c>Frozen</c> = il suo output
/// viene congelato nello snapshot della release e il pubblico lo vede immutato fino alla ripubblicazione;
/// <c>Live</c> = il pubblico vede sempre la derivazione corrente (es. SID aeroporto). Ignorato per le sezioni statiche
/// (sempre Frozen). Default <c>Frozen</c>.</summary>
public enum RenderMode { Frozen, Live }

/// <summary>Tipo di bersaglio di una release AIRAC (documento versionato per snapshot editoriale).</summary>
public enum ReleaseTargetType { Vloa, AccVipi, App, Airport }

/// <summary>Stato di una <c>DocRelease</c>: schedulata (ciclo futuro), in vigore (effettiva ora), superata da una successiva dello stesso ciclo.</summary>
public enum ReleaseStatus { Scheduled, Effective, Superseded }

/// <summary>Stato di avanzamento di un incarico editoriale (<c>EditorTask</c>).</summary>
public enum EditorTaskStatus { Todo, InProgress, InReview, Done, Blocked }

/// <summary>Priorità di un incarico editoriale.</summary>
public enum EditorTaskPriority { Low, Normal, High }

/// <summary>Lingua fissa per documento: IT per le vIPI, EN per le vLOA.</summary>
public enum Language { It, En }

/// <summary>Ruolo di una parte di vLOA: Home (italiana, editabile) o Neighbour (confinante, sola lettura).</summary>
public enum PartyRole { Home, Neighbour }

/// <summary>Livello di dettaglio in cui compare un blocco.</summary>
public enum BlockTier { Reduced, Extended }

/// <summary>Formato di un blocco di contenuto.</summary>
public enum BlockFormat { Table, Prose, Image, List, AorMap, Callout }

/// <summary>Comportamento di visibilità live (tabella di verità in SPEC_Logica_AoR §4).</summary>
public enum BlockVisibility { Operational, Handoff, Always }

/// <summary>Variante semantica di un blocco callout (brand §15.1).</summary>
public enum CalloutKind { Info, Success, Warning, Danger }

/// <summary>Semantica di una sezione di documento (ex enum piatto BlockSection).</summary>
public enum BlockSection
{
    Aor, Frequencies, OperationalSettings, Atis, Airport,
    TrafficManagement, Coordination, OperationalTechnique,
    Separations, AreasCorridors, BestPractice, Purpose, Validity, Other
}

/// <summary>Azione registrata nell'audit log.</summary>
// ⚠️ Salvato come STRINGA: aggiungere un valore è additivo e sicuro, RINOMINARNE uno lascia le righe
// vecchie non più trovabili (voce B2 dell'audit del 22 luglio 2026).
// Delete = la riga non c'è più (documento eliminato, permesso revocato). Distinto da Archive, che nel resto
// del modello significa «tolto di mezzo ma conservato»: per un atto irreversibile sarebbe una bugia gentile.
// ForceUnlock è un valore suo e non un Update perché la domanda a cui il registro deve rispondere è «chi ha
// tolto il lock a chi», e la risposta sta nei dettagli della riga.
public enum AuditAction { Create, Update, Publish, Archive, HierarchyChange, Discard, Delete, ForceUnlock }

/// <summary>Tipo di riferimento nav per la validazione semantica.</summary>
public enum NavRefType { Fix, Airway, Navaid }

/// <summary>Origine di un Coordination Point: fix reale (nav-data) o convenzionale (whitelist, es. J1).</summary>
public enum CopKind { Fix, Conventional }

/// <summary>Stato runtime di un settore (NON persistito, calcolato da AorService).</summary>
public enum SectorState { Covered, Online }

/// <summary>Tipo di flusso di traffico di un settore nei coordinamenti: arrivi/partenze a un aeroporto,
/// sorvoli, VFR o generico. Rispetto all'aeroporto del flusso (per Arrival/Departure).</summary>
public enum TransferFlowKind { Arrival, Departure, Overflight, Vfr, Other }

/// <summary>Unità del livello di un punto di trasferimento: Flight Level o piedi.</summary>
public enum LevelUnit { Fl, Feet }

/// <summary>Vincolo del livello di trasferimento: a/o sopra (↑), a/o sotto (↓), esatto, oppure speciale
/// (testo libero tipo «per aerovia»; il valore numerico è ignorato).</summary>
public enum LevelConstraint { AtOrAbove, AtOrBelow, Exact, Special }

/// <summary>Stato verticale del traffico al trasferimento (parola «stabile/in discesa/in salita» nella frase di
/// coordinamento). Dimensione INDIPENDENTE dal <see cref="LevelConstraint"/> (che è solo un vincolo di livello:
/// «a 130 o inferiore» non implica una discesa). <c>Unspecified</c> = nessuna parola di stato nella frase.</summary>
public enum TransferVerticalState { Unspecified, Level, Descending, Climbing }

/// <summary>Dove avviene il trasferimento (del controllo o delle comunicazioni), quando NON coincide con il punto
/// d'ingresso del traffico. <c>Unspecified</c> = coincide con l'ingresso, cioè il comportamento storico di un
/// accordo ACC↔ACC: al CoP il traffico entra e lì passa il controllo. Gli altri valori servono agli accordi
/// ACC→APP, dove ingresso e trasferimento sono due eventi distinti («via CHI … al confine dell'AoR»).</summary>
public enum TransferHandoffKind { Unspecified, Point, AorBoundary, Custom }

/// <summary>Vincolo di una restrizione di velocità al trasferimento. <c>Unspecified</c> = nessuna restrizione.
/// Enum dedicato e non riuso di <see cref="LevelConstraint"/>: quello porta un valore <c>Special</c> («per
/// aerovia») che su una velocità non significa niente.</summary>
public enum SpeedConstraint { Unspecified, AtOrBelow, AtOrAbove, Exact }

/// <summary>Quale dei due capi di un <see cref="Entities.CoordinationAgreement"/>. I lati non hanno un verso
/// proprio — quello lo dice <see cref="AgreementDirection"/> sulla singola clausola — e non sono «mittente» e
/// «ricevente»: in un accordo bilaterale ognuno dei due è entrambe le cose, a seconda della direzione.</summary>
public enum AgreementSide { A, B }

/// <summary>Il verso di una clausola: dal lato A al lato B, o viceversa. È la partizione con cui i documenti
/// veri aprono la tabella dei coordinamenti (EUROCONTROL Annex D.2: «Flights from [unit 1] to [unit 2]» e il
/// suo gemello opposto).</summary>
public enum AgreementDirection { AtoB, BtoA }

/// <summary>Parità dei livelli di crociera cui si applica una riga di trasferimento (regola semicircolare:
/// tipicamente est = dispari, ovest = pari). Any = indifferente (tutti i livelli). Distinto da
/// <see cref="DateParity"/> (parità del giorno del mese per le piste): stessa forma, semantica diversa.</summary>
public enum LevelParity { Any, Even, Odd }

/// <summary>Vincolo di parità del giorno del mese per una regola pista (es. alternanza Malpensa). Any = indifferente.</summary>
public enum DateParity { Any, Even, Odd }

/// <summary>Condizione della superficie pista in una regola di scelta pista. Wet = pioggia o neve nel METAR. Any = indifferente.</summary>
public enum RunwaySurface { Any, Dry, Wet }

/// <summary>Categoria di dati che la sorgente esterna può fornire (governata dalla ImportPolicy globale).</summary>
/// <summary>
/// Le categorie che la policy di import può escludere. ⚠️ <c>AtcSessions</c> (dal 24 agosto 2026) è diversa
/// dalle altre: non rende «manuale» nulla — nessuno scrive a mano una connessione ATC — ma <b>spegne la
/// raccolta</b> delle statistiche, dal vivo e dallo storico. È l'interruttore per una divisione che non
/// volesse conservare l'attività dei propri controllori.
/// </summary>
public enum ImportCategory { TransitionAltitude, Runways, Sectors, Sids, SpecialAreas, AtcSessions }

/// <summary>Stato di una coppia ACC confinante candidata a diventare una vLOA: proposta dal calcolo di
/// adiacenza, confermata dall'admin (→ vLOA generabile), o rifiutata (falso positivo, non riproporre).</summary>
public enum NeighbourCandidateStatus { Pending, Confirmed, Rejected }

/// <summary>
/// In che fase è un aeroplano, dal punto di vista di chi lo controlla. Tre gradini, perché tre sono le
/// posizioni che se li dividono.
/// </summary>
public enum FlightPhase
{
    /// <summary>Fermo al parcheggio con una partenza da fare: è il traffico della DEL.</summary>
    Parked,

    /// <summary>A terra e in movimento (rullaggio, decollo iniziato, appena atterrato): traffico della GND.</summary>
    Ground,

    /// <summary>In volo.</summary>
    Airborne,
}
