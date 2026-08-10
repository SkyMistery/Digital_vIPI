using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>Riga di audit per il viewer admin.</summary>
public sealed record AuditEntry(
    long Id,
    int UserId,
    AuditAction Action,
    string EntityType,
    string EntityId,
    DateTime TimestampUtc,
    string? DetailsJson);

/// <summary>Lettura dell'audit log (per la pagina admin). Impl. EF.</summary>
public interface IAuditLogReader
{
    Task<IReadOnlyList<AuditEntry>> ListRecentAsync(int max = 200, CancellationToken ct = default);

    /// <summary>Audit di uno specifico bersaglio (EntityType+EntityId), più recente prima. Per la storia modifiche
    /// contestuale nel dettaglio del profilo.</summary>
    Task<IReadOnlyList<AuditEntry>> ListForEntityAsync(string entityType, string entityId, int max = 50, CancellationToken ct = default);

    /// <summary>Audit di più bersagli dello stesso tipo, più recente prima. Serve alla storia di un DOCUMENTO, che
    /// è l'unione di quella delle sue versioni: l'audit lo si scrive per <c>DocumentVersion</c>.</summary>
    Task<IReadOnlyList<AuditEntry>> ListForEntitiesAsync(string entityType, IReadOnlyList<string> entityIds, int max = 50, CancellationToken ct = default);
}
