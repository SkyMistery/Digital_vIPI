using System.Globalization;
using Aip;
using Aip.Aurora;

// aip-aurora <modello.json> <cartella> [--densita N] [--con-stato]
// Esce 0 se ha scritto (le segnalazioni si stampano ma non fermano), 1 se non ha potuto leggere o scrivere, 2 se
// gli argomenti non vanno.

var posizionali = new List<string>();
var densita = OutlineOptions.DefaultPointsPerDegree;
var togliStato = true;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--densita" when i + 1 < args.Length
            && double.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d > 0:
            densita = d;
            i++;
            break;
        case "--con-stato":
            togliStato = false;
            break;
        case var a when a.StartsWith("--", StringComparison.Ordinal):
            return Uso($"opzione sconosciuta: {a}");
        default:
            posizionali.Add(args[i]);
            break;
    }
}

if (posizionali.Count != 2)
    return Uso("servono il modello e la cartella");

AipDocument modello;
try
{
    using var flusso = File.OpenRead(posizionali[0]);
    modello = AipJson.Deserialize(flusso);
}
catch (Exception e) when (e is IOException or UnauthorizedAccessException or AipJsonException)
{
    Console.Error.WriteLine($"Il modello non si legge: {e.Message}");
    return 1;
}

foreach (var p in AipCheck.Run(modello))
    Console.WriteLine($"modello: {p.Kind} {p.Key} {p.Detail}".TrimEnd());

var opzioni = new AuroraOptions
{
    StripState = togliStato,
    Outline = new OutlineOptions { PointsPerDegree = densita },
};
var (file, problemi) = AuroraFiles.Convert(modello, opzioni);

try
{
    AuroraFiles.Write(file, posizionali[1]);
}
catch (Exception e) when (e is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"La cartella non si scrive: {e.Message}");
    return 1;
}

foreach (var f in file)
    Console.WriteLine($"{f.Name}: {f.Areas} aree, {f.Lines.Count} righe");
foreach (var p in problemi)
    Console.WriteLine($"{p.Kind}: {p.Key} {p.Detail}".TrimEnd());
Console.WriteLine($"Stato {modello.State}, ciclo {modello.Airac ?? "-"}, densità {densita.ToString(CultureInfo.InvariantCulture)} punti per grado, {problemi.Count} segnalazioni.");
return 0;

static int Uso(string perche)
{
    Console.Error.WriteLine(perche);
    Console.Error.WriteLine("uso: aip-aurora <modello.json> <cartella> [--densita N] [--con-stato]");
    return 2;
}
