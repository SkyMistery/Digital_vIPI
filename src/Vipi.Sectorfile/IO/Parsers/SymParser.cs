using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Legge i simboli di un <c>.sym</c> (<see cref="Simbolo"/>; lotto «Subito» slice 4d, lettore e scrittore dalla 17a).
/// <para>Il manuale non descrive il formato (carta «file per file» §20): un simbolo per riga, il nome nel commento
/// subito sopra. Il record è il nome e i pixel insieme, così un nome cambiato si riscrive al suo posto. Una riga di
/// testo che non è né commento né pixel, subito sopra i pixel (<c>AC_comb SEL</c>, <c>AC_DUPE</c> sul fork), è il nome
/// scritto senza <c>//</c>: Aurora la salta (prova T3-2). Una riga di pixel che non è 13×13, o un testo che non sta
/// sopra un simbolo, resta com'è con un avviso.</para>
/// </summary>
public sealed class SymParser : IFileParser<Simbolo>
{
    private readonly IWarningCollector _warnings;

    public SymParser(IWarningCollector warnings)
        => _warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));

    public ParseResult<Simbolo> Parse(string filePath, ColorPalette palette)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(palette);
        return Parse(SectorFileReader.Read(filePath), filePath);
    }

    /// <summary>Righe già lette (senza disco: per i test).</summary>
    public ParseResult<Simbolo> Parse(FileReadResult read, string source)
    {
        var simboli = new List<Simbolo>();
        var chunks = new List<FileChunk<Simbolo>>();
        var raw = new List<string>();
        var commenti = new List<string>();   // commenti di fila, senza righe vuote in mezzo

        void Grezze()
        {
            raw.AddRange(commenti);
            commenti.Clear();
        }

        void Simbolo(string? rigaDelNome, string? nome, bool senzaBarre, string pixel, int riga)
        {
            if (raw.Count > 0)
            {
                chunks.Add(new RawChunk<Simbolo>(raw.ToArray()));
                raw.Clear();
            }

            var simbolo = new Simbolo
            {
                Nome = nome,
                NomeSenzaBarre = senzaBarre,
                Pixel = Models.Simbolo.Scrivi(Models.Simbolo.Leggi(pixel)!),
                Source = new SourceRef(source, riga),
            };
            chunks.Add(new RecordChunk<Simbolo>(simbolo, rigaDelNome is null ? [pixel] : [rigaDelNome, pixel], hasMarkers: false, commenti.ToArray()));
            commenti.Clear();
            simboli.Add(simbolo);
        }

        var righe = read.Lines;
        for (int i = 0; i < righe.Count; i++)
        {
            string riga = righe[i];
            string pulita = riga.Trim();

            if (pulita.Length == 0)
            {
                Grezze();
                raw.Add(riga);
                continue;
            }

            if (pulita.StartsWith("//", StringComparison.Ordinal))
            {
                commenti.Add(riga);
                continue;
            }

            if (SembraPixel(pulita))
            {
                if (Models.Simbolo.Leggi(pulita) is null)
                {
                    _warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source, "Simbolo che non è 13×13", i + 1, riga);
                    Grezze();
                    raw.Add(riga);
                    continue;
                }

                // Il nome è l'ultimo commento subito sopra (////ALTRI è un titolo: resta un commento davanti).
                if (commenti.Count > 0)
                {
                    string rigaDelNome = commenti[^1];
                    commenti.RemoveAt(commenti.Count - 1);
                    string nome = rigaDelNome.Trim().TrimStart('/').Trim();
                    Simbolo(rigaDelNome, nome.Length > 0 ? nome : null, senzaBarre: false, riga, i + 1);
                }
                else
                {
                    Simbolo(null, null, senzaBarre: false, riga, i + 1);
                }

                continue;
            }

            if (i + 1 < righe.Count && SembraPixel(righe[i + 1].Trim()) && Models.Simbolo.Leggi(righe[i + 1].Trim()) is not null)
            {
                Simbolo(riga, pulita, senzaBarre: true, righe[i + 1], i + 2);
                i++;
                continue;
            }

            _warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source, "Riga che non è un simbolo né il suo nome", i + 1, riga);
            Grezze();
            raw.Add(riga);
        }

        Grezze();
        if (raw.Count > 0)
            chunks.Add(new RawChunk<Simbolo>(raw.ToArray()));

        return new ParseResult<Simbolo>(simboli, chunks, read.Encoding, read.HasByteOrderMark)
        {
            NewLine = read.NewLine,
            HasFinalNewLine = read.HasFinalNewLine,
        };
    }

    /// <summary>Una riga di pixel: solo cifre 0/1, punti e virgola e spazi.</summary>
    private static bool SembraPixel(string riga)
        => riga.Contains(';', StringComparison.Ordinal) && riga.All(c => c is '0' or '1' or ';' or ' ' or '\t');
}
