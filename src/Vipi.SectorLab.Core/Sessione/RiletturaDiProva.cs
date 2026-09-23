using Vipi.Sectorfile.IO;

namespace Vipi.SectorLab.Core.Sessione;

/// <summary>
/// Un file coi byte che AVREBBE, letto dal motore prima che esistano sul disco (slice 9): per sapere che errori
/// avrebbe e quanti record ne tornerebbero. Si scrive una copia in una cartella temporanea e la si legge lì.
/// <para>🔴 La copia porta il percorso relativo INTERO, non solo il nome: il motore sceglie il lettore anche dalla
/// cartella (<c>/ENRMVA/</c> = MVA di rotta, <see cref="Formati"/>), e con il solo nome un <c>.mva</c> di rotta si
/// leggerebbe come quello di un aeroporto — errori «nuovi» che nuovi non sono.</para>
/// </summary>
public static class RiletturaDiProva
{
    /// <summary>Scrive <paramref name="byteDelFile"/> in una copia di prova, ci chiama <paramref name="leggi"/>, e la toglie.</summary>
    public static T Con<T>(string relativo, byte[] byteDelFile, Func<string, T> leggi)
    {
        ArgumentException.ThrowIfNullOrEmpty(relativo);
        ArgumentNullException.ThrowIfNull(byteDelFile);
        ArgumentNullException.ThrowIfNull(leggi);

        string cartella = Path.Combine(Path.GetTempPath(), "VipiSectorLab-" + Guid.NewGuid().ToString("N"));
        try
        {
            string copia = Path.Combine(cartella, relativo.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(copia)!);
            File.WriteAllBytes(copia, byteDelFile);
            return leggi(copia);
        }
        finally
        {
            try
            {
                Directory.Delete(cartella, recursive: true);
            }
            catch (IOException)
            {
                // Una cartella temporanea rimasta non ferma niente.
            }
        }
    }

    /// <summary>Quanti record il motore leggerebbe da quei byte; nullo se il file non lo interpreta.</summary>
    public static int? Record(string relativo, byte[] byteDelFile)
        => Con(relativo, byteDelFile, QuantiRecord);

    /// <summary>Quanti record il motore legge da un file sul disco; nullo se non lo interpreta.</summary>
    public static int? QuantiRecord(string percorso)
        => Formati.Usa(percorso, new RaccoltaDiAvvisi(), new ContaIRecord(percorso), out int quanti) ? quanti : null;

    private sealed class ContaIRecord(string percorso) : IUsoDelFormato<int>
    {
        public int Usa<T>(IFileParser<T> lettore, IFileSaver<T> scrittore)
            where T : class
            => lettore.Parse(percorso, new Vipi.Sectorfile.Shared.ColorPalette()).Records.Count;
    }
}
