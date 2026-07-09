using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Implementazione EF di <see cref="IEditAuditWriter"/>. Registra le modifiche ai profili sulla tabella
/// <see cref="AuditLog"/> (Action=Update). Deduplica i salvataggi ravvicinati: entro <see cref="SessionWindow"/>
/// dall'ultima voce dello stesso (EntityType, EntityId, UserId) aggiorna quella voce (timestamp + aree accumulate +
/// conteggio) invece di crearne una nuova, così una sessione di editing = una sola riga di storia.
/// </summary>
public sealed class EfEditAuditWriter : IEditAuditWriter
{
    private static readonly TimeSpan SessionWindow = TimeSpan.FromMinutes(15);

    private readonly VipiDbContext _db;
    public EfEditAuditWriter(VipiDbContext db) => _db = db;

    public async Task RecordEditAsync(string entityType, string entityId, int userId, string area, CancellationToken ct = default)
    {
        if (userId == 0) return;   // utente non risolto: niente audit anonimo
        var now = DateTime.UtcNow;
        var since = now - SessionWindow;

        var recent = await _db.AuditLogs
            .Where(a => a.EntityType == entityType && a.EntityId == entityId
                        && a.UserId == userId && a.Action == AuditAction.Update
                        && a.TimestampUtc >= since)
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync(ct);

        if (recent is not null)
        {
            recent.TimestampUtc = now;
            recent.DetailsJson = Merge(recent.DetailsJson, area);
        }
        else
        {
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = AuditAction.Update,
                EntityType = entityType,
                EntityId = entityId,
                TimestampUtc = now,
                DetailsJson = JsonSerializer.Serialize(new EditDetails(new List<string> { area }, 1)),
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Accumula l'area nell'insieme distinto e incrementa il conteggio salvataggi della voce di sessione.</summary>
    private static string Merge(string? json, string area)
    {
        var d = Parse(json);
        var areas = d.Areas.ToList();
        if (!areas.Contains(area, StringComparer.OrdinalIgnoreCase)) areas.Add(area);
        return JsonSerializer.Serialize(new EditDetails(areas, d.Saves + 1));
    }

    private static EditDetails Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new EditDetails(new List<string>(), 0);
        try { return JsonSerializer.Deserialize<EditDetails>(json) ?? new EditDetails(new List<string>(), 0); }
        catch (JsonException) { return new EditDetails(new List<string>(), 0); }
    }

    private sealed record EditDetails(List<string> Areas, int Saves);
}
