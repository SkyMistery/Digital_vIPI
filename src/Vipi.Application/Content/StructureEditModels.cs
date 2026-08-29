using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Riga ACC per l'elenco struttura.</summary>
public sealed record AccRow(int Id, string Code, string Name, string CountryPrefix, int Sectors);

/// <summary>Esito del lookup di un aeroporto sulla sorgente esterna (IVAO), SOLO per riempire il nome di un
/// aeroporto fuori DB. <paramref name="AccCode"/> = ACC/FIR di competenza (centerId), se noto.</summary>
public sealed record ExternalAirportInfo(string Icao, string Name, string? City, string? AccCode);

/// <summary>Aeroporto di una ACC per l'editor struttura.
/// <paramref name="IsHidden"/> = nascosto dall'admin; la visibilità pubblica effettiva richiede anche almeno un settore (vedi <see cref="IsPublic"/>).</summary>
public sealed record AirportRow(int Id, string Icao, string Name, int Sectors, int? FeaturedRank = null, bool IsHidden = false,
    string? ParentCallsign = null, bool HasMilitaryPresence = false, bool IsMilitaryOnly = false)
{
    /// <summary>Vero se l'aeroporto è visibile al pubblico: non nascosto dall'admin e con almeno un settore.</summary>
    public bool IsPublic => !IsHidden && Sectors > 0;
}

/// <summary>
/// Aeroporto (cross-ACC) per la pagina di gestione aeroporti: ACC assegnata + n. settori che vi puntano.
/// <paramref name="HasTower"/> = ha almeno una torre (TWR o I_TWR): invariante "ogni aeroporto ha sempre una torre".
/// <paramref name="IsHidden"/> = nascosto dall'admin; la visibilità pubblica effettiva richiede anche almeno un settore (vedi <see cref="IsPublic"/>).
/// </summary>
/// <param name="DocumentId">Il documento vIPI dell'aeroporto, se già esiste. Serve a chi deve distinguere
/// «crea» da «apri»: la pagina «Nuovo documento» si chiama così e per l'aeroporto apre quasi sempre.</param>
/// <param name="HasMilitaryPresence">Dalla sorgente: c'è una base militare sull'aeroporto. ⚠️ Non vuol dire
/// «aeroporto militare» — è vero anche per Linate, Pisa, Ciampino, Catania, Elmas, Lamezia e Rimini.</param>
/// <param name="IsMilitaryOnly">Scelta di un amministratore: nessun traffico civile. La sorgente non lo dice.</param>
/// <param name="MilDocumentId">Il vSOP MILITARE dello scalo, se esiste. Sta accanto a <paramref name="DocumentId"/>
/// perché la domanda «crea o apri?» ha DUE risposte su un campo militare, e chiederle in due letture diverse
/// vuol dire poterle vedere in due istanti diversi.</param>
public sealed record AirportAdminRow(int Id, string Icao, string Name, string AccCode, int Sectors, bool HasTower,
    bool IsHidden = false, int? DocumentId = null, bool HasMilitaryPresence = false, bool IsMilitaryOnly = false,
    int? MilDocumentId = null)
{
    /// <summary>Vero se l'aeroporto è visibile al pubblico: non nascosto dall'admin e con almeno un settore.</summary>
    public bool IsPublic => !IsHidden && Sectors > 0;
}

/// <summary>Settore sintetico (id+callsign+ACC) per popolare i menu padre nella gestione aeroporti.</summary>
public sealed record SectorBriefRow(int Id, string Callsign, string AccCode);

/// <summary>Settore proiettato in vista GLOBALE (cross-ACC) per il picker di «Nuovo documento»:
/// porta l'ACC + il prefisso nazione (IT/estero), l'albero (<paramref name="ParentSectorId"/>),
/// la natura APP (<paramref name="ApproachKind"/>) e il documento di riferimento (per i già descritti).</summary>
public sealed record GlobalSectorRow(int Id, string Callsign, string AccCode, string CountryPrefix,
    SectorType Type, SectorKind Kind, ApproachKind? ApproachKind, int? ParentSectorId, int? DocumentId);

/// <summary>Esito della generazione automatica del documento di aeroporto.</summary>
public sealed record AirportDocResult(string Icao, bool Created, int SectorsCreated, int? DocumentId, string? Skipped);

/// <summary>Settore (entità unificata ex Position+Sector) per l'editor struttura.</summary>
public sealed record SectorRow(
    int Id, string Callsign, SectorType Type, SectorKind Kind, string Name,
    string? DefaultFrequency, int CoverageOrder, ApproachKind? ApproachKind,
    int? ParentSectorId, int? AirportId, string? AirportIcao, bool IsActive,
    int? DocumentId, bool IsPrimary, int? FeaturedRank = null);

/// <summary>vLOA pubblicata di una ACC (per elenco/card landing): documento + centro confinante + ordine "in evidenza".</summary>
public sealed record VloaRow(int DocId, string Title, string? Neighbour, int? FeaturedRank = null, string? NeighbourCode = null);

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
