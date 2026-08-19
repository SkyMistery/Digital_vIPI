using System.Collections.Generic;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Una sezione dell'accordo in lettura: un tipo di traffico, in un verso, per un gruppo di aeroporti — cioè
/// <b>una tabella</b> di clausole.
/// <para>⚠️ L'outline delle varianti vive DENTRO la sezione: le clausole di un'altra sezione non sono
/// alternative di queste, sono un'altra tabella (EUROCONTROL Annex D.2 ne ha due per relazione).</para>
/// </summary>
public sealed record AgreementSectionRow
{
    public required int Id { get; init; }
    public required TransferFlowKind Kind { get; init; }
    public required AgreementDirection Direction { get; init; }

    /// <summary>Prosa che introduce la tabella.</summary>
    public string? Description { get; init; }

    public required int Order { get; init; }

    public required IReadOnlyList<AgreementAirportRow> Airports { get; init; }
    public required IReadOnlyList<AgreementClauseRow> Clauses { get; init; }

    /// <summary>Gli scali in una riga sola («LIBD · LIBR»); vuoto se non ne ha.</summary>
    public string AirportsLabel => string.Join(" · ", Airports.Select(a => a.Icao));
}
