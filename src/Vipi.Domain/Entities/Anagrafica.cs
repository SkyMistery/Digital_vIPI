namespace Vipi.Domain.Entities;

/// <summary>Regione di informazioni di volo (es. Roma LIRR). SPEC_Modello_Dati §3.1.</summary>
public class Fir
{
    public int Id { get; set; }
    public string Code { get; set; } = default!;       // univoco, es. "LIRR"
    public string Name { get; set; } = default!;
    public string CountryPrefix { get; set; } = "LI";  // "LI" per l'Italia

    public ICollection<Sector> Sectors { get; set; } = new List<Sector>();
    public ICollection<Airport> Airports { get; set; } = new List<Airport>();
    public ICollection<UnificationRule> UnificationRules { get; set; } = new List<UnificationRule>();
}

/// <summary>
/// Aeroporto appartenente a una FIR. Entità di prima classe (non più solo stringa su <see cref="Sector"/>):
/// permette creazione/rimozione/spostamento sotto una FIR. I settori d'aeroporto (TWR/GND/DEL/APP)
/// vi puntano via <see cref="Sector.AirportId"/>; <see cref="Sector.AirportIcao"/> resta come denormalizzazione.
/// </summary>
public class Airport
{
    public int Id { get; set; }
    public string Icao { get; set; } = default!;       // univoco, es. "LIRF"
    public string Name { get; set; } = default!;
    public int FirId { get; set; }
    public Fir? Fir { get; set; }

    /// <summary>Transition Altitude (ft). Sorgente strutturata: da qui si rigenera la sezione del documento.</summary>
    public int? TransitionAltitudeFt { get; set; }

    /// <summary>Frequenza ATIS (da IVAO): non è un settore controllabile, ma compare nella tabella Frequenze.</summary>
    public string? AtisFrequency { get; set; }

    /// <summary>Ordine "in evidenza" (1..3) nella card Aeroporti della landing ACC; null = non in evidenza.</summary>
    public int? FeaturedRank { get; set; }

    /// <summary>Settori che puntano a questo aeroporto (Sector.AirportId). La gerarchia si ricostruisce da qui.</summary>
    public ICollection<Sector> Sectors { get; set; } = new List<Sector>();

    // --- Profilo strutturato editoriale (sorgente da cui si rigenerano le sezioni del documento) ---
    public ICollection<AirportTransitionLevel> TransitionLevels { get; set; } = new List<AirportTransitionLevel>();
    public ICollection<AirportRunway> Runways { get; set; } = new List<AirportRunway>();
    public ICollection<AirportRunwayRule> RunwayRules { get; set; } = new List<AirportRunwayRule>();
    public ICollection<AirportSid> Sids { get; set; } = new List<AirportSid>();
    public ICollection<AirportFrequencyLink> FrequencyLinks { get; set; } = new List<AirportFrequencyLink>();
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
    public int FirId { get; set; }
    public Fir? Fir { get; set; }

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

    // --- Contenimento (albero top-down): un settore può essere diviso in sotto-settori. ---
    public int? ParentSectorId { get; set; }           // null = settore radice
    public Sector? ParentSector { get; set; }
    public ICollection<Sector> Children { get; set; } = new List<Sector>();

    // --- Documento di riferimento (uno-a-molti): un documento descrive N settori, ---
    // --- ogni settore è descritto da un solo documento. Invariante: 1 settore IsPrimary per documento. ---
    public int? DocumentId { get; set; }
    public Document? Document { get; set; }
    public bool IsPrimary { get; set; }                // settore principale del proprio documento

    // --- Geometria per la mappa AoR ---
    public int? GeometryId { get; set; }
    public SectorGeometry? Geometry { get; set; }

    public ICollection<Frequency> Frequencies { get; set; } = new List<Frequency>();
}

