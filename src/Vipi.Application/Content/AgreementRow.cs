using System.Collections.Generic;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Un accordo di coordinamento in lettura: le due parti, gli aeroporti, le clausole. È la forma che
/// l'applicazione manipola — l'entità di persistenza sta in <c>Vipi.Domain.Entities</c> e non arriva qui, come
/// per tutte le altre aree.
/// <para>Da qui si passa alle righe piatte di sempre con <see cref="AgreementExpansion"/>: derivazione, frasi,
/// tabelle, vista live e matcher Aurora continuano a leggere <see cref="TransferFlowRow"/>.</para>
/// </summary>
public sealed record AgreementRow
{
    public required int Id { get; init; }

    /// <summary>ACC responsabile: serve all'autorizzazione, non alla visibilità (quella passa dalle parti).</summary>
    public required string OwnerAccCode { get; init; }

    public required TransferFlowKind TrafficKind { get; init; }
    public string? Description { get; init; }
    public required int Order { get; init; }

    public required IReadOnlyList<AgreementPartyRow> Parties { get; init; }
    public required IReadOnlyList<AgreementAirportRow> Airports { get; init; }
    public required IReadOnlyList<AgreementClauseRow> Clauses { get; init; }
}
