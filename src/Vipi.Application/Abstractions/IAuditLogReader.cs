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
    /// <summary>Eventi più recenti, opzionalmente dal momento indicato in poi. <paramref name="max"/> è un
    /// tetto di sicurezza sulla query, non il filtro: quello lo fa <paramref name="sinceUtc"/>.
    /// <para>⚠️ Un tetto <b>muto</b> su un registro fa credere completo un elenco che non lo è: chi chiama
    /// legge anche <see cref="CountAsync"/> e dice quante righe ci sono davvero nel periodo.</para></summary>
    Task<IReadOnlyList<AuditEntry>> ListRecentAsync(DateTime? sinceUtc = null, int max = 500, CancellationToken ct = default);

    /// <summary>Quanti eventi ci sono nel periodo (nessun tetto). Serve a dire «mostrate 500 di 1 240».</summary>
    Task<int> CountAsync(DateTime? sinceUtc = null, CancellationToken ct = default);

    /// <summary>Audit di uno specifico bersaglio (EntityType+EntityId), più recente prima. Per la storia modifiche
    /// contestuale nel dettaglio del profilo.</summary>
    Task<IReadOnlyList<AuditEntry>> ListForEntityAsync(string entityType, string entityId, int max = 50, CancellationToken ct = default);

    /// <summary>Audit di più bersagli dello stesso tipo, più recente prima. Serve alla storia di un DOCUMENTO, che
    /// è l'unione di quella delle sue versioni: l'audit lo si scrive per <c>DocumentVersion</c>.</summary>
    Task<IReadOnlyList<AuditEntry>> ListForEntitiesAsync(string entityType, IReadOnlyList<string> entityIds, int max = 50, CancellationToken ct = default);
}
