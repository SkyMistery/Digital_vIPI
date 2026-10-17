using System.Text;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Validazione;

/// <summary>
/// Gli avvisi dei simboli dei <c>.sym</c> (lotto «Subito» slice 17a; «file per file» §20, T2): il nome scritto senza
/// <c>//</c> e il simbolo senza nome. Il simbolo che non è 13×13 lo dice già il lettore (<see cref="Regola.RigaIllegibile"/>).
/// </summary>
/// <remarks>
/// Misure sul fork del 17 ottobre 2026: un <c>symbols.sym</c>, 23 simboli; 2 nomi senza <c>//</c> (<c>AC_comb SEL</c>
/// riga 22, <c>AC_DUPE</c> riga 25) e un simbolo senza nome (il 19°, riga 53). Prova T3-2 in Aurora: le due righe senza
/// <c>//</c> non spostano i simboli dopo, quindi aggiungere le barre non cambia niente in Aurora.
/// </remarks>
public static class ControlloDeiSimboli
{
    /// <summary>I problemi dei simboli. <paramref name="file"/>: i <c>.sym</c>, ognuno col percorso da mostrare e le righe.</summary>
    public static IEnumerable<ProblemaDelSector> Di(IReadOnlyList<(string Relativo, IReadOnlyList<string> Righe)> file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var problemi = new List<ProblemaDelSector>();
        foreach (var (relativo, righe) in file)
        {
            var letto = new SymParser(new RaccoltaMuta()).Parse(new FileReadResult(righe, Encoding.UTF8, false, Environment.NewLine, true), relativo);
            for (int numero = 1; numero <= letto.Records.Count; numero++)
            {
                var simbolo = letto.Records[numero - 1];
                int riga = simbolo.Source.LineNumber;
                if (simbolo.NomeSenzaBarre)
                {
                    string testo = righe[riga - 2];
                    problemi.Add(new(Regola.NomeDelSimboloSenzaBarre, relativo, riga - 1, testo,
                        $"«{simbolo.Nome}» è il nome del simbolo sotto, ma senza // è una riga che il formato non prevede (Aurora la salta)",
                        "//" + testo.Trim()));
                }
                else if (simbolo.Nome is null)
                {
                    problemi.Add(new(Regola.SimboloSenzaNome, relativo, riga, righe[riga - 1],
                        $"Il {numero}° simbolo non ha il commento col nome sopra: nella tavolozza di Aurora non si sa cos'è"));
                }
            }
        }

        return problemi;
    }

    /// <summary>Gli avvisi del lettore qui non servono: li dà il validatore leggendo il file.</summary>
    private sealed class RaccoltaMuta : IWarningCollector
    {
        public event EventHandler<LoadWarning>? WarningAdded { add { } remove { } }

        public int Count => 0;

        public void Add(LoadWarning warning) { }

        public void Add(WarningSeverity severity, WarningCategory category, string source, string message,
                        int? lineNumber = null, string? rawSnippet = null) { }

        public IReadOnlyList<LoadWarning> Snapshot() => [];

        public void Clear() { }
    }
}
