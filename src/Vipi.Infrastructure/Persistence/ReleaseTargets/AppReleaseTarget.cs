using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence.ReleaseTargets;

/// <summary>
/// Descrittore della vIPI APP (doc 09 §3a). Chiave di release = il <b>codice dell'ente</b>; ACC = quello dell'ente.
/// <para>🔴 Fino al 29 settembre 2026 (S49) decideva dal settore APP primario del documento: un APP spuntato
/// «remotizzato», o un documento spostato su una torre, non era più riconosciuto — e il documento cadeva nel
/// descrittore d'aeroporto con l'ICAO vuoto, cioè diventava irraggiungibile. Il codice dell'ente, per gli enti
/// nati dal legame vecchio, è il nominativo che portava il documento: le chiavi già pubblicate restano quelle.</para>
/// <para>⚠️ Chi chiama <see cref="TryDescribe"/> deve caricare <c>.Include(d => d.AtcUnit).ThenInclude(u => u!.Acc)</c>.</para>
/// </summary>
public sealed class AppReleaseTarget : IReleaseTarget
{
    private readonly VipiDbContext _db;
    public AppReleaseTarget(VipiDbContext db) => _db = db;

    public ReleaseTargetType Type => ReleaseTargetType.App;
    public int DescribeOrder => 2;

    public async Task<int?> ResolveDocumentIdAsync(string key, CancellationToken ct = default)
    {
        var ente = await new EfAtcUnitRepository(_db).FindAsync(key, ct);
        return ente is { Mode: AtcUnitMode.OwnDocument } ? ente.DocumentId : null;
    }

    public async Task<string?> AuthAccCodeAsync(string key, CancellationToken ct = default) =>
        (await new EfAtcUnitRepository(_db).FindAsync(key, ct))?.AccCode
        ?? await _db.Sectors.AsNoTracking()
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

        if (doc.AtcUnit is not { Mode: AtcUnitMode.OwnDocument } ente) return false;
        managed = new ManagedDoc(ReleaseTargetType.App, doc.Title, ente.Code, ente.Acc?.Code,
            doc.Status == DocumentStatus.Published, hasDraft, doc.IsHidden,
            ReleaseTargetType.App, ente.Code, doc.Id);
        return true;
    }
}
