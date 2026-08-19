using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Porta (lettura + scrittura) agli **accordi di coordinamento**. Impl. EF in Infrastructure.
///
/// <para><b>Tre livelli, e ognuno ha il suo scopo.</b> L'<b>accordo</b> è la relazione fra due enti — uno solo
/// per coppia; la <b>sezione</b> è un traffico in un verso, cioè una tabella; la <b>clausola</b> è una riga di
/// quella tabella. Tutto ciò che sposta, annida o scioglie ragiona dentro <b>una sezione</b>: le clausole di
/// un'altra non sono alternative di queste, sono un'altra tabella (EUROCONTROL Annex D.2 ne ha due).</para>
///
/// <para><b>Cosa è sparito rispetto a ferragosto.</b> Il ricevente non è più un campo di riga da tenere
/// d'accordo fra le sorelle (è il lato opposto dell'accordo, e il verso lo dice la sezione), e non esiste più
/// «unisci i due versi»: due versi della stessa coppia <b>sono</b> lo stesso accordo, per indice.</para>
/// </summary>
public interface IAgreementRepository
{
    /// <summary>
    /// Gli accordi che riguardano una ACC: quelli che la elencano come responsabile <b>e</b> quelli che hanno
    /// un capo fra i suoi settori.
    /// <para>È la differenza che chiude la duplicazione per ACC: prima un flusso viveva nel «secchio» di una
    /// sola, quindi un centro estero confinante con due ACC italiane andava riscritto due volte, e un accordo
    /// poteva essere invisibile a uno dei suoi due capi.</para>
    /// </summary>
    Task<IReadOnlyList<AgreementRow>> ListByAccAsync(string accCode, CancellationToken ct = default);

    /// <summary>L'accordo fra due enti, se esiste — <b>in qualunque ordine</b> siano indicati. Serve al form di
    /// creazione: una coppia già scritta non è un errore dell'utente, è una domanda a cui esiste una risposta
    /// migliore di «no» (si apre quello che c'è).</summary>
    Task<int?> FindByPairAsync(string accCode, int sectorX, int sectorY, CancellationToken ct = default);

    /// <summary>Crea l'accordo fra i due enti. I lati si mettono in <b>forma canonica</b> (id minore = A):
    /// l'unicità della coppia è un indice, e in SQL non esiste «insieme di due».</summary>
    Task<int> AddAgreementAsync(string accCode, AgreementInput input, CancellationToken ct = default);

    Task UpdateAgreementAsync(string accCode, int agreementId, AgreementInput input, CancellationToken ct = default);
    Task DeleteAgreementAsync(string accCode, int agreementId, CancellationToken ct = default);

    // ---- sezioni ----------------------------------------------------------------------------------------

    Task<int> AddSectionAsync(string accCode, int agreementId, AgreementSectionInput input, CancellationToken ct = default);
    Task UpdateSectionAsync(string accCode, int sectionId, AgreementSectionInput input, CancellationToken ct = default);
    Task DeleteSectionAsync(string accCode, int sectionId, CancellationToken ct = default);

    /// <summary>
    /// Copia la sezione nel verso opposto, come punto di partenza per il reciproco. Non è un «rendi bilaterale»
    /// automatico: i livelli dei due versi sono diversi quasi sempre, e indovinarli sarebbe scrivere un accordo
    /// che nessuno ha concordato. Ritorna l'id della sezione nuova, o <c>null</c> se il reciproco esiste già.
    /// </summary>
    Task<int?> CopySectionToReverseAsync(string accCode, int sectionId, CancellationToken ct = default);

    /// <summary>
    /// Porta le clausole di <paramref name="absorbId"/> in fondo a <paramref name="keepId"/> e cancella la
    /// sezione rimasta vuota: è il tasto «unisci» delle gemelle. Ritorna quante clausole ha spostato.
    /// <para>⚠️ Le due sezioni devono dire davvero la stessa cosa (stesso accordo, stesso tipo, stesso verso,
    /// stessi scali), e la condizione si <b>rivalida qui</b>: fra la segnalazione e il tasto l'archivio può
    /// essere cambiato. ⚠️ Non è invertibile riga per riga — due tabelle diventano una.</para>
    /// </summary>
    Task<int> MergeSectionsAsync(string accCode, int keepId, int absorbId, CancellationToken ct = default);

