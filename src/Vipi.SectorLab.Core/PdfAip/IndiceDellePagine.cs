using System.Globalization;
using System.Text;
using System.Text.Json;
using Aip.Pdf;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Le pagine di un ciclo rispetto a quelle del ciclo prima nell'AIP espanso: quelle con un'altra data, le nuove, le tolte.
/// <c>Completo</c> falso = il ciclo prima non aveva l'indice ma solo il modello, che cita le pagine lette: allora si sanno
/// solo le date diverse, non le pagine nuove né quelle tolte.
/// </summary>
public sealed record PagineCambiate(string Ciclo, IReadOnlyList<string> ConAltraData, IReadOnlyList<string> Nuove, IReadOnlyList<string> Tolte, bool Completo);

/// <summary>
/// L'indice delle pagine nell'AIP espanso (carta «L'AIP intero e gli emendamenti», passo 1): <c>2611\pagine.json</c>
/// accanto al modello, una pagina per riga, così in git la differenza fra due cicli è l'elenco delle pagine cambiate.
/// </summary>
public static class IndiceDellePagine
{
    public const string NomeDelFile = "pagine.json";

    /// <summary>Il file dell'indice dentro la cartella del ciclo.</summary>
    public static string File(string espanso, string ciclo) => Path.Combine(espanso, ciclo, NomeDelFile);

    private sealed record Pagina(string File, int Posizione, string? Riferimento, string? Data, bool? DallaPosizione);

    private sealed record SulDisco(string Ciclo, IReadOnlyList<Pagina> Pagine);

    /// <summary>L'indice come testo: il ciclo, poi una pagina per riga, nell'ordine dato.</summary>
    public static string Testo(string ciclo, IReadOnlyList<IndexedPage> pagine)
    {
        ArgumentNullException.ThrowIfNull(pagine);
        var sb = new StringBuilder();
        sb.Append("{\"Ciclo\":").Append(JsonSerializer.Serialize(ciclo)).Append(",\"Pagine\":[\n");
        for (var i = 0; i < pagine.Count; i++)
        {
            var p = pagine[i];
            sb.Append(JsonSerializer.Serialize(new Pagina(p.File, p.Position, p.Reference,
                p.Effective?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), p.ByPosition ? true : null), Opzioni));
            sb.Append(i + 1 < pagine.Count ? ",\n" : "\n");
        }
        return sb.Append("]}\n").ToString();
    }

    private static readonly JsonSerializerOptions Opzioni = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    /// <summary>L'indice scritto da <see cref="Testo"/>. <exception cref="JsonException">Il testo non è un indice.</exception></summary>
    public static IReadOnlyList<IndexedPage> Leggi(string testo)
    {
        var letto = JsonSerializer.Deserialize<SulDisco>(testo) ?? throw new JsonException("Indice delle pagine vuoto.");
        return letto.Pagine.Select(p => new IndexedPage(p.File, p.Posizione, p.Riferimento,
                p.Data is { } d ? DateOnly.ParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture) : null, p.DallaPosizione == true))
            .ToList();
    }

    /// <summary>
    /// Le pagine di <paramref name="ora"/> rispetto al ciclo prima di <paramref name="ciclo"/> nell'AIP espanso: il più
    /// recente che ha l'indice o, se nessuno ce l'ha, il più recente che ha il modello (le pagine che cita). Null se prima
    /// non c'è niente, o se quel che c'è non si legge.
    /// </summary>
    public static PagineCambiate? RispettoAlCicloPrima(string espanso, string ciclo, IReadOnlyList<IndexedPage> ora)
    {
        ArgumentNullException.ThrowIfNull(ora);
        if (!Directory.Exists(espanso))
            return null;
        var prima = Directory.EnumerateDirectories(espanso).Select(Path.GetFileName).OfType<string>()
            .Where(c => CicloAirac.Valido(c) && string.CompareOrdinal(c, ciclo) < 0)
            .OrderDescending(StringComparer.Ordinal)
            .ToList();
        var adesso = ora.Where(p => p.Reference is not null).GroupBy(p => p.Reference!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Effective, StringComparer.Ordinal);
        try
        {
            if (prima.FirstOrDefault(c => System.IO.File.Exists(File(espanso, c))) is { } conIndice)
            {
                var vecchie = Leggi(System.IO.File.ReadAllText(File(espanso, conIndice))).Where(p => p.Reference is not null)
                    .GroupBy(p => p.Reference!, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Effective, StringComparer.Ordinal);
                return new PagineCambiate(conIndice,
                    adesso.Where(p => vecchie.TryGetValue(p.Key, out var d) && d != p.Value).Select(p => p.Key).Order(StringComparer.Ordinal).ToList(),
                    adesso.Keys.Where(r => !vecchie.ContainsKey(r)).Order(StringComparer.Ordinal).ToList(),
                    vecchie.Keys.Where(r => !adesso.ContainsKey(r)).Order(StringComparer.Ordinal).ToList(),
                    Completo: true);
            }
            if (prima.FirstOrDefault(c => System.IO.File.Exists(AipEspanso.File(espanso, c))) is { } colModello)
            {
                var citate = PagineCitate(System.IO.File.ReadAllText(AipEspanso.File(espanso, colModello)));
                return new PagineCambiate(colModello,
                    citate.Where(p => adesso.TryGetValue(p.Key, out var d) && d != p.Value).Select(p => p.Key).Order(StringComparer.Ordinal).ToList(),
                    [], [], Completo: false);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or FormatException)
        {
            return null;
        }
        return null;
    }

    /// <summary>
    /// Le pagine che un modello cita, con la data che aveva letto: ogni oggetto <c>"page": { "reference", "effective" }</c>,
    /// dovunque stia (aree, rotte, punti, scali, procedure…).
    /// </summary>
    public static IReadOnlyDictionary<string, DateOnly?> PagineCitate(string modello)
    {
        using var doc = JsonDocument.Parse(modello);
        var citate = new Dictionary<string, DateOnly?>(StringComparer.Ordinal);
        Visita(doc.RootElement, citate);
        return citate;
    }

    private static void Visita(JsonElement e, Dictionary<string, DateOnly?> citate)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject())
                {
                    if (p.NameEquals("page") && p.Value.ValueKind == JsonValueKind.Object
                        && p.Value.TryGetProperty("reference", out var r) && r.GetString() is { } riferimento)
                    {
                        DateOnly? data = p.Value.TryGetProperty("effective", out var d) && d.GetString() is { } s
                            ? DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                            : null;
                        citate.TryAdd(riferimento, data);
                    }
                    else
                    {
                        Visita(p.Value, citate);
                    }
                }
                break;
            case JsonValueKind.Array:
                foreach (var x in e.EnumerateArray())
                    Visita(x, citate);
                break;
        }
    }
}
