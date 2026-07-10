namespace Vipi.Domain.Entities;

/// <summary>Area Control Center (es. Roma LIRR). Importato dalla sorgente esterna (centers); read-only nei campi di origine. SPEC_Modello_Dati §3.1.</summary>
public class Acc
{
    public int Id { get; set; }
    public string Code { get; set; } = default!;       // univoco, es. "LIRR" (centerId della sorgente)
    public string Name { get; set; } = default!;
    public string CountryPrefix { get; set; } = "LI";  // "LI" per l'Italia

    /// <summary>ACC militare (da sorgente). Solo informativo/filtro.</summary>
    public bool IsMilitary { get; set; }

    /// <summary>ACC estero confinante (materializzato dall'import confinanti), non della divisione italiana.
    /// Escluso dalle basi "domestiche" (adiacenza, navigazione) e gated admin per l'editing gerarchia.</summary>
    public bool IsForeign { get; set; }

    /// <summary>Nascosto dall'admin: resta nel DB ma non compare nella navigazione pubblica (home/landing). Default false = attivo.</summary>
    public bool IsHidden { get; set; }

    /// <summary>Istante dell'ultimo import dalla sorgente.</summary>
    public DateTime? ImportedAtUtc { get; set; }

    public ICollection<Sector> Sectors { get; set; } = new List<Sector>();
    public ICollection<Airport> Airports { get; set; } = new List<Airport>();
    public ICollection<UnificationRule> UnificationRules { get; set; } = new List<UnificationRule>();
    public ICollection<AccSector> AccSectors { get; set; } = new List<AccSector>();
}

/// <summary>
/// Settore ATC di un ACC (subcenter), importato dalla sorgente: identificato dal callsign
/// <see cref="ComposePosition"/> (chiave naturale univoca) e legato all'ACC via <see cref="CenterId"/>.
/// I campi di origine (position, middleIdentifier, frequency, regionMapPolygon) sono read-only/import;
/// i limiti di quota li imposta l'admin (la sorgente oggi non li espone → predisposti nullable).
/// </summary>
public class AccSector
{
    public int Id { get; set; }
    public string ComposePosition { get; set; } = default!;   // univoco, es. "LIBB_ES_CTR" (chiave naturale)
    public string CenterId { get; set; } = default!;          // FK → Acc.Code, es. "LIBB"
    public Acc? Acc { get; set; }

    public string? Position { get; set; }                     // es. "CTR"
    public string? MiddleIdentifier { get; set; }             // es. "ES"
    public string? AtcCallsign { get; set; }                  // nome visualizzato IVAO, es. "Roma Radar"
    public string? Frequency { get; set; }                    // MHz, da /v2/subcenters/{compose}
    public string? RegionMapPolygon { get; set; }             // poligono shape (JSON grezzo), da /v2/subcenters/{compose}

    /// <summary>Padre nella gerarchia di copertura, per callsign (= ComposePosition del padre). Cross-ACC ammesso.
    /// null = radice / da assegnare. SPEC §9.12 (Round 20).</summary>
    public string? ParentCallsign { get; set; }

    /// <summary>Limite inferiore (ft/FL). Impostato dall'admin nella webapp. Predisposto anche per la sorgente.</summary>
    public int? LowerLimit { get; set; }
    /// <summary>Limite superiore (ft/FL). Impostato dall'admin nella webapp. Predisposto anche per la sorgente.</summary>
    public int? UpperLimit { get; set; }

    /// <summary>Nascosto dall'admin (resta nel DB, fuori dalla navigazione pubblica). Default false = attivo.</summary>
    public bool IsHidden { get; set; }
    public DateTime? ImportedAtUtc { get; set; }
}

/// <summary>
/// Settore ATC di un aeroporto (DEL/GND/TWR/APP…), importato dalla sorgente: identificato dal callsign
/// <see cref="ComposePosition"/> (chiave naturale univoca, es. "LIRN_TWR" / "LIRN_US0_APP") e legato
/// all'aeroporto via <see cref="AirportIcao"/> e all'ACC di competenza via <see cref="AccCode"/>
/// (ereditato dall'aeroporto). Catalogo a parte rispetto a <see cref="Sector"/> (operativi per documenti/AoR).
/// I campi di origine sono read-only/import; i limiti di quota e IsHidden li imposta l'admin.
/// </summary>
public class AirportSector
{
    public int Id { get; set; }
    public string ComposePosition { get; set; } = default!;    // univoco, es. "LIRN_TWR" (chiave naturale)
    public string AirportIcao { get; set; } = default!;        // FK → Airport.Icao
    public Airport? Airport { get; set; }
    public string AccCode { get; set; } = default!;            // ACC di competenza, ereditato da Airport.Acc.Code
    public Acc? Acc { get; set; }

