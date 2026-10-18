using System.Globalization;
using Aip;
using Aip.Pdf.Italia;

// aip-pdf <pdf o cartella>... [--modello uscita.json] [--airac AACC] [--dettagli]
// Legge i PDF dell'AIP italiano (le cartelle per intero, sottocartelle comprese), stampa la misura e, con --modello,
// scrive il modello AIP. Esce 0 se ha letto, 1 se un file non si apre o il modello non si scrive, 2 se gli argomenti
// non vanno.

var ingressi = new List<string>();
string? uscita = null;
string? airac = null;
var dettagli = false;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--modello" when i + 1 < args.Length: uscita = args[++i]; break;
        case "--airac" when i + 1 < args.Length: airac = args[++i]; break;
        case "--dettagli": dettagli = true; break;
        case var a when a.StartsWith("--", StringComparison.Ordinal): return Uso($"opzione sconosciuta: {a}");
        default: ingressi.Add(args[i]); break;
    }
}
if (ingressi.Count == 0) return Uso("serve almeno un PDF o una cartella");

var file = new List<string>();
foreach (var x in ingressi)
{
    if (Directory.Exists(x)) file.AddRange(Directory.GetFiles(x, "*.pdf", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
    else if (File.Exists(x)) file.Add(x);
    else { Console.Error.WriteLine($"Non c'è: {x}"); return 1; }
}

var aree = new List<Airspace>();
var esito = 0;
foreach (var f in file)
{
    ImportResult letto;
    try
    {
        letto = ItalianAip.ReadFile(f);
    }
    catch (Exception e)
    {
        Console.Error.WriteLine($"{Path.GetFileName(f)}: non si legge ({e.GetType().Name}: {e.Message})");
        esito = 1;
        continue;
    }

    if (letto.Issues.Any(p => p.Kind == ImportIssueKind.Unsupported))
    {
        Console.WriteLine($"{Path.GetFileName(f)}: non ancora letto (sezione di una slice futura)");
        continue;
    }

    aree.AddRange(letto.Airspaces);
    var date = letto.Pages.GroupBy(p => p.Effective).OrderBy(g => g.Key)
        .Select(g => $"{(g.Key is { } d ? d.ToString("dd MMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant() : "senza data")} ×{g.Count()}");
    Console.WriteLine($"{Path.GetFileName(f)}: {letto.Pages.Count} pagine ({string.Join(", ", date)}), "
        + $"{letto.Pages.Count(p => !p.Table)} senza tabella, {letto.Airspaces.Count} aree");
    foreach (var g in letto.Airspaces.GroupBy(a => a.Geometry.GetType().Name).OrderBy(g => g.Key))
        Console.WriteLine($"    {g.Key}: {g.Count()}");
    foreach (var g in letto.Issues.GroupBy(p => p.Kind).OrderBy(g => g.Key))
        Console.WriteLine($"    {g.Key}: {g.Count()}");
    if (dettagli)
        foreach (var p in letto.Issues)
            Console.WriteLine($"      {p.Kind} p.{p.Page} {p.Key} {p.Detail}".TrimEnd());
}

var doc = new AipDocument { State = ItalianAip.State, Airac = airac, Airspaces = aree };
foreach (var p in AipCheck.Run(doc))
    Console.WriteLine($"modello: {p.Kind} {p.Key} {p.Detail}".TrimEnd());
Console.WriteLine($"Totale: {aree.Count} aree da {file.Count} file.");

if (uscita is not null)
{
    try
    {
        await using var flusso = File.Create(uscita);
        AipJson.Serialize(flusso, doc);
        Console.WriteLine($"Modello scritto: {uscita}");
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"Il modello non si scrive: {e.Message}");
        return 1;
    }
}
return esito;

static int Uso(string perche)
{
    Console.Error.WriteLine(perche);
    Console.Error.WriteLine("uso: aip-pdf <pdf o cartella>... [--modello uscita.json] [--airac AACC] [--dettagli]");
    return 2;
}
