namespace Vipi.Domain.Entities;

/// <summary>Regione di informazioni di volo (es. Roma LIRR). SPEC_Modello_Dati §3.1.</summary>
public class Fir
{
    public int Id { get; set; }
    public string Code { get; set; } = default!;       // univoco, es. "LIRR"
    public string Name { get; set; } = default!;
    public string CountryPrefix { get; set; } = "LI";  // "LI" per l'Italia

    public ICollection<Position> Positions { get; set; } = new List<Position>();
    public ICollection<Sector> Sectors { get; set; } = new List<Sector>();
    public ICollection<UnificationRule> UnificationRules { get; set; } = new List<UnificationRule>();
}

/// <summary>Anagrafica piatta delle posizioni apribili. Importata dalle API IVAO. SPEC §3.2 + §7.3.</summary>
public class Position
{
    public int Id { get; set; }
    public string Callsign { get; set; } = default!;   // univoco, es. "LIRR_NE_CTR"
    public int FirId { get; set; }
    public Fir? Fir { get; set; }
    public PositionType Type { get; set; }
    public PositionKind Kind { get; set; }
    public ApproachKind? ApproachKind { get; set; }    // solo per Type=App
    public int? FacilityId { get; set; }
    public string Name { get; set; } = default!;
    public string? DefaultFrequency { get; set; }
    public string? GeometryRef { get; set; }
    public int CoverageOrder { get; set; }             // più basso = più in alto nella gerarchia
    public DateTime? ImportedAtUtc { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Frequency> Frequencies { get; set; } = new List<Frequency>();
    public ICollection<PositionSector> PositionSectors { get; set; } = new List<PositionSector>();
}

/// <summary>Volume di spazio aereo atomico. Unità minima di ownership e di tag dei contenuti. SPEC §3.3.</summary>
public class Sector
{
    public int Id { get; set; }
    public string Key { get; set; } = default!;        // univoco per FIR, es. "LIRR-NE-01"
    public string Name { get; set; } = default!;
    public int FirId { get; set; }
    public Fir? Fir { get; set; }
    public string? Description { get; set; }
    public int? GeometryId { get; set; }
    public SectorGeometry? Geometry { get; set; }
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

/// <summary>Settori posseduti di default da una posizione ("da sola"). La risoluzione runtime applica le UnificationRule. SPEC §3.5.</summary>
public class PositionSector
{
    public int PositionId { get; set; }
    public Position? Position { get; set; }
    public int SectorId { get; set; }
    public Sector? Sector { get; set; }
}

/// <summary>Relazione top-down manuale padre→figlio tra posizioni. SPEC §3.6.</summary>
public class HierarchyRelation
{
    public int Id { get; set; }
    public int ParentPositionId { get; set; }
    public Position? ParentPosition { get; set; }
    public int ChildPositionId { get; set; }
    public Position? ChildPosition { get; set; }
    public int FirId { get; set; }
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
    public string AssignmentJson { get; set; } = "{}"; // mappa sector→ownerPosition
    public bool IsActive { get; set; } = true;
    public byte[]? RowVersion { get; set; }
}

/// <summary>Frequenze associate a una posizione. SPEC §3.8.</summary>
public class Frequency
{
    public int Id { get; set; }
    public int PositionId { get; set; }
    public Position? Position { get; set; }
    public string Label { get; set; } = default!;      // es. "Roma Tower"
    public string Callsign { get; set; } = default!;
    public string FrequencyMhz { get; set; } = default!; // es. "118.450"
    public bool IsPrimary { get; set; }                // principale (★ / grassetto)
}