    public string? Position { get; set; }                      // suffisso: DEL/GND/TWR/APP/DEP…
    public string? MiddleIdentifier { get; set; }              // es. "US0"
    public string? AtcCallsign { get; set; }                   // nome visualizzato IVAO, es. "Pisa Approach"
    public string? Frequency { get; set; }                     // MHz, da /v2/ATCPositions/{compose}
    public string? RegionMapPolygon { get; set; }              // poligono shape (JSON grezzo), da /v2/ATCPositions/{compose}

    /// <summary>Padre nella gerarchia di copertura, per callsign (solo per le posizioni APP, che sono nodi interni
    /// dell'albero). DEL/GND/TWR non sono nodi → resta null. Cross-ACC ammesso. SPEC §9.12 (Round 20).</summary>
    public string? ParentCallsign { get; set; }

    /// <summary>Limite inferiore (ft/FL). Impostato dall'admin; default GND (0).</summary>
    public int? LowerLimit { get; set; }
    /// <summary>Limite superiore (ft/FL). Impostato dall'admin; default 19500.</summary>
    public int? UpperLimit { get; set; }

    /// <summary>Vero se i limiti (Lower/Upper) provengono dalla SORGENTE (IVAO li ha esposti all'ultimo import):
    /// in tal caso sono verità primaria e read-only nell'editor. Falso = limiti admin/default, editabili.
    /// Ricalcolato a ogni import. Default false (oggi la sorgente non espone limiti → editabili).</summary>
    public bool LimitsFromSource { get; set; }

    /// <summary>Nascosto dall'admin (resta nel DB). Default false = attivo.</summary>
    public bool IsHidden { get; set; }

    /// <summary>Frequenza principale dell'aeroporto (★). Unica per aeroporto; scelta nell'editor (default: TWR→GND→APP).</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Solo per le posizioni APP: vero se l'avvicinamento appartiene a un ACC (callsign a 3 pezzi, es. LIRN_UN0_APP →
    /// remotizzato, vive nella vIPI di ACC); falso se è l'APP proprio dell'aeroporto (callsign a 2 pezzi, es. LIRP_APP →
    /// non remotizzato, documento proprio). Editabile dall'editor aeroporto; default derivato dal callsign all'import.
    /// Guida <see cref="Sector.ApproachKind"/> nella proiezione (Round 20).</summary>
    public bool IsAccApp { get; set; }

    /// <summary>Vero se <see cref="RegionMapPolygon"/> è una shape SINTETICA generata da vIPI (cerchio di fallback
    /// per le TWR prive di poligono dalla sorgente), non una shape reale. Permette al futuro fallback GitHub di
    /// rimpiazzarla senza mai sovrascrivere una shape reale. Default false.</summary>
    public bool IsShapeSynthetic { get; set; }

    public DateTime? ImportedAtUtc { get; set; }
}

/// <summary>
/// Aeroporto appartenente a una ACC. Entità di prima classe (non più solo stringa su <see cref="Sector"/>):
/// permette creazione/rimozione/spostamento sotto una ACC. I settori d'aeroporto (TWR/GND/DEL/APP)
/// vi puntano via <see cref="Sector.AirportId"/>; <see cref="Sector.AirportIcao"/> resta come denormalizzazione.
/// </summary>
public class Airport
{
    public int Id { get; set; }
    public string Icao { get; set; } = default!;       // univoco, es. "LIRF"
    public string Name { get; set; } = default!;
    public int AccId { get; set; }
    public Acc? Acc { get; set; }

    /// <summary>Transition Altitude (ft). Sorgente strutturata: da qui si rigenera la sezione del documento.</summary>
    public int? TransitionAltitudeFt { get; set; }

    /// <summary>Coordinate del riferimento aeroporto (gradi decimali), dalla sorgente. Usate per generare la shape
    /// tonda di fallback dei settori TWR privi di poligono. null = non ancora note.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Ordine "in evidenza" (1..3) nella card Aeroporti della landing ACC; null = non in evidenza.</summary>
    public int? FeaturedRank { get; set; }

