using System.Globalization;
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
/// 24 etichette nominano un'aerovia che non c'è (22 vecchie «U», <c>UL81-L81</c>, e <c>Y11</c>), sempre accanto a una
/// vera; 25 stanno a più di mezzo miglio da ogni tratto delle loro aerovie (<c>M730</c> a 11,8 NM, <c>L153</c> a 3,3).</para>
/// <para>Si legge dalle righe: nel modello un'aerovia è un record per ogni fila di righe con lo stesso nome, e
/// tracciati ed etichette stanno in parti diverse del file.</para>
/// </remarks>
public static class ControlloDelleAerovie
{
    // Un'etichetta sta sulla sua aerovia: sul fork quelle giuste sono sul tratto, le altre da mezzo miglio in su.
    private const double DallAeroviaNm = 0.5;

    private const double MetriPerNm = 1852;

    private const string Interruzione = "BREAK";

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
            // Per aerovia: la riga dove comincia e i suoi tratti (un BREAK, un commento o un'altra aerovia chiudono il pezzo).
            var prima = new Dictionary<string, int>(StringComparer.Ordinal);
            var tratti = new Dictionary<string, List<(Coordinate Da, Coordinate A)>>(StringComparer.Ordinal);
            var etichette = new List<(int Riga, string Nome, Coordinate? Dove)>();
            string? aperta = null;
            Coordinate? ultimo = null;
            for (int i = 0; i < righe.Count; i++)
            {
                string riga = righe[i].Trim();
                string[] campi = riga.Split(';');
                string tipo = campi[0].Trim();
                if (riga.Length == 0 || riga.StartsWith("//", StringComparison.Ordinal) || campi.Length < 4 || tipo is not ("T" or "L"))
                {
                    (aperta, ultimo) = (null, null);
                    continue;
                }

                string nome = campi[1].Trim();
                var dove = Leggi(campi[2], campi[3], punto);
                if (tipo == "L")
                {
                    etichette.Add((i + 1, nome, dove));
                    (aperta, ultimo) = (null, null);
                    continue;
                }

                if (string.Equals(nome, Interruzione, StringComparison.OrdinalIgnoreCase))
                {
                    // Il pezzo dopo è della stessa aerovia, ma non è attaccato a questo.
                    ultimo = null;
                    continue;
                }

                if (!string.Equals(nome, aperta, StringComparison.Ordinal))
                {
                    (aperta, ultimo) = (nome, null);
                    prima.TryAdd(nome, i + 1);
                    if (!tratti.ContainsKey(nome))
                        tratti[nome] = [];
                }

                if (ultimo is { } da && dove is { } a)
                    tratti[nome].Add((da, a));
                ultimo = dove;
            }

            var nominate = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (numero, nome, dove) in etichette)
            {
                string[] nomi = nome.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var vere = nomi.Where(tratti.ContainsKey).ToList();
                nominate.UnionWith(vere);

                var assenti = nomi.Where(n => !tratti.ContainsKey(n)).ToList();
                if (assenti.Count > 0)
                {
                    string[] campi = righe[numero - 1].Split(';');
                    campi[1] = string.Join('-', vere);
                    yield return new(Regola.EtichettaDiUnAeroviaAssente, relativo, numero, righe[numero - 1],
                        $"l'etichetta nomina {string.Join(", ", assenti)}, che {(assenti.Count == 1 ? "non è un'aerovia" : "non sono aerovie")} di questo file"
                        + (vere.Count > 0 ? $": resta {string.Join("-", vere)}" : string.Empty),
                        vere.Count > 0 ? string.Join(';', campi) : null);
                }

                if (dove is { } qui && vere.SelectMany(v => tratti[v]).ToList() is { Count: > 0 } suoi
                    && suoi.Min(t => ControlloDellaTerra.MetriDalSegmento(qui, t.Da, t.A)) / MetriPerNm is var lontana && lontana > DallAeroviaNm)
                {
                    yield return new(Regola.EtichettaLontanaDallAerovia, relativo, numero, righe[numero - 1],
                        $"l'etichetta è a {lontana.ToString("0.#", CultureInfo.InvariantCulture)} NM dal tratto più vicino di {string.Join("-", vere)}: l'aerovia non passa più di lì");
                }
            }

            foreach (var (nome, numero) in prima.OrderBy(p => p.Value))
            {
                if (!nominate.Contains(nome))
                {
                    yield return new(Regola.AeroviaSenzaEtichetta, relativo, numero, righe[numero - 1],
                        $"nessuna etichetta nomina {nome}: in Aurora si vede la linea senza il suo nome");
                }
            }
        }
    }

    private static Coordinate? Leggi(string lat, string lon, Func<string, Coordinate?> punto)
    {
        string t = lat.Trim();
        if (t.Length < 2 || !"NSEWnsew".Contains(t[0], StringComparison.Ordinal) || !char.IsAsciiDigit(t[1]))
            return punto(t);
        try
        {
            return CoordinateConverter.ParsePair(t, lon.Trim());
        }
        catch (CoordinateParseException)
        {
            return null;
        }
    }
}
