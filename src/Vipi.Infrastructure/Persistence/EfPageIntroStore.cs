using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// EF: l'intro di pagina, in una riga di <see cref="SharedBlock"/>. Le regole stanno su
/// <see cref="IPageIntroStore"/>; qui c'è come si applicano.
///
/// <para>⚠️ <b>La riga si crea sola al primo salvataggio</b> e si <b>cancella</b> quando l'intro resta senza
/// sezioni. Lasciare in giro una riga con <c>BodyJson</c> nullo vorrebbe dire due modi di essere vuota, e
/// prima o poi qualcuno ne gestisce uno solo.</para>
/// </summary>
public sealed class EfPageIntroStore : IPageIntroStore
{
    private readonly VipiDbContext _db;
    private readonly IEditAuthorizationService _authz;
    private readonly IResourceLockService _locks;

    private readonly Vipi.Application.Media.IMediaMaintenance? _media;

    /// <param name="media">La pulizia delle immagini che l'intro smette di citare (gemello di U-137). Opzionale:
    /// senza, come prima.</param>
    public EfPageIntroStore(VipiDbContext db, IEditAuthorizationService authz, IResourceLockService locks,
        Vipi.Application.Media.IMediaMaintenance? media = null)
    {
        _media = media;
        _db = db;
        _authz = authz;
        _locks = locks;
    }

    public async Task<IReadOnlyList<PageIntroSection>> LeggiAsync(string pagina, CancellationToken ct = default)
    {
        var chiave = PageIntro.Chiave(pagina);
        var json = await _db.SharedBlocks.AsNoTracking()
            .Where(b => b.Key == chiave)
            .Select(b => b.BodyJson)
            .FirstOrDefaultAsync(ct);

        return PageIntro.Parse(json);
    }

    public async Task SalvaAsync(string pagina, IReadOnlyList<PageIntroSection> sezioni, string etichetta,
        CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        // ⚠️ Il lock si VERIFICA qui, non solo nella barra che lo prende (U-109, gemello di T-025): la riga si
        // riscrive per intero, e chi l'ha perso coprirebbe il lavoro di chi ce l'ha adesso.
        await _locks.EnsureHeldAsync(PageIntro.ChiaveLock(pagina), ct);

        var chiave = PageIntro.Chiave(pagina);
        var json = PageIntro.Serialize(sezioni);
        var riga = await _db.SharedBlocks.FirstOrDefaultAsync(b => b.Key == chiave, ct);
        // Le foto che l'intro citava prima (gemello di U-137): quelle che non cita più si liberano dopo il salvataggio.
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

        riga.Title = Etichetta(etichetta, pagina);
        // ⚠️ `Prose` è un valore che nessuno legge: l'intro porta blocchi di formati diversi, e il formato
        // di ognuno sta DENTRO il JSON. Una colonna che dichiarasse un formato per tutti mentirebbe.
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

    /// <summary>L'etichetta, con un tetto di ragionevolezza. Vuota → la chiave, che per chi guarda la tabella
    /// è meglio di niente.</summary>
    private static string Etichetta(string? etichetta, string pagina)
    {
        var t = (etichetta ?? "").Trim();
        if (t.Length == 0) t = PageIntro.Chiave(pagina);
        return t.Length <= 200 ? t : t[..200];
    }
}