    /// <summary>Nascosto dall'admin: l'aeroporto resta nel DB ma la sua pagina e l'elenco pubblico non lo mostrano. Default false = visibile.
    /// La visibilità pubblica effettiva è inoltre negata quando l'aeroporto non ha nemmeno un settore.</summary>
    public bool IsHidden { get; set; }

    /// <summary>Padre nella gerarchia di copertura, per callsign (= ComposePosition del settore APP/CTR di fallback immediato).
    /// L'aeroporto è la FOGLIA dell'albero (DEL/GND/TWR condividono la sua vista rapida). Cross-ACC ammesso.
    /// null = aeroporto non ancora collocato nell'albero. SPEC §9.12 (Round 20, sostituisce ParentSectorId di Round 19).</summary>
    public string? ParentCallsign { get; set; }

    /// <summary>Settori che puntano a questo aeroporto (Sector.AirportId). La gerarchia si ricostruisce da qui.</summary>
    public ICollection<Sector> Sectors { get; set; } = new List<Sector>();

    /// <summary>Settori ATC catalogati dalla sorgente (DEL/GND/TWR/APP…) con mostra/nascondi + limiti admin.</summary>
    public ICollection<AirportSector> AirportSectors { get; set; } = new List<AirportSector>();

    // --- Profilo strutturato editoriale (sorgente da cui si rigenerano le sezioni del documento) ---
    public ICollection<AirportTransitionLevel> TransitionLevels { get; set; } = new List<AirportTransitionLevel>();
    public ICollection<AirportRunway> Runways { get; set; } = new List<AirportRunway>();
    public ICollection<AirportRunwayRule> RunwayRules { get; set; } = new List<AirportRunwayRule>();
    public ICollection<AirportSid> Sids { get; set; } = new List<AirportSid>();
    public ICollection<AirportFrequencyLink> FrequencyLinks { get; set; } = new List<AirportFrequencyLink>();

    /// <summary>Sezioni editoriali libere (testo) mostrate nella colonna destra del documento (sotto le SID su schermi stretti).</summary>
    public ICollection<AirportExtraSection> ExtraSections { get; set; } = new List<AirportExtraSection>();
}

/// <summary>
/// Settore = unità unica del modello (ex Position + Sector fusi). È al tempo stesso il callsign
/// apribile su IVAO e il volume di spazio aereo. Contenimento ad albero via <see cref="ParentSectorId"/>
/// (logica top-down). Alcuni settori (APP/TWR/GND/DEL) sono legati a un aeroporto. SPEC_Modello_Dati §3.2/§3.3.
/// </summary>
public class Sector
{
    public int Id { get; set; }
    public string Callsign { get; set; } = default!;   // univoco, es. "LIRR_NE_CTR" — identificatore AoR
    public string Name { get; set; } = default!;
    public int AccId { get; set; }
    public Acc? Acc { get; set; }

    public SectorType Type { get; set; }               // Del/Gnd/Twr/ITwr/App/Ctr
    public SectorKind Kind { get; set; }               // Acc | Airport
    public ApproachKind? ApproachKind { get; set; }    // solo per Type=App
    public int? AirportId { get; set; }                // aeroporto di riferimento (solo Kind=Airport)
    public Airport? Airport { get; set; }
    public string? AirportIcao { get; set; }           // denormalizzazione dell'ICAO dell'aeroporto (es. "LIRP")
    public int? FacilityId { get; set; }               // id facility IVAO
    public string? DefaultFrequency { get; set; }
    public int CoverageOrder { get; set; }             // più basso = più in alto nella gerarchia
    public int? FeaturedRank { get; set; }             // ordine "in evidenza" (1..3) nella card APP della landing ACC; null = non in evidenza
    public DateTime? ImportedAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }

    /// <summary>Vero se questo settore è una PROIEZIONE rigenerata dai cataloghi importati (AccSector/AirportSector),
    /// fonte autoritativa unica (Round 20). I settori proiettati non si editano a mano; la sync li disattiva
    /// (IsActive=false) se il callsign sparisce/viene nascosto nel catalogo. I settori seed/manuali restano IsProjected=false
    /// e la sync non li tocca mai. SPEC §9.12.</summary>
    public bool IsProjected { get; set; }

    // --- Contenimento (albero top-down): un settore può essere diviso in sotto-settori. ---
    public int? ParentSectorId { get; set; }           // null = settore radice
    public Sector? ParentSector { get; set; }
    public ICollection<Sector> Children { get; set; } = new List<Sector>();

    // --- Documento di riferimento (uno-a-molti): un documento descrive N settori, ---
    // --- ogni settore è descritto da un solo documento. Invariante: 1 settore IsPrimary per documento. ---
    public int? DocumentId { get; set; }
    public Document? Document { get; set; }
    public bool IsPrimary { get; set; }                // settore principale del proprio documento
}

