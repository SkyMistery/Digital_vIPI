using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Riga ACC per l'elenco struttura.</summary>
public sealed record AccRow(int Id, string Code, string Name, string CountryPrefix, int Sectors);

/// <summary>Aeroporto di una ACC per l'editor struttura.
/// <paramref name="IsHidden"/> = nascosto dall'admin; la visibilità pubblica effettiva richiede anche almeno un settore (vedi <see cref="IsPublic"/>).</summary>
public sealed record AirportRow(int Id, string Icao, string Name, int Sectors, int? FeaturedRank = null, bool IsHidden = false,
    string? ParentCallsign = null)
{
    /// <summary>Vero se l'aeroporto è visibile al pubblico: non nascosto dall'admin e con almeno un settore.</summary>
    public bool IsPublic => !IsHidden && Sectors > 0;
}

/// <summary>
/// Aeroporto (cross-ACC) per la pagina di gestione aeroporti: ACC assegnata + n. settori che vi puntano.
/// <paramref name="HasTower"/> = ha almeno una torre (TWR o I_TWR): invariante "ogni aeroporto ha sempre una torre".
/// <paramref name="IsHidden"/> = nascosto dall'admin; la visibilità pubblica effettiva richiede anche almeno un settore (vedi <see cref="IsPublic"/>).
/// </summary>
public sealed record AirportAdminRow(int Id, string Icao, string Name, string AccCode, int Sectors, bool HasTower, bool IsHidden = false)
{
    /// <summary>Vero se l'aeroporto è visibile al pubblico: non nascosto dall'admin e con almeno un settore.</summary>
    public bool IsPublic => !IsHidden && Sectors > 0;
}

/// <summary>Settore sintetico (id+callsign+ACC) per popolare i menu padre nella gestione aeroporti.</summary>
public sealed record SectorBriefRow(int Id, string Callsign, string AccCode);

/// <summary>Esito della generazione automatica del documento di aeroporto.</summary>
public sealed record AirportDocResult(string Icao, bool Created, int SectorsCreated, int? DocumentId, string? Skipped);

/// <summary>Settore (entità unificata ex Position+Sector) per l'editor struttura.</summary>
public sealed record SectorRow(
    int Id, string Callsign, SectorType Type, SectorKind Kind, string Name,
    string? DefaultFrequency, int CoverageOrder, ApproachKind? ApproachKind,
    int? ParentSectorId, int? AirportId, string? AirportIcao, bool IsActive,
    int? DocumentId, bool IsPrimary, int? FeaturedRank = null);

/// <summary>vLOA pubblicata di una ACC (per elenco/card landing): documento + centro confinante + ordine "in evidenza".</summary>
public sealed record VloaRow(int DocId, string Title, string? Neighbour, int? FeaturedRank = null);

/// <summary>Dati struttura completi di una ACC per la pagina di authoring.</summary>
public sealed class StructureData
{
    public required int AccId { get; init; }
    public required string AccCode { get; init; }
    public required string AccName { get; init; }
    public required IReadOnlyList<AirportRow> Airports { get; init; }
    public required IReadOnlyList<SectorRow> Sectors { get; init; }
    public IReadOnlyList<VloaRow> Vloas { get; init; } = Array.Empty<VloaRow>();
}
