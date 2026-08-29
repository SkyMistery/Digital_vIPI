using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// EF: la biblioteca degli allegati. Le regole che fa rispettare stanno scritte su
/// <see cref="IAttachmentLibrary"/>; qui c'è come si applicano.
/// </summary>
public sealed class EfAttachmentLibrary : IAttachmentLibrary
{
    private readonly VipiDbContext _db;

    public EfAttachmentLibrary(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<AttachmentRow>> ListAsync(CancellationToken ct = default)
    {
        // Le versioni servono tutte, ma solo per due numeri: la corrente e quante sono state. Il carico è
        // quello di una biblioteca di divisione — decine di voci, non decine di migliaia.
        var voci = await _db.Attachments.AsNoTracking()
            .Include(a => a.Versions)
            .OrderBy(a => a.Title)
            .ToListAsync(ct);

        return voci.Select(Riga).Where(r => r is not null).Select(r => r!).ToList();
    }

    public async Task<AttachmentRow?> BySlugAsync(string slug, CancellationToken ct = default)
    {
        var chiave = AttachmentRules.Norm(slug).ToLowerInvariant();
        var voce = await _db.Attachments.AsNoTracking()
            .Include(a => a.Versions)
            .FirstOrDefaultAsync(a => a.Slug == chiave, ct);
        return voce is null ? null : Riga(voce);
    }

    public async Task<(AttachmentCreate Esito, AttachmentRow? Riga)> CreateAsync(
        AttachmentDraft draft, int userId, CancellationToken ct = default)
    {
        var slug = AttachmentRules.Norm(draft.Slug).ToLowerInvariant();
        if (!AttachmentRules.SlugValido(slug)) return (AttachmentCreate.SlugNonValido, null);

        var titolo = AttachmentRules.Norm(draft.Title);
        if (titolo.Length == 0) return (AttachmentCreate.TitoloMancante, null);

        if (!AttachmentRules.ScopeValido(draft.Scope, draft.ScopeKey)) return (AttachmentCreate.AmbitoNonValido, null);

        var externalId = AttachmentRules.ExternalIdDa(draft.Link);
        if (externalId is null) return (AttachmentCreate.LinkNonValido, null);

        // ⚠️ Si guarda prima, ma l'indice unico resta il vero guardiano: fra questa lettura e la scrittura
        // può passare un altro salvataggio. Qui si guarda per poter DIRE quale delle due cose è andata
        // storta, non per garantirla.
        if (await _db.Attachments.AnyAsync(a => a.Slug == slug, ct)) return (AttachmentCreate.SlugOccupato, null);

        var ora = DateTime.UtcNow;
        var voce = new Attachment
        {
            Slug = slug,
            Title = titolo,
            Kind = draft.Kind,
            Scope = draft.Scope,
            ScopeKey = AttachmentRules.ScopeKeyNorm(draft.Scope, draft.ScopeKey),
            Notes = AttachmentRules.Norm(draft.Notes) is { Length: > 0 } n ? n : null,
            CreatedUtc = ora,
            CreatedByUserId = userId,
            UpdatedUtc = ora,
            UpdatedByUserId = userId,
        };

        // La v1 nasce insieme alla voce: lo stato «voce senza file» non esiste (vedi IAttachmentLibrary).
        voce.Versions.Add(new AttachmentVersion
        {
            Number = 1,
            Provider = AttachmentProvider.Drive,
            ExternalId = externalId,
            CreatedUtc = ora,
            CreatedByUserId = userId,
        });

        _db.Attachments.Add(voce);
        AuditScribe.Write(_db, userId, AuditAction.Create, "Attachment", slug,
            new { Titolo = titolo, Tipo = draft.Kind.ToString(), Ambito = voce.ScopeKey ?? draft.Scope.ToString() });
        await _db.SaveChangesAsync(ct);

        return (AttachmentCreate.Ok, Riga(voce));
    }

    public async Task<(AttachmentReplace Esito, AttachmentRow? Riga)> ReplaceAsync(
        string slug, string link, string? note, int userId, CancellationToken ct = default)
    {
        var chiave = AttachmentRules.Norm(slug).ToLowerInvariant();

        // ⚠️ Tracciata, non AsNoTracking: qui si scrive. Con le versioni incluse, perché il progressivo è
        // «il più alto più uno» e leggerlo altrove vorrebbe dire una seconda query che può già mentire.
        var voce = await _db.Attachments.Include(a => a.Versions).FirstOrDefaultAsync(a => a.Slug == chiave, ct);
        if (voce is null) return (AttachmentReplace.NonTrovata, null);

        var externalId = AttachmentRules.ExternalIdDa(link);
        if (externalId is null) return (AttachmentReplace.LinkNonValido, null);

        var corrente = voce.Versions.OrderByDescending(v => v.Number).FirstOrDefault();

        // Il non-evento non si registra: una versione identica alla corrente manderebbe delle persone a
        // rileggere un documento che non è cambiato. Stessa regola dell'anagrafica radioassistenze.
        if (corrente is not null && corrente.ExternalId == externalId) return (AttachmentReplace.Invariato, null);

        var ora = DateTime.UtcNow;
        var nuova = new AttachmentVersion
        {
            Number = (corrente?.Number ?? 0) + 1,
            Provider = AttachmentProvider.Drive,
            ExternalId = externalId,
            Note = AttachmentRules.Norm(note) is { Length: > 0 } n ? n : null,
            CreatedUtc = ora,
            CreatedByUserId = userId,
        };
        voce.Versions.Add(nuova);
        voce.UpdatedUtc = ora;
        voce.UpdatedByUserId = userId;

        // Il registro porta il valore VECCHIO e quello nuovo: «Tizio ha sostituito la LoA» non permette né di
        // accorgersene né di rimettere a posto.
        AuditScribe.Write(_db, userId, AuditAction.Update, "Attachment", chiave, new
        {
            Versione = nuova.Number,
            Precedente = corrente?.Number,
            IdVecchio = corrente?.ExternalId,
            IdNuovo = externalId,
            Nota = nuova.Note,
        });

        await _db.SaveChangesAsync(ct);
        return (AttachmentReplace.Ok, Riga(voce));
    }

    /// <summary>
    /// La voce più la sua versione corrente, che è quella col <c>Number</c> più alto.
    /// <para>Torna <c>null</c> per una voce senza nemmeno una versione: non dovrebbe esistere — nascono
    /// insieme — ma una riga che non sa dire dove stanno i byte non si mostra come se lo sapesse.</para>
    /// </summary>
    private static AttachmentRow? Riga(Attachment a)
    {
        var corrente = a.Versions.OrderByDescending(v => v.Number).FirstOrDefault();
        if (corrente is null) return null;

        return new AttachmentRow(
            a.Id, a.Slug, a.Title, a.Kind, a.Scope, a.ScopeKey, a.Notes,
            corrente.Number, a.Versions.Count, corrente.Provider, corrente.ExternalId,
            a.UpdatedUtc, corrente.CreatedUtc);
    }
}
