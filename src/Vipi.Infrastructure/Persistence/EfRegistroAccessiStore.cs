using Microsoft.EntityFrameworkCore;
using Vipi.Application.Auth;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Il registro di chi è entrato nel sito. Carta <c>docs/feature/2026-09-30-registro-accessi.md</c>.</summary>
public sealed class EfRegistroAccessiStore : IRegistroAccessiStore
{
    private readonly VipiDbContext _db;
    public EfRegistroAccessiStore(VipiDbContext db) => _db = db;

    public async Task RegistraAsync(int userId, string nome, string? divisione, string? acc, DateTime oraUtc,
                                    string? nomeBreve = null, CancellationToken ct = default)
    {
        var riga = await _db.AccessiAlSito.FirstOrDefaultAsync(a => a.UserId == userId, ct).ConfigureAwait(false);
        if (riga is null)
        {
            riga = new AccessoAlSito { UserId = userId };
            _db.AccessiAlSito.Add(riga);
        }
        riga.Registra(nome, divisione, acc, oraUtc, nomeBreve);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<int, string>> NomiBreviAsync(IReadOnlyCollection<int> userIds,
                                                                     CancellationToken ct = default)
    {
        var ids = userIds.Distinct().ToList();
        var righe = await _db.AccessiAlSito.AsNoTracking()
            .Where(a => ids.Contains(a.UserId) && a.NomeBreve != null)
            .Select(a => new { a.UserId, a.NomeBreve })
            .ToListAsync(ct).ConfigureAwait(false);
        return righe.ToDictionary(r => r.UserId, r => r.NomeBreve!);
    }

    public Task<AccessoAlSitoRiga?> TrovaAsync(int userId, CancellationToken ct = default) =>
        _db.AccessiAlSito.AsNoTracking().Where(a => a.UserId == userId)
            .Select(a => new AccessoAlSitoRiga(a.UserId, a.Nome, a.Divisione, a.Acc, a.PrimoUtc, a.UltimoUtc, a.Giorni))
            .FirstOrDefaultAsync(ct);

    public Task<int> PotaAsync(DateTime ultimoPrimaDi, CancellationToken ct = default) =>
        _db.AccessiAlSito.Where(a => a.UltimoUtc < ultimoPrimaDi).ExecuteDeleteAsync(ct);

    public async Task<ElencoAccessi> ElencoAsync(string? cerca, int limite, CancellationToken ct = default)
    {
        var q = _db.AccessiAlSito.AsNoTracking();
        var testo = cerca?.Trim();
        if (!string.IsNullOrEmpty(testo))
        {
            // Un numero è un VID (anche solo l'inizio); altrimenti si cerca nel nome, senza badare alle maiuscole.
            if (testo.All(char.IsDigit))
                q = q.Where(a => a.UserId.ToString().StartsWith(testo));
            else
            {
                var basso = testo.ToLower();
                q = q.Where(a => a.Nome.ToLower().Contains(basso));
            }
        }

        var totale = await q.CountAsync(ct).ConfigureAwait(false);
        var righe = await q.OrderByDescending(a => a.UltimoUtc).ThenBy(a => a.UserId)
            .Take(limite)
            .Select(a => new AccessoAlSitoRiga(a.UserId, a.Nome, a.Divisione, a.Acc, a.PrimoUtc, a.UltimoUtc, a.Giorni))
            .ToListAsync(ct).ConfigureAwait(false);
        return new ElencoAccessi(righe, totale);
    }
}
