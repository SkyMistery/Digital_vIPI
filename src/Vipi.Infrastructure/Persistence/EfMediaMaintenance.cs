using Microsoft.EntityFrameworkCore;
using Vipi.Application.Media;
using Vipi.Domain;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Pulizia del deposito immagini su EF. Sa UNA cosa che nessun altro sa: <b>dove</b> può comparire il riferimento a
/// un'immagine. Sono quattro posti, e vanno guardati tutti — saltarne uno significa cancellare una foto ancora in
/// uso (docs/feature/2026-07-31-pulizia-immagini-orfane.md §1). Da qui passa anche la pulizia automatica alla
/// cancellazione di un blocco o di una sezione: e' lo stesso controllo, quindi eredita le stesse garanzie.
/// </summary>
public sealed class EfMediaMaintenance : IMediaMaintenance
{
    private readonly VipiDbContext _db;
    public EfMediaMaintenance(VipiDbContext db) => _db = db;

    public async Task<MediaUsageReport> AnalyzeAsync(CancellationToken ct = default)
    {
        var citati = await ReferencedShasAsync(ct);

        // I byte NON si caricano: pesano quanto le immagini stesse. Basta la lunghezza, che il DB calcola in casa.
        var assets = await _db.MediaAssets.AsNoTracking()
            .OrderByDescending(m => m.ByteSize)
            .Select(m => new { m.Sha256, m.OriginalFileName, m.ByteSize, m.CreatedUtc, m.CreatedByUserId })
            .ToListAsync(ct);

        var orfani = assets
            .Where(a => !citati.Contains(a.Sha256))
            .Select(a => new OrphanMedia(a.Sha256, a.OriginalFileName, a.ByteSize, a.CreatedUtc, a.CreatedByUserId))
            .ToList();

        return new MediaUsageReport(assets.Count, assets.Sum(a => (long)a.ByteSize), orfani);
    }

    public async Task<int> DeleteOrphansAsync(IReadOnlyList<string> sha256, CancellationToken ct = default)
    {
        var richiesti = sha256
            .Select(s => (s ?? "").Trim().ToLowerInvariant())
            .Where(s => s.Length == 64)
            .ToHashSet(StringComparer.Ordinal);
        if (richiesti.Count == 0) return 0;

        // Ricontrollo al momento della cancellazione: l'elenco in mano all'utente può avere minuti, e in mezzo una
        // pubblicazione o un incolla in bozza possono aver rimesso in uso proprio quello sha.
        var citati = await ReferencedShasAsync(ct);
        richiesti.ExceptWith(citati);
        if (richiesti.Count == 0) return 0;

        var daTogliere = await _db.MediaAssets.Where(m => richiesti.Contains(m.Sha256)).ToListAsync(ct);
        _db.MediaAssets.RemoveRange(daTogliere);
        await _db.SaveChangesAsync(ct);
        return daTogliere.Count;
    }

    public async Task<long> DocumentImageBytesAsync(int documentId, CancellationToken ct = default)
    {
        // Tutte le versioni del documento, non solo quella di lavoro: anche una versione archiviata tiene occupati
        // i suoi byte, e la quota deve dire la verita' su quanto pesa il documento nel suo insieme.
        var versioni = await _db.DocumentVersions.AsNoTracking()
            .Where(v => v.DocumentId == documentId)
            .Select(v => v.Id)
            .ToListAsync(ct);
        if (versioni.Count == 0) return 0;

        var json = await _db.ContentBlocks.AsNoTracking()
            .Where(b => versioni.Contains(b.DocumentVersionId) && b.Format == BlockFormat.Image && b.BodyJson != null)
            .Select(b => b.BodyJson)
            .ToListAsync(ct);

        var citati = MediaReferenceScanner.ScanAll(json);
        if (citati.Count == 0) return 0;

        // Somma sulle RIGHE, non sui riferimenti: la stessa foto usata in due blocchi occupa lo spazio una volta.
        return await _db.MediaAssets.AsNoTracking()
            .Where(m => citati.Contains(m.Sha256))
            .SumAsync(m => (long)m.ByteSize, ct);
    }

    /// <summary>Tutti gli sha citati da qualche parte. Un asset non elencato qui non serve più a nessuno.</summary>
    private async Task<HashSet<string>> ReferencedShasAsync(CancellationToken ct)
    {
        // 1) blocchi immagine di TUTTE le versioni — bozze comprese: è la foto che qualcuno sta scrivendo adesso.
        var daiBlocchi = await _db.ContentBlocks.AsNoTracking()
            .Where(b => b.Format == BlockFormat.Image && b.BodyJson != null)
            .Select(b => b.BodyJson)
            .ToListAsync(ct);

        // 2) sezioni extra d'aeroporto: i blocchi stanno serializzati dentro un campo solo.
        //    ⚠️ LEGACY: nessuno scrive più quelle righe (carta 2026-08-26), ma finché il trasloco one-shot non ha
        //    girato ovunque una foto può essere citata solo da lì — e cancellarla la farebbe sparire dal documento
        //    un attimo prima che qualcuno lo sposti. Questa riga se ne va con la tabella.
        var dagliExtra = await _db.AirportExtraSections.AsNoTracking()
            .Where(s => s.Body != null)
            .Select(s => s.Body)
            .ToListAsync(ct);

        // 3) payload delle release: le fotografie congelate dei documenti. Una vIPI dell'AIRAC scorso continua a
        //    citare lo sha, e se lo cancellassimo mostrerebbe un buco senza che nessuno se ne accorga.
        var dalleRelease = await _db.DocReleases.AsNoTracking()
            .Where(r => r.PayloadJson != null)
            .Select(r => r.PayloadJson)
            .ToListAsync(ct);

        // 4) blocchi condivisi: oggi NESSUNO li crea, ma il modello li prevede (ContentBlock.SharedBlockId) e hanno
        //    Format + BodyJson come i blocchi normali. Sono esattamente il «quarto posto» che rende pericolosa una
        //    pulizia automatica: costa una query guardarli adesso, costerebbe una foto persa scoprirli dopo.
        var daiCondivisi = await _db.SharedBlocks.AsNoTracking()
            .Where(s => s.BodyJson != null)
            .Select(s => s.BodyJson)
            .ToListAsync(ct);

        // NON si guarda l'audit log: registra che cosa è successo, non che cosa si mostra. Se citasse uno sha
        // cancellato resterebbe una traccia storica con un riferimento morto — nessun documento si rompe.
        return MediaReferenceScanner.ScanAll(
            daiBlocchi.Concat(dagliExtra).Concat(dalleRelease).Concat(daiCondivisi));
    }
}
