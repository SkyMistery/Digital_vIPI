using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>Porta (lettura+scrittura) ai coordinamenti strutturati di una ACC: flussi e loro punti. Impl. EF in Infrastructure.</summary>
public interface ITransferRepository
{
    /// <summary>Tutti i flussi di una ACC (per codice), coi loro punti, ordinati per settore/ordine. Vuoto se ACC assente.</summary>
    Task<IReadOnlyList<TransferFlowRow>> ListFlowsByAccAsync(string accCode, CancellationToken ct = default);

    Task<int> AddFlowAsync(string accCode, TransferFlowInput input, CancellationToken ct = default);
    Task UpdateFlowAsync(string accCode, int flowId, TransferFlowInput input, CancellationToken ct = default);
    Task DeleteFlowAsync(string accCode, int flowId, CancellationToken ct = default);

    Task<int> AddPointAsync(string accCode, int flowId, TransferPointInput input, CancellationToken ct = default);
    Task UpdatePointAsync(string accCode, int pointId, TransferPointInput input, CancellationToken ct = default);
    Task DeletePointAsync(string accCode, int pointId, CancellationToken ct = default);

    /// <summary>Sposta un punto su/giù scambiando l'<c>Order</c> col punto adiacente nello stesso flusso. No-op agli estremi.</summary>
    Task MovePointAsync(string accCode, int pointId, bool up, CancellationToken ct = default);

    /// <summary>Sposta un punto in cima (<paramref name="top"/>=true) o in fondo al suo flusso, ricompattando gli <c>Order</c>. No-op se già all'estremo.</summary>
    Task MovePointToEndAsync(string accCode, int pointId, bool top, CancellationToken ct = default);

    /// <summary>Sposta un punto (col suo sottoalbero) dove sta <paramref name="targetPointId"/>: è il gesto del
    /// trascinamento, dove la posizione di arrivo è una riga e non una direzione. Scendendo si va DOPO il
    /// bersaglio, salendo PRIMA — come si aspetta chi trascina. No-op fra flussi diversi: un accordo appartiene
    /// al suo gruppo di traffico, e spostarlo altrove sarebbe un'altra operazione.</summary>
    Task MovePointToAsync(string accCode, int pointId, int targetPointId, CancellationToken ct = default);

    /// <summary>Aggiunge un'ALTERNATIVA pari-grado alla riga indicata (stessa profondità), dopo tutto il suo
    /// sottoalbero: «pista 25» accanto a «pista 07». Copia l'intera riga tranne la condizione — che è ciò che
    /// l'alternativa deve dire di diverso. Se la riga non è ancora in un gruppo, il gruppo nasce qui: è il
    /// repository ad assegnarlo, perché è un'identità condivisa fra righe e non un campo che l'editor possa
    /// comporre da solo.</summary>
    Task<int> AddAlternativeAsync(string accCode, int pointId, CancellationToken ct = default);

    /// <summary>Aggiunge un'ECCEZIONE della riga indicata: un livello più dentro, subito sotto. Stessa copia
    /// senza condizione dell'alternativa; cambia dove finisce nell'outline, cioè a chi appartiene.</summary>
    Task<int> AddExceptionAsync(string accCode, int pointId, CancellationToken ct = default);

    /// <summary>Duplica il GRUPPO di varianti a cui la riga appartiene, con la sua struttura (profondità e
    /// righe trasversali), in fondo allo stesso flusso. Un accordo con tre varianti si ricopiava tre volte a
    /// mano, e la struttura andava ricostruita a mano dopo. Ritorna quante righe ha creato; 0 se la riga non
    /// sta in un gruppo.</summary>
    Task<int> DuplicateVariantGroupAsync(string accCode, int pointId, CancellationToken ct = default);

    /// <summary>Cambia il ricevente di più righe in un colpo: serve quando un settore cambia nome o assorbe un
    /// altro, e riga per riga sono decine di aperture del pannello. Ritorna quante righe ha toccato.
    /// <para>Il ricevente è identità dell'accordo e si <b>propaga</b> al gruppo di varianti di ogni riga
    /// toccata: una selezione parziale spaccherebbe l'invariante.</para></summary>
    Task<int> SetReceiverAsync(string accCode, IReadOnlyList<int> pointIds, int? nextSectorId, CancellationToken ct = default);

    /// <summary>Cambia il livello autorizzato di più righe. <b>Non</b> si propaga al gruppo: il livello è della
    /// singola riga, ed è proprio ciò che due varianti dicono diverso. Ritorna quante righe ha toccato.</summary>
    Task<int> SetLevelAsync(string accCode, IReadOnlyList<int> pointIds, ParsedLevel level, CancellationToken ct = default);

    /// <summary>Cambia area e condizione personalizzata di più righe (<c>null</c> = togli). Niente piste:
    /// dipendono dall'aeroporto del flusso. Come il livello, non si propaga. Ritorna quante righe ha toccato.</summary>
    Task<int> SetConditionAsync(string accCode, IReadOnlyList<int> pointIds, string? areaLabel, string? customLabel,
        CancellationToken ct = default);

    /// <summary>Elimina più righe, sciogliendo i gruppi che restino di una riga sola. Ritorna quante ne ha tolte.</summary>
    Task<int> DeletePointsAsync(string accCode, IReadOnlyList<int> pointIds, CancellationToken ct = default);

    /// <summary>Rimette un gruppo eliminato con le sue righe <b>e il loro outline</b>. Ricostruirlo con
    /// <see cref="AddPointAsync"/> lo appiattirebbe: quello scrive righe nuove, e la posizione la decide lui.
    /// Gli invarianti si rivalidano — una fotografia vecchia di un archivio cambiato non deve entrare rotta.</summary>
    Task<int> RestoreFlowAsync(string accCode, TransferFlowSnapshot snapshot, CancellationToken ct = default);

    /// <summary>Rimette righe eliminate nei loro flussi, se esistono ancora. Ritorna quante ne ha rimesse.</summary>
    Task<int> RestorePointsAsync(string accCode, IReadOnlyList<TransferPointRestore> points, CancellationToken ct = default);

    /// <summary>Sfila una riga <b>col suo sottoalbero</b> dal gruppo: le eccezioni descrivono la riga che le
    /// ospita, e lasciarle indietro le riassegnerebbe alla riga di sopra. Il pezzo staccato riparte da
    /// profondità 0 e resta un gruppo solo se ha più di una riga; se il gruppo d'origine resta con una riga
    /// sola, si scioglie anche quello.</summary>
    Task DetachVariantAsync(string accCode, int pointId, CancellationToken ct = default);
}
