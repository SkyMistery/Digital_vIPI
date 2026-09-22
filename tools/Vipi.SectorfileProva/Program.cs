using System.Text.RegularExpressions;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

// La prova del motore del sector sull'albero intero (carta F2, docs/feature/2026-09-22-f2-motore-del-sector.md §5).
//
//   dotnet run --project tools/Vipi.SectorfileProva -- <cartella SectorFiles/Include/IT>
//
// Due misure, e servono tutte e due:
//   1. ROUND-TRIP: ogni file letto e riscritto deve uscire identico byte per byte. Esce 1 se uno non lo è.
//   2. RIGHE OPACHE: quelle che il motore conserva senza capirle («Skipping malformed line» e simili).
//      🔴 Un round-trip perfetto non prova la lettura: il 22 settembre 2026 erano 681/681 file esatti e
//      7 579 righe opache. È questa la misura che deve scendere slice dopo slice.
//
// Nato da RealFileIntegrationTests (§27.1) della libreria A, che nei test tornava verde quando l'albero
// mancava, cioè sempre in CI.

if (args.Length != 1 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("Uso: Vipi.SectorfileProva <cartella SectorFiles/Include/IT>");
    return 2;
}

string radice = Path.GetFullPath(args[0]);
var avvisi = new Avvisi();
var perEstensione = new SortedDictionary<string, (int Esatti, int Diversi, int SenzaLettore)>();
var diversi = new List<string>();

