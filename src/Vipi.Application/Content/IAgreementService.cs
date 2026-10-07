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

    /// <summary>
    /// Le clausole di <paramref name="accordi"/> scritte per un ente d'area che, su quel punto e a quella quota,
    /// quel cielo non lo tiene (<see cref="AgreementLevelCheck"/>): l'avviso «quota di un altro settore» del
    /// cruscotto delle lacune. Gli accordi sono quelli che l'editor ha già in mano.
    /// <para>Il corpo di ripiego (nessun avviso) serve ai finti dei test.</para>
    /// </summary>
    Task<IReadOnlyList<AgreementLevelWarning>> LevelWarningsAsync(IReadOnlyList<AgreementRow> accordi,
        CancellationToken ct = default) => Task.FromResult<IReadOnlyList<AgreementLevelWarning>>(Array.Empty<AgreementLevelWarning>());

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

    /// <summary>Elimina la sezione. ⚠️ Le sue clausole <b>condivise</b> non se ne vanno con lei: la casa di
    /// ognuna passa alla prima sezione che la ospita. Le clausole che ospitava restano dove stanno di casa.</summary>
    Task DeleteSectionAsync(string accCode, int sectionId, CancellationToken ct = default);

    // ---- clausole condivise fra accordi -----------------------------------------------------------------

    /// <summary>Fa comparire delle clausole — coi loro gruppi di varianti, interi — anche nell'accordo della coppia
    /// «chi cede → chi riceve». Vanno nella sezione che lì dice la stessa cosa (stesso traffico, stesso verso,
    /// stessi scali); accordo e sezione nascono solo se non ci sono. Il contenuto resta uno: si corregge una volta.</summary>
    Task<AgreementShareResult> ShareClausesAsync(string accCode, IReadOnlyList<int> clauseIds, int senderSectorId,
        int receiverSectorId, CancellationToken ct = default);

    /// <summary>Come <see cref="ShareClausesAsync"/>, per tutte le clausole che la sezione mostra.</summary>
    Task<AgreementShareResult> ShareSectionAsync(string accCode, int sectionId, int senderSectorId, int receiverSectorId,
        CancellationToken ct = default);

    /// <summary>Disfa un «Condividi con…»: toglie le presenze aggiunte, e quel che era nato per ospitarle se è
    /// rimasto vuoto.</summary>
    Task UndoShareAsync(string accCode, AgreementShareUndo undo, CancellationToken ct = default);

    /// <summary>«Stacca»: in quell'accordo le clausole condivise scelte — coi loro gruppi interi — diventano copie
    /// indipendenti; negli altri restano com'erano.</summary>
    Task<AgreementDetachResult> DetachClausesAsync(string accCode, IReadOnlyList<int> clauseIds, int agreementId,
        CancellationToken ct = default);

    /// <summary>Disfa uno «Stacca»: via le copie, tornano le presenze.</summary>
    Task UndoDetachAsync(string accCode, AgreementDetachUndo undo, CancellationToken ct = default);

    /// <summary>Copia la sezione nel verso opposto, come punto di partenza per il reciproco.</summary>
    Task<int?> CopySectionToReverseAsync(string accCode, int sectionId, CancellationToken ct = default);

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
    /// <summary>
    /// Elimina delle clausole. Con <paramref name="agreementId"/> si dice <b>da quale accordo</b> le si sta
    /// guardando, e una clausola condivisa si toglie solo da quello — resta negli altri. Senza, la clausola se ne
    /// va ovunque. ⚠️ Una variante sola di un gruppo condiviso si elimina sempre per davvero: la struttura di un
    /// gruppo è contenuto, ed è la stessa in ogni accordo che lo porta.
    /// </summary>
    Task DeleteClauseAsync(string accCode, int clauseId, int? agreementId = null, CancellationToken ct = default);

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
    Task<int> DeleteClausesAsync(string accCode, IReadOnlyList<int> clauseIds, int? agreementId = null,
        CancellationToken ct = default);

    Task<int> RestoreAgreementAsync(string accCode, AgreementSnapshot snapshot, CancellationToken ct = default);
    Task<int?> RestoreSectionAsync(string accCode, AgreementSectionRestore section, CancellationToken ct = default);
    Task<int> RestoreClausesAsync(string accCode, IReadOnlyList<AgreementClauseRestore> clauses,
        IReadOnlyList<AgreementOutlineRestore>? sorelle = null, CancellationToken ct = default);
}
