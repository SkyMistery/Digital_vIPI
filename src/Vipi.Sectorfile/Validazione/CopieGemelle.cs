using System.Collections;
using System.Globalization;
using System.Reflection;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Validazione;

/// <summary>Una copia di un record in un file della famiglia: il file, l'indice del record nel file, la riga (da 1).</summary>
public sealed record CopiaGemella(string File, int Indice, object Record, int Riga);

/// <summary>
/// Le copie dello stesso record nei file di una famiglia (carta F3-bis §2.1).
/// <para><see cref="PerOrdine"/> è falso quando la chiave si ripete dentro un file e i file non ne hanno lo stesso
/// numero (D10, <c>LIPY 22/04</c> due volte in <c>libb.rw</c>): allora non si sa quale copia va con quale, il gruppo
/// le contiene tutte e una modifica non si propaga.</para>
/// </summary>
public sealed record GruppoDiGemelli(string Famiglia, string Chiave, IReadOnlyList<CopiaGemella> Copie, bool PerOrdine);

/// <summary>
/// Le copie gemelle (carta F3-bis §2.1, slice 1): lo stesso scalo, la stessa pista, la stessa posizione scritti nel file
/// nazionale e in quelli delle FIR (<c>OTHER/itap.ap</c> e <c>OTHER/lirr.ap</c>…).
/// </summary>
/// <remarks>
/// <para>Una <b>famiglia</b> sono i file con la stessa estensione (<c>.ap</c>, <c>.rw</c>, <c>.frq</c>) nella stessa
/// cartella: si ricava dai file, non da un elenco, e un file di FIR nuovo entra da solo.</para>
/// <para>La <b>chiave</b>: l'ICAO per gli scali, scalo + le due piste per i <c>.rw</c> (<c>MAPS</c> compresa), il codice
/// della posizione per le frequenze. Se un file ha la stessa chiave più volte, la prima copia va con la prima degli
/// altri file, la seconda con la seconda… ma solo se ogni file ne ha lo stesso numero (D10).</para>
/// <para>I <b>campi</b> si confrontano per riflessione sul modello, come li mostra l'ispettore del Lab: una tabella
/// scritta a mano sarebbe una seconda verità.</para>
/// </remarks>
public static class CopieGemelle
{
    /// <summary>Le estensioni che hanno famiglie (senza punto, minuscole).</summary>
    public static IReadOnlyList<string> Estensioni { get; } = ["ap", "rw", "frq"];

    // Proprietà che non sono dati del record: da dove viene, e un segno che nessuno imposta.
    private static readonly HashSet<string> FuoriConfronto = new(StringComparer.Ordinal) { "Sources", "Source", "HasConflict" };

    /// <summary>
    /// La chiave di un record di famiglia; null per ogni altro record. Uno scalo commentato (<c>//LIBB;…</c>, che il
    /// lettore tiene come <see cref="AirportInfo.IsDisabled"/>) non ha gemelli: Aurora non lo legge.
    /// </summary>
    public static string? Chiave(object record) => record switch
    {
        AirportInfo { IsDisabled: true } => null,
        AirportInfo a => a.IcaoCode,
        Runway r => $"{r.IcaoCode} {r.Designator1}/{r.Designator2}",
        AtcPosition p => p.Code,
        _ => null,
    };

    /// <summary>
    /// I gruppi di gemelli fra i file dati (percorso relativo e record nell'ordine del file): solo le chiavi che stanno
    /// in almeno due file. I percorsi possono usare <c>/</c> o <c>\</c>.
    /// </summary>
    public static IReadOnlyList<GruppoDiGemelli> Trova(IEnumerable<(string File, IReadOnlyList<object> Record)> file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var gruppi = new List<GruppoDiGemelli>();
        foreach (var famiglia in file.Where(f => Famiglia(f.File) is not null).GroupBy(f => Famiglia(f.File)!, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            // chiave -> file -> copie nell'ordine del file
            var perChiave = new Dictionary<string, SortedDictionary<string, List<CopiaGemella>>>(StringComparer.Ordinal);
            foreach (var (percorso, record) in famiglia)
            {
                for (int i = 0; i < record.Count; i++)
                {
                    if (Chiave(record[i]) is not { } chiave)
                        continue;
                    if (!perChiave.TryGetValue(chiave, out var perFile))
                        perChiave[chiave] = perFile = new SortedDictionary<string, List<CopiaGemella>>(StringComparer.Ordinal);
                    if (!perFile.TryGetValue(percorso, out var copie))
                        perFile[percorso] = copie = [];
                    copie.Add(new CopiaGemella(percorso, i, record[i], Riga(record[i])));
                }
            }

            foreach (var (chiave, perFile) in perChiave.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                if (perFile.Count < 2)
                    continue;
                int quante = perFile.Values.First().Count;
                if (perFile.Values.All(c => c.Count == quante))
                {
                    for (int n = 0; n < quante; n++)
                        gruppi.Add(new GruppoDiGemelli(famiglia.Key, chiave, perFile.Values.Select(c => c[n]).ToList(), PerOrdine: true));
                }
                else
                {
                    gruppi.Add(new GruppoDiGemelli(famiglia.Key, chiave, perFile.Values.SelectMany(c => c).ToList(), PerOrdine: false));
                }
            }
        }

        return gruppi;
    }

    /// <summary>I campi del modello di un record, col valore scritto in un modo solo (per confrontarli e per dirli).</summary>
    public static IReadOnlyList<(string Campo, string Valore)> Campi(object record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && !FuoriConfronto.Contains(p.Name))
            .Select(p => (p.Name, Testo(p.GetValue(record))))
            .ToList();
    }

