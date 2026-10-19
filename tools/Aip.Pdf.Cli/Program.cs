using System.Globalization;
using Aip;
using Aip.Pdf.Italia;

// aip-pdf <pdf o cartella>... [--modello uscita.json] [--airac AACC] [--dettagli] [--aree]
// Legge i PDF dell'AIP italiano (le cartelle per intero, sottocartelle comprese), stampa la misura e, con --modello,
// scrive il modello AIP. Esce 0 se ha letto, 1 se un file non si apre o il modello non si scrive, 2 se gli argomenti
// non vanno.

var ingressi = new List<string>();
string? uscita = null;
string? airac = null;
var dettagli = false;
var elenco = false;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--modello" when i + 1 < args.Length: uscita = args[++i]; break;
        case "--airac" when i + 1 < args.Length: airac = args[++i]; break;
        case "--dettagli": dettagli = true; break;
        case "--aree": elenco = true; break;
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
var rotte = new List<Route>();
var attese = new List<Holding>();
var radioassistenze = new List<Navaid>();
var punti = new List<SignificantPoint>();
var luci = new List<GroundLight>();
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
    rotte.AddRange(letto.Routes);
    attese.AddRange(letto.Holdings);
    radioassistenze.AddRange(letto.Navaids);
    punti.AddRange(letto.Points);
    luci.AddRange(letto.Lights);
    var date = letto.Pages.GroupBy(p => p.Effective).OrderBy(g => g.Key)
        .Select(g => $"{(g.Key is { } d ? d.ToString("dd MMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant() : "senza data")} ×{g.Count()}");
    Console.WriteLine($"{Path.GetFileName(f)}: {letto.Pages.Count} pagine ({string.Join(", ", date)}), "
        + $"{letto.Pages.Count(p => !p.Table)} senza tabella, {letto.Airspaces.Count} aree"
        + (letto.Routes.Count > 0 ? $", rotte {string.Join(" ", letto.Routes.Select(r => $"{r.Designator} ({r.Points.Count} punti)"))}" : "")
        + (letto.Holdings.Count > 0 ? $", {letto.Holdings.Count} attese su {letto.Holdings.Select(a => a.Fix).Distinct().Count()} punti" : "")
        + (letto.Navaids.Count > 0 ? $", {letto.Navaids.Count} radioassistenze" : "")
        + (letto.Points.Count > 0 ? $", {letto.Points.Count} punti" : "")
        + (letto.Lights.Count > 0 ? $", {letto.Lights.Count} luci" : ""));
    foreach (var g in letto.Airspaces.GroupBy(a => a.Geometry.GetType().Name).OrderBy(g => g.Key))
        Console.WriteLine($"    {g.Key}: {g.Count()}");
    foreach (var g in letto.Issues.GroupBy(p => p.Kind).OrderBy(g => g.Key))
        Console.WriteLine($"    {g.Key}: {g.Count()}");
    if (elenco)
        foreach (var a in letto.Airspaces)
            Console.WriteLine($"      {a.Type,-10} {a.Key} | {a.Upper.Text} / {a.Lower.Text} {a.Class} {(a.Bands is { } b ? $"[{b.Count} fasce]" : "")}"
                + $" | {a.Geometry switch { RingGeometry r => $"anello {r.Segments.Count}", CircleGeometry => "cerchio", CorridorGeometry k => $"corridoio {k.Width.Value} {k.Width.Unit} {k.Points.Count} punti", _ => "?" }}"
                + $" | {a.Unit?.Replace('\n', ';')} {a.Hours} {a.TransitionAltitude} | p.{a.Page?.Reference}");
    if (elenco)
        foreach (var r in letto.Routes)
            for (var k = 0; k < r.Points.Count; k++)
                Console.WriteLine($"      {r.Designator} {(r.Points[k].Compulsory ? '▲' : '∆')} {r.Points[k].Designator,-6} {r.Points[k].Position.Lat:0.0000} {r.Points[k].Position.Lon:0.0000}"
                    + (k < r.Segments.Count ? $" → {r.Segments[k].Track} {r.Segments[k].DistanceNm} | {r.Segments[k].Upper?.Text} / {r.Segments[k].Lower?.Text} | {r.Segments[k].LateralLimits} | dispari {r.Segments[k].Odd} {r.Segments[k].MinimumOdd} pari {r.Segments[k].Even} {r.Segments[k].MinimumEven} | {r.Segments[k].Remarks}" : ""));
    if (elenco)
        foreach (var a in letto.Holdings)
            Console.WriteLine($"      {a.Fix,-6} {a.Bearing} | {a.Minimum.Text} {a.Turn} {a.InboundTrack:000} {a.NoteRefs} | p.{a.Page?.Reference}");
    if (elenco)
        foreach (var n in letto.Navaids)
            Console.WriteLine($"      {n.Designator,-4} {n.Type,-9} {n.Name} | {n.Frequency} {n.Channel} | {string.Join(" ; ", n.Components.Select(a => $"{a.Equipment} {a.Position.Lat:0.00000} {a.Position.Lon:0.00000} {a.Hours}"))}"
                + $" | {n.Elevation} | var {n.MagneticVariation} | {n.Fra} | p.{n.Page?.Reference}");
    if (elenco)
        foreach (var p in letto.Points)
            Console.WriteLine($"      {p.Designator} {p.Position.Lat:0.00000} {p.Position.Lon:0.00000} | {string.Join(",", p.Routes)} | {p.Procedures} | {p.Fra} | p.{p.Page?.Reference}");
    if (elenco)
        foreach (var l in letto.Lights)
            Console.WriteLine($"      {l.Name} | {(l.Position is { } q ? $"{q.Lat:0.00000} {q.Lon:0.00000}" : "senza posizione")} | {l.Type} | {l.Characteristics} | {l.Hours} {l.Intensity} | {l.Remarks}");
    if (dettagli)
        foreach (var p in letto.Issues)
            Console.WriteLine($"      {p.Kind} p.{p.Page} {p.Key} {p.Detail}".TrimEnd());
}

var doc = new AipDocument
{
    State = ItalianAip.State, Airac = airac, Airspaces = aree, Routes = rotte.Count == 0 ? null : rotte, Holdings = attese.Count == 0 ? null : attese,
    Navaids = radioassistenze.Count == 0 ? null : radioassistenze, Points = punti.Count == 0 ? null : punti, Lights = luci.Count == 0 ? null : luci,
};
foreach (var p in AipCheck.Run(doc))
    Console.WriteLine($"modello: {p.Kind} {p.Key} {p.Detail}".TrimEnd());
Console.WriteLine($"Totale: {aree.Count} aree, {rotte.Count} rotte, {attese.Count} attese, {radioassistenze.Count} radioassistenze, {punti.Count} punti, {luci.Count} luci da {file.Count} file.");

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
    Console.Error.WriteLine("uso: aip-pdf <pdf o cartella>... [--modello uscita.json] [--airac AACC] [--dettagli] [--aree]");
    return 2;
}
