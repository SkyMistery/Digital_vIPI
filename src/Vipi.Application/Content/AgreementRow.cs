using System.Collections.Generic;
using System.Linq;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Un capo dell'accordo: il settore e il suo callsign. Il callsign è ciò che la derivazione usa
/// davvero (ragiona per callsign); l'id serve alle scritture.</summary>
public sealed record AgreementEndpoint(int SectorId, string Callsign);

/// <summary>
/// Un accordo di coordinamento in lettura: i due capi e le sue sezioni. È la forma che l'applicazione manipola
/// — l'entità di persistenza sta in <c>Vipi.Domain.Entities</c> e non arriva qui, come per tutte le altre aree.
/// <para>Da qui si passa alle righe piatte di sempre con <see cref="AgreementExpansion"/>: derivazione, frasi,
/// tabelle, vista live e matcher Aurora continuano a leggere <see cref="TransferFlowRow"/>.</para>
/// </summary>
public sealed record AgreementRow
{
    public required int Id { get; init; }

    /// <summary>ACC responsabile: serve all'autorizzazione, non alla visibilità (quella passa dai due capi).</summary>
    public required string OwnerAccCode { get; init; }

    public required AgreementEndpoint SideA { get; init; }
    public required AgreementEndpoint SideB { get; init; }

    /// <summary>Nota libera sull'accordo (l'ex <c>Description</c>). La prosa che introduce una tabella sta sulla
    /// sezione.</summary>
    public string? Note { get; init; }

    public required int Order { get; init; }

    public required IReadOnlyList<AgreementSectionRow> Sections { get; init; }

    /// <summary>Il capo indicato dal lato.</summary>
    public AgreementEndpoint Side(AgreementSide side) => side == AgreementSide.A ? SideA : SideB;

    /// <summary>Chi cede in un verso.</summary>
    public AgreementEndpoint Sender(AgreementDirection direction) =>
        direction == AgreementDirection.AtoB ? SideA : SideB;

    /// <summary>Chi riceve in un verso.</summary>
    public AgreementEndpoint Receiver(AgreementDirection direction) =>
        direction == AgreementDirection.AtoB ? SideB : SideA;

    /// <summary>Tutte le clausole dell'accordo, sezione per sezione: serve ai conteggi e alle letture che non
    /// hanno bisogno di sapere in quale tabella stanno.</summary>
    public IEnumerable<AgreementClauseRow> AllClauses => Sections.SelectMany(s => s.Clauses);
}
