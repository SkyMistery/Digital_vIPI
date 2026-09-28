using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Legge i simboli di un <c>.sym</c> (<see cref="SimboloDelSector"/>). Solo lettura, come <c>.def</c> e <c>.clr</c>: il
/// file passa ancora intatto come testo nel round-trip; l'editor a pixel è della slice 17 (T1). Lotto «Subito» slice 4d.
/// <para>Il manuale non descrive il formato (carta «file per file» §20): un simbolo per riga, il nome nel commento sopra.
/// Una riga di testo che non è né commento né pixel (<c>AC_comb SEL</c>, <c>AC_DUPE</c> sul fork) si prende per il nome
/// del simbolo sotto, come fa chi legge il file; una riga di pixel che non è 13×13 si salta con un avviso.</para>
/// </summary>
public sealed class SymParser
{
    private readonly IWarningCollector _warnings;

    public SymParser(IWarningCollector warnings)
        => _warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));

    public IReadOnlyList<SimboloDelSector> Parse(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        return Parse(SectorFileReader.Read(filePath), filePath);
    }

    /// <summary>Righe già lette (senza disco: per i test).</summary>
    public IReadOnlyList<SimboloDelSector> Parse(FileReadResult read, string source)
    {
        var simboli = new List<SimboloDelSector>();
        string? nome = null;
        bool senzaCommento = false;

        for (int i = 0; i < read.Lines.Count; i++)
        {
            string riga = read.Lines[i].Trim();
            if (riga.Length == 0)
                continue;

            if (riga.StartsWith("//", StringComparison.Ordinal))
            {
                // L'ultimo commento prima dei pixel è il nome (////ALTRI è un titolo, il commento dopo lo sostituisce).
                nome = riga.TrimStart('/').Trim() is { Length: > 0 } n ? n : null;
                senzaCommento = false;
                continue;
            }

            if (!SembraPixel(riga))
            {
                nome = riga;
                senzaCommento = true;
                continue;
            }

            var colonne = riga.Split(';', StringSplitOptions.TrimEntries).Where(c => c.Length > 0).ToList();
            if (colonne.Count != SimboloDelSector.Lato
                || colonne.Any(c => c.Length != SimboloDelSector.Lato || c.Any(ch => ch is not ('0' or '1'))))
            {
                _warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source,
                    "Simbolo che non è 13×13", i + 1, read.Lines[i]);
            }
            else
            {
                simboli.Add(new SimboloDelSector(simboli.Count + 1, nome, colonne, i + 1, senzaCommento && nome is not null));
            }

            nome = null;
            senzaCommento = false;
        }

        return simboli;
    }

    /// <summary>Una riga di pixel: solo cifre 0/1, punti e virgola e spazi.</summary>
    private static bool SembraPixel(string riga)
        => riga.Contains(';', StringComparison.Ordinal) && riga.All(c => c is '0' or '1' or ';' or ' ' or '\t');
}
