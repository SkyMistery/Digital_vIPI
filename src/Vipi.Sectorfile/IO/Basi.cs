using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.IO;

/// <summary>La fotografia dei record appena letti, che permette di riscriverli fedeli (carta F2 §9.5).</summary>
public static class Basi
{
    /// <summary>
    /// Fotografa la <see cref="RecordChunk{T}.Base"/> di ogni record: le righe che <paramref name="saver"/>
    /// produce dal record così com'è. Va chiamata subito dopo la lettura, prima di qualunque modifica: un
    /// record toccato senza base non si può salvare (<see cref="FileSaverOrchestrator.Save{T}"/> lo rifiuta),
    /// perché non si saprebbe più che cosa è cambiato.
    /// </summary>
    public static ParseResult<T> FissaLeBasi<T>(this ParseResult<T> letto, IFileSaver<T> saver)
    {
        ArgumentNullException.ThrowIfNull(letto);
        ArgumentNullException.ThrowIfNull(saver);

        foreach (var chunk in letto.Chunks)
        {
            if (chunk is RecordChunk<T> record)
            {
                record.Base = saver.Serialize(record.Record).ToArray();
            }
        }

        return letto;
    }
}
