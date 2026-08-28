using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence.ReleaseTargets;

/// <summary>Descrittore APP standalone (doc 09 §3a). Chiave di release = callsign del settore APP primario; ACC = quello del settore.</summary>
public sealed class AppReleaseTarget : IReleaseTarget
{
    private readonly VipiDbContext _db;
    public AppReleaseTarget(VipiDbContext db) => _db = db;

    public ReleaseTargetType Type => ReleaseTargetType.App;
    public int DescribeOrder => 1;

    public async Task<int?> ResolveDocumentIdAsync(string key, CancellationToken ct = default) =>
        await _db.Sectors.AsNoTracking()
            .Where(s => s.Callsign == key && s.Type == SectorType.App
                        && s.ApproachKind == ApproachKind.Standalone && s.DocumentId != null)
            .Select(s => s.DocumentId).FirstOrDefaultAsync(ct);

    public async Task<string?> AuthAccCodeAsync(string key, CancellationToken ct = default) =>
        await _db.Sectors.AsNoTracking()
            .Where(s => s.Callsign == key).Select(s => s.Acc!.Code).FirstOrDefaultAsync(ct);

    public bool TryDescribe(Document doc, bool hasDraft, out ManagedDoc managed)
    {
        managed = default!;
        if (doc.Type != DocumentType.Vipi) return false;
        // ⚠️ SECONDA MANO della difesa contro il catch-all (carta vSOP militari §7.1): un documento
        // dell'edizione MILITARE non appartiene a questo descrittore, e va rifiutato QUI e non solo
        // sperando nell'ordine. Aggiungere il controllo ai soli descrittori militari lascerebbe i civili
        // disposti ad accettare un documento militare, e l'ordine sarebbe l'unica cosa a impedirlo: due
        // difese indipendenti, ognuna sufficiente -- la stessa forma delle guardie sulle corse del context.
        if (doc.Edition != DocumentEdition.Civil) return false;

        var primary = doc.Sectors.FirstOrDefault(s => s.IsPrimary) ?? doc.Sectors.FirstOrDefault();
        if (primary is not { Type: SectorType.App, ApproachKind: ApproachKind.Standalone }) return false;
        managed = new ManagedDoc(ReleaseTargetType.App, doc.Title, primary.Callsign, primary.Acc?.Code,
            doc.Status == DocumentStatus.Published, hasDraft, doc.IsHidden,
            ReleaseTargetType.App, primary.Callsign, doc.Id);
        return true;
    }
}