/// <summary>Shape geografica per la vista mappa AoR. Separata dal Sector per non appesantire le query. SPEC §3.4.</summary>
public class SectorGeometry
{
    public int Id { get; set; }
    public GeometryFormat Format { get; set; }
    public string Data { get; set; } = default!;       // poligono/i (GeoJSON o WKT)
    public string? SourceCallsign { get; set; }
    public DateTime? ImportedAtUtc { get; set; }
}

/// <summary>Regola dichiarativa editabile che riassegna l'ownership dei settori in base ai callsign online. SPEC §3.7, PIANO §20.5.</summary>
public class UnificationRule
{
    public int Id { get; set; }
    public int FirId { get; set; }
    public Fir? Fir { get; set; }
    public string Name { get; set; } = default!;       // es. "Split WS2/WS5"
    public int Priority { get; set; }                  // ordine di applicazione
    public string ConditionJson { get; set; } = "{}";  // predicato su callsign online
    public string AssignmentJson { get; set; } = "{}"; // mappa settore→ownerCallsign
    public bool IsActive { get; set; } = true;
    public byte[]? RowVersion { get; set; }
}

/// <summary>Frequenze associate a un settore. SPEC §3.8.</summary>
public class Frequency
{
    public int Id { get; set; }
    public int SectorId { get; set; }
    public Sector? Sector { get; set; }
    public string Label { get; set; } = default!;      // es. "Roma Tower"
    public string Callsign { get; set; } = default!;
    public string FrequencyMhz { get; set; } = default!; // es. "118.450"
    public bool IsPrimary { get; set; }                // principale (★ / grassetto)
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

/// <summary>Regola di scelta pista: condizione (vento + pioggia/neve) → piste DEP/ARR. Nel viewer prevale sul calcolo headwind.</summary>
public class AirportRunwayRule
{
    public int Id { get; set; }
    public int AirportId { get; set; }
    public Airport? Airport { get; set; }
    public int Order { get; set; }                     // priorità: la prima che matcha vince
    public int? WindDirFrom { get; set; }              // ° (incluso); con WindDirTo definisce un arco (gestisce wrap)
    public int? WindDirTo { get; set; }                // ° (incluso)
    public int? WindSpeedMin { get; set; }             // kt (incluso)
    public int? WindSpeedMax { get; set; }             // kt (incluso)
    public bool? Rain { get; set; }                    // null = indifferente
    public bool? Snow { get; set; }                    // null = indifferente
    public string DepRunways { get; set; } = "";       // CSV di ident, es. "16R,16L"
    public string ArrRunways { get; set; } = "";       // CSV di ident
    public string? Note { get; set; }
    // --- Condizioni temporali (tutte opzionali; null/Any = indifferente). Orario in UTC/Zulu. ---
    public int? TimeFromUtcMin { get; set; }           // minuti da mezzanotte UTC (0..1439), incluso; con TimeToUtcMin = finestra (gestisce wrap notturno)
    public int? TimeToUtcMin { get; set; }             // minuti da mezzanotte UTC (0..1439), incluso
    public int? DaysOfWeekMask { get; set; }           // bitmask: bit0=Lun … bit6=Dom; null/0 = tutti i giorni
    public DateParity DateParity { get; set; } = DateParity.Any;  // parità giorno del mese (alternanza tipo Malpensa)
}

/// <summary>Riga SID (editabile a mano; import sectorfile = follow-up con merge).</summary>
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
}

/// <summary>Frequenza "linkata" a un altro settore (riferimento vivo): si risolve dalla Frequency sorgente.</summary>
public class AirportFrequencyLink
{
    public int Id { get; set; }
    public int AirportId { get; set; }
    public Airport? Airport { get; set; }
    public int Order { get; set; }
    public int SourceFrequencyId { get; set; }         // FK → Frequency (la sorgente; cambi riflessi al rebuild/render)
    public Frequency? SourceFrequency { get; set; }
    public string? LabelOverride { get; set; }         // etichetta custom (altrimenti usa quella della sorgente)
}
