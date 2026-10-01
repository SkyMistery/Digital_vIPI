using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// EF del pacchetto dell'evento (vedi <see cref="EventKit"/>). ⚠️ Un pacchetto SOLO: si legge sempre il primo per Id,
/// e si crea al primo salvataggio.
/// </summary>
public sealed class EfEventKitRepository : IEventKitRepository
{
    private readonly VipiDbContext _db;
    public EfEventKitRepository(VipiDbContext db) => _db = db;

    /// <summary>
    /// Salva, e se il database rifiuta lascia il contesto PULITO: il pannello dello staff prende questo archivio dal
    /// circuito, e le righe di un salvataggio fallito resterebbero lì a far cadere anche i successivi (U-048).
    /// </summary>
    private async Task SalvaAsync(CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<EventKitSnapshot?> LoadAsync(CancellationToken ct = default)
    {
        var testata = await _db.EventKits.AsNoTracking().OrderBy(k => k.Id).FirstOrDefaultAsync(ct);
        if (testata is null) return null;

        // ⚠️ Una proiezione e non un Include: i byte non escono di qui, o ogni lettura della pagina si porterebbe in
        // memoria tutti i file del pacchetto.
        var voci = await _db.EventKitItems.AsNoTracking()
            .Where(v => v.EventKitId == testata.Id)
            .OrderBy(v => v.SortOrder).ThenBy(v => v.Id)
            .Select(v => new EventKitItemRow(v.Id, v.Label, v.Note, v.Kind, v.Url, v.FileName, v.ByteSize, v.SortOrder,
                v.CreatedUtc, v.CreatedByName))
            .ToListAsync(ct);
        return new EventKitSnapshot(testata, voci);
    }

    public async Task SaveVidAsync(string? testo, DateTime? svuotaUtc, int userId, string userName, DateTime adessoUtc,
        CancellationToken ct = default)
    {
        var kit = await PacchettoAsync(ct);
        kit.VidEvento = string.IsNullOrWhiteSpace(testo) ? null : testo.Trim();
        kit.VidSvuotaUtc = svuotaUtc;
        kit.UpdatedUtc = adessoUtc;
        kit.UpdatedByUserId = userId;
        kit.UpdatedByName = userName;
        await SalvaAsync(ct);
    }

    public async Task<bool> ClearVidAsync(CancellationToken ct = default)
    {
        var kit = await _db.EventKits.OrderBy(k => k.Id).FirstOrDefaultAsync(ct);
        if (kit is null || (kit.VidEvento is null && kit.VidSvuotaUtc is null)) return false;
        kit.VidEvento = null;
        kit.VidSvuotaUtc = null;
        await SalvaAsync(ct);
        return true;
    }

    public async Task SaveHeaderAsync(string nome, bool attivo, DateTime? daUtc, DateTime? aUtc, int userId,
        string userName, DateTime adessoUtc, CancellationToken ct = default)
    {
        var kit = await PacchettoAsync(ct);
        kit.Name = nome;
        kit.IsActive = attivo;
        kit.StartsUtc = daUtc;
        kit.EndsUtc = aUtc;
        kit.UpdatedUtc = adessoUtc;
        kit.UpdatedByUserId = userId;
        kit.UpdatedByName = userName;
        await SalvaAsync(ct);
    }

    public async Task<int> AddItemAsync(EventKitItem voce, CancellationToken ct = default)
    {
        var kit = await PacchettoAsync(ct);
        if (kit.Id == 0) await SalvaAsync(ct);   // appena nato: serve il suo Id

        var ultimo = await _db.EventKitItems.Where(v => v.EventKitId == kit.Id)
            .Select(v => (int?)v.SortOrder).MaxAsync(ct);
        voce.EventKitId = kit.Id;
        voce.SortOrder = (ultimo ?? 0) + 1;
        _db.EventKitItems.Add(voce);
        await SalvaAsync(ct);
        return voce.Id;
    }

    public async Task<int> CountItemsAsync(CancellationToken ct = default) =>
        await _db.EventKitItems.CountAsync(ct);

    public async Task<bool> DeleteItemAsync(int id, CancellationToken ct = default)
    {
        var voce = await _db.EventKitItems.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (voce is null) return false;
        _db.EventKitItems.Remove(voce);
        await SalvaAsync(ct);
        return true;
    }

    public async Task<bool> MoveItemAsync(int id, int verso, CancellationToken ct = default)
    {
        if (verso == 0) return false;
        // Le sole colonne dell'ordine, senza byte: si scambiano due numeri.
        var voci = await _db.EventKitItems
            .Select(v => new { v.Id, v.SortOrder })
            .OrderBy(v => v.SortOrder).ThenBy(v => v.Id)
            .ToListAsync(ct);
        var i = voci.FindIndex(v => v.Id == id);
        var j = i + Math.Sign(verso);
        if (i < 0 || j < 0 || j >= voci.Count) return false;

        // Si rinumera tutto 1..n: se due voci avessero lo stesso ordine, uno scambio di valori non le muoverebbe.
        var ordine = voci.Select(v => v.Id).ToList();
        (ordine[i], ordine[j]) = (ordine[j], ordine[i]);
        var righe = await _db.EventKitItems.Where(v => ordine.Contains(v.Id)).ToListAsync(ct);
        foreach (var r in righe) r.SortOrder = ordine.IndexOf(r.Id) + 1;
        await SalvaAsync(ct);
        return true;
    }

    public async Task<EventKitFile?> FileAsync(int id, CancellationToken ct = default)
    {
        var voce = await _db.EventKitItems.AsNoTracking()
            .Where(v => v.Id == id && v.Kind == EventKitItemKind.File)
            .Select(v => new { v.FileName, v.Bytes })
            .FirstOrDefaultAsync(ct);
        return voce?.Bytes is { } b ? new EventKitFile(voce.FileName, b) : null;
    }

    /// <summary>Il pacchetto da scrivere: quello che c'è, o uno nuovo spento e senza nome (aggiunto al contesto).</summary>
    private async Task<EventKit> PacchettoAsync(CancellationToken ct)
    {
        var kit = await _db.EventKits.OrderBy(k => k.Id).FirstOrDefaultAsync(ct);
        if (kit is not null) return kit;
        kit = new EventKit();
        _db.EventKits.Add(kit);
        return kit;
    }
}
