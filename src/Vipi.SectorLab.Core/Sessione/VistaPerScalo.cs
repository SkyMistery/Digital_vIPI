using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Core.Sessione;

/// <summary>Uno scalo da scegliere nella vista: il codice e il nome dell'<c>.ap</c>.</summary>
public sealed record ScaloDellaVista(string Icao, string Nome);

/// <summary>Una voce della vista per scalo: un record (si sceglie) o un file intero (si apre).</summary>
/// <param name="Record">L'indice del record nel file, o null se la voce è il file.</param>
/// <param name="Nota">Quel che aiuta a riconoscerla: il file, la frequenza, quanti record.</param>
public sealed record VoceDelloScalo(string Titolo, string File, int? Record, string? Nota = null);

/// <summary>Un gruppo di voci della vista per scalo: lo scalo, le piste, i file, le marcature di una pista.</summary>
/// <param name="Id">Stabile, per la sezione che si apre e si chiude: <c>piste</c>, <c>marcature:16L</c>.</param>
public sealed record SezioneDelloScalo(string Id, string Nome, IReadOnlyList<VoceDelloScalo> Voci);

/// <summary>
/// La vista per scalo (lotto «Subito» slice 12d, «file per file» I6, O1): scelto LIRF, tutto quello che lo riguarda,
/// da qualunque file — le sue righe di <c>.ap</c>, <c>.rw</c> e <c>.frq</c>, i file col suo nome (<c>lirf.sid</c>,
/// <c>lirf.gts</c>…), i disegni e i riempimenti, e le marcature raggruppate per pista e per parte. Non sposta niente:
/// ogni voce porta al suo record o al suo file, e il Lab scrive nei file dove le cose stanno già.
/// <para>Vale con qualunque organizzazione dei file (I7): i file che non portano il nome dello scalo
/// (<c>rf_ad_gnd.pol</c>, <c>br_mark.geo</c>) si riconoscono da dove stanno, entro 10 km dal suo centro.</para>
/// </summary>
public static class VistaPerScalo
{
    private const double MetriDalloScalo = 10_000;

    // Le parole che fanno di un commento il nome di una parte di marcature (misura della slice 12 sui 34 file).
    private static readonly string[] ParoleDelleMarcature =
        ["designator", "threshold", "aiming", "displaced", "stripes", "closed", "chevron", "touchdown", "centre", "center"];

