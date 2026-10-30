using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Un PNG da pixel RGB, e la sua misura da un PNG già scritto (carta «Gli aeroporti nel Lab», S3: l'immagine di una carta
/// nella cache del Lab). Fatto a mano perché il Lab non ha una libreria di immagini e non gliene serve una: un PNG sono
/// tre blocchi, e i dati sono le righe compresse con zlib (il filtro «Sub» per riga: una carta è quasi tutta bianca e a
/// tinte piatte, e così si comprime meglio).
/// </summary>
public static class Png
{
    private static readonly byte[] Firma = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>Il PNG dell'immagine.</summary>
    public static byte[] Scrivi(ImmagineDiUnaPagina immagine)
    {
        ArgumentNullException.ThrowIfNull(immagine);
        int larghezza = immagine.Larghezza, altezza = immagine.Altezza, riga = larghezza * 3;
        using var uscita = new MemoryStream();
        uscita.Write(Firma);

        var testata = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(testata, larghezza);
        BinaryPrimitives.WriteInt32BigEndian(testata.AsSpan(4), altezza);
        testata[8] = 8;   // 8 bit per canale
        testata[9] = 2;   // RGB
        Blocco(uscita, "IHDR", testata);

        using (var dati = new MemoryStream())
        {
            using (var zlib = new ZLibStream(dati, CompressionLevel.Optimal, leaveOpen: true))
            {
                var filtrata = new byte[riga + 1];
                filtrata[0] = 1;   // Sub: ogni byte meno quello del pixel a sinistra
                for (int y = 0; y < altezza; y++)
                {
                    var r = immagine.Rgb.AsSpan(y * riga, riga);
                    for (int i = 0; i < riga; i++)
                        filtrata[i + 1] = (byte)(r[i] - (i >= 3 ? r[i - 3] : 0));
                    zlib.Write(filtrata);
                }
            }
            Blocco(uscita, "IDAT", dati.ToArray());
        }
        Blocco(uscita, "IEND", []);
        return uscita.ToArray();
    }

    /// <summary>La misura di un PNG (dalla testata), o null se non è un PNG.</summary>
    public static (int Larghezza, int Altezza)? Misura(ReadOnlySpan<byte> png) =>
        png.Length >= 24 && png[..8].SequenceEqual(Firma) && png.Slice(12, 4).SequenceEqual("IHDR"u8)
            ? (BinaryPrimitives.ReadInt32BigEndian(png.Slice(16, 4)), BinaryPrimitives.ReadInt32BigEndian(png.Slice(20, 4)))
            : null;

    private static void Blocco(Stream uscita, string tipo, byte[] dati)
    {
        Span<byte> quattro = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(quattro, dati.Length);
        uscita.Write(quattro);
        var nome = Encoding.ASCII.GetBytes(tipo);
        uscita.Write(nome);
        uscita.Write(dati);
        BinaryPrimitives.WriteUInt32BigEndian(quattro, Crc(nome, dati));
        uscita.Write(quattro);
    }

    private static readonly uint[] Tavola = FaiLaTavola();

    private static uint[] FaiLaTavola()
    {
        var tavola = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            tavola[n] = c;
        }
        return tavola;
    }

    private static uint Crc(byte[] nome, byte[] dati)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in nome) c = Tavola[(c ^ b) & 0xFF] ^ (c >> 8);
        foreach (var b in dati) c = Tavola[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}
