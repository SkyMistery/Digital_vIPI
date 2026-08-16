using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Porta (lettura + scrittura) agli **accordi di coordinamento**. Prende il posto di
/// <c>ITransferRepository</c> (rimosso col modello vecchio); impl. EF in Infrastructure.
///
/// <para><b>Cosa cambia rispetto alla porta di prima.</b> Il ricevente non è più un campo di riga da tenere
/// d'accordo fra le sorelle: è il lato B dell'accordo, quindi <c>SetReceiverAsync</c> non esiste e la sua
/// invariante non può più rompersi. Al suo posto c'è la modifica dell'intestazione, che tocca l'accordo intero
/// per costruzione.</para>
///
/// <para><b>L'outline vive dentro una direzione.</b> Tutto ciò che sposta, annida o scioglie ragiona sulle
/// clausole di uno stesso <c>(accordo, verso)</c>: le clausole del verso opposto non sono alternative delle
/// prime, sono un'altra tabella (EUROCONTROL Annex D.2 ne ha due).</para>
/// </summary>
public interface IAgreementRepository
{
    /// <summary>
    /// Gli accordi che riguardano una ACC: quelli che la elencano come responsabile <b>e</b> quelli che hanno
    /// una parte fra i suoi settori.
    /// <para>È la differenza che chiude la duplicazione per ACC: prima un flusso viveva nel «secchio» di una
    /// sola, quindi un centro estero confinante con due ACC italiane andava riscritto due volte, e un accordo
    /// poteva essere invisibile a uno dei suoi due capi.</para>
    /// </summary>
    Task<IReadOnlyList<AgreementRow>> ListByAccAsync(string accCode, CancellationToken ct = default);

    Task<int> AddAgreementAsync(string accCode, AgreementInput input, CancellationToken ct = default);
    Task UpdateAgreementAsync(string accCode, int agreementId, AgreementInput input, CancellationToken ct = default);
    Task DeleteAgreementAsync(string accCode, int agreementId, CancellationToken ct = default);

    Task<int> AddClauseAsync(string accCode, int agreementId, AgreementDirection direction,
        AgreementClauseInput input, CancellationToken ct = default);
    Task UpdateClauseAsync(string accCode, int clauseId, AgreementClauseInput input, CancellationToken ct = default);
    Task DeleteClauseAsync(string accCode, int clauseId, CancellationToken ct = default);

    /// <summary>Sposta una clausola (col suo sottoalbero) su o giù dentro il proprio verso. No-op agli estremi.</summary>
    Task MoveClauseAsync(string accCode, int clauseId, bool up, CancellationToken ct = default);

    /// <summary>Sposta una clausola (col suo sottoalbero) dove sta un'altra: è il gesto del trascinamento.
    /// Scendendo si va DOPO il bersaglio, salendo PRIMA. No-op fra accordi o versi diversi.</summary>
    Task MoveClauseToAsync(string accCode, int clauseId, int targetClauseId, CancellationToken ct = default);

    /// <summary>Alternativa pari-grado alla clausola, dopo tutto il suo sottoalbero. Copia tutto tranne la
    /// condizione — che è ciò che l'alternativa deve dire di diverso. Il gruppo, se non c'è, nasce qui.</summary>
    Task<int> AddAlternativeAsync(string accCode, int clauseId, CancellationToken ct = default);

    /// <summary>Eccezione della clausola: un livello più dentro, subito sotto.</summary>
    Task<int> AddExceptionAsync(string accCode, int clauseId, CancellationToken ct = default);

    /// <summary>Duplica il gruppo di varianti della clausola, con la sua struttura, in fondo allo stesso verso.
    /// Ritorna quante clausole ha creato; 0 se la clausola non sta in un gruppo.</summary>
    Task<int> DuplicateVariantGroupAsync(string accCode, int clauseId, CancellationToken ct = default);

    /// <summary>Sfila la clausola col suo sottoalbero dal gruppo; scioglie ciò che resta di un gruppo di una.</summary>
    Task DetachVariantAsync(string accCode, int clauseId, CancellationToken ct = default);

    /// <summary>Copia le clausole di un verso nell'altro, come punto di partenza per il reciproco. Non è un
    /// «rendi bilaterale» automatico: i livelli dei due versi sono diversi quasi sempre, e indovinarli sarebbe
    /// scrivere un accordo che nessuno ha concordato. Ritorna quante clausole ha creato; 0 se il verso di
    /// destinazione ne ha già.</summary>
    Task<int> CopyDirectionAsync(string accCode, int agreementId, AgreementDirection from, CancellationToken ct = default);

    /// <summary>Cambia il livello autorizzato di più clausole. <b>Non</b> si propaga al gruppo: il livello è
    /// della singola clausola, ed è proprio ciò che due varianti dicono diverso.</summary>
    Task<int> SetLevelAsync(string accCode, IReadOnlyList<int> clauseIds, ParsedLevel level, CancellationToken ct = default);

    /// <summary>Cambia area e condizione personalizzata di più clausole (<c>null</c> = togli). Niente piste:
    /// dipendono dall'aeroporto, e la stessa sigla su scali diversi è una pista diversa.</summary>
    Task<int> SetConditionAsync(string accCode, IReadOnlyList<int> clauseIds, string? areaLabel, string? customLabel,
        CancellationToken ct = default);

    /// <summary>Elimina più clausole, sciogliendo i gruppi che restino di una sola.</summary>
    Task<int> DeleteClausesAsync(string accCode, IReadOnlyList<int> clauseIds, CancellationToken ct = default);

    /// <summary>Rimette un accordo eliminato con le sue clausole <b>e il loro outline</b>. Gli invarianti si
    /// rivalidano: una fotografia vecchia di un archivio cambiato non deve poter rientrare rotta.</summary>
    Task<int> RestoreAgreementAsync(string accCode, AgreementSnapshot snapshot, CancellationToken ct = default);

    /// <summary>Rimette clausole eliminate nei loro accordi, se esistono ancora.</summary>
    Task<int> RestoreClausesAsync(string accCode, IReadOnlyList<AgreementClauseRestore> clauses, CancellationToken ct = default);
}
