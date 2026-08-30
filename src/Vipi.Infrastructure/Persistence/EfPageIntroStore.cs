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

    public EfPageIntroStore(VipiDbContext db, IEditAuthorizationService authz)
    {
        _db = db;
        _authz = authz;
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

        var chiave = PageIntro.Chiave(pagina);
        var json = PageIntro.Serialize(sezioni);
        var riga = await _db.SharedBlocks.FirstOrDefaultAsync(b => b.Key == chiave, ct);

        if (json is null)
        {
            if (riga is not null) _db.SharedBlocks.Remove(riga);
            await _db.SaveChangesAsync(ct);
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
