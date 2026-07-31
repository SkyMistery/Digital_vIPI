using Vipi.Application.Aor;
using Vipi.Application.Media;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il tipo di un file caricato lo dicono i BYTE, non l'estensione né l'header <c>Content-Type</c> del browser
/// (entrambi li sceglie chi carica). Qui si presidia il riconoscimento dei quattro formati ammessi e — soprattutto —
/// il rifiuto di ciò che immagine non è: un <c>.txt</c> ribattezzato <c>.png</c> e un SVG, che è markup e potrebbe
/// eseguire script servito dal nostro dominio.
/// </summary>
public class ImageProbeTests
{
    // --- Intestazioni minime ma vere: è l'header a essere letto, non il contenuto dell'immagine. ---

    private static byte[] Png(int w, int h)
    {
        var b = new byte[24];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(b, 0);
        b[11] = 0x0D;                                    // lunghezza chunk IHDR
        "IHDR"u8.ToArray().CopyTo(b, 12);
        Be32(b, 16, w);
        Be32(b, 20, h);
        return b;
    }

    private static byte[] Jpeg(int w, int h)
    {
        var b = new byte[24];
        b[0] = 0xFF; b[1] = 0xD8;                        // SOI
        b[2] = 0xFF; b[3] = 0xE0; b[4] = 0x00; b[5] = 0x04;   // APP0 vuoto: il SOF non è a offset fisso
        b[8] = 0xFF; b[9] = 0xC0; b[10] = 0x00; b[11] = 0x11; // SOF0, dopo il segmento precedente
        b[12] = 0x08;                                    // precisione
        b[13] = (byte)(h >> 8); b[14] = (byte)h;
        b[15] = (byte)(w >> 8); b[16] = (byte)w;
        return b;
    }

    private static byte[] Gif(int w, int h)
    {
        var b = new byte[16];
        "GIF89a"u8.ToArray().CopyTo(b, 0);
        b[6] = (byte)w; b[7] = (byte)(w >> 8);
        b[8] = (byte)h; b[9] = (byte)(h >> 8);
        return b;
    }

    private static byte[] WebpLossy(int w, int h)
    {
        var b = Riff("VP8 ");
        b[23] = 0x9D; b[24] = 0x01; b[25] = 0x2A;        // sync code
        b[26] = (byte)w; b[27] = (byte)(w >> 8);
        b[28] = (byte)h; b[29] = (byte)(h >> 8);
        return b;
    }

    private static byte[] WebpLossless(int w, int h)
    {
        var b = Riff("VP8L");
        b[20] = 0x2F;
        var bits = (uint)((w - 1) & 0x3FFF) | ((uint)((h - 1) & 0x3FFF) << 14);
        b[21] = (byte)bits; b[22] = (byte)(bits >> 8); b[23] = (byte)(bits >> 16); b[24] = (byte)(bits >> 24);
        return b;
    }

    private static byte[] WebpExtended(int w, int h)
    {
        var b = Riff("VP8X");
        Le24(b, 24, w - 1);
        Le24(b, 27, h - 1);
        return b;
    }

    private static byte[] Riff(string fourcc)
    {
        var b = new byte[32];
        "RIFF"u8.ToArray().CopyTo(b, 0);
        "WEBP"u8.ToArray().CopyTo(b, 8);
        System.Text.Encoding.ASCII.GetBytes(fourcc).CopyTo(b, 12);
        return b;
    }

    private static void Be32(byte[] b, int i, int v)
    {
        b[i] = (byte)(v >> 24); b[i + 1] = (byte)(v >> 16); b[i + 2] = (byte)(v >> 8); b[i + 3] = (byte)v;
    }

    private static void Le24(byte[] b, int i, int v)
    {
        b[i] = (byte)v; b[i + 1] = (byte)(v >> 8); b[i + 2] = (byte)(v >> 16);
    }

    public static TheoryData<string, byte[], string, int, int> Riconosciute => new()
    {
        { "png", Png(800, 600), ImageProbe.Png, 800, 600 },
        { "jpeg", Jpeg(1024, 768), ImageProbe.Jpeg, 1024, 768 },
        { "gif", Gif(320, 200), ImageProbe.Gif, 320, 200 },
        { "webp lossy", WebpLossy(640, 480), ImageProbe.Webp, 640, 480 },
        { "webp lossless", WebpLossless(1600, 900), ImageProbe.Webp, 1600, 900 },
        { "webp esteso", WebpExtended(2048, 1152), ImageProbe.Webp, 2048, 1152 },
    };

    [Theory]
    [MemberData(nameof(Riconosciute))]
    public void Riconosce_formato_e_dimensioni(string caso, byte[] bytes, string contentType, int w, int h)
    {
        var info = ImageProbe.Inspect(bytes);

        Assert.True(info is not null, $"formato non riconosciuto: {caso}");
        Assert.Equal(contentType, info!.ContentType);
        Assert.Equal(w, info.Width);
        Assert.Equal(h, info.Height);
    }

    [Fact]
    public void Testo_travestito_da_png_viene_rifiutato()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("questo non è un PNG, ma il file si chiama foto.png");

        Assert.Null(ImageProbe.Inspect(bytes));
    }

    [Fact]
    public void Svg_non_e_un_formato_ammesso()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");

        Assert.Null(ImageProbe.Inspect(bytes));
    }

    [Fact]
    public void File_troncato_non_manda_in_eccezione()
    {
        var png = Png(800, 600);

        // Nessun IndexOutOfRange: l'input non è fidato e può finire a metà header.
        for (var len = 0; len < png.Length; len++)
            Assert.Null(ImageProbe.Inspect(png.AsSpan(0, len)));
    }

    // --- Regole di accettazione (limite, formato, pixel) ---

    [Fact]
    public void Oltre_il_limite_di_byte_il_messaggio_cita_il_limite()
    {
        var options = new MediaOptions { MaxUploadBytes = 1024 };

        var ex = Assert.Throws<ValidationException>(() => MediaValidator.Validate(new byte[2048], options));

        Assert.Contains("1 KB", ex.Message);
    }

    [Fact]
    public void Oltre_il_lato_massimo_in_pixel_viene_rifiutata()
    {
        var options = new MediaOptions { MaxImagePixels = 1000 };

        var ex = Assert.Throws<ValidationException>(() => MediaValidator.Validate(Png(4000, 10), options));

        Assert.Contains("4000", ex.Message);
    }

    [Fact]
    public void Immagine_valida_passa_e_torna_le_dimensioni()
    {
        var info = MediaValidator.Validate(Png(1600, 900), new MediaOptions());

        Assert.Equal(ImageProbe.Png, info.ContentType);
        Assert.Equal(1600, info.Width);
        Assert.Equal(900, info.Height);
    }
}
