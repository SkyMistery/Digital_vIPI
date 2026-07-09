using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Riga candidato confinante per la pagina admin.</summary>
public sealed record NeighbourCandidateRow(
    int Id, string HomeAccCode, string ForeignAccCode, string ForeignAccName, string CountryId,
    string ForeignRootCallsign, bool HasPolygon, double? MinDistanceNm, int AdjacentSectorCount,
    NeighbourCandidateStatus Status, int? VloaDocumentId);
