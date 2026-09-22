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
//   3. LA SCRITTURA. 🔴 Un round-trip perfetto non prova nemmeno la scrittura: riscrive le righe com'erano solo
//      perché nessuno le tocca. Due prove:
//      a. TUTTO TOCCATO — ogni record segnato come modificato senza cambiarlo. Con gli scrittori di A (22
//         settembre) cambiavano 60 750 righe su 257 435; con «riga come campi» (F2 §9.5) zero.
//      b. UNA MODIFICA PER RECORD — il primo punto di ogni record spostato di un millesimo di secondo: deve
//         cambiare una riga per record, e in quella riga un campo. Dalla slice 3: 99 707 su 99 707; dalla
//         slice 4 (punti per nome, si sposta il primo punto PER COORDINATE) 100 098 su 100 098.
//      Il secondo argomento facoltativo è una cartella dove lasciare i file fuori misura, per un diff.
//   4. CONCORDANZA: ogni token DMS letto dal motore e dal DMS di vIPI, stesso esito e stesso valore.
//      Esce 1 se ce n'è uno discorde.
//
// Nato da RealFileIntegrationTests (§27.1) della libreria A, che nei test tornava verde quando l'albero
// mancava, cioè sempre in CI.

if (args.Length is not (1 or 2) || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("Uso: Vipi.SectorfileProva <cartella SectorFiles/Include/IT> [cartella dove lasciare i file fuori misura]");
    return 2;
}

string radice = Path.GetFullPath(args[0]);
// Facoltativa: vi si copiano, coi percorsi dell'albero, i file che con «una modifica per record» cambiano più
// (o meno) righe dei record spostati — per guardarli con un diff.
string? cartellaFuoriMisura = args.Length == 2 ? Path.GetFullPath(args[1]) : null;
var avvisi = new Avvisi();
var perEstensione = new SortedDictionary<string, (int Esatti, int Diversi, int SenzaLettore)>();
var diversi = new List<string>();
var toccato = new List<(string File, int Righe, int Cambiate, string? Esempio)>();
var modifica = new List<(string File, int Spostati, int Cambiate, string? Esempio)>();
var piuDiUnCampo = new List<string>();

