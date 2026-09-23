using Vipi.Sectorfile.IO;

namespace Vipi.SectorLab.Core.Ispezione;

/// <summary>
/// Le righe di un file com'è sul DISCO intorno a una riga (slice 10): per un problema che non sta in un record — un
/// commento, una riga che il lettore non capisce, un <c>F;</c> di un <c>.isc</c> — non c'è una scheda da aprire, ma
/// la riga si deve vedere lo stesso. Dal disco, perché i numeri del validatore sono quelli del disco.
/// </summary>
public static class RigheDelDisco
{
    public static IReadOnlyList<RigaGrezza> Intorno(string percorso, int riga, int contesto)
    {
        ArgumentException.ThrowIfNullOrEmpty(percorso);
        if (riga < 1 || !File.Exists(percorso))
            return [];

        // Il lettore del motore: la codifica e i fine riga del file, come li vede il validatore.
        var righe = SectorFileReader.Read(percorso).Lines;
        int da = Math.Max(1, riga - contesto);
        int a = Math.Min(righe.Count, riga + contesto);
        return [.. Enumerable.Range(da, Math.Max(0, a - da + 1)).Select(n => new RigaGrezza(n, righe[n - 1], n == riga))];
    }
}
