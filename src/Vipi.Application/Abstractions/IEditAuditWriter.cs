namespace Vipi.Application.Abstractions;

/// <summary>
/// Scrittura dell'audit di editing (chi/quando) per i profili ACC/APP, che non hanno storia versioni. Registra
/// solo metadati (VID + timestamp + area modificata), MAI il contenuto. I salvataggi ravvicinati dello stesso
/// bersaglio/utente sono deduplicati in un'unica voce di "sessione" (vedi implementazione). Impl. EF su AuditLog.
/// </summary>
public interface IEditAuditWriter
{
    /// <summary>Registra una modifica editoriale del bersaglio (<paramref name="entityType"/>/<paramref name="entityId"/>)
    /// da parte di <paramref name="userId"/>, annotando l'<paramref name="area"/> toccata. userId=0 → no-op.</summary>
    Task RecordEditAsync(string entityType, string entityId, int userId, string area, CancellationToken ct = default);
}
