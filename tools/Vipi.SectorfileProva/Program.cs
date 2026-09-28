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
//   5. I TAG //@ di .sid e .str (slice 7). 6. IL VALIDATORE (slice 8).
//   7. CONCORDANZA col lettore di vIPI (slice 9): punti, SID e STAR contro AuroraSectorfileParser (Concordanza.cs).
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
var commenti = new List<(string File, int NelFile, int Scritti, string? Esempio, string? CambiaSpostandoli)>();

foreach (string percorso in Directory.GetFiles(radice, "*.*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
{
    string estensione = Path.GetExtension(percorso).TrimStart('.').ToLowerInvariant();
    perEstensione.TryGetValue(estensione, out var conto);

    (byte[] Originale, byte[] Riscritto)? esito;
    try
    {
        esito = Formati.Usa(percorso, avvisi, new ProvaDelFile(percorso, Relativo(percorso), cartellaFuoriMisura, toccato, modifica, piuDiUnCampo, commenti), out var prova)
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

// 5b. Il <br> nel modello (F3-bis slice 3): in ogni record di .str/.sid per nome o misto, i <br> delle righe di corpo
//     devono essere tanti quanti ne scrive lo scrittore dal modello. Fino alla slice 3 si perdevano.
int brNelleRighe = 0, brDalModello = 0, recordConBrDiversi = 0;
foreach (string percorso in Directory.GetFiles(radice, "*.*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
{
    if (Path.GetExtension(percorso).ToLowerInvariant() is not (".str" or ".sid"))
    {
        continue;
    }

    foreach (var pezzo in new StrParser(avvisi).Parse(percorso, new ColorPalette()).Chunks.OfType<RecordChunk<StrRecord>>())
    {
        if (pezzo.Record is not (ProcedureStrRecord or HoldingStrRecord))
        {
            continue;
        }

        int nelleRighe = pezzo.RawLines.Skip(1).Count(r => !r.TrimStart().StartsWith("//", StringComparison.Ordinal)
            && r.Split(';') is { Length: >= 3 } campi && campi[2].Trim() == "<br>");
        int dalModello = new StrSaver().Serialize(pezzo.Record).Skip(1).Count(r => r.EndsWith("<br>", StringComparison.Ordinal));
        brNelleRighe += nelleRighe;
        brDalModello += dalModello;
        recordConBrDiversi += nelleRighe == dalModello ? 0 : 1;
    }
}

Console.WriteLine($"<br> NEL MODELLO (record per nome e misti): {brNelleRighe} nelle righe, {brDalModello} dal modello, " +
    $"{recordConBrDiversi} record diversi");

// Solo i file dell'albero, una volta sola: le misure che rileggono un file (commenti spostati, slice 2a) o una sua
// copia temporanea riportano gli stessi avvisi, e non sono righe opache in più.
string cartellaTemporanea = Path.GetTempPath();
var opache = avvisi.Snapshot()
    .Where(a => !a.Source.StartsWith(cartellaTemporanea, StringComparison.OrdinalIgnoreCase))
    .DistinctBy(a => (a.Source, a.LineNumber, a.Message))
    .ToList();
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

Console.WriteLine($"\nCOMMENTI IN CODA: {commenti.Sum(c => c.NelFile)} righe in {commenti.Count(c => c.NelFile > 0)} file letti dal motore; " +
    $"gli scrittori, riscrivendo ogni record dal modello, ne scrivono {commenti.Sum(c => c.Scritti)}");
foreach (var (file, _, scritti, esempio, _) in commenti.Where(c => c.Scritti > 0).Take(10))
{
    Console.WriteLine($"  {file}: {scritti}, es. «{esempio}»");
}

Console.WriteLine($"SPOSTATI SOPRA (il gesto del Lab su ogni file): {commenti.Count(c => c.NelFile > 0 && c.CambiaSpostandoli is null)} file su " +
    $"{commenti.Count(c => c.NelFile > 0)} riletti con gli stessi record");
foreach (var (file, _, _, _, cambia) in commenti.Where(c => c.CambiaSpostandoli is not null))
{
    Console.WriteLine($"  {file}: {cambia}");
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

// 5. I TAG //@ (F2 slice 7, carta madre §8.2; lotto «Subito» slice 1b: anche i file a una riga per record). Sull'albero com'è: quanti tag e quanti problemi.
//    Poi TAG SU TUTTO: ogni record riceve il suo blocco (dichiarazione con una chiave, START, END) e il file il suo
//    //@source; si salva e si rilegge. Ogni record deve ritrovare i suoi tag, delimitati, senza problemi, e tolte
//    le righe //@ il file deve tornare quello di prima, byte per byte. Le MAPS dei .str ricevono anche `composta`
//    (F3-bis slice 3): i loro nomi hanno spazi, e si scrivono fra virgolette.
//    Slice 1d, i file a blocchi: un record senza nome suo (.pol, .geo di scalo, BREAK) riceve il blocco col nome
//    «PROVA»; ogni punto delle aerovie il suo //@@. Poi BLOCCHI A PIÙ PEZZI: i record di fila con lo stesso nome (o
//    senza) in un blocco solo — la zona MVA, il gruppo del .geo, l'aerovia coi BREAK — e ognuno deve ritrovarlo.
int tagNelFile = 0, problemiNelFile = 0, recordEtichettati = 0, recordRitrovati = 0, fileTornati = 0, fileEtichettati = 0;
int puntiEtichettati = 0, puntiRitrovati = 0;
int bloccoRecord = 0, bloccoRitrovati = 0, blocchiScritti = 0, blocchiAPiuPezzi = 0, bloccoFile = 0, bloccoFileTornati = 0;
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
        // Lotto «Subito» slice 1b: i file a una riga per record («file per file» §M).
        case ".rw":
            ProvaITag(new RwParser(avvisi), new RwSaver(), r => Metadati.NomeDelRecord(r), percorso);
            break;
        case ".ap":
            ProvaITag(new ApParser(avvisi), new ApSaver(), r => Metadati.NomeDelRecord(r), percorso);
            break;
        case ".gts":
            ProvaITag(new GtsParser(avvisi), new GtsSaver(), r => Metadati.NomeDelRecord(r), percorso);
            break;
        case ".txi":
            ProvaITag(new TxiParser(avvisi), new TxiSaver(), r => Metadati.NomeDelRecord(r), percorso);
            break;
        case ".fix":
            ProvaITag(new FixParser(avvisi), new FixSaver(), r => Metadati.NomeDelRecord(r), percorso);
            break;
        case ".vor":
            ProvaITag(new VorParser(avvisi), new VorSaver(), r => Metadati.NomeDelRecord(r), percorso);
            break;
        case ".ndb":
            ProvaITag(new NdbParser(avvisi), new NdbSaver(), r => Metadati.NomeDelRecord(r), percorso);
            break;
        case ".vfi":
            ProvaITag(new VfiParser(avvisi), new VfiSaver(), r => Metadati.NomeDelRecord(r), percorso);
            break;
        case ".frq":
            ProvaITag(new FrqParser(avvisi), new FrqSaver(), r => Metadati.NomeDelRecord(r), percorso);
            break;
        case ".hold":
            ProvaITag(new HoldParser(avvisi), new HoldSaver(), r => Metadati.NomeDelRecord(r), percorso);
            break;
        // Lotto «Subito» slice 1d: i file a blocchi (lettori scelti come in Formati: ENRMVA/ e *fic.tfl a parte).
        case ".artcc":
            ProvaITag(new ArtccParser(avvisi), new ArtccSaver(), r => Metadati.NomeDelRecord(r), percorso);
            break;
        case ".lairway" or ".hairway":
            ProvaITag(new AirwayParser(avvisi), new AirwaySaver(), r => Metadati.NomeDelRecord(r), percorso);
            break;
        case ".mva" when percorso.Replace('\\', '/').Contains("/ENRMVA/", StringComparison.OrdinalIgnoreCase):
            ProvaITag(new MvaEnrouteParser(avvisi), new MvaSaver(enroute: true), r => Metadati.NomeDelRecord(r), percorso);
            break;
        case ".mva":
            ProvaITag(new MvaAirportParser(avvisi), new MvaSaver(enroute: false), r => Metadati.NomeDelRecord(r), percorso);
            break;
        case ".tfl" when Path.GetFileName(percorso).EndsWith("fic.tfl", StringComparison.OrdinalIgnoreCase):
            ProvaITag(new FicParser(avvisi), new FicSaver(), r => Metadati.NomeDelRecord(r), percorso);
            break;
        case ".tfl":
            ProvaITag(new TflParser(avvisi), new TflSaver(), r => Metadati.NomeDelRecord(r), percorso);
            break;
        case ".hartcc":
            ProvaITag(new HartccParser(avvisi), new HartccSaver(), r => Metadati.NomeDelRecord(r), percorso);
            break;
        case ".lartcc":
            ProvaITag(new LartccParser(avvisi), new LartccSaver(), r => Metadati.NomeDelRecord(r), percorso);
            break;
        case ".geo" or ".restrict" or ".prohibit" or ".danger":
            ProvaITag(new GeoParser(avvisi), new GeoSaver(), r => Metadati.NomeDelRecord(r), percorso);
            break;
        case ".pol":
            ProvaITag(new PolParser(avvisi), new PolSaver(), r => Metadati.NomeDelRecord(r), percorso);
            break;
        // Slice 1e: le rotte VFR di scalo, coi //@@ per tratto (F8, S6).
        case ".vrt":
            ProvaITag(new VrtParser(avvisi), new VrtSaver(), r => Metadati.NomeDelRecord(r), percorso);
            break;
    }
}

Console.WriteLine($"\nTAG //@ nell'albero: {tagNelFile} record con tag, {problemiNelFile} problemi");
Console.WriteLine($"TAG SU TUTTO: {recordRitrovati} record ritrovati su {recordEtichettati} etichettati; " +
    $"{fileTornati} file su {fileEtichettati} tornano identici senza le righe //@; {guastiDeiTag.Count} guasti");
Console.WriteLine($"TAG DEI PUNTI: {puntiRitrovati} punti di SID, STAR, aerovie e rotte VFR ritrovati coi loro //@@ su {puntiEtichettati} etichettati");
Console.WriteLine($"BLOCCHI A PIÙ PEZZI: {bloccoRitrovati} record ritrovati nel loro blocco su {bloccoRecord}, in {blocchiScritti} blocchi " +
    $"({blocchiAPiuPezzi} con più di un record); {bloccoFileTornati} file su {bloccoFile} tornano identici senza le righe //@");
foreach (string riga in guastiDeiTag.Take(20))
{
    Console.WriteLine("  " + riga);
}

// 6. IL VALIDATORE (F2 slice 8, carta §3): i problemi del sector per regola, e gli errori uno per uno — sono la lista
//    da passare agli AOD. Non fa uscire 1: il sector ha errori veri, e dirli è il suo mestiere.
//    Con gli .isc (due cartelle sopra: SectorFiles/Include/IT) anche le regole dell'albero; senza, quelle dei file.
string cartellaSectorFiles = Path.GetFullPath(Path.Combine(radice, "..", ".."));
var problemiDelSector = Directory.GetFiles(cartellaSectorFiles, "*.isc").Length > 0
    ? Vipi.Sectorfile.Validazione.Validatore.ValidaLAlbero(cartellaSectorFiles).ToList()
    : Directory.GetFiles(radice, "*.*", SearchOption.AllDirectories)
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

// Le correzioni proposte (lotto «Subito» slice 2c): applicate a una copia del file, le righe corrette non devono avere
// più problemi di coordinate, e il lettore deve capirle tutte.
var conProposta = problemiDelSector.Where(p => p.Proposta is not null).ToList();
int righeCorrette = 0, righeAncoraStorte = 0;
foreach (var perFile in conProposta.GroupBy(p => p.File))
{
    string originale = Path.Combine(cartellaSectorFiles, perFile.Key);
    var lettura = SectorFileReader.Read(originale);
    var righe = lettura.Lines.ToList();
    var corrette = perFile.GroupBy(p => p.Riga).ToDictionary(g => g.Key, g => g.First().Proposta!);
    foreach (var (riga, proposta) in corrette)
    {
        righe[riga - 1] = proposta;
    }

    string copia = Path.Combine(Path.GetTempPath(), "sectorfile-correzioni-" + Guid.NewGuid().ToString("N") + Path.GetExtension(originale));
    try
    {
        File.WriteAllText(copia, string.Join(lettura.NewLine, righe) + (lettura.HasFinalNewLine ? lettura.NewLine : ""), lettura.Encoding);
        var dopo = Vipi.Sectorfile.Validazione.Validatore.ValidaIlFile(copia, perFile.Key);
        foreach (int riga in corrette.Keys)
        {
            bool storta = dopo.Any(p => p.Riga == riga && p.Regola is not (Vipi.Sectorfile.Validazione.Regola.CommentoInCoda
                or Vipi.Sectorfile.Validazione.Regola.TagNonValido or Vipi.Sectorfile.Validazione.Regola.TagFuoriCatalogo));
            righeCorrette += storta ? 0 : 1;
            righeAncoraStorte += storta ? 1 : 0;
            if (storta)
            {
                Console.WriteLine($"  ancora storta: {perFile.Key}:{riga} «{corrette[riga]}»");
            }
        }
    }
    finally
    {
        File.Delete(copia);
    }
}

Console.WriteLine($"\nCORREZIONI PROPOSTE: {conProposta.Count} problemi con la proposta, su {conProposta.Select(p => (p.File, p.Riga)).Distinct().Count()} righe; " +
    $"applicate a una copia, {righeCorrette} righe tornano pulite, {righeAncoraStorte} no");
foreach (var gruppo in conProposta.GroupBy(p => p.Regola).OrderBy(g => g.Key))
{
    Console.WriteLine($"  {gruppo.Count(),5}  {gruppo.Key,-24} es. {gruppo.First().File}:{gruppo.First().Riga} «{gruppo.First().Testo.Trim()}» → «{gruppo.First().Proposta}»");
}

// I file e gli .isc (lotto «Subito» slice 2b): uno per uno, col dettaglio — sono pochi, e sono la lista per gli AOD.
Console.WriteLine("\nFILE E INCLUDE:");
foreach (var p in problemiDelSector.Where(p => p.Regola is Vipi.Sectorfile.Validazione.Regola.FileMaiCitato
             or Vipi.Sectorfile.Validazione.Regola.FileCitatoAssente or Vipi.Sectorfile.Validazione.Regola.FileInclusoDueVolte
             or Vipi.Sectorfile.Validazione.Regola.FileNellaSezioneSbagliata or Vipi.Sectorfile.Validazione.Regola.FileVuoto)
             .OrderBy(p => p.Regola))
{
    Console.WriteLine($"  {p.Regola,-26} {p.File}{(p.Riga > 0 ? ":" + p.Riga : "")}  {p.Dettaglio}");
}

// I COLORI (lotto «Subito» slice 4): gli schemi di Aurora (ColorSchemes\ accanto a SectorFiles, e i .clr dentro)
// e i colors.def si leggono senza avvisi; ogni colore scritto nell'albero (teste di .tfl/.pol, 5° campo dei .geo e
// delle aree P/R/D) è un nome di colors.def, un nome che lo schema colora da sé (manuale) o un valore.
var avvisiDeiColori = new Avvisi();   // solo schemi e .def: le righe opache dei .geo le conta già la misura 2
var schemi = new List<(string File, SchemaDeiColori Schema)>();
string cartellaDegliSchemi = Path.Combine(cartellaSectorFiles, "..", "ColorSchemes");
foreach (string clr in (Directory.Exists(cartellaDegliSchemi) ? Directory.GetFiles(cartellaDegliSchemi, "*.clr") : [])
             .Concat(Directory.GetFiles(cartellaSectorFiles, "*.clr", SearchOption.AllDirectories)).Order(StringComparer.Ordinal))
{
    schemi.Add((Path.GetRelativePath(Path.Combine(cartellaSectorFiles, ".."), clr).Replace('\\', '/'), new ClrParser(avvisiDeiColori).Parse(clr)));
}

var definiti = new ColorPalette();
foreach (string def in Directory.GetFiles(radice, "*.def", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
{
    foreach (var voce in new DefParser(avvisiDeiColori).Parse(def).Entries.Values)
    {
        definiti.Add(voce);
    }
}

var usiDeiColori = new SortedDictionary<string, (int Volte, string Come)>(StringComparer.OrdinalIgnoreCase);
void ContaIlColore(string? scritto, string dove)
{
    string nome = (scritto ?? string.Empty).Trim();
    if (nome.Length == 0)
    {
        return;
    }

    string come = definiti.TryResolve(nome, out _) ? "colors.def"
        : NomiDeiColoriDelGeo.Chiave(nome) is { } chiave && dove == "geo" ? "schema " + chiave
        : ColoreDelSector.TryLeggi(nome, out _, out var forma) ? "valore " + forma
        : "SCONOSCIUTO";
    usiDeiColori.TryGetValue(dove + " " + nome, out var uso);
    usiDeiColori[dove + " " + nome] = (uso.Volte + 1, come);
}

foreach (string percorso in Directory.GetFiles(radice, "*.*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
{
    switch (Path.GetExtension(percorso).ToLowerInvariant())
    {
        case ".pol":
            foreach (var p in new PolParser(new Avvisi()).Parse(percorso, new ColorPalette()).Records)
            {
                ContaIlColore(p.FillColor, "pol");
                ContaIlColore(p.LineColor, "pol");
            }

            break;
        case ".tfl":
            foreach (var s in new TflParser(new Avvisi()).Parse(percorso, new ColorPalette()).Records)
            {
                ContaIlColore(s.FillColor, "tfl");
                ContaIlColore(s.StrokeColor, "tfl");
            }

            break;
        case ".geo" or ".restrict" or ".prohibit" or ".danger":
            foreach (var l in new GeoParser(new Avvisi()).Parse(percorso, new ColorPalette()).Records)
            {
                ContaIlColore(l.Color, "geo");
            }

            break;
    }
}

Console.WriteLine($"\nCOLORI: {schemi.Count} schemi, {definiti.Entries.Count} nomi definiti, {avvisiDeiColori.Count} avvisi di lettura; " +
    $"{usiDeiColori.Values.Sum(u => u.Volte)} colori scritti nell'albero, {usiDeiColori.Values.Where(u => u.Come == "SCONOSCIUTO").Sum(u => u.Volte)} sconosciuti");
foreach (var (file, schema) in schemi)
{
    var mancanti = NomiDeiColoriDelGeo.ChiaveDelloSchema.Values.Distinct().Where(k => !schema.TryColore(k, out _)).ToList();
    Console.WriteLine($"  {file,-40} {schema.Colori.Count,4} colori ({schema.Colori.Values.Count(c => c is null)} clNone), " +
        $"{schema.Altri.Count,3} impostazioni; chiavi del .geo mancanti: {(mancanti.Count == 0 ? "nessuna" : string.Join(", ", mancanti))}");
}

foreach (var gruppo in usiDeiColori.GroupBy(u => u.Value.Come).OrderBy(g => g.Key, StringComparer.Ordinal))
{
    Console.WriteLine($"  {gruppo.Sum(u => u.Value.Volte),7}  {gruppo.Key,-28} {string.Join(", ", gruppo.OrderByDescending(u => u.Value.Volte).Take(8).Select(u => $"{u.Key} {u.Value.Volte}"))}");
}

// 7. CONCORDANZA col lettore di vIPI (F2 slice 9, carta §2.4): AuroraSectorfileParser, quello dell'import di
//    produzione, contro il motore. I punti dei file che l'.isc cita (come li sceglie AuroraNavaidSource), le SID e le
//    STAR degli <icao>.sid/.str della cartella (come le scarica AuroraProcedureProvider).
//    Non fa uscire 1: una differenza che è un difetto di vIPI non si corregge in F2 (l'import di produzione non
//    cambia, carta §4) e resta scritta nei lavori aperti; le differenze si leggono qui, una per una.
var fileDiPunti = File.Exists(Path.Combine(cartellaSectorFiles, "ITALY.isc"))
    ? Vipi.Infrastructure.Sectorfile.AuroraNavaidSource.FileDiPunti(File.ReadAllText(Path.Combine(cartellaSectorFiles, "ITALY.isc")))
    : Array.Empty<(Vipi.Application.Abstractions.NavaidKind Kind, string Path)>();
var colLettoreDiVipi = new List<(string Cosa, string File, Vipi.SectorfileProva.Concordanza.Esito Esito)>();
foreach (var (natura, relativo) in fileDiPunti)
{
    string percorso = Path.Combine(radice, relativo);
    if (File.Exists(percorso))
    {
        colLettoreDiVipi.Add(("punti", relativo, Vipi.SectorfileProva.Concordanza.DeiPunti(percorso, natura)));
    }
}

foreach (string percorso in Directory.GetFiles(radice, "*.sid").Concat(Directory.GetFiles(radice, "*.str")).Order(StringComparer.Ordinal))
{
    bool star = percorso.EndsWith(".str", StringComparison.OrdinalIgnoreCase);
    colLettoreDiVipi.Add((star ? "STAR" : "SID", Relativo(percorso), Vipi.SectorfileProva.Concordanza.DelleProcedure(percorso, star)));
}

Console.WriteLine($"\nCONCORDANZA col lettore di vIPI: {fileDiPunti.Count} file di punti dall'.isc, " +
    $"{colLettoreDiVipi.Count(p => p.Cosa == "SID")} .sid, {colLettoreDiVipi.Count(p => p.Cosa == "STAR")} .str");
foreach (var gruppo in colLettoreDiVipi.GroupBy(p => p.Cosa))
{
    Console.WriteLine($"  {gruppo.Key,-6} {gruppo.Sum(p => p.Esito.Concordi),7} concordi, {gruppo.Sum(p => p.Esito.Discordi.Count)} discordi, " +
        $"{gruppo.Sum(p => p.Esito.SoloVipi.Count)} solo vIPI, {gruppo.Sum(p => p.Esito.SoloMotore.Count)} solo motore, " +
        $"{gruppo.Sum(p => p.Esito.RifiutatiDaEntrambi.Count)} rifiutati da tutti e due, " +
        $"{gruppo.Count(p => !p.Esito.Pulito)} file su {gruppo.Count()} con differenze");
}

foreach (var (cosa, file, esito) in colLettoreDiVipi.Where(p => !p.Esito.Pulito))
{
    foreach (string riga in esito.Discordi)
    {
        Console.WriteLine($"  {file}  discorde      {riga}");
    }

    foreach (string riga in esito.SoloVipi)
    {
        Console.WriteLine($"  {file}  solo vIPI     {riga}");
    }

    foreach (string riga in esito.SoloMotore)
    {
        Console.WriteLine($"  {file}  solo motore   {riga}");
    }
}

return diversi.Count == 0 && discordi.Count == 0 && guastiDeiTag.Count == 0 && recordConBrDiversi == 0 ? 0 : 1;

void ProvaITag<T>(IFileParser<T> lettore, IFileSaver<T> scrittore, Func<T, string?> nomeDi, string percorso)
    where T : class
{
    var letto = lettore.Parse(percorso, new ColorPalette());
    var diOggi = Metadati.Leggi(letto, nomeDi);
    tagNelFile += diOggi.Record.Count;
    problemiNelFile += diOggi.Problemi.Count;

    // Un file senza record (i .fix vuoti di NAVAIDS) non ha niente da etichettare: il solo //@source, tolto, lascerebbe
    // un a capo che il file non aveva.
    if (letto.Records.Count == 0)
    {
        return;
    }

    // Il nome del blocco: quello del record, o «PROVA» per chi non ne ha uno suo (slice 1d).
    string NomeDellaProva(T record) => nomeDi(record) ?? "PROVA";

    var etichettato = Metadati.ScriviSorgente(letto, nomeDi, "AIRAC2610");
    try
    {
        foreach (var record in letto.Records)
        {
            // `note` è una chiave comune a ogni file (§M), e fra virgolette prova anche i valori con spazi.
            var chiavi = new Dictionary<string, string> { ["note"] = "\"prova dei tag\"" };
            if (record is StrRecord { RunwaySpec: "MAPS" })
            {
                chiavi[Metadati.Compose] = "ODINA4E,25:NENI5A";
            }

            etichettato = nomeDi(record) is null
                ? Metadati.ScriviIlBlocco(etichettato, record, record, nomeDi, "PROVA", chiavi)
                : Metadati.Scrivi(etichettato, record, nomeDi, chiavi);
        }

        // Lotto «Subito» slice 1c: ogni punto di SID e STAR riceve il suo //@@, dal fondo del record verso l'alto
        // (un tag in più non sposta le righe che restano da fare). Slice 1d: anche ogni punto delle aerovie (B2), che
        // non hanno l'intestazione; slice 1e: e delle rotte VFR (F8, S6).
        if (letto.Records.FirstOrDefault() is SidProcedure or StrRecord or Airway or RottaVfr)
        {
            bool aerovia = letto.Records[0] is Airway or RottaVfr;
            var delPunto = aerovia
                ? new Dictionary<string, string> { ["dir"] = "both", ["lower"] = "FL95" }
                : new Dictionary<string, string> { ["alt"] = "+FL80" };
            foreach (var record in letto.Records)
            {
                var righe = ((RecordChunk<T>)etichettato.Chunks.First(c => c is RecordChunk<T> r && ReferenceEquals(r.Record, record))).RawLines;
                for (int i = righe.Length - 1; i >= (aerovia ? 0 : 1); i--)
                {
                    if (righe[i].Trim().Length > 0 && !righe[i].TrimStart().StartsWith("//", StringComparison.Ordinal)
                        && Metadati.ChiaveDelPunto<T>(righe[i]) is { } punto && !punto.Contains('"', StringComparison.Ordinal))
                    {
                        etichettato = Metadati.ScriviIlPunto(etichettato, record, i, delPunto);
                        puntiEtichettati++;
                    }
                }
            }
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
        puntiRitrovati += metadati.Punti.Count(m => m.Chiavi.GetValueOrDefault("alt") == "+FL80" || m.Chiavi.GetValueOrDefault("dir") == "both");

        fileEtichettati++;
        recordEtichettati += riletto.Records.Count;
        recordRitrovati += riletto.Records.Count(r => metadati.Di(r) is { Delimitato: true } m
            && m.Nome == NomeDellaProva(r) && m.Records.Count == 1 && m.Chiavi.GetValueOrDefault("note") == "\"prova dei tag\""
            && (r is not StrRecord { RunwaySpec: "MAPS" } || m.Chiavi.GetValueOrDefault(Metadati.Compose) == "ODINA4E,25:NENI5A"));
        if (riletto.Records.Count != letto.Records.Count || metadati.Record.Count != riletto.Records.Count
            || metadati.Problemi.Count > 0 || metadati.DelFile.GetValueOrDefault("source") != "AIRAC2610"
            // I //@@ stanno nel record senza cambiarne il modello: ogni record riletto si scrive come quello di prima.
            || riletto.Records.Zip(letto.Records).Any(c => !scrittore.Serialize(c.First).SequenceEqual(scrittore.Serialize(c.Second))))
        {
            guastiDeiTag.Add($"{Relativo(percorso)} — {letto.Records.Count} record, {riletto.Records.Count} riletti, " +
                $"{metadati.Record.Count} con tag, problemi: {string.Join(", ", metadati.Problemi.Take(3).Select(p => $"{p.Tipo}@{p.Riga} «{p.Testo}»"))}");
        }

        if (SenzaTag(temporaneo))
        {
            fileTornati++;
        }
    }
    finally
    {
        File.Delete(temporaneo);
    }

    // BLOCCHI A PIÙ PEZZI (slice 1d): solo i file a blocchi, dove un blocco può tenere più record.
    if (letto.Records[0] is ElementoArtcc or Airway or MvaSector or TflSector or StaticBoundaryGroup or Line or Polygon or RottaVfr)
    {
        ProvaIBlocchi(letto, lettore, scrittore, nomeDi, percorso);
    }

    // Tolte le righe //@, i byte di prima: i tag non hanno cambiato nient'altro.
    bool SenzaTag(string scritto)
    {
        byte[] originale = File.ReadAllBytes(percorso);
        var lettura = SectorFileReader.Read(scritto);
        string senzaTag = string.Join(lettura.NewLine, lettura.Lines.Where(r => !Metadati.EUnTag(r.TrimStart())))
            + (lettura.HasFinalNewLine ? lettura.NewLine : "");
        byte[] ricostruito = (lettura.HasByteOrderMark ? new byte[] { 0xEF, 0xBB, 0xBF } : Array.Empty<byte>())
            .Concat(lettura.Encoding.GetBytes(senzaTag)).ToArray();
        if (originale.AsSpan().SequenceEqual(ricostruito))
        {
            return true;
        }

        guastiDeiTag.Add($"{Relativo(percorso)} — senza le righe //@ non torna uguale ({originale.Length} → {ricostruito.Length} byte)");
        return false;
    }
}

// I record di fila (in ordine di file) col nome del gruppo o senza nome vanno in un blocco solo, col nome del primo
// che ne ha uno (o «PROVA»); si salva, si rilegge, e ogni record deve stare nel blocco del suo gruppo.
void ProvaIBlocchi<T>(ParseResult<T> letto, IFileParser<T> lettore, IFileSaver<T> scrittore, Func<T, string?> nomeDi, string percorso)
    where T : class
{
    var gruppi = new List<(string? Nome, List<T> Record)>();
    foreach (var record in letto.Records)
    {
        string? nome = nomeDi(record);
        if (gruppi.Count > 0 && (nome is null || gruppi[^1].Nome is null || gruppi[^1].Nome == nome))
        {
            gruppi[^1] = (gruppi[^1].Nome ?? nome, gruppi[^1].Record);
            gruppi[^1].Record.Add(record);
        }
        else
        {
            gruppi.Add((nome, [record]));
        }
    }

    var etichettato = letto;
    try
    {
        foreach (var (nome, record) in gruppi)
        {
            etichettato = Metadati.ScriviIlBlocco(etichettato, record[0], record[^1], nomeDi, nome ?? "PROVA",
                new Dictionary<string, string> { ["note"] = "\"blocco di prova\"" });
        }
    }
    catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
    {
        guastiDeiTag.Add($"{Relativo(percorso)} — il blocco a più pezzi non si scrive: {ex.Message}");
        return;
    }

    string temporaneo = Path.Combine(Path.GetTempPath(), "sectorfile-blocchi-" + Guid.NewGuid().ToString("N") + ".tmp");
    try
    {
        new FileSaverOrchestrator().Save(etichettato, new HashSet<T>(), scrittore, temporaneo);
        var riletto = lettore.Parse(temporaneo, new ColorPalette());
        var metadati = Metadati.Leggi(riletto, nomeDi);

        bloccoFile++;
        blocchiScritti += gruppi.Count;
        blocchiAPiuPezzi += gruppi.Count(g => g.Record.Count > 1);
        bloccoRecord += riletto.Records.Count;

        // Il record i-esimo riletto deve stare nel blocco del gruppo che teneva il record i-esimo di prima.
        var gruppoDi = gruppi.SelectMany((g, n) => g.Record.Select(_ => n)).ToList();
        var bloccoDi = metadati.Record.Select((m, n) => (m, n)).ToDictionary(c => c.m, c => c.n);
        bloccoRitrovati += riletto.Records.Select((r, i) => (r, i)).Count(c => c.i < gruppoDi.Count
            && metadati.Di(c.r) is { Delimitato: true } m && bloccoDi[m] == gruppoDi[c.i]);
        if (riletto.Records.Count != letto.Records.Count || metadati.Record.Count != gruppi.Count || metadati.Problemi.Count > 0)
        {
            guastiDeiTag.Add($"{Relativo(percorso)} — blocchi a più pezzi: {gruppi.Count} scritti, {metadati.Record.Count} riletti, " +
                $"{riletto.Records.Count}/{letto.Records.Count} record, problemi: {string.Join(", ", metadati.Problemi.Take(3).Select(p => $"{p.Tipo}@{p.Riga} «{p.Testo}»"))}");
        }

        byte[] originale = File.ReadAllBytes(percorso);
        var lettura = SectorFileReader.Read(temporaneo);
        string senzaTag = string.Join(lettura.NewLine, lettura.Lines.Where(r => !Metadati.EUnTag(r.TrimStart())))
            + (lettura.HasFinalNewLine ? lettura.NewLine : "");
        byte[] ricostruito = (lettura.HasByteOrderMark ? new byte[] { 0xEF, 0xBB, 0xBF } : Array.Empty<byte>())
            .Concat(lettura.Encoding.GetBytes(senzaTag)).ToArray();
        if (originale.AsSpan().SequenceEqual(ricostruito))
        {
            bloccoFileTornati++;
        }
        else
        {
            guastiDeiTag.Add($"{Relativo(percorso)} — blocchi a più pezzi: senza le righe //@ non torna uguale");
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
    List<string> piuDiUnCampo,
    List<(string File, int NelFile, int Scritti, string? Esempio, string? CambiaSpostandoli)> commenti) : IUsoDelFormato<(byte[], byte[])>
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
            // 3c. COMMENTI IN CODA (lotto «Subito» slice 2): quanti ce ne sono nel file, e quanti ne scrivono gli
            //     scrittori del motore riscrivendo OGNI record dal modello. Il Lab non ne scrive mai: deve fare zero.
            var scrittiDalModello = letto.Records.SelectMany(r => scrittore.Serialize(r)).Where(r => CommentiInCoda.Dove(r) is not null).ToList();
            //     E spostati sopra col gesto del Lab: il file riletto deve dare gli stessi record (stesso modello).
            string? cambia = null;
            if (CommentiInCoda.Righe(prima).Count > 0)
            {
                var lettura = SectorFileReader.Read(percorso);
                File.WriteAllText(temporaneo, string.Join(lettura.NewLine, CommentiInCoda.SpostaSopra(lettura.Lines)) + lettura.NewLine, lettura.Encoding);
                var spostato = lettore.Parse(temporaneo, new ColorPalette());
                var diPrima = lettore.Parse(percorso, new ColorPalette());   // `letto` ha già i punti spostati di 3b
                if (spostato.Records.Count != diPrima.Records.Count)
                {
                    cambia = $"{diPrima.Records.Count} record → {spostato.Records.Count}";
                }
                else if (spostato.Records.Zip(diPrima.Records).FirstOrDefault(c => !scrittore.Serialize(c.First).SequenceEqual(scrittore.Serialize(c.Second))) is { First: not null } diverso)
                {
                    cambia = $"«{string.Join(" | ", scrittore.Serialize(diverso.Second).Take(2))}» → «{string.Join(" | ", scrittore.Serialize(diverso.First).Take(2))}»";
                }
            }

            commenti.Add((relativo, CommentiInCoda.Righe(prima).Count, scrittiDalModello.Count, scrittiDalModello.FirstOrDefault(), cambia));

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
