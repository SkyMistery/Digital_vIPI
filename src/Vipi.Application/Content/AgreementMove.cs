using System.Collections.Generic;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Dove stava una clausola prima di uno spostamento: quel che serve a rimetterla esattamente lì.</summary>
public sealed record AgreementClausePlacement(int ClauseId, int SectionId, int Order, int? VariantGroup);

/// <summary>Dove stava una sezione prima di uno spostamento.</summary>
public sealed record AgreementSectionPlacement(int SectionId, int AgreementId, AgreementDirection Direction, int Order);

/// <summary>
/// Come si <b>disfa</b> uno spostamento: i posti di prima, e quel che lo spostamento ha creato per fare posto.
///
/// <para>⚠️ Non è «sposta all'indietro»: rispostando, sezione e clausole finirebbero in <b>coda</b>, e negli accordi
/// l'ordine è struttura — è l'ordine in cui il documento le stampa, e quello che tiene insieme varianti ed
/// eccezioni. Qui si rimettono i posti <b>com'erano</b>.</para>
/// </summary>
/// <param name="Section">La sezione spostata; null se si sono spostate solo clausole.</param>
/// <param name="Clauses">Ogni clausola toccata: sezione, ordine e gruppo di prima.</param>
/// <param name="CreatedSectionIds">Le sezioni nate per accogliere le clausole: se restano vuote, annullando se ne vanno.</param>
/// <param name="CreatedAgreementId">L'accordo nato per la coppia nuova: se resta vuoto, annullando se ne va.</param>
public sealed record AgreementMoveUndo(
    AgreementSectionPlacement? Section,
    IReadOnlyList<AgreementClausePlacement> Clauses,
    IReadOnlyList<int> CreatedSectionIds,
    int? CreatedAgreementId);

/// <summary>L'esito di uno spostamento fra accordi.</summary>
/// <param name="AgreementId">L'accordo di arrivo: quello della coppia «chi cede → chi riceve».</param>
/// <param name="SectionId">La sezione di arrivo; null se non si è spostato niente.</param>
/// <param name="Clauses">Quante clausole hanno cambiato accordo.</param>
/// <param name="AgreementCreated">L'accordo di arrivo non esisteva ed è stato creato.</param>
public sealed record AgreementMoveResult(
    int AgreementId, int? SectionId, int Clauses, bool AgreementCreated, AgreementMoveUndo Undo);
