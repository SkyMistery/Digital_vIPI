using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>EF: stato di freschezza degli import periodici (una riga per categoria).</summary>
public sealed class EfImportStateStore : IImportStateStore
{
    private readonly VipiDbContext _db;
    public EfImportStateStore(VipiDbContext db) => _db = db;

    public async Task<DateTime?> GetLastSuccessAsync(string category, CancellationToken ct = default)
    {
        var row = await _db.ImportStates.AsNoTracking().FirstOrDefaultAsync(x => x.Category == category, ct);
        return row?.LastSuccessUtc;
    }

    public async Task<DateTime?> GetPrevSuccessAsync(string category, CancellationToken ct = default)
    {
        var row = await _db.ImportStates.AsNoTracking().FirstOrDefaultAsync(x => x.Category == category, ct);
        return row?.PrevSuccessUtc;
    }

    public async Task MarkSuccessAsync(string category, DateTime utc, CancellationToken ct = default)
    {
        var row = await _db.ImportStates.FirstOrDefaultAsync(x => x.Category == category, ct);
        if (row is null) { row = new ImportState { Category = category }; _db.ImportStates.Add(row); }

        // Il penultimo scorre solo se fra i due giri è passato abbastanza: due clic di fila sul bottone di
        // re-import non devono «consumare» le due conferme che autorizzano un'eliminazione.
        // ⚠️ La riga appena creata ha LastSuccessUtc a default(DateTime), che non è un giro: il penultimo
        // resta null finché non ce ne sono davvero due.
        if (row.LastSuccessUtc != default && SogliaEliminazione.IlPenultimoScorre(row.LastSuccessUtc, utc))
            row.PrevSuccessUtc = row.LastSuccessUtc;

        row.LastSuccessUtc = utc;
        row.LastAttemptUtc = utc;
        row.LastError = null;               // l'ultimo tentativo è riuscito: azzera l'errore precedente
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkFailureAsync(string category, DateTime utc, string error, CancellationToken ct = default)
    {
        var row = await _db.ImportStates.FirstOrDefaultAsync(x => x.Category == category, ct);
        if (row is null) { row = new ImportState { Category = category }; _db.ImportStates.Add(row); }
        row.LastAttemptUtc = utc;
        row.LastError = error;              // LastSuccessUtc resta invariato (l'ultimo successo storico)
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ImportState>> GetAllAsync(CancellationToken ct = default) =>
        await _db.ImportStates.AsNoTracking().OrderBy(x => x.Category).ToListAsync(ct);
}