foreach (string percorso in Directory.GetFiles(radice, "*.*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
{
    string estensione = Path.GetExtension(percorso).TrimStart('.').ToLowerInvariant();
    perEstensione.TryGetValue(estensione, out var conto);

    (byte[] Originale, byte[] Riscritto)? esito;
    try
    {
        esito = Formati.Usa(percorso, avvisi, new ProvaDelFile(percorso, Relativo(percorso), cartellaFuoriMisura, toccato, modifica, piuDiUnCampo), out var prova)
            ? prova
            : null;
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

    // Dalla slice 5 le opache sono poche, e sono errori veri del sector: si elencano tutte, per gli AOD (93 dalla slice 6).
    if (opache.Count <= 100)
    {
        foreach (var avviso in gruppo.Skip(1))
        {
            Console.WriteLine($"{"",10}{Relativo(avviso.Source)}:{avviso.LineNumber} «{avviso.RawSnippet}»");
        }
    }
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

int spostatiTutti = modifica.Sum(m => m.Spostati);
int cambiateTutte = modifica.Sum(m => m.Cambiate);
Console.WriteLine($"\nUNA MODIFICA PER RECORD: {spostatiTutti} record spostati, {cambiateTutte} righe cambiate " +
    $"(ideale: tante quanti i record), {modifica.Count(m => m.Cambiate != m.Spostati)} file fuori misura, " +
    $"{piuDiUnCampo.Count} righe con più di un campo cambiato");
foreach (string riga in piuDiUnCampo.Take(10))
{
    Console.WriteLine("  " + riga);
}

foreach (var gruppo in modifica.Where(m => m.Cambiate != m.Spostati)
    .GroupBy(m => Path.GetExtension(m.File))
    .OrderByDescending(g => g.Sum(m => Math.Abs(m.Cambiate - m.Spostati))))
{
    var peggiore = gruppo.MaxBy(m => Math.Abs(m.Cambiate - m.Spostati));
    Console.WriteLine($"  {gruppo.Key,-8} {gruppo.Sum(m => m.Spostati),6} spostati {gruppo.Sum(m => m.Cambiate),6} righe in {gruppo.Count(),3} file" +
        $"   es. {peggiore.File} ({peggiore.Spostati} → {peggiore.Cambiate}): {peggiore.Esempio}");
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

// 5. I TAG //@ (F2 slice 7, carta madre §8.2), solo su .sid e .str. Sull'albero com'è: quanti tag e quanti problemi.
//    Poi TAG SU TUTTO: ogni record riceve il suo blocco (dichiarazione con una chiave, START, END) e il file il suo
//    //@source; si salva e si rilegge. Ogni record deve ritrovare i suoi tag, delimitati, senza problemi, e tolte
//    le righe //@ il file deve tornare quello di prima, byte per byte.
int tagNelFile = 0, problemiNelFile = 0, recordEtichettati = 0, recordRitrovati = 0, fileTornati = 0, fileEtichettati = 0;
var guastiDeiTag = new List<string>();
foreach (string percorso in Directory.GetFiles(radice, "*.*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
{
    switch (Path.GetExtension(percorso).ToLowerInvariant())
    {
        case ".sid":
            ProvaITag(new SidParser(avvisi), new SidSaver(), Metadati.NomeSid, percorso);
            break;
        case ".str":
            ProvaITag(new StrParser(avvisi), new StrSaver(), Metadati.NomeStr, percorso);
            break;
    }
}

Console.WriteLine($"\nTAG //@ nell'albero: {tagNelFile} record con tag, {problemiNelFile} problemi");
Console.WriteLine($"TAG SU TUTTO: {recordRitrovati} record ritrovati su {recordEtichettati} etichettati; " +
    $"{fileTornati} file su {fileEtichettati} tornano identici senza le righe //@; {guastiDeiTag.Count} guasti");
foreach (string riga in guastiDeiTag.Take(20))
{
    Console.WriteLine("  " + riga);
}

// 6. IL VALIDATORE (F2 slice 8, carta §3): i problemi del sector per regola, e gli errori uno per uno — sono la lista
//    da passare agli AOD. Non fa uscire 1: il sector ha errori veri, e dirli è il suo mestiere.
var problemiDelSector = Directory.GetFiles(radice, "*.*", SearchOption.AllDirectories)
    .Order(StringComparer.Ordinal)
    .SelectMany(p => Vipi.Sectorfile.Validazione.Validatore.ValidaIlFile(p, Relativo(p)))
    .ToList();
Console.WriteLine($"\nVALIDATORE: {problemiDelSector.Count(p => p.Gravita == Vipi.Sectorfile.Validazione.Gravita.Errore)} errori, " +
    $"{problemiDelSector.Count(p => p.Gravita == Vipi.Sectorfile.Validazione.Gravita.Avviso)} avvisi");
foreach (var gruppo in problemiDelSector.GroupBy(p => (p.Gravita, p.Regola)).OrderBy(g => g.Key))
{
    var primo = gruppo.First();
    Console.WriteLine($"  {gruppo.Count(),6}  {gruppo.Key.Gravita,-7} {gruppo.Key.Regola,-24} es. {primo.File}:{primo.Riga} {primo.Dettaglio}");
}

Console.WriteLine("\nERRORI, uno per uno:");
foreach (var p in problemiDelSector.Where(p => p.Gravita == Vipi.Sectorfile.Validazione.Gravita.Errore).Take(300))
{
    Console.WriteLine($"  {p.File}:{p.Riga}  {p.Regola}  {p.Dettaglio}");
}

return diversi.Count == 0 && discordi.Count == 0 && guastiDeiTag.Count == 0 ? 0 : 1;

void ProvaITag<T>(IFileParser<T> lettore, IFileSaver<T> scrittore, Func<T, string> nomeDi, string percorso)
    where T : class
{
    var letto = lettore.Parse(percorso, new ColorPalette());
    var diOggi = Metadati.Leggi(letto, nomeDi);
    tagNelFile += diOggi.Record.Count;
    problemiNelFile += diOggi.Problemi.Count;

    var etichettato = Metadati.ScriviSorgente(letto, nomeDi, "AIRAC2610");
    try
    {
        foreach (var record in letto.Records)
        {
            etichettato = Metadati.Scrivi(etichettato, record, nomeDi, new Dictionary<string, string> { ["initialclimb"] = "5000" });
        }
    }
    catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
    {
        guastiDeiTag.Add($"{Relativo(percorso)} — non si scrive: {ex.Message}");
        return;
    }

    string temporaneo = Path.Combine(Path.GetTempPath(), "sectorfile-tag-" + Guid.NewGuid().ToString("N") + ".tmp");
    try
    {
        new FileSaverOrchestrator().Save(etichettato, new HashSet<T>(), scrittore, temporaneo);
        var riletto = lettore.Parse(temporaneo, new ColorPalette());
        var metadati = Metadati.Leggi(riletto, nomeDi);

        fileEtichettati++;
        recordEtichettati += riletto.Records.Count;
        recordRitrovati += riletto.Records.Count(r => metadati.Di(r) is { Delimitato: true } m
            && m.Nome == nomeDi(r) && m.Chiavi.GetValueOrDefault("initialclimb") == "5000");
        if (riletto.Records.Count != letto.Records.Count || metadati.Record.Count != riletto.Records.Count
            || metadati.Problemi.Count > 0 || metadati.DelFile.GetValueOrDefault("source") != "AIRAC2610")
        {
            guastiDeiTag.Add($"{Relativo(percorso)} — {letto.Records.Count} record, {riletto.Records.Count} riletti, " +
                $"{metadati.Record.Count} con tag, problemi: {string.Join(", ", metadati.Problemi.Take(3).Select(p => $"{p.Tipo}@{p.Riga} «{p.Testo}»"))}");
        }

        // Tolte le righe //@, i byte di prima: i tag non hanno cambiato nient'altro.
        byte[] originale = File.ReadAllBytes(percorso);
        var lettura = SectorFileReader.Read(temporaneo);
        string senzaTag = string.Join(lettura.NewLine, lettura.Lines.Where(r => !Metadati.EUnTag(r.TrimStart())))
            + (lettura.HasFinalNewLine ? lettura.NewLine : "");
        byte[] ricostruito = (lettura.HasByteOrderMark ? new byte[] { 0xEF, 0xBB, 0xBF } : Array.Empty<byte>())
            .Concat(lettura.Encoding.GetBytes(senzaTag)).ToArray();
        if (originale.AsSpan().SequenceEqual(ricostruito))
        {
            fileTornati++;
        }
        else
        {
            guastiDeiTag.Add($"{Relativo(percorso)} — senza le righe //@ non torna uguale ({originale.Length} → {ricostruito.Length} byte)");
        }
    }
    finally
    {
        File.Delete(temporaneo);
    }
}

string Relativo(string percorso) => Path.GetRelativePath(radice, percorso);

/// <summary>Le misure 1 e 3 su un file, col lettore e lo scrittore che sceglie <see cref="Formati"/>.</summary>
sealed class ProvaDelFile(
    string percorso,
    string relativo,
    string? cartellaFuoriMisura,
    List<(string File, int Righe, int Cambiate, string? Esempio)> toccato,
    List<(string File, int Spostati, int Cambiate, string? Esempio)> modifica,
    List<string> piuDiUnCampo) : IUsoDelFormato<(byte[], byte[])>
{
    public (byte[], byte[]) Usa<T>(IFileParser<T> lettore, IFileSaver<T> scrittore)
        where T : class
    {
        byte[] originale = File.ReadAllBytes(percorso);
        var letto = lettore.Parse(percorso, new ColorPalette()).FissaLeBasi(scrittore);
        string temporaneo = Path.Combine(Path.GetTempPath(), "sectorfile-prova-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            new FileSaverOrchestrator().Save(letto, new HashSet<T>(), scrittore, temporaneo);
            byte[] riscritto = File.ReadAllBytes(temporaneo);

            // 3a. TUTTO TOCCATO: ogni record segnato come modificato senza cambiarlo. Con «riga come campi» (F2
            //     §9.5) deve uscire identico per costruzione; è il controllo che la costruzione regge.
            var prima = File.ReadAllLines(percorso);
            new FileSaverOrchestrator().Save(letto, new HashSet<T>(letto.Records), scrittore, temporaneo);
            var (cambiate, esempio) = Differenze(prima, File.ReadAllLines(temporaneo));
            toccato.Add((relativo, prima.Length, cambiate, esempio));

            // 3b. UNA MODIFICA PER RECORD: il primo punto di ogni record spostato di un millesimo di secondo d'arco
            //     in latitudine. Ogni record spostato deve cambiare UNA riga, e nient'altro deve cambiare.
            var spostati = new HashSet<T>(letto.Records.Where(r => r is not null && Sposta(r)));
            new FileSaverOrchestrator().Save(letto, spostati, scrittore, temporaneo);
            var dopoLoSpostamento = File.ReadAllLines(temporaneo);
            var (cambiateSpostando, esempioSpostando) = Differenze(prima, dopoLoSpostamento);
            modifica.Add((relativo, spostati.Count, cambiateSpostando, esempioSpostando));

            // …e in ogni riga cambiata deve cambiare UN campo, la latitudine spostata.
            if (prima.Length == dopoLoSpostamento.Length)
            {
                foreach (var (riga, nuova) in prima.Zip(dopoLoSpostamento).Where(c => c.First != c.Second))
                {
                    string[] a = riga.Split(';'), b = nuova.Split(';');
                    if (a.Length != b.Length || a.Zip(b).Count(c => c.First != c.Second) != 1)
                    {
                        piuDiUnCampo.Add($"{relativo}: «{riga}» → «{nuova}»");
                    }
                }
            }
            if (cartellaFuoriMisura is not null && cambiateSpostando != spostati.Count)
            {
                string copia = Path.Combine(cartellaFuoriMisura, relativo);
                Directory.CreateDirectory(Path.GetDirectoryName(copia)!);
                File.Copy(temporaneo, copia, overwrite: true);
            }

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

    // Le righe cambiate fra due versioni di un file, e la prima coppia diversa come esempio. Stessa lunghezza:
    // riga per riga; lunghezze diverse: tutte le righe fuori dalla testa e dalla coda comuni.
    static (int Cambiate, string? Esempio) Differenze(string[] prima, string[] dopo)
    {
        int testa = 0;
        while (testa < prima.Length && testa < dopo.Length && prima[testa] == dopo[testa])
        {
            testa++;
        }

        if (testa == prima.Length && testa == dopo.Length)
        {
            return (0, null);
        }

        string esempio = $"«{(testa < prima.Length ? prima[testa] : "")}» → «{(testa < dopo.Length ? dopo[testa] : "")}»";
        if (prima.Length == dopo.Length)
        {
            return (prima.Zip(dopo).Count(c => c.First != c.Second), esempio);
        }

        int coda = 0;
        while (coda < prima.Length - testa && coda < dopo.Length - testa && prima[^(coda + 1)] == dopo[^(coda + 1)])
        {
            coda++;
        }

        return (Math.Max(prima.Length, dopo.Length) - testa - coda, esempio);
    }

    // Sposta di un millesimo di secondo d'arco il PRIMO punto del record, dovunque stia: una proprietà Coordinate
    // (o Coordinate? valorizzata), il primo elemento di una lista di Coordinate, o il primo elemento di una lista
    // di oggetti che ne hanno una. Falso se il record non ha punti.
    static bool Sposta(object record)
    {
        const double UnMillesimo = 1 / 3_600_000.0;
        static Coordinate Spostata(Coordinate c) => new(c.LatitudeDeg + UnMillesimo, c.LongitudeDeg);

        foreach (var p in record.GetType().GetProperties())
        {
            if (p.GetIndexParameters().Length > 0)
            {
                continue;
            }

            if (p.PropertyType == typeof(Coordinate) && p.CanWrite)
            {
                p.SetValue(record, Spostata((Coordinate)p.GetValue(record)!));
                return true;
            }

            if (p.PropertyType == typeof(Coordinate?) && p.CanWrite && p.GetValue(record) is Coordinate c)
            {
                p.SetValue(record, Spostata(c));
                return true;
            }

            if (p.PropertyType == typeof(Punto) && p.CanWrite && ((Punto)p.GetValue(record)!).Posizione is { } posizione)
            {
                p.SetValue(record, Punto.Da(Spostata(posizione)));
                return true;
            }

            if (p.GetValue(record) is IList<Coordinate> { Count: > 0 } punti)
            {
                punti[0] = Spostata(punti[0]);
                return true;
            }

            // Il primo punto PER COORDINATE: un punto per nome non ha niente da spostare (F2 slice 4).
            if (p.GetValue(record) is IList<Punto> vertici)
            {
                for (int i = 0; i < vertici.Count; i++)
                {
                    if (vertici[i].Posizione is { } v)
                    {
                        vertici[i] = Punto.Da(Spostata(v));
                        return true;
                    }
                }
            }

            if (p.GetValue(record) is System.Collections.IList elenco)
            {
                foreach (object? elemento in elenco)
                {
                    if (elemento is not null && elemento.GetType().IsClass && elemento is not string && Sposta(elemento))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
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
