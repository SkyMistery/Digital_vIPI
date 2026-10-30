using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Le sezioni dichiarate facoltative (S133, <see cref="OptionalSection"/>).</summary>
public sealed class EfSezioniFacoltative : ISezioniFacoltativeStore
{
    private readonly VipiDbContext _db;

    public EfSezioniFacoltative(VipiDbContext db) => _db = db;

    public async Task<IReadOnlySet<(SectionProfile Profilo, string Chiave)>> TutteAsync(CancellationToken ct = default)
    {
        var righe = await _db.OptionalSections.AsNoTracking().Select(o => new { o.Profile, o.SectionKey }).ToListAsync(ct);
        // ⚠️ Un profilo che questa versione non conosce (una riga scritta da un ramo più nuovo) si salta: non si legge
        // come un altro profilo, e non fa cadere il conto delle sezioni.
        return righe
            .Where(r => Enum.TryParse<SectionProfile>(r.Profile, out _))
            .Select(r => (Enum.Parse<SectionProfile>(r.Profile), r.SectionKey))
            .ToHashSet();
    }

    public async Task ImpostaAsync(SectionProfile profilo, string chiave, bool facoltativa, int userId,
        CancellationToken ct = default)
    {
        var nome = profilo.ToString();
        var gia = await _db.OptionalSections.FirstOrDefaultAsync(o => o.Profile == nome && o.SectionKey == chiave, ct);
        if (facoltativa && gia is null)
            _db.OptionalSections.Add(new OptionalSection
            {
                Profile = nome, SectionKey = chiave, SetUtc = DateTime.UtcNow, SetByUserId = userId,
            });
        else if (!facoltativa && gia is not null)
            _db.OptionalSections.Remove(gia);
        else
            return;
        await _db.SaveChangesAsync(ct);
    }
}