foreach (string percorso in Directory.GetFiles(radice, "*.*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
{
    string estensione = Path.GetExtension(percorso).TrimStart('.').ToLowerInvariant();
    perEstensione.TryGetValue(estensione, out var conto);

    (byte[] Originale, byte[] Riscritto)? esito;
    try
    {
        esito = RoundTrip(estensione, percorso);
    }
    catch (Exception ex)
    {
        conto.Diversi++;
        perEstensione[estensione] = conto;
        diversi.Add($"{Relativo(percorso)} — {ex.GetType().Name}: {ex.Message}");
        continue;
    }

    if (esito is null)
    {
        conto.SenzaLettore++;
    }
    else if (esito.Value.Originale.AsSpan().SequenceEqual(esito.Value.Riscritto))
    {
        conto.Esatti++;
    }
    else
    {
        conto.Diversi++;
        diversi.Add($"{Relativo(percorso)} — {esito.Value.Originale.Length} byte letti, {esito.Value.Riscritto.Length} riscritti");
    }

    perEstensione[estensione] = conto;
}

Console.WriteLine($"Albero: {radice}\n");
Console.WriteLine($"{"estensione",-12} {"esatti",7} {"diversi",8} {"senza lettore",14}");
foreach (var (estensione, conto) in perEstensione)
{
    Console.WriteLine($"{estensione,-12} {conto.Esatti,7} {conto.Diversi,8} {conto.SenzaLettore,14}");
}

int esatti = perEstensione.Values.Sum(c => c.Esatti);
int senzaLettore = perEstensione.Values.Sum(c => c.SenzaLettore);
Console.WriteLine($"\nROUND-TRIP: {esatti} esatti, {diversi.Count} diversi, {senzaLettore} senza lettore");
foreach (string riga in diversi)
{
    Console.WriteLine("  " + riga);
}

var opache = avvisi.Snapshot();
Console.WriteLine($"\nRIGHE OPACHE (avvisi del lettore): {opache.Count}");
foreach (var gruppo in opache
    .GroupBy(a => $"{Path.GetExtension(a.Source),-8} {Regex.Replace(a.Message, "[0-9]+", "#")}")
    .OrderByDescending(g => g.Count()))
{
    var primo = gruppo.First();
    Console.WriteLine($"  {gruppo.Count(),6}  {gruppo.Key}   es. {Path.GetFileName(primo.Source)}:{primo.LineNumber} «{primo.RawSnippet}»");
}

return diversi.Count == 0 ? 0 : 1;

string Relativo(string percorso) => Path.GetRelativePath(radice, percorso);

// Il lettore e lo scrittore per estensione: la stessa scelta di A (§27.1), nome del file compreso.
(byte[], byte[])? RoundTrip(string estensione, string percorso) => estensione switch
{
    "ap" => Prova(new ApParser(avvisi), new ApSaver(), percorso),
    "geo" => Prova(new GeoParser(avvisi), new GeoSaver(), percorso),
    "pol" => Prova(new PolParser(avvisi), new PolSaver(), percorso),
    "txi" => Prova(new TxiParser(avvisi), new TxiSaver(), percorso),
    "gts" => Prova(new GtsParser(avvisi), new GtsSaver(), percorso),
    "sid" => Prova(new SidParser(avvisi), new SidSaver(), percorso),
    "vfi" => Prova(new VfiParser(avvisi), new VfiSaver(), percorso),
    "atis" => Prova(new AtisParser(avvisi), new AtisSaver(), percorso),
    "vor" => Prova(new VorParser(avvisi), new VorSaver(), percorso),
    "ndb" => Prova(new NdbParser(avvisi), new NdbSaver(), percorso),
    "fix" => Prova(new FixParser(avvisi), new FixSaver(), percorso),
    "lairway" or "hairway" => Prova(new AirwayParser(avvisi), new AirwaySaver(), percorso),
    "frq" => Prova(new FrqParser(avvisi), new FrqSaver(), percorso),
    "rw" => Prova(new RwParser(avvisi), new RwSaver(), percorso),
    "str" => Prova(new StrParser(avvisi), new StrSaver(), percorso),
    "hartcc" => Prova(new HartccParser(avvisi), new HartccSaver(), percorso),
    "lartcc" => Prova(new LartccParser(avvisi), new LartccSaver(), percorso),
    "artcc" => Prova(new ArtccParser(avvisi), new ArtccSaver(), percorso),
    "tfl" => Path.GetFileName(percorso).EndsWith("fic.tfl", StringComparison.OrdinalIgnoreCase)
        ? Prova(new FicParser(avvisi), new FicSaver(), percorso)
        : Prova(new TflParser(avvisi), new TflSaver(), percorso),
    "mva" => percorso.Replace('\\', '/').Contains("/ENRMVA/", StringComparison.OrdinalIgnoreCase)
        ? Prova(new MvaEnrouteParser(avvisi), new MvaSaver(enroute: true), percorso)
        : Prova(new MvaAirportParser(avvisi), new MvaSaver(enroute: false), percorso),
    _ => null,
};

static (byte[], byte[]) Prova<T>(IFileParser<T> lettore, IFileSaver<T> scrittore, string percorso)
{
    byte[] originale = File.ReadAllBytes(percorso);
    var letto = lettore.Parse(percorso, new ColorPalette());
    string temporaneo = Path.Combine(Path.GetTempPath(), "sectorfile-prova-" + Guid.NewGuid().ToString("N") + ".tmp");
    try
    {
        new FileSaverOrchestrator().Save(letto, new HashSet<T>(), scrittore, temporaneo);
        return (originale, File.ReadAllBytes(temporaneo));
    }
    finally
    {
        if (File.Exists(temporaneo))
        {
            File.Delete(temporaneo);
        }
    }
}

/// <summary>Raccoglie gli avvisi dei lettori: sono loro a dire quali righe il motore non ha capito.</summary>
sealed class Avvisi : IWarningCollector
{
    private readonly List<LoadWarning> _avvisi = new();

    public event EventHandler<LoadWarning>? WarningAdded;

    public int Count => _avvisi.Count;

    public void Add(LoadWarning warning)
    {
        _avvisi.Add(warning);
        WarningAdded?.Invoke(this, warning);
    }

    public void Add(WarningSeverity severity, WarningCategory category, string source, string message,
                    int? lineNumber = null, string? rawSnippet = null)
        => Add(new LoadWarning(severity, category, source, message, lineNumber, rawSnippet));

    public IReadOnlyList<LoadWarning> Snapshot() => _avvisi.ToArray();

    public void Clear() => _avvisi.Clear();
}
