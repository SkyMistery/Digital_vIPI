using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence.ReleaseTargets;

/// <summary>Descrittore vIPI ACC (doc 09 §3a). Chiave di release = "{accCode}|{root}"; Document = quello del CTR radice
/// primario dell'ACC. ACC-wide su un solo Document (doc 08e-acc), nessun overlay (visibilità nel blockmeta).</summary>
public sealed class AccVipiReleaseTarget : IReleaseTarget
{
    private readonly VipiDbContext _db;
    public AccVipiReleaseTarget(VipiDbContext db) => _db = db;

    public ReleaseTargetType Type => ReleaseTargetType.AccVipi;
    public int DescribeOrder => 3;

    public async Task<int?> ResolveDocumentIdAsync(string key, CancellationToken ct = default)
    {
        var parts = key.Split('|', 2);
        // Confronti in maiuscolo: i codici ACC e i callsign lo sono per convenzione e le chiavi si costruiscono già
        // così, ma il confronto stringa di EF è sensibile al caso e una chiave scritta a mano non deve mancare il
        // bersaglio in silenzio.
        var accCode = parts[0].Trim().ToUpperInvariant();
        var root = parts.Length > 1 ? parts[1].Trim().ToUpperInvariant() : "";

        var roots = _db.Sectors.AsNoTracking()
            .Where(s => s.Acc!.Code.ToUpper() == accCode && s.Type == SectorType.Ctr
                        && s.ParentSectorId == null && s.IsActive && s.DocumentId != null);

        // La parte root della chiave sceglie QUALE albero (quindi quale documento) dell'ACC si pubblica: va rispettata,
        // altrimenti su una ACC a più alberi si promuove la bozza del documento sbagliato. Nessun fallback quando il
        // root è indicato ma non risolve: meglio «nessun contenuto da pubblicare» che pubblicare un altro documento.
        if (root.Length > 0)
            return await roots.Where(s => s.Callsign.ToUpper() == root)
                .Select(s => s.DocumentId).FirstOrDefaultAsync(ct);

        // Chiave legacy col solo codice ACC: criterio storico (copertura, poi callsign).
        return await roots
            .OrderBy(s => s.CoverageOrder).ThenBy(s => s.Callsign)
            .Select(s => s.DocumentId).FirstOrDefaultAsync(ct);
    }

    public Task<string?> AuthAccCodeAsync(string key, CancellationToken ct = default) =>
        Task.FromResult<string?>(key.Split('|', 2)[0]);

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
        if (primary is not { Type: SectorType.Ctr, ParentSectorId: null }) return false;
        var acc = primary.Acc?.Code ?? "";
        managed = new ManagedDoc(ReleaseTargetType.AccVipi, doc.Title, primary.Callsign, acc,
            doc.Status == DocumentStatus.Published, hasDraft, doc.IsHidden,
            ReleaseTargetType.AccVipi, $"{acc}|{primary.Callsign}", doc.Id);
        return true;
    }
}
