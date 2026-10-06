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

    /// <summary>
    /// Le SID/STAR scritte fra i punti di <paramref name="accordi"/> che negli scali della sezione non si trovano
    /// (S5, <see cref="ProceduraNeiPunti.NonTrovate"/>): l'avviso dell'editor dei trasferimenti. Gli accordi sono
    /// quelli che l'editor ha già in mano, così non si rilegge niente.
    /// <para>Il corpo di ripiego (nessun avviso) serve ai finti dei test.</para>
    /// </summary>
    Task<IReadOnlyList<ProceduraNonTrovata>> ProcedureNonTrovateAsync(IReadOnlyList<AgreementRow> accordi,
        CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ProceduraNonTrovata>>(Array.Empty<ProceduraNonTrovata>());

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
    /// <summary>Modifica una sezione. Traffico, aeroporti e prosa valgono per <b>tutti</b> gli accordi che la
    /// portano; il verso vale per la presenza nell'accordo indicato (quello di casa, se non si dice).</summary>
    Task UpdateSectionAsync(string accCode, int sectionId, AgreementSectionInput input, int? agreementId = null,
        CancellationToken ct = default);

    /// <summary>Toglie la sezione dal suo accordo di casa. ⚠️ Se è condivisa <b>non</b> la distrugge: la casa passa
    /// al primo accordo che la ospita.</summary>
    Task DeleteSectionAsync(string accCode, int sectionId, CancellationToken ct = default);

    /// <summary>Toglie la sezione da <b>quell'</b>accordo. Se vive anche altrove si stacca soltanto, e torna come
    /// rimetterla; se era l'ultima presenza la sezione se ne va per intero, e torna <c>null</c>.</summary>
    Task<AgreementPresenceUndo?> RemoveSectionAsync(string accCode, int sectionId, int agreementId,
        CancellationToken ct = default);

    /// <summary>Fa comparire la sezione anche nell'accordo della coppia «chi cede → chi riceve», che nasce se non
    /// c'è. Il contenuto resta uno: si scrive e si corregge una volta.</summary>
    Task<AgreementShareResult> ShareSectionAsync(string accCode, int sectionId, int senderSectorId, int receiverSectorId,
        CancellationToken ct = default);

    /// <summary>«Stacca»: in quell'accordo la sezione diventa una copia indipendente, con le stesse clausole; negli
    /// altri resta com'è.</summary>
    Task<AgreementDetachResult> DetachSectionAsync(string accCode, int sectionId, int agreementId,
        CancellationToken ct = default);

    /// <summary>Rimette le presenze di una sezione com'erano prima di condividere, togliere o staccare.</summary>
    Task UndoPresenceAsync(string accCode, AgreementPresenceUndo undo, CancellationToken ct = default);

    /// <summary>Copia la sezione nel verso opposto, come punto di partenza per il reciproco.</summary>
    Task<int?> CopySectionToReverseAsync(string accCode, int sectionId, int? agreementId = null,
        CancellationToken ct = default);

    /// <summary>Unisce due sezioni gemelle: le clausole dell'assorbita passano in fondo a quella che resta.</summary>
    Task<int> MergeSectionsAsync(string accCode, int keepId, int absorbId, CancellationToken ct = default);

    Task<int> AddClauseAsync(string accCode, int sectionId, AgreementClauseInput input, CancellationToken ct = default);

    /// <summary>
    /// Le clausole di un «Incolla tabella». 🔴 U-178 (revisione totale 3): si validano TUTTE prima di scriverne una,
    /// e si scrivono in un salvataggio solo. Prima si scrivevano una per una: se la riga k veniva rifiutata, le
    /// righe 1..k-1 restavano salvate (ma non a schermo), e al nuovo invio entravano una seconda volta.
    /// </summary>
    Task<int> AddClausesAsync(string accCode, int sectionId, IReadOnlyList<AgreementClauseInput> inputs,
        CancellationToken ct = default);
    Task UpdateClauseAsync(string accCode, int clauseId, AgreementClauseInput input, CancellationToken ct = default);
    Task DeleteClauseAsync(string accCode, int clauseId, CancellationToken ct = default);

    Task MoveClauseAsync(string accCode, int clauseId, bool up, CancellationToken ct = default);
    Task MoveClauseToAsync(string accCode, int clauseId, int targetClauseId, CancellationToken ct = default);

    /// <summary>Porta una sezione nell'accordo della coppia «chi cede → chi riceve» (nasce se non c'è).</summary>
    Task<AgreementMoveResult> MoveSectionAsync(string accCode, int sectionId, int senderSectorId, int receiverSectorId,
        CancellationToken ct = default);

    /// <summary>Porta delle clausole, coi loro gruppi di varianti interi, nella sezione gemella dell'accordo della
    /// coppia «chi cede → chi riceve». Accordo e sezione nascono se non ci sono.</summary>
    Task<AgreementMoveResult> MoveClausesAsync(string accCode, IReadOnlyList<int> clauseIds, int senderSectorId,
        int receiverSectorId, CancellationToken ct = default);

    /// <summary>Disfa uno spostamento fra accordi, rimettendo i posti di prima.</summary>
    Task UndoMoveAsync(string accCode, AgreementMoveUndo undo, CancellationToken ct = default);

    Task<int> AddAlternativeAsync(string accCode, int clauseId, CancellationToken ct = default);
    Task<int> AddExceptionAsync(string accCode, int clauseId, CancellationToken ct = default);
    Task<int> DuplicateVariantGroupAsync(string accCode, int clauseId, CancellationToken ct = default);

    /// <summary>Copia UNA clausola subito sotto l'originale, condizione compresa. Il gesto sta sulla RIGA;
    /// quello del gruppo sta nel pannello, e i due non fanno la stessa cosa.</summary>
    Task<int> DuplicateClauseAsync(string accCode, int clauseId, CancellationToken ct = default);
    Task DetachVariantAsync(string accCode, int clauseId, CancellationToken ct = default);

    Task<int> SetLevelAsync(string accCode, IReadOnlyList<int> clauseIds, ParsedLevel level, CancellationToken ct = default);
    Task<int> SetConditionAsync(string accCode, IReadOnlyList<int> clauseIds, string? areaLabel, bool areaNegated, bool areaAll, string? customLabel,
        CancellationToken ct = default);
    Task<int> DeleteClausesAsync(string accCode, IReadOnlyList<int> clauseIds, CancellationToken ct = default);

    Task<int> RestoreAgreementAsync(string accCode, AgreementSnapshot snapshot, CancellationToken ct = default);
    Task<int?> RestoreSectionAsync(string accCode, AgreementSectionRestore section, CancellationToken ct = default);
    Task<int> RestoreClausesAsync(string accCode, IReadOnlyList<AgreementClauseRestore> clauses,
        IReadOnlyList<AgreementOutlineRestore>? sorelle = null, CancellationToken ct = default);
}