/// <summary>Regola dichiarativa editabile che riassegna l'ownership dei settori in base ai callsign online. SPEC §3.7, PIANO §20.5.</summary>
public class UnificationRule
{
    public int Id { get; set; }
    public int AccId { get; set; }
    public Acc? Acc { get; set; }
    public string Name { get; set; } = default!;       // es. "Split WS2/WS5"
    public int Priority { get; set; }                  // ordine di applicazione
    public string ConditionJson { get; set; } = "{}";  // predicato su callsign online
    public string AssignmentJson { get; set; } = "{}"; // mappa settore→ownerCallsign
    public bool IsActive { get; set; } = true;
    public byte[]? RowVersion { get; set; }
}

// =========================================================================================
//  Profilo strutturato dell'aeroporto: sorgente di verità editoriale da cui si rigenerano
//  le sezioni del documento vIPI aeroporto. Editabili da AeroportoEditorPage; il merge da IVAO
//  sovrascrive solo i campi di origine IVAO e preserva i campi editoriali.
// =========================================================================================

/// <summary>Riga della tabella QNH→Transition Level. Range numerico (estremi null = aperto) per match QNH esatto.</summary>
public class AirportTransitionLevel
{
    public int Id { get; set; }
    public int AirportId { get; set; }
    public Airport? Airport { get; set; }
    public int Order { get; set; }
    public int? QnhFrom { get; set; }                  // hPa, estremo inferiore (incluso); null = aperto
    public int? QnhTo { get; set; }                    // hPa, estremo superiore (incluso); null = aperto
    public string Level { get; set; } = default!;      // es. "FL75"
}

/// <summary>Estremità di pista. Ident/Length/Bearing sono di origine IVAO; le altre colonne sono editoriali.</summary>
public class AirportRunway
{
    public int Id { get; set; }
    public int AirportId { get; set; }
    public Airport? Airport { get; set; }
    public int Order { get; set; }
    public string Ident { get; set; } = default!;      // es. "16L" (IVAO)
    public int? LengthM { get; set; }                  // lunghezza in metri (IVAO)
    public int? Bearing { get; set; }                  // rotta vera (IVAO o derivata dall'ident)
    // --- Editoriali (preservati nel merge) ---
    public string? ToraM { get; set; }
    public string? LdaM { get; set; }
    public string? AppProcedures { get; set; }
    public string? Patterns { get; set; }
    public string? Circling { get; set; }
}

/// <summary>
/// Regola di scelta pista, espressa in termini operativi: quando le piste indicate hanno vento in coda
/// ≤ <see cref="MaxTailwindKt"/> e traverso ≤ <see cref="MaxCrosswindKt"/> e la superficie corrisponde a
/// <see cref="Surface"/>, diventano preferenziali (DEP/ARR). Tailwind/crosswind sono calcolati dal vento
/// corrente: l'editor imposta solo le soglie, non la direzione. Valutate in ordine (<see cref="Order"/>):
/// la prima regola che si applica vince; se nessuna, il viewer usa il fallback headwind.
/// I campi temporali (orario/giorni/parità) sono un filtro di eleggibilità OPZIONALE (caso Malpensa).
/// </summary>
public class AirportRunwayRule
{
    public int Id { get; set; }
    public int AirportId { get; set; }
    public Airport? Airport { get; set; }
    public int Order { get; set; }                     // priorità: la prima che si applica vince
    public string? Name { get; set; }                  // etichetta opzionale, es. "Config 35" / "pista bagnata"
    public string DepRunways { get; set; } = "";       // CSV di ident preferenziali per le partenze, es. "16R,16L"
    public string ArrRunways { get; set; } = "";       // CSV di ident preferenziali per gli arrivi

