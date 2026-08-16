using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Use-case degli **accordi di coordinamento**: lettura aperta, scrittura ACC-gated con validazione soft.
/// Prende il posto di <c>ITransferService</c> (rimosso col modello vecchio).
///
/// <para>Espone due letture, e la differenza è il cuore del disegno: <see cref="ListByAccAsync"/> dà gli
/// <b>accordi</b>, che è ciò su cui si scrive; <see cref="ListFlowsByAccAsync"/> dà le <b>righe piatte</b>
/// proiettate da quegli accordi, che è ciò che leggono derivazione, frasi, tabelle, vista live e matcher
/// Aurora. Una fonte, due forme — come i cataloghi e la proiezione <c>Sector</c>.</para>
/// </summary>
public interface IAgreementService
{
    /// <summary>Gli accordi che riguardano la ACC: quelli di cui è responsabile e quelli che hanno una parte
    /// fra i suoi settori.</summary>
    Task<IReadOnlyList<AgreementRow>> ListByAccAsync(string accCode, CancellationToken ct = default);

    /// <summary>Le righe piatte proiettate dagli accordi della ACC: la forma che i cinque consumatori a valle
    /// hanno sempre letto.</summary>
    Task<IReadOnlyList<TransferFlowRow>> ListFlowsByAccAsync(string accCode, CancellationToken ct = default);

    /// <summary>Le righe della ACC risolte live: mittente e ricevente risalgono la gerarchia di copertura in
    /// base a chi è <paramref name="online"/>; terminale = UNICOM.</summary>
    Task<IReadOnlyList<ResolvedTransferFlow>> ResolveForAccAsync(
        string accCode, IReadOnlySet<string> online, CancellationToken ct = default);

    Task<int> AddAgreementAsync(string accCode, AgreementInput input, CancellationToken ct = default);
    Task UpdateAgreementAsync(string accCode, int agreementId, AgreementInput input, CancellationToken ct = default);
    Task DeleteAgreementAsync(string accCode, int agreementId, CancellationToken ct = default);

    Task<int> AddClauseAsync(string accCode, int agreementId, AgreementDirection direction,
        AgreementClauseInput input, CancellationToken ct = default);
    Task UpdateClauseAsync(string accCode, int clauseId, AgreementClauseInput input, CancellationToken ct = default);
    Task DeleteClauseAsync(string accCode, int clauseId, CancellationToken ct = default);

    Task MoveClauseAsync(string accCode, int clauseId, bool up, CancellationToken ct = default);
    Task MoveClauseToAsync(string accCode, int clauseId, int targetClauseId, CancellationToken ct = default);

    Task<int> AddAlternativeAsync(string accCode, int clauseId, CancellationToken ct = default);
    Task<int> AddExceptionAsync(string accCode, int clauseId, CancellationToken ct = default);
    Task<int> DuplicateVariantGroupAsync(string accCode, int clauseId, CancellationToken ct = default);
    Task DetachVariantAsync(string accCode, int clauseId, CancellationToken ct = default);

    /// <summary>Copia le clausole di un verso nell'altro, come punto di partenza per il reciproco.</summary>
    Task<int> CopyDirectionAsync(string accCode, int agreementId, AgreementDirection from, CancellationToken ct = default);

    Task<int> SetLevelAsync(string accCode, IReadOnlyList<int> clauseIds, ParsedLevel level, CancellationToken ct = default);
    Task<int> SetConditionAsync(string accCode, IReadOnlyList<int> clauseIds, string? areaLabel, string? customLabel,
        CancellationToken ct = default);
    Task<int> DeleteClausesAsync(string accCode, IReadOnlyList<int> clauseIds, CancellationToken ct = default);

    Task<int> RestoreAgreementAsync(string accCode, AgreementSnapshot snapshot, CancellationToken ct = default);
    Task<int> RestoreClausesAsync(string accCode, IReadOnlyList<AgreementClauseRestore> clauses, CancellationToken ct = default);
}