    /// <summary>Gli scali degli <c>.ap</c> dell'albero, una volta ciascuno, per codice.</summary>
    public static IReadOnlyList<ScaloDellaVista> Scali(SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        return [.. Record<AirportInfo>(sessione).Select(r => r.Record)
            .GroupBy(a => a.IcaoCode.Trim().ToUpperInvariant(), StringComparer.Ordinal)
            .Where(g => g.Key.Length > 0)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new ScaloDellaVista(g.Key, g.First().Name.Trim()))];
    }

    /// <summary>Le sezioni di uno scalo, nell'ordine in cui si leggono; quelle vuote non ci sono.</summary>
    public static IReadOnlyList<SezioneDelloScalo> Di(SessioneAperta sessione, string icao)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        ArgumentException.ThrowIfNullOrWhiteSpace(icao);
        string scalo = icao.Trim().ToUpperInvariant();

        var arp = new Dictionary<string, Coordinate>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, _, info) in Record<AirportInfo>(sessione))
            arp.TryAdd(info.IcaoCode.Trim(), info.Centre);

        var sezioni = new List<SezioneDelloScalo>
        {
            new("scalo", "Scalo", [.. Record<AirportInfo>(sessione).Where(r => Stesso(r.Record.IcaoCode, scalo))
                .Select(r => new VoceDelloScalo($"{scalo} {r.Record.Name.Trim()}".Trim(), r.File, r.Indice, NomeDelFile(r.File)))]),
            new("piste", "Piste", [.. Record<Runway>(sessione).Where(r => Stesso(r.Record.IcaoCode, scalo))
                .Select(r => new VoceDelloScalo($"{r.Record.Designator1.Trim()}/{r.Record.Designator2.Trim()}", r.File, r.Indice, NomeDelFile(r.File)))]),
            new("posizioni", "Posizioni", [.. Record<AtcPosition>(sessione)
                .Where(r => r.Record.Code.Trim().StartsWith(scalo + "_", StringComparison.OrdinalIgnoreCase))
                .Select(r => new VoceDelloScalo(r.Record.Code.Trim(), r.File, r.Indice,
                    r.Record.FrequencyMhz.ToString("0.000", CultureInfo.InvariantCulture) + " · " + NomeDelFile(r.File)))]),
        };

        // I file col nome dello scalo, tranne il suo .geo, che sta coi disegni.
        var colSuoNome = sessione.File.Values
            .Where(f => Stesso(Path.GetFileNameWithoutExtension(f.Relativo), scalo))
            .OrderBy(f => f.Relativo, StringComparer.Ordinal).ToList();
        sezioni.Add(new("file", "File dello scalo", [.. colSuoNome.Where(f => !EUnDisegno(f.Relativo)).Select(DelFile)]));

        // Disegni e riempimenti: il suo .geo, e i .geo e .pol che stanno sul suo sedime senza portarne il nome.
        var terra = colSuoNome.Where(f => EUnDisegno(f.Relativo))
            .Concat(sessione.File.Values
                .Where(f => EUnDisegno(f.Relativo) && !arp.ContainsKey(Path.GetFileNameWithoutExtension(f.Relativo))
                            && Stesso(ScaloPiuVicino(f, arp), scalo))
                .OrderBy(f => f.Relativo, StringComparer.Ordinal))
            .ToList();
        sezioni.Add(new("terra", "Disegni e riempimenti", [.. terra.Select(DelFile)]));

        sezioni.AddRange(Marcature(terra));
        return [.. sezioni.Where(s => s.Voci.Count > 0)];
    }

    /// <summary>
    /// Le marcature per pista e per parte (O1), dai commenti: in un file di <c>RW_MARKINGS</c> ogni commento apre una
    /// parte, e una parte che non nomina una pista (<c>//Runway stripes (1)</c>, <c>//2</c>) è della pista nominata prima;
    /// nel <c>.geo</c> dello scalo contano solo i commenti che nominano una pista o una parte di marcature.
    /// </summary>
    private static IEnumerable<SezioneDelloScalo> Marcature(IReadOnlyList<FileAperto> terra)
    {
        var perPista = new SortedDictionary<string, List<VoceDelloScalo>>(StringComparer.Ordinal);
        foreach (var file in terra.Where(f => f.Relativo.EndsWith(".geo", StringComparison.OrdinalIgnoreCase)))
        {
            if (file is not IFileConRecord conRecord)
                continue;
            bool delleMarcature = file.Relativo.Replace('\\', '/').Contains("/RW_MARKINGS/", StringComparison.OrdinalIgnoreCase);
            var righe = conRecord.RigheDelFile([]);
            var posti = conRecord.PostiDeiRecord([]);
            string? pistaDiPrima = null;
            int prossimo = 0;
            for (int i = 0; i < righe.Count; i++)
            {
                string riga = righe[i].Trim();
                if (!riga.StartsWith("//", StringComparison.Ordinal) || riga.Trim('/', ' ').Length == 0)
                    continue;
                var citate = ControlloDellaTerra.PisteCitate(riga).ToList();
                bool parla = ParoleDelleMarcature.Any(p => riga.Contains(p, StringComparison.OrdinalIgnoreCase));
                if (!delleMarcature && (citate.Count == 0 || !parla))
                    continue;

                while (prossimo < posti.Count && posti[prossimo].Da <= i)
                    prossimo++;
                if (prossimo >= posti.Count)
                    break;
                // Il commento deve stare subito sopra dei dati: fra lui e il record solo altri commenti o righe vuote.
                if (Enumerable.Range(i + 1, posti[prossimo].Da - i - 1).Any(r => righe[r].Trim().Length > 0 && !righe[r].TrimStart().StartsWith("//", StringComparison.Ordinal)))
                    continue;

                string pista = citate.Count > 0 ? string.Join("/", citate) : pistaDiPrima ?? "altre";
                if (citate.Count > 0)
                    pistaDiPrima = pista;
                if (!perPista.TryGetValue(pista, out var voci))
                    perPista[pista] = voci = [];
                voci.Add(new VoceDelloScalo(riga.TrimStart('/').Trim(), file.Relativo, prossimo, $"{NomeDelFile(file.Relativo)}:{i + 1}"));
            }
        }

        return perPista.Select(p => new SezioneDelloScalo("marcature:" + p.Key,
            p.Key == "altre" ? "Marcature · senza pista" : "Marcature · pista " + p.Key, p.Value));
    }

    private static VoceDelloScalo DelFile(FileAperto file)
        => new(NomeDelFile(file.Relativo), file.Relativo, null, file.Record == 1 ? "1 record" : $"{file.Record.ToString("N0", CultureInfo.InvariantCulture)} record");

    // I disegni e i riempimenti di terra: .geo e .pol (non lo sfondo, non le aree P/R/D, che hanno altre estensioni).
    private static bool EUnDisegno(string relativo)
        => (relativo.EndsWith(".geo", StringComparison.OrdinalIgnoreCase) || relativo.EndsWith(".pol", StringComparison.OrdinalIgnoreCase))
           && !relativo.Replace('\\', '/').EndsWith("GEO/itgeo.geo", StringComparison.OrdinalIgnoreCase);

    // Lo scalo sul cui sedime sta il primo punto del file, entro 10 km dal centro; null se nessuno.
    private static string? ScaloPiuVicino(FileAperto file, Dictionary<string, Coordinate> arp)
    {
        if (file is not IFileConRecord conRecord || arp.Count == 0)
            return null;
        Coordinate? primo = conRecord.RecordDelModello.Select(r => r switch
        {
            Line l => l.Start,
            Polygon p when p.Vertices.Count > 0 => p.Vertices[0],
            _ => (Coordinate?)null,
        }).FirstOrDefault(c => c is not null);
        if (primo is not { } punto)
            return null;
        var (scalo, metri) = arp.Select(a => (a.Key, Metri: Metri(a.Value, punto))).MinBy(a => a.Metri);
        return metri <= MetriDalloScalo ? scalo : null;
    }

    private static double Metri(Coordinate a, Coordinate b)
    {
        double dy = (a.LatitudeDeg - b.LatitudeDeg) * 111_320;
        double dx = (a.LongitudeDeg - b.LongitudeDeg) * 111_320 * Math.Cos(a.LatitudeDeg * Math.PI / 180);
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static IEnumerable<(string File, int Indice, T Record)> Record<T>(SessioneAperta sessione)
        where T : class
        => sessione.File.Values.OrderBy(f => f.Relativo, StringComparer.Ordinal).OfType<IFileConRecord>()
            .SelectMany(f => f.RecordDelModello.Select((r, i) => (File: ((FileAperto)f).Relativo, Indice: i, Record: r as T)))
            .Where(r => r.Record is not null)
            .Select(r => (r.File, r.Indice, r.Record!));

    private static bool Stesso(string? a, string b) => string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);

    private static string NomeDelFile(string relativo) => relativo[(relativo.Replace('\\', '/').LastIndexOf('/') + 1)..];
}
