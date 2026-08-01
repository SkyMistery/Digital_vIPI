using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Media;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Deposito immagini su database (ADR-0007: lo stato dell'app sta tutto nel DB — backup, redeploy e copia del DB
/// se lo portano dietro senza un secondo servizio da tenere in vita).
/// <para>
/// Content-addressed: la chiave è lo sha256 del contenuto, quindi lo stesso file caricato due volte è una riga sola e
/// l'URL pubblico è immutabile per costruzione. Le righe non si aggiornano e non si cancellano: uno snapshot di
/// release già pubblicato cita lo sha.
/// </para>
/// </summary>
public sealed class EfMediaStore : IMediaStore
{
    private readonly VipiDbContext _db;
    private readonly ICurrentUserProvider _user;
    private readonly MediaOptions _options;

    public EfMediaStore(VipiDbContext db, ICurrentUserProvider user, IOptions<MediaOptions> options)
    {
        _db = db;
        _user = user;
        _options = options.Value;
    }

    public async Task<StoredMedia> SaveAsync(Stream content, string? originalFileName, CancellationToken ct = default)
    {
        var bytes = await ReadCappedAsync(content, ct);
        var info = MediaValidator.Validate(bytes, _options);

        // ToHexString+ToLowerInvariant e non ToHexStringLower: quest'ultimo è .NET 9+ e il modulo
        // multi-targetta net8.0 per l'embedding nel sito Ivao.It. Stesso output, stesso sha in DB.
        var sha = Convert.ToHexString(SHA256.HashData(bytes.Span)).ToLowerInvariant();

        // Già presente: nessuna riga nuova, nessun byte riscritto. Il contenuto è l'identità.
        var existing = await _db.MediaAssets.AsNoTracking()
            .Where(m => m.Sha256 == sha)
            .Select(m => new StoredMedia(m.Sha256, m.ContentType, m.Width, m.Height, m.ByteSize))
            .FirstOrDefaultAsync(ct);
        if (existing is not null) return existing;

        _db.MediaAssets.Add(new MediaAsset
        {
            Sha256 = sha,
            ContentType = info.ContentType,
            ByteSize = bytes.Length,
            Width = info.Width,
            Height = info.Height,
            Bytes = bytes.ToArray(),
            OriginalFileName = Trim(originalFileName),
            CreatedUtc = DateTime.UtcNow,
            CreatedByUserId = _user.Get()?.UserId ?? 0,
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Corsa fra due caricamenti dello stesso file: l'indice unico su Sha256 fa vincere il primo, e per noi
            // va benissimo — il contenuto è identico per definizione.
            _db.ChangeTracker.Clear();
            if (!await _db.MediaAssets.AnyAsync(m => m.Sha256 == sha, ct)) throw;
        }

        return new StoredMedia(sha, info.ContentType, info.Width, info.Height, bytes.Length);
    }

    public async Task<MediaContent?> GetAsync(string sha256, CancellationToken ct = default)
    {
        var sha = (sha256 ?? "").Trim().ToLowerInvariant();
        if (sha.Length != 64 || !sha.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return null;

        return await _db.MediaAssets.AsNoTracking()
            .Where(m => m.Sha256 == sha)
            .Select(m => new MediaContent(m.Sha256, m.ContentType, m.OriginalFileName, m.Bytes))
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Legge lo stream fermandosi UN byte oltre il limite: così un input enorme non entra mai in memoria per intero,
    /// e il byte in più basta a distinguere "al limite" da "oltre il limite" (il messaggio lo dà il validatore).
    /// </summary>
    private async Task<ReadOnlyMemory<byte>> ReadCappedAsync(Stream content, CancellationToken ct)
    {
        var cap = _options.MaxUploadBytes + 1;
        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length >= cap) break;
        }
        return buffer.GetBuffer().AsMemory(0, (int)buffer.Length);
    }

    private static string? Trim(string? name)
    {
        var n = name?.Trim();
        if (string.IsNullOrEmpty(n)) return null;
        return n.Length <= 200 ? n : n[^200..];
    }
}
