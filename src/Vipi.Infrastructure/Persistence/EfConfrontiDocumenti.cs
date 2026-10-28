using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>I riassunti dei confronti per la pagina Documenti (S133, <see cref="DocumentComparison"/>).</summary>
public sealed class EfConfrontiDocumenti : IConfrontiDocumentiRepository
{
    private readonly VipiDbContext _db;

    public EfConfrontiDocumenti(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<int, ConfrontoSalvato>> TuttiAsync(CancellationToken ct = default) =>
        (await _db.DocumentComparisons.AsNoTracking().ToListAsync(ct))
            .ToDictionary(c => c.DocumentId, c => new ConfrontoSalvato(c.DocumentId, c.ComputedUtc, c.EffectiveCycle,
                c.ChangedVsEffective, c.ScheduledCycle, c.ChangedVsScheduled, c.ScheduledDerivedChanged));

    public async Task ScriviAsync(IReadOnlyList<ConfrontoSalvato> righe, CancellationToken ct = default)
    {
        if (righe.Count == 0) return;
        var ids = righe.Select(r => r.DocumentId).Distinct().ToList();
        // ⚠️ Solo i documenti che esistono: fra il calcolo e la scrittura uno può essere stato eliminato, e la chiave
        // esterna farebbe cadere tutto il salvataggio — cioè il giro intero — per una riga che non serve più.
        var esistono = (await _db.Documents.Where(d => ids.Contains(d.Id)).Select(d => d.Id).ToListAsync(ct)).ToHashSet();
        var gia = await _db.DocumentComparisons.Where(c => ids.Contains(c.DocumentId)).ToDictionaryAsync(c => c.DocumentId, ct);
        foreach (var r in righe.Where(r => esistono.Contains(r.DocumentId)))
        {
            if (!gia.TryGetValue(r.DocumentId, out var riga))
            {
                riga = new DocumentComparison { DocumentId = r.DocumentId };
                _db.DocumentComparisons.Add(riga);
                gia[r.DocumentId] = riga;
            }
            riga.ComputedUtc = r.CalcolatoUtc;
            riga.EffectiveCycle = r.CicloInVigore;
            riga.ChangedVsEffective = r.VsPubblico;
            riga.ScheduledCycle = r.CicloProgrammata;
            riga.ChangedVsScheduled = r.VsProgrammata;
            riga.ScheduledDerivedChanged = r.DerivateProgrammata;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task TogliAltriAsync(IReadOnlyCollection<int> tenuti, CancellationToken ct = default)
    {
        var via = await _db.DocumentComparisons.Where(c => !tenuti.Contains(c.DocumentId)).ToListAsync(ct);
        if (via.Count == 0) return;
        _db.DocumentComparisons.RemoveRange(via);
        await _db.SaveChangesAsync(ct);
    }
}
