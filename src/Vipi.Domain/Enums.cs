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
public enum AuditAction { Create, Update, Publish, Archive, HierarchyChange }

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

/// <summary>Parità dei livelli di crociera cui si applica una riga di trasferimento (regola semicircolare:
/// tipicamente est = dispari, ovest = pari). Any = indifferente (tutti i livelli). Distinto da
/// <see cref="DateParity"/> (parità del giorno del mese per le piste): stessa forma, semantica diversa.</summary>
public enum LevelParity { Any, Even, Odd }

/// <summary>Vincolo di parità del giorno del mese per una regola pista (es. alternanza Malpensa). Any = indifferente.</summary>
public enum DateParity { Any, Even, Odd }

/// <summary>Condizione della superficie pista in una regola di scelta pista. Wet = pioggia o neve nel METAR. Any = indifferente.</summary>
public enum RunwaySurface { Any, Dry, Wet }

/// <summary>Categoria di dati che la sorgente esterna può fornire (governata dalla ImportPolicy globale).</summary>
public enum ImportCategory { TransitionAltitude, Runways, Sectors, Sids }

/// <summary>Stato di una coppia ACC confinante candidata a diventare una vLOA: proposta dal calcolo di
/// adiacenza, confermata dall'admin (→ vLOA generabile), o rifiutata (falso positivo, non riproporre).</summary>
public enum NeighbourCandidateStatus { Pending, Confirmed, Rejected }