    /// <summary>I campi in cui due copie differiscono.</summary>
    public static IReadOnlyList<string> CampiDiversi(object a, object b)
    {
        var di = Campi(b).ToDictionary(c => c.Campo, c => c.Valore, StringComparer.Ordinal);
        return Campi(a).Where(c => !di.TryGetValue(c.Campo, out string? v) || v != c.Valore).Select(c => c.Campo).ToList();
    }

    /// <summary>
    /// Le copie che non sono come la maggioranza del loro gruppo, coi campi che le separano da essa. Se non c'è una
    /// maggioranza (due copie diverse, o tre tutte diverse) sono tutte fuori posto: non si sa quale sia giusta.
    /// </summary>
    public static IReadOnlyList<(CopiaGemella Copia, IReadOnlyList<string> Campi)> Divergenti(GruppoDiGemelli gruppo)
    {
        ArgumentNullException.ThrowIfNull(gruppo);
        var perValore = gruppo.Copie.GroupBy(c => Impronta(c.Record), StringComparer.Ordinal).ToList();
        if (perValore.Count < 2)
            return [];

        int massimo = perValore.Max(g => g.Count());
        var maggioranza = perValore.Where(g => g.Count() == massimo).ToList();
        var riferimento = maggioranza.Count == 1 ? maggioranza[0].First() : null;

        var fuori = new List<(CopiaGemella, IReadOnlyList<string>)>();
        foreach (var copia in gruppo.Copie)
        {
            if (riferimento is not null)
            {
                var campi = CampiDiversi(copia.Record, riferimento.Record);
                if (campi.Count > 0)
                    fuori.Add((copia, campi));
            }
            else
            {
                var altre = gruppo.Copie.Where(c => !ReferenceEquals(c, copia)).SelectMany(c => CampiDiversi(copia.Record, c.Record))
                    .Distinct(StringComparer.Ordinal).ToList();
                fuori.Add((copia, altre));
            }
        }

        return fuori;
    }

    /// <summary>La famiglia di un file (cartella + estensione, minuscole, barre dritte), o null se non ne ha.</summary>
    public static string? Famiglia(string percorso)
    {
        ArgumentNullException.ThrowIfNull(percorso);
        string dritto = percorso.Replace('\\', '/');
        int punto = dritto.LastIndexOf('.');
        if (punto < 0 || punto < dritto.LastIndexOf('/'))
            return null;
        string estensione = dritto[(punto + 1)..].ToLowerInvariant();
        if (!Estensioni.Contains(estensione))
            return null;
        int barra = dritto.LastIndexOf('/');
        return (barra < 0 ? string.Empty : dritto[..(barra + 1)].ToLowerInvariant()) + "*." + estensione;
    }

    private static string Impronta(object record) => string.Join("\u001f", Campi(record).Select(c => c.Campo + "=" + c.Valore));

    private static int Riga(object record)
        => record.GetType().GetProperty("Sources")?.GetValue(record) is IEnumerable<SourceRef> fonti && fonti.FirstOrDefault() is { } prima
            ? prima.LineNumber
            : 0;

    private static string Testo(object? valore) => valore switch
    {
        null => "—",
        Coordinate c => $"{CoordinateConverter.LatitudeToDottedDms(c.LatitudeDeg)} {CoordinateConverter.LongitudeToDottedDms(c.LongitudeDeg)}",
        Transfer t => (t.IsNegative ? "-" : string.Empty) + t.PositionCode,
        string s => s.Length == 0 ? "—" : s,
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        IEnumerable elenco => string.Join(" ", elenco.Cast<object?>().Select(Testo)),
        _ => valore.ToString() ?? "—",
    };
}
