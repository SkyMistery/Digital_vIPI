using System.Text.RegularExpressions;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

// La prova del motore del sector sull'albero intero (carta F2, docs/feature/2026-09-22-f2-motore-del-sector.md §5).
//
//   dotnet run --project tools/Vipi.SectorfileProva -- <cartella SectorFiles/Include/IT>
//
// Quattro misure, e nessuna basta da sola:
//   1. ROUND-TRIP: ogni file letto e riscritto deve uscire identico byte per byte. Esce 1 se uno non lo è.
//   2. RIGHE OPACHE: quelle che il motore conserva senza capirle («Skipping malformed line» e simili).
//      🔴 Un round-trip perfetto non prova la lettura: il 22 settembre 2026 erano 681/681 file esatti e
//      7 579 righe opache.
//   3. TUTTO TOCCATO: ogni record segnato come modificato e riscritto dallo scrittore. 🔴 Un round-trip
//      perfetto non prova nemmeno la SCRITTURA: riscrive le righe com'erano solo perché nessuno le tocca.
//      Il 22 settembre, tutto toccato, cambiavano 60 750 righe su 257 435 (commenti riattivati, campi in
//      coda persi, terminatori spariti). È questa, con la 2, la misura che deve scendere a zero.
//   4. CONCORDANZA: ogni token DMS letto dal motore e dal DMS di vIPI, stesso esito e stesso valore.
//      Esce 1 se ce n'è uno discorde.
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
var toccato = new List<(string File, int Righe, int Cambiate, string? Esempio)>();

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

Console.WriteLine($"\nTUTTO TOCCATO: {toccato.Sum(t => t.Cambiate)} righe cambiate su {toccato.Sum(t => t.Righe)}, " +
    $"in {toccato.Count(t => t.Cambiate > 0)} file su {toccato.Count}");
foreach (var gruppo in toccato.Where(t => t.Cambiate > 0)
    .GroupBy(t => Path.GetExtension(t.File))
    .OrderByDescending(g => g.Sum(t => t.Cambiate)))
{
    var peggiore = gruppo.MaxBy(t => t.Cambiate);
    Console.WriteLine($"  {gruppo.Key,-8} {gruppo.Sum(t => t.Cambiate),7} righe in {gruppo.Count(),3} file   es. {peggiore.File}: {peggiore.Esempio}");
}

// 4. CONCORDANZA: vIPI legge il sector col suo DMS (Vipi.Application/Coordinates/DmsCoordinate), il motore col
//    suo. Due lettori dello stesso formato devono dire la stessa cosa su OGNI token dell'albero: tutti e due
//    lo accettano con lo stesso valore (entro un decimillesimo di secondo d'arco), o tutti e due lo rifiutano.
var discordi = new List<string>();
int tokenDms = 0;
foreach (string percorso in Directory.GetFiles(radice, "*.*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
{
    int numero = 0;
    foreach (string riga in File.ReadLines(percorso))
    {
        numero++;
        foreach (string campo in riga.Split(';'))
        {
            string token = campo.Trim();
            if (token.Length < 2 || "NSEWnsew".IndexOf(token[0]) < 0 || !char.IsAsciiDigit(token[1]))
            {
                continue;
            }

            tokenDms++;
            bool perVipi = Vipi.Application.Coordinates.DmsCoordinate.TryParse(token, out double valoreVipi);
            bool perMotore;
            double valoreMotore;
            try
            {
                var letto = CoordinateConverter.Parse(token);
                valoreMotore = char.ToUpperInvariant(token[0]) is 'N' or 'S' ? letto.LatitudeDeg : letto.LongitudeDeg;
                perMotore = true;
            }
            catch (CoordinateParseException)
            {
                valoreMotore = double.NaN;
                perMotore = false;
            }

            if (perVipi != perMotore || (perVipi && Math.Abs(valoreVipi - valoreMotore) > 1e-4 / 3600))
            {
                discordi.Add($"{Relativo(percorso)}:{numero} «{token}» — vIPI {(perVipi ? valoreVipi.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : "rifiuta")}, motore {(perMotore ? valoreMotore.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : "rifiuta")}");
            }
        }
    }
}

Console.WriteLine($"\nCONCORDANZA col DMS di vIPI: {tokenDms} token DMS, {discordi.Count} discordi");
foreach (string riga in discordi.Take(40))
{
    Console.WriteLine("  " + riga);
}

return diversi.Count == 0 && discordi.Count == 0 ? 0 : 1;

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

(byte[], byte[]) Prova<T>(IFileParser<T> lettore, IFileSaver<T> scrittore, string percorso)
{
    byte[] originale = File.ReadAllBytes(percorso);
    var letto = lettore.Parse(percorso, new ColorPalette());
    string temporaneo = Path.Combine(Path.GetTempPath(), "sectorfile-prova-" + Guid.NewGuid().ToString("N") + ".tmp");
    try
    {
        new FileSaverOrchestrator().Save(letto, new HashSet<T>(), scrittore, temporaneo);
        byte[] riscritto = File.ReadAllBytes(temporaneo);

        // 3. TUTTO TOCCATO: ogni record segnato come modificato, e riscritto dallo scrittore. Un record toccato
        //    ma non cambiato dovrebbe uscire com'era; le righe che non lo fanno sono quelle che un AOD vedrebbe
        //    cambiate nella PR senza averle cambiate.
        new FileSaverOrchestrator().Save(letto, new HashSet<T>(letto.Records), scrittore, temporaneo);
        var prima = File.ReadAllLines(percorso);
        var dopo = File.ReadAllLines(temporaneo);
        int cambiate = prima.Length == dopo.Length
            ? prima.Zip(dopo).Count(c => c.First != c.Second)
            : Math.Max(prima.Length, dopo.Length);
        toccato.Add((Relativo(percorso), prima.Length, cambiate,
            prima.Zip(dopo).Where(c => c.First != c.Second).Select(c => $"«{c.First}» → «{c.Second}»").FirstOrDefault()));

        return (originale, riscritto);
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
