namespace Vipi.Domain.Entities;

/// <summary>
/// Immagine caricata da uno staffista e citata da un blocco <c>Image</c> (docs/feature/2026-07-31-immagini-nei-blocchi).
/// <para>
/// Due invarianti reggono tutto il resto:
/// <list type="bullet">
/// <item><b>Content-addressed</b>: l'identità è <see cref="Sha256"/>, cioè il contenuto. Lo stesso file caricato due
/// volte è una riga sola, e l'URL pubblico può essere servito come <c>immutable</c> senza rischio di stantio.</item>
/// <item><b>Mai modificata, mai cancellata dall'editing</b>: uno snapshot di release già pubblicato cita lo sha, quindi
/// eliminare il blocco (o l'intero documento) non deve togliere i byte da sotto una release.</item>
/// </list>
/// </para>
/// </summary>
public class MediaAsset
{
    public int Id { get; set; }

    /// <summary>SHA-256 esadecimale minuscolo del contenuto: identità dell'asset e chiave dell'URL pubblico.</summary>
    public string Sha256 { get; set; } = "";

    /// <summary>Tipo MIME dedotto dai BYTE (mai da quel che dichiara il browser).</summary>
    public string ContentType { get; set; } = "";

    public int ByteSize { get; set; }

    /// <summary>Dimensioni in pixel: servono all'<c>&lt;img&gt;</c> per non far ballare il layout durante il caricamento.</summary>
    public int Width { get; set; }
    public int Height { get; set; }

    public byte[] Bytes { get; set; } = Array.Empty<byte>();

    /// <summary>Nome del file scelto dall'utente: solo diagnostica e <c>Content-Disposition</c>, non identità.</summary>
    public string? OriginalFileName { get; set; }

    public DateTime CreatedUtc { get; set; }
    public int CreatedByUserId { get; set; }
}
