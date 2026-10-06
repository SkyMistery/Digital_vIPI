using System.Globalization;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Validazione;

/// <summary>
/// I controlli delle aerovie (lotto «Subito» slice 14b, «file per file» B12): i tracciati <c>T;</c> contro le loro
/// etichette <c>L;</c>. Il punto di un tracciato che non si trova lo dice già <see cref="Regola.NomeNonRisolto"/>.
/// </summary>
/// <remarks>
/// <para>Misure sul fork del 6 ottobre 2026 (<c>itawlow.lairway</c>): 246 aerovie in 274 pezzi (28 <c>BREAK</c>), 1 089
/// tratti distinti, 21 condivisi da più aerovie; 896 etichette, tutte per coordinate. Il nome di un'etichetta unisce col
/// trattino le aerovie che condividono il tratto (<c>L53-P873</c>). 12 aerovie non sono nominate da nessuna etichetta;
/// 18 etichette nominano un'aerovia che non c'è (22 vecchie «U», <c>UL81-L81</c>, e <c>Y11</c>), sempre accanto a una
/// vera; 25 stanno a più di mezzo miglio da ogni tratto delle loro aerovie (<c>M740</c> a 38 NM, <c>L153</c> a 3,3).</para>
/// <para>Si legge dalle righe (<see cref="RigheDelleAerovie"/>): nel modello un'aerovia è un record per ogni fila di
/// righe con lo stesso nome, e tracciati ed etichette stanno in parti diverse del file.</para>
/// </remarks>
public static class ControlloDelleAerovie
{
    /// <summary>
    /// Un'etichetta sta sulla sua aerovia entro questa distanza (NM): sul fork quelle giuste sono sul tratto, le altre
    /// da mezzo miglio in su. La usano anche le etichette calcolate del Lab.
    /// </summary>
    public const double DallAeroviaNm = 0.5;

    private const double MetriPerNm = 1852;

    /// <summary>
    /// I problemi di <c>.lairway</c> e <c>.hairway</c>. <paramref name="file"/>: il percorso da mostrare e le righe di
    /// ognuno; <paramref name="punto"/>: la posizione di un fix, VOR o NDB per nome, o null.
    /// </summary>
    public static IEnumerable<ProblemaDelSector> Di(IReadOnlyList<(string Relativo, IReadOnlyList<string> Righe)> file,
                                                    Func<string, Coordinate?> punto)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(punto);

        foreach (var (relativo, righe) in file)
        {
            var lette = RigheDelleAerovie.Leggi(righe, punto);
            var tratti = lette.Nomi.ToDictionary(n => n, n => lette.TrattiDi(n).Select(t => (Da: t.Da.Posizione!.Value, A: t.A.Posizione!.Value)).ToList(),
                StringComparer.Ordinal);

            var nominate = new HashSet<string>(StringComparer.Ordinal);
            foreach (var etichetta in lette.Etichette)
            {
                string testo = righe[etichetta.Riga - 1];
                var vere = etichetta.Nomi.Where(tratti.ContainsKey).ToList();
                nominate.UnionWith(vere);

                var assenti = etichetta.Nomi.Where(n => !tratti.ContainsKey(n)).ToList();
                if (assenti.Count > 0)
                {
                    string[] campi = testo.Split(';');
                    campi[1] = string.Join('-', vere);
                    yield return new(Regola.EtichettaDiUnAeroviaAssente, relativo, etichetta.Riga, testo,
                        $"l'etichetta nomina {string.Join(", ", assenti)}, che {(assenti.Count == 1 ? "non è un'aerovia" : "non sono aerovie")} di questo file"
                        + (vere.Count > 0 ? $": resta {string.Join("-", vere)}" : string.Empty),
                        vere.Count > 0 ? string.Join(';', campi) : null);
                }

                if (etichetta.Posizione is { } qui && vere.SelectMany(v => tratti[v]).ToList() is { Count: > 0 } suoi
                    && suoi.Min(t => ControlloDellaTerra.MetriDalSegmento(qui, t.Da, t.A)) / MetriPerNm is var lontana && lontana > DallAeroviaNm)
                {
                    yield return new(Regola.EtichettaLontanaDallAerovia, relativo, etichetta.Riga, testo,
                        $"l'etichetta è a {lontana.ToString("0.#", CultureInfo.InvariantCulture)} NM dal tratto più vicino di {string.Join("-", vere)}: l'aerovia non passa più di lì");
                }
            }

            foreach (string nome in lette.Nomi.Where(n => !nominate.Contains(n)))
            {
                int riga = lette.Pezzi.First(p => p.Aerovia == nome).Punti[0].Riga;
                yield return new(Regola.AeroviaSenzaEtichetta, relativo, riga, righe[riga - 1],
                    $"nessuna etichetta nomina {nome}: in Aurora si vede la linea senza il suo nome");
            }
        }
    }
}
