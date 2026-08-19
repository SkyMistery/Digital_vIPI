using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Use-case degli **accordi di coordinamento**: lettura aperta, scrittura ACC-gated con validazione soft.
///
/// <para>Espone due letture, e la differenza è il cuore del disegno: <see cref="ListByAccAsync"/> dà gli
/// <b>accordi</b> con le loro sezioni, che è ciò su cui si scrive; <see cref="ListFlowsByAccAsync"/> dà le
/// <b>righe piatte</b> proiettate da quelle sezioni, che è ciò che leggono derivazione, frasi, tabelle, vista
/// live e matcher Aurora. Una fonte, due forme — come i cataloghi e la proiezione <c>Sector</c>.</para>
/// </summary>
public interface IAgreementService
{
    /// <summary>Gli accordi che riguardano la ACC: quelli di cui è responsabile e quelli che hanno un capo
    /// fra i suoi settori.</summary>
    Task<IReadOnlyList<AgreementRow>> ListByAccAsync(string accCode, CancellationToken ct = default);

    /// <summary>Le righe piatte proiettate dagli accordi della ACC: la forma che i cinque consumatori a valle
    /// hanno sempre letto.</summary>
    Task<IReadOnlyList<TransferFlowRow>> ListFlowsByAccAsync(string accCode, CancellationToken ct = default);

    /// <summary>Le righe della ACC risolte live: mittente e ricevente risalgono la gerarchia di copertura in
    /// base a chi è <paramref name="online"/>; terminale = UNICOM.</summary>
    Task<IReadOnlyList<ResolvedTransferFlow>> ResolveForAccAsync(
        string accCode, IReadOnlySet<string> online, CancellationToken ct = default);

    /// <summary>L'accordo fra due enti, se esiste, in qualunque ordine siano indicati.</summary>
    Task<int?> FindByPairAsync(string accCode, int sectorX, int sectorY, CancellationToken ct = default);

    Task<int> AddAgreementAsync(string accCode, AgreementInput input, CancellationToken ct = default);
    Task UpdateAgreementAsync(string accCode, int agreementId, AgreementInput input, CancellationToken ct = default);
    Task DeleteAgreementAsync(string accCode, int agreementId, CancellationToken ct = default);

    Task<int> AddSectionAsync(string accCode, int agreementId, AgreementSectionInput input, CancellationToken ct = default);
    Task UpdateSectionAsync(string accCode, int sectionId, AgreementSectionInput input, CancellationToken ct = default);
    Task DeleteSectionAsync(string accCode, int sectionId, CancellationToken ct = default);

    /// <summary>Copia la sezione nel verso opposto, come punto di partenza per il reciproco.</summary>
    Task<int?> CopySectionToReverseAsync(string accCode, int sectionId, CancellationToken ct = default);

    /// <summary>Unisce due sezioni gemelle: le clausole dell'assorbita passano in fondo a quella che resta.</summary>
    Task<int> MergeSectionsAsync(string accCode, int keepId, int absorbId, CancellationToken ct = default);

    Task<int> AddClauseAsync(string accCode, int sectionId, AgreementClauseInput input, CancellationToken ct = default);
    Task UpdateClauseAsync(string accCode, int clauseId, AgreementClauseInput input, CancellationToken ct = default);
    Task DeleteClauseAsync(string accCode, int clauseId, CancellationToken ct = default);

    Task MoveClauseAsync(string accCode, int clauseId, bool up, CancellationToken ct = default);
    Task MoveClauseToAsync(string accCode, int clauseId, int targetClauseId, CancellationToken ct = default);

    Task<int> AddAlternativeAsync(string accCode, int clauseId, CancellationToken ct = default);
    Task<int> AddExceptionAsync(string accCode, int clauseId, CancellationToken ct = default);
    Task<int> DuplicateVariantGroupAsync(string accCode, int clauseId, CancellationToken ct = default);
    Task DetachVariantAsync(string accCode, int clauseId, CancellationToken ct = default);

    Task<int> SetLevelAsync(string accCode, IReadOnlyList<int> clauseIds, ParsedLevel level, CancellationToken ct = default);
    Task<int> SetConditionAsync(string accCode, IReadOnlyList<int> clauseIds, string? areaLabel, string? customLabel,
        CancellationToken ct = default);
    Task<int> DeleteClausesAsync(string accCode, IReadOnlyList<int> clauseIds, CancellationToken ct = default);

    Task<int> RestoreAgreementAsync(string accCode, AgreementSnapshot snapshot, CancellationToken ct = default);
    Task<int?> RestoreSectionAsync(string accCode, AgreementSectionRestore section, CancellationToken ct = default);
    Task<int> RestoreClausesAsync(string accCode, IReadOnlyList<AgreementClauseRestore> clauses, CancellationToken ct = default);
}