    /// <summary>Vento in coda massimo tollerato (kt) sulle piste della regola perché si applichi. Default 5.</summary>
    public int MaxTailwindKt { get; set; } = 5;
    /// <summary>Vento al traverso massimo tollerato (kt); null = nessun vincolo di traverso.</summary>
    public int? MaxCrosswindKt { get; set; }
    /// <summary>Condizione della superficie richiesta: Any/Dry/Wet (Wet = pioggia o neve nel METAR).</summary>
    public RunwaySurface Surface { get; set; } = RunwaySurface.Any;

    public string? Note { get; set; }
    // --- Condizioni temporali AVANZATE (tutte opzionali; null/Any = indifferente). Orario in ora LOCALE (LT, come in AIP). ---
    public int? TimeFromLocalMin { get; set; }         // minuti da mezzanotte locale (0..1439), incluso; con TimeToLocalMin = finestra (gestisce wrap notturno)
    public int? TimeToLocalMin { get; set; }           // minuti da mezzanotte locale (0..1439), incluso
    public int? DaysOfWeekMask { get; set; }           // bitmask: bit0=Lun … bit6=Dom; null/0 = tutti i giorni
    public DateParity DateParity { get; set; } = DateParity.Any;  // parità giorno del mese (alternanza tipo Malpensa)

    // --- Finestra di validità stagionale RICORRENTE (ogni anno), opzionale. Codifica MMDD (mese*100+giorno),
    // estremi inclusi; gestisce il wrap di fine anno (es. 1101→0228). Entrambi null = nessun vincolo di data. ---
    public int? DateFromMonthDay { get; set; }         // es. 101 = 1° gennaio
    public int? DateToMonthDay { get; set; }           // es. 331 = 31 marzo
}

/// <summary>Riga SID (editabile a mano oppure importata dal sectorfile Aurora, con merge che preserva le manuali).</summary>
public class AirportSid
{
    public int Id { get; set; }
    public int AirportId { get; set; }
    public Airport? Airport { get; set; }
    public int Order { get; set; }
    public string? Runway { get; set; }                // pista di validità (filtro nel viewer)
    public string Fix { get; set; } = default!;
    public string Name { get; set; } = default!;       // nome SID
    public string? Transition { get; set; }
    public string? InitialClimb { get; set; }
    public string? Type { get; set; }
    public string? Cat { get; set; }
    public string? Wtc { get; set; }
    public string? Condition { get; set; }

    // --- Import da sectorfile ---
    /// <summary>Vera se la riga proviene dall'import sectorfile (false = inserita a mano dallo staff).</summary>
    public bool IsImported { get; set; }
    /// <summary>Ordine di preferenza tra le SID dello stesso punto (fix). Impostato a mano, persiste tra import per StableKey.</summary>
    public int? Priority { get; set; }
    /// <summary>Identità stabile della SID (ICAO|fix|lettera|transition|pista), esclusa la cifra della revisione. Per ri-applicare priorità/pubblicazione tra import.</summary>
    public string? StableKey { get; set; }
    /// <summary>Ciclo AIRAC in cui la riga è stata prelevata dalla sorgente (YYNN). Governa la pubblicazione differita.</summary>
    public string? SourceAiracCycle { get; set; }
    /// <summary>Forzatura manuale della pubblicazione di una riga importata: scavalca il differimento al ciclo successivo.</summary>
    public bool ForcePublished { get; set; }
    /// <summary>Fix non risolto automaticamente dal parser (prefisso troncato irregolare): da completare a mano.</summary>
    public bool NeedsFixReview { get; set; }
}

/// <summary>Alias autoritativo per completare i prefissi SID troncati irregolari (es. "SIV" → "SOSIV"). Globale.</summary>
public class SidFixAlias
{
    public int Id { get; set; }
    public string Prefix { get; set; } = default!;     // prefisso troncato come appare nel codice SID
    public string FixName { get; set; } = default!;    // fix reale completo
}

/// <summary>
/// Sezione editoriale libera dell'aeroporto: titolo + corpo testuale. Sorgente strutturata, indipendente dalle
/// sezioni standard; nel viewer compare nella colonna libera di destra (desktop) o sotto le SID (schermi stretti).
/// </summary>
public class AirportExtraSection
{
    public int Id { get; set; }
    public int AirportId { get; set; }
    public Airport? Airport { get; set; }
    public int Order { get; set; }
    public string Title { get; set; } = default!;      // titolo della sezione
    public string? Body { get; set; }                  // corpo testo libero (a capo preservati)
}

