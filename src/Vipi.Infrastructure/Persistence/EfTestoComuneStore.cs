using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// EF: il testo comune di una sezione (S127), in una riga di <see cref="SharedBlock"/>. Le regole stanno su
/// <see cref="ITestoComuneStore"/>; qui c'è come si applicano, come per le intro (<see cref="EfPageIntroStore"/>).
///
/// <para>⚠️ La riga si crea al primo salvataggio e si <b>cancella</b> quando il testo resta senza blocchi: un solo modo
/// di essere vuoto.</para>
/// </summary>
public sealed class EfTestoComuneStore : ITestoComuneStore
{
    private readonly VipiDbContext _db;
    private readonly IEditAuthorizationService _authz;
    private readonly IResourceLockService _locks;
    private readonly Vipi.Application.Media.IMediaMaintenance? _media;

    /// <param name="media">La pulizia delle immagini che il testo smette di citare. Opzionale, come per le intro.</param>
    public EfTestoComuneStore(VipiDbContext db, IEditAuthorizationService authz, IResourceLockService locks,
        Vipi.Application.Media.IMediaMaintenance? media = null)
    {
        _db = db;
        _authz = authz;
        _locks = locks;
        _media = media;
    }

    public async Task<IReadOnlyList<ExtraBlock>> LeggiAsync(string sezione, CancellationToken ct = default)
    {
        var chiave = TestoComune.Chiave(sezione);
        var json = await _db.SharedBlocks.AsNoTracking()
            .Where(b => b.Key == chiave)
            .Select(b => b.BodyJson)
            .FirstOrDefaultAsync(ct);
        return TestoComune.Parse(json);
    }

    public async Task SalvaAsync(string sezione, IReadOnlyList<ExtraBlock> blocchi, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        await _locks.EnsureHeldAsync(TestoComune.ChiaveLock(sezione), ct);

        var chiave = TestoComune.Chiave(sezione);
        var json = TestoComune.Serialize(blocchi);
        var riga = await _db.SharedBlocks.FirstOrDefaultAsync(b => b.Key == chiave, ct);
        var prima = Vipi.Application.Media.MediaReferenceScanner.ScanAll(new[] { riga?.BodyJson });

        if (json is null)
        {
            if (riga is not null) _db.SharedBlocks.Remove(riga);
            await _db.SaveChangesAsync(ct);
            await LiberaAsync(prima, null, ct);
            return;
        }

        if (riga is null)
        {
            riga = new SharedBlock { Key = chiave };
            _db.SharedBlocks.Add(riga);
        }

        // Per chi un giorno guarderà la tabella: di che cosa è questa riga.
        riga.Title = "Testo comune: " + sezione.Trim().ToLowerInvariant();
        // ⚠️ Un valore che nessuno legge: il formato di ogni blocco sta DENTRO il JSON (come per le intro).
        riga.Format = BlockFormat.Prose;
        riga.BodyJson = json;
        riga.Body = null;

        await _db.SaveChangesAsync(ct);
        await LiberaAsync(prima, json, ct);
    }

    /// <summary>Ripassa dalla pulizia vera, che ricontrolla TUTTI i posti: una foto citata anche altrove resta.</summary>
    private async Task LiberaAsync(HashSet<string> prima, string? dopo, CancellationToken ct)
    {
        if (_media is null || prima.Count == 0) return;
        prima.ExceptWith(Vipi.Application.Media.MediaReferenceScanner.ScanAll(new[] { dopo }));
        if (prima.Count > 0) await _media.DeleteOrphansAsync(prima.ToList(), ct);
    }
}
