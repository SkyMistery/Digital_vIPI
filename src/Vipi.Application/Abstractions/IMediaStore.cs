using Vipi.Application.Media;

namespace Vipi.Application.Abstractions;

/// <summary>Immagine salvata: quel che serve al blocco per citarla (docs/feature/2026-07-31-immagini-nei-blocchi §2).</summary>
public sealed record StoredMedia(string Sha256, string ContentType, int Width, int Height, int ByteSize);

/// <summary>Contenuto servito all'utente dall'endpoint pubblico.</summary>
public sealed record MediaContent(string Sha256, string ContentType, string? FileName, byte[] Bytes);

/// <summary>
/// Deposito delle immagini dei documenti. Esiste come porta perché i byte oggi stanno nel DB e domani potrebbero
/// stare altrove (object storage): editor e viewer parlano solo di uno <c>sha256</c>, quindi il trasloco è una
/// registrazione DI diversa e nient'altro.
/// <para>Implementazioni: salvano il contenuto UNA volta per sha (stesso file = stessa riga) e non lo modificano mai
/// — le release pubblicate citano lo sha e devono continuare a risolverlo.</para>
/// </summary>
public interface IMediaStore
{
    /// <summary>Valida e salva (o riconosce come già presente) l'immagine letta da <paramref name="content"/>.</summary>
    /// <exception cref="Vipi.Application.Aor.ValidationException">File troppo grande, non immagine, o fuori misura.</exception>
    Task<StoredMedia> SaveAsync(Stream content, string? originalFileName, CancellationToken ct = default);

    /// <summary>Contenuto per lo sha dato, o null se non esiste.</summary>
    Task<MediaContent?> GetAsync(string sha256, CancellationToken ct = default);
}
