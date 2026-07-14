using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>Persistenza del segnale di revisione sui documenti + reverse-lookup settore→documenti. Impl. EF in Infrastructure.</summary>
public interface IDocumentReviewRepository
{
    /// <summary>Documenti (ACC vIPI + APP + vLOA confinanti) dove il settore <paramref name="composePosition"/>
    /// dell'ACC <paramref name="accCode"/> compare in FREQUENZE/AoR/CONFIGURAZIONI.</summary>
    Task<IReadOnlyList<AffectedDoc>> FindDocumentsForSectorAsync(string composePosition, string accCode, CancellationToken ct = default);

    /// <summary>Marca il documento come da revisionare (timestamp + motivo).</summary>
    Task SetReviewAsync(int documentId, DateTime whenUtc, string reason, CancellationToken ct = default);

    /// <summary>Scioglie la revisione del documento.</summary>
    Task ClearReviewAsync(int documentId, CancellationToken ct = default);

    /// <summary>Stato di revisione del documento (per il banner). null se il documento non esiste.</summary>
    Task<DocumentReviewState?> GetReviewAsync(int documentId, CancellationToken ct = default);

    /// <summary>Codice ACC del documento (per l'autorizzazione dello scioglimento). null se non risolvibile.</summary>
    Task<string?> GetDocAccCodeAsync(int documentId, CancellationToken ct = default);
}