/// <summary>Frequenza "linkata" a un altro settore (riferimento vivo): si risolve da Sector.DefaultFrequency.</summary>
public class AirportFrequencyLink
{
    public int Id { get; set; }
    public int AirportId { get; set; }
    public Airport? Airport { get; set; }
    public int Order { get; set; }
    public int SourceSectorId { get; set; }            // FK → Sector (la sorgente; cambi riflessi al rebuild/render)
    public Sector? SourceSector { get; set; }
    public string? LabelOverride { get; set; }         // etichetta custom (altrimenti usa il callsign del settore)
}

// =========================================================================================
//  Profilo dell'APP non remotizzato (standalone): vIPI propria del settore di avvicinamento.
//  1:1 col Sector APP (Type=App, ApproachKind=Standalone). Solo le parti EDITORIALI sono qui;
//  frequenze (sottoalbero), coordinamenti (trasferimenti) e poligono AoR si derivano LIVE.
// =========================================================================================

// APP standalone: lo storage editoriale è migrato su Document + DocumentProfile (doc refactor 08e); le entità
// profile-based AppProfile/AppFrequencyLink sono state rimosse (08e-app cleanup).

// =========================================================================================
//  Profilo della vIPI di ACC: documento a BLOCCHI (Aerovia/CTR + gruppi APP). 1:1 con l'Acc.
//  Tutta la struttura (blocchi, sezioni, ordine, hidden, custom, configurazioni, editoriale)
//  è serializzata in BlocksJson; le sezioni derivate (AoR/Frequenze/Coordinamenti) si calcolano
//  LIVE dai cataloghi/trasferimenti. Mirror, in chiave ACC multi-settore, di AppProfile.
// =========================================================================================

/// <summary>
/// Profilo editoriale della vIPI ACC, ancorato 1:1 all'<see cref="Acc"/> via <see cref="AccId"/>.
/// Il documento è una lista ordinata di blocchi (un blocco Aerovia + N blocchi gruppo-APP), ciascuno
/// con le sue sezioni piatte (ordine/hidden/custom), configurazioni (settori aperti) e dati editoriali.
/// Serializzato tutto in <see cref="BlocksJson"/>; le parti derivate non si salvano.
/// </summary>
public class AccProfile
{
    public int Id { get; set; }

    /// <summary>FK → Acc. Con <see cref="RootCallsign"/> forma l'identità del profilo (unique composito).</summary>
    public int AccId { get; set; }
    public Acc? Acc { get; set; }

    /// <summary>Callsign del CTR radice dell'albero a cui appartiene questa vIPI (una vIPI per albero).
    /// Es. "LIRR_NE_CTR". Backfill al radice primario per i profili legacy.</summary>
    public string? RootCallsign { get; set; }

    /// <summary>Lista dei blocchi (Aerovia + gruppi APP) con tutto il loro stato, serializzata JSON.</summary>
    public string BlocksJson { get; set; } = "[]";

    /// <summary>vIPI ACC nascosta dal pubblico (reversibile): il viewer non la serve, l'editor resta accessibile.</summary>
    public bool IsHidden { get; set; }
}

/// <summary>
/// Area speciale/regolamentata importata dalla sorgente (IVAO), legata a un ACC via <see cref="CenterId"/>.
/// <see cref="IvaoId"/> è la chiave naturale (reference per gli update). La shape (<see cref="RegionMapPolygon"/>)
/// è il JSON grezzo dal dettaglio: proiettabile con AorPolygonProjector.
/// </summary>
public class SpecialArea
{
    public int Id { get; set; }
    public string IvaoId { get; set; } = default!;           // univoco, id IVAO (reference update)
    public string CenterId { get; set; } = default!;         // FK → Acc.Code
    public Acc? Acc { get; set; }

    public string? Type { get; set; }                        // es. "R"
    public string Name { get; set; } = default!;             // es. "LI R14A - S.Severa"
    public string? Description { get; set; }
    public string? ActivationDetails { get; set; }           // es. "Permanently active"
    public int? MinimumAlt { get; set; }
    public int? MaximumAlt { get; set; }
    public bool Range { get; set; }

    public string? RegionMapPolygon { get; set; }            // shape (JSON grezzo), da /v2/specialAreas/{id}
    public DateTime? ImportedAtUtc { get; set; }
}