    // ---- clausole ---------------------------------------------------------------------------------------

    Task<int> AddClauseAsync(string accCode, int sectionId, AgreementClauseInput input, CancellationToken ct = default);
    Task UpdateClauseAsync(string accCode, int clauseId, AgreementClauseInput input, CancellationToken ct = default);
    Task DeleteClauseAsync(string accCode, int clauseId, CancellationToken ct = default);

    /// <summary>Sposta una clausola (col suo sottoalbero) su o giù dentro la propria sezione. No-op agli estremi.</summary>
    Task MoveClauseAsync(string accCode, int clauseId, bool up, CancellationToken ct = default);

    /// <summary>Sposta una clausola (col suo sottoalbero) dove sta un'altra: è il gesto del trascinamento.
    /// Scendendo si va DOPO il bersaglio, salendo PRIMA. No-op fra sezioni diverse.</summary>
    Task MoveClauseToAsync(string accCode, int clauseId, int targetClauseId, CancellationToken ct = default);

    /// <summary>Alternativa pari-grado alla clausola, dopo tutto il suo sottoalbero. Copia tutto tranne la
    /// condizione — che è ciò che l'alternativa deve dire di diverso. Il gruppo, se non c'è, nasce qui.</summary>
    Task<int> AddAlternativeAsync(string accCode, int clauseId, CancellationToken ct = default);

    /// <summary>Eccezione della clausola: un livello più dentro, subito sotto.</summary>
    Task<int> AddExceptionAsync(string accCode, int clauseId, CancellationToken ct = default);

    /// <summary>Duplica il gruppo di varianti della clausola, con la sua struttura, in fondo alla stessa sezione.
    /// Ritorna quante clausole ha creato; 0 se la clausola non sta in un gruppo.</summary>
    Task<int> DuplicateVariantGroupAsync(string accCode, int clauseId, CancellationToken ct = default);

    /// <summary>Sfila la clausola col suo sottoalbero dal gruppo; scioglie ciò che resta di un gruppo di una.</summary>
    Task DetachVariantAsync(string accCode, int clauseId, CancellationToken ct = default);

    /// <summary>Cambia il livello autorizzato di più clausole. <b>Non</b> si propaga al gruppo: il livello è
    /// della singola clausola, ed è proprio ciò che due varianti dicono diverso.</summary>
    Task<int> SetLevelAsync(string accCode, IReadOnlyList<int> clauseIds, ParsedLevel level, CancellationToken ct = default);

    /// <summary>Cambia area e condizione personalizzata di più clausole (<c>null</c> = togli). Niente piste:
    /// dipendono dall'aeroporto, e la stessa sigla su scali diversi è una pista diversa.</summary>
    Task<int> SetConditionAsync(string accCode, IReadOnlyList<int> clauseIds, string? areaLabel, string? customLabel,
        CancellationToken ct = default);

    /// <summary>Elimina più clausole, sciogliendo i gruppi che restino di una sola.</summary>
    Task<int> DeleteClausesAsync(string accCode, IReadOnlyList<int> clauseIds, CancellationToken ct = default);

    // ---- ripristino -------------------------------------------------------------------------------------

    /// <summary>Rimette un accordo eliminato con le sue sezioni, le clausole <b>e il loro outline</b>. Gli
    /// invarianti dell'outline si rivalidano: una fotografia vecchia di un archivio cambiato non deve poter
    /// rientrare rotta.</summary>
    Task<int> RestoreAgreementAsync(string accCode, AgreementSnapshot snapshot, CancellationToken ct = default);

    /// <summary>Rimette una sezione eliminata nel suo accordo, se esiste ancora.</summary>
    Task<int?> RestoreSectionAsync(string accCode, AgreementSectionRestore section, CancellationToken ct = default);

    /// <summary>Rimette clausole eliminate nelle loro sezioni, se esistono ancora.</summary>
    Task<int> RestoreClausesAsync(string accCode, IReadOnlyList<AgreementClauseRestore> clauses, CancellationToken ct = default);
}
