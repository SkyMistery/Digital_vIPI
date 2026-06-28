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
}
