using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Persistenza della <b>casella degli impatti</b> + il reverse-lookup che dice quali documenti raccontano un
/// dato a monte. Impl. EF in Infrastructure. Carta <c>docs/feature/2026-08-25-documenti-da-rivedere.md</c>.
/// </summary>
public interface IDocumentImpactRepository
{
    /// <summary>Documenti legati in modo <b>dimostrabile</b> al callsign: quello che lo descrive, quello dello
    /// scalo se è una posizione d'aeroporto, la vIPI ACC se il settore pesa sulla sezionazione, i vicini nella
    /// catena di copertura, chi lo cita per Id e le vLOA che lo hanno fra i confinanti.</summary>
    Task<IReadOnlyList<AffectedDoc>> FindDocumentsForSectorAsync(string composePosition, string accCode, CancellationToken ct = default);

    /// <summary>Documenti la cui sezione «aree regolamentate» cita l'area indicata (per id IVAO).</summary>
    Task<IReadOnlyList<AffectedDoc>> FindDocumentsForSpecialAreaAsync(string ivaoId, CancellationToken ct = default);

    /// <summary>Apre un impatto se non ce n'è già uno <b>aperto</b> con la stessa chiave
    /// <c>(documento, tipo, origine)</c>. Ritorna l'Id della riga (nuova o già esistente).</summary>
    Task<int> RaiseAsync(RaiseImpactInput input, CancellationToken ct = default);

    /// <summary>Chiude una riga aperta. <paramref name="byUserId"/> 0 = l'ha chiusa il calcolo.</summary>
    Task ClearAsync(int impactId, int byUserId, DateTime whenUtc, CancellationToken ct = default);

    /// <summary>Chiude tutte le righe aperte dei tipi dati con quella origine. Ritorna quante. Serve quando la
    /// CAUSA sparisce (un callsign che torna in catalogo): la riga non l'ha risolta nessuno, si è risolta.</summary>
    Task<int> ClearBySourceAsync(IReadOnlyCollection<ImpactKind> kinds, string sourceKey, int byUserId,
        DateTime whenUtc, CancellationToken ct = default);

    /// <summary>Una riga <b>aperta</b> per Id, o null se non esiste o è già chiusa.</summary>
    Task<DocumentImpactRow?> GetOpenAsync(int impactId, CancellationToken ct = default);

    /// <summary>Le righe aperte di un documento, dalla più recente.</summary>
    Task<IReadOnlyList<DocumentImpactRow>> ListOpenAsync(int documentId, CancellationToken ct = default);

    /// <summary>Tutte le righe aperte di un tipo (per la riconciliazione dei rivelatori calcolati).</summary>
    Task<IReadOnlyList<DocumentImpactRow>> ListOpenByKindAsync(ImpactKind kind, CancellationToken ct = default);

    /// <summary>Quante righe aperte per ciascuno dei documenti indicati (per le pill degli elenchi).
    /// I documenti senza righe non compaiono nel risultato.</summary>
    Task<IReadOnlyDictionary<int, ImpactBadge>> CountOpenAsync(IReadOnlyCollection<int> documentIds, CancellationToken ct = default);

    /// <summary>Documenti, fra quelli indicati, che hanno una sezione <b>Live</b> alimentata dalla famiglia data.
    /// È ciò che distingue «da rivedere» da «è già cambiato in pubblico».</summary>
    Task<IReadOnlySet<int>> WithLiveSectionAsync(IReadOnlyCollection<int> documentIds, ImpactFamily family, CancellationToken ct = default);

    /// <summary>Elimina le righe <b>chiuse</b> prima della soglia. Ritorna quante.</summary>
    Task<int> PruneClearedBeforeAsync(DateTime cutoffUtc, CancellationToken ct = default);

    /// <summary>Codice ACC del documento, per autorizzare chi chiude una riga. null se non risolvibile.</summary>
    Task<string?> GetDocAccCodeAsync(int documentId, CancellationToken ct = default);

    /// <summary>Titolo del documento (per le frasi e i log). null se non esiste.</summary>
    Task<string?> GetDocTitleAsync(int documentId, CancellationToken ct = default);
}
