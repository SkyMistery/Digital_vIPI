using UglyToad.PdfPig;
using UglyToad.PdfPig.Writer;

namespace Aip.Pdf;

/// <summary>
/// Scrive un PDF fatto di pagine prese da altri PDF, nell'ordine dato (carta «L'AIP intero e gli emendamenti», passo 7:
/// le pagine nuove dell'emendamento al posto delle vecchie). La pagina si copia com'è — dimensioni, rotazione, testo,
/// immagini, disegno —: provato il 21 ottobre su una carta solo vettoriale di ENR 6.3, una carta con immagine di AD 2 e
/// una pagina dell'AMDT A12/26. È l'unico posto che scrive PDF con PdfPig.
/// </summary>
public static class PdfPages
{
    /// <summary>
    /// Scrive in <paramref name="path"/> le pagine <paramref name="pages"/> (file e numero di pagina da 1). Prima in un file
    /// accanto, poi al posto: un'interruzione a metà non lascia un PDF rotto.
    /// </summary>
    /// <exception cref="InvalidDataException">Un file sorgente non è un PDF che PdfPig sa leggere.</exception>
    public static void Write(string path, IReadOnlyList<(string File, int Page)> pages)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(pages);
        var aperti = new Dictionary<string, PdfDocument>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var builder = new PdfDocumentBuilder();
            foreach (var (file, page) in pages)
            {
                if (!aperti.TryGetValue(file, out var doc))
                    aperti[file] = doc = PdfDocument.Open(file);
                builder.AddPage(doc, page);
            }
            var provvisorio = path + ".scrivendo";
            File.WriteAllBytes(provvisorio, builder.Build());
            File.Move(provvisorio, path, overwrite: true);
        }
        catch (UglyToad.PdfPig.Core.PdfDocumentFormatException e)
        {
            throw new InvalidDataException(e.Message, e);
        }
        finally
        {
            foreach (var doc in aperti.Values)
                doc.Dispose();
        }
    }
}
