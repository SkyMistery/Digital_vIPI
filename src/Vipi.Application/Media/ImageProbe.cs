namespace Vipi.Application.Media;

/// <summary>Esito del riconoscimento: tipo MIME reale e dimensioni in pixel.</summary>
public sealed record ImageInfo(string ContentType, int Width, int Height);

/// <summary>
/// Riconosce formato e dimensioni di un'immagine leggendone l'INTESTAZIONE, senza decodificarla e senza librerie di
/// imaging (nessuna dipendenza nuova, nessun costo di decodifica su un input non fidato).
/// <para>
/// Il tipo dichiarato dal browser si ignora: un <c>.txt</c> rinominato <c>.png</c> arriva con
/// <c>Content-Type: image/png</c>. Qui il tipo lo dicono i byte, e ciò che non è riconosciuto viene rifiutato —
/// compreso l'SVG, che è markup e potrebbe eseguire script servito dal nostro dominio.
/// </para>
/// Le dimensioni servono anche come guardia contro le "decompression bomb": si conoscono prima di aprire l'immagine.
/// </summary>
public static class ImageProbe
{
    public const string Png = "image/png";
    public const string Jpeg = "image/jpeg";
    public const string Gif = "image/gif";
    public const string Webp = "image/webp";

    /// <summary>Formati accettati, nell'ordine in cui si presentano all'utente.</summary>
    public static readonly IReadOnlyList<string> SupportedContentTypes = new[] { Png, Jpeg, Webp, Gif };

    /// <summary>Estensioni corrispondenti, per l'attributo <c>accept</c> del file input.</summary>
    public const string AcceptAttribute = ".png,.jpg,.jpeg,.webp,.gif";

    /// <summary>Riconosce l'immagine, o null se i byte non sono una delle immagini supportate.</summary>
    public static ImageInfo? Inspect(ReadOnlySpan<byte> bytes) =>
        Png_(bytes) ?? Gif_(bytes) ?? Webp_(bytes) ?? Jpeg_(bytes);

    // --- PNG: firma di 8 byte, poi il chunk IHDR con larghezza e altezza big-endian. ---
    private static ImageInfo? Png_(ReadOnlySpan<byte> b)
    {
        ReadOnlySpan<byte> sig = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        if (b.Length < 24 || !b[..8].SequenceEqual(sig)) return null;
        if (b[12] != 'I' || b[13] != 'H' || b[14] != 'D' || b[15] != 'R') return null;
        return new ImageInfo(Png, (int)Be32(b, 16), (int)Be32(b, 20));
    }

    // --- GIF: "GIF87a"/"GIF89a", poi larghezza e altezza little-endian. ---
    private static ImageInfo? Gif_(ReadOnlySpan<byte> b)
    {
        if (b.Length < 10) return null;
        if (b[0] != 'G' || b[1] != 'I' || b[2] != 'F' || b[3] != '8' || (b[4] != '7' && b[4] != '9') || b[5] != 'a') return null;
        return new ImageInfo(Gif, Le16(b, 6), Le16(b, 8));
    }

    // --- WebP: contenitore RIFF; le dimensioni stanno in un posto diverso per ognuno dei tre sotto-formati. ---
    private static ImageInfo? Webp_(ReadOnlySpan<byte> b)
    {
        if (b.Length < 30) return null;
        if (b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F') return null;
        if (b[8] != 'W' || b[9] != 'E' || b[10] != 'B' || b[11] != 'P') return null;

        // "VP8 " = lossy: dopo il sync code 9D 01 2A, due interi a 14 bit.
        if (b[12] == 'V' && b[13] == 'P' && b[14] == '8' && b[15] == ' ')
        {
            if (b.Length < 30 || b[23] != 0x9D || b[24] != 0x01 || b[25] != 0x2A) return null;
            return new ImageInfo(Webp, Le16(b, 26) & 0x3FFF, Le16(b, 28) & 0x3FFF);
        }

        // "VP8L" = lossless: 14 bit di larghezza e 14 di altezza impacchettati, entrambi meno uno.
        if (b[12] == 'V' && b[13] == 'P' && b[14] == '8' && b[15] == 'L')
        {
            if (b.Length < 25 || b[20] != 0x2F) return null;
            var bits = Le32(b, 21);
            return new ImageInfo(Webp, (int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1);
        }

        // "VP8X" = esteso (animazioni, alpha): dimensioni del canvas su 24 bit, meno uno.
        if (b[12] == 'V' && b[13] == 'P' && b[14] == '8' && b[15] == 'X')
        {
            if (b.Length < 30) return null;
            return new ImageInfo(Webp, Le24(b, 24) + 1, Le24(b, 27) + 1);
        }

        return null;
    }

    // --- JPEG: catena di segmenti; le dimensioni stanno nel primo SOF (che non è a offset fisso). ---
    private static ImageInfo? Jpeg_(ReadOnlySpan<byte> b)
    {
        if (b.Length < 4 || b[0] != 0xFF || b[1] != 0xD8) return null;

        var i = 2;
        while (i + 3 < b.Length)
        {
            if (b[i] != 0xFF) return null;               // fuori sincrono: non è un JPEG leggibile
            var marker = b[i + 1];
            if (marker == 0xFF) { i++; continue; }        // riempimento fra segmenti
            if (marker is 0xD8 or 0x01 || (marker >= 0xD0 && marker <= 0xD7)) { i += 2; continue; }  // senza payload
            if (marker == 0xD9 || marker == 0xDA) return null;  // fine immagine / inizio dati: nessun SOF trovato

            var length = Be16(b, i + 2);
            if (length < 2) return null;

            // SOF0..SOF15 tranne DHT (C4), JPG (C8) e DAC (CC), che non descrivono il frame.
            if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
            {
                if (i + 9 > b.Length) return null;
                return new ImageInfo(Jpeg, Be16(b, i + 7), Be16(b, i + 5));   // prima l'altezza, poi la larghezza
            }

            i += 2 + length;
        }
        return null;
    }

    private static uint Be32(ReadOnlySpan<byte> b, int i) => (uint)((b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3]);
    private static int Be16(ReadOnlySpan<byte> b, int i) => (b[i] << 8) | b[i + 1];
    private static int Le16(ReadOnlySpan<byte> b, int i) => b[i] | (b[i + 1] << 8);
    private static int Le24(ReadOnlySpan<byte> b, int i) => b[i] | (b[i + 1] << 8) | (b[i + 2] << 16);
    private static uint Le32(ReadOnlySpan<byte> b, int i) => (uint)(b[i] | (b[i + 1] << 8) | (b[i + 2] << 16) | (b[i + 3] << 24));
}
