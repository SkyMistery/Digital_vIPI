using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <summary>
/// Una riga della tabella «Aeroporti alternati» come la vede chi legge: lo scalo col suo nome, le
/// radioassistenze risolte sull'anagrafica, il rilevamento e la distanza.
/// </summary>
public sealed record MilDiversionView(
    string Icao, string Name, IReadOnlyList<NavaidRow> Navaids, int? Bearing, int? DistanceNm);

/// <summary>
/// Come si scrivono rilevamento e distanza in tabella. ⚠️ Chi compila scrive <b>solo il numero</b> — è la
/// richiesta del committente — e l'unità la mette il documento: così non ci finiscono dentro tre modi diversi
/// di dire gradi, e la colonna resta ordinabile e confrontabile.
/// </summary>
public static class MilDiversionText
{
    public static string Rilevamento(int? gradi) =>
        gradi is { } g ? g.ToString("000", CultureInfo.InvariantCulture) + "°" : "";

    public static string Distanza(int? nm) =>
        nm is { } d ? d.ToString(CultureInfo.InvariantCulture) + " NM" : "";
}

/// <summary>
/// Che cosa il <b>documento</b> dice della sezione «Aeroporti alternati» (carta vSOP militari §12f): quali
/// scali, in che ordine, con quali radioassistenze e a che rilevamento e distanza.
///
/// <para>
/// ⚠️ <b>Il nome dello scalo si porta dietro</b>, e non è una duplicazione per pigrizia: un alternato può
/// essere <b>estero</b> (LGKR, LDDU) e quindi non stare nel nostro archivio, e la pagina di un documento non
/// può dipendere da una chiamata a IVAO per stampare una cella. Quando lo scalo <i>è</i> in archivio vince il
/// nome dell'archivio — quello è il dato vero — e questo resta il ripiego.
/// </para>
/// <para>
/// ⚠️ Rilevamento e distanza sono <b>numeri</b> e si salvano come numeri: sono i valori del SOP, scritti a
/// mano per decisione del committente (nessuno sa come li abbiano ricavati, e calcolarli darebbe numeri veri
/// e <i>diversi dal PDF</i>). L'unità la mette la resa.
/// </para>
/// </summary>
public sealed class MilDiversionPayload
{
    public const string Variante = "mildiversion";

    [JsonPropertyName("variant")]
    public string Variant { get; init; } = Variante;

    [JsonPropertyName("rows")]
    public IReadOnlyList<Riga> Rows { get; init; } = Array.Empty<Riga>();

    public sealed class Riga
    {
        [JsonPropertyName("icao")] public string Icao { get; init; } = "";

        /// <summary>Il nome com'era quando lo scalo è stato aggiunto: serve solo se non è in archivio.</summary>
        [JsonPropertyName("name")] public string? Name { get; init; }

        [JsonPropertyName("navaids")] public IReadOnlyList<Nav> Navaids { get; init; } = Array.Empty<Nav>();
        [JsonPropertyName("bearing")] public int? Bearing { get; init; }
        [JsonPropertyName("distance")] public int? Distance { get; init; }
    }

    /// <summary>⚠️ Tre campi come nelle Radioassistenze: il canale è nell'identità.</summary>
    public sealed class Nav
    {
        [JsonPropertyName("code")] public string Code { get; init; } = "";
        [JsonPropertyName("kind")] public string Kind { get; init; } = "";
        [JsonPropertyName("channel")] public string? Channel { get; init; }
    }

    private static readonly JsonSerializerOptions Opzioni = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Le righe salvate. JSON assente, vuoto o illeggibile ⇒ nessuna riga: una tabella vuota è un
    /// documento da compilare, non un errore in faccia a chi legge.</summary>
    public static IReadOnlyList<Riga> Leggi(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<Riga>();
        try
        {
            var p = JsonSerializer.Deserialize<MilDiversionPayload>(json, Opzioni);
            return (p?.Rows ?? Array.Empty<Riga>())
                .Where(r => !string.IsNullOrWhiteSpace(r.Icao))
                .Select(Pulisci)
                .ToList();
        }
        catch (JsonException) { return Array.Empty<Riga>(); }
    }

    /// <summary>Il JSON da salvare, o null se non c'è nessuna riga.</summary>
    public static string? Scrivi(IReadOnlyList<Riga> righe)
    {
        var pulite = righe.Where(r => !string.IsNullOrWhiteSpace(r.Icao)).Select(Pulisci).ToList();
        return pulite.Count == 0 ? null : JsonSerializer.Serialize(new MilDiversionPayload { Rows = pulite });
    }

    /// <summary>
    /// Normalizza una riga: ICAO e codici maiuscoli, navaid senza identità scartate, e i due numeri
    /// riportati dentro il loro campo di validità.
    /// <para>⚠️ Un rilevamento di 400° o una distanza negativa non sono «quasi giusti»: sono un refuso, e
    /// stamparli darebbe a una tabella di documento l'aria di dire una cosa precisa e falsa.</para>
    /// </summary>
    private static Riga Pulisci(Riga r) => new()
    {
        Icao = NavaidRules.Norm(r.Icao),
        Name = string.IsNullOrWhiteSpace(r.Name) ? null : r.Name!.Trim(),
        Navaids = r.Navaids
            .Where(n => !string.IsNullOrWhiteSpace(n.Code) && !string.IsNullOrWhiteSpace(n.Kind))
            .Select(n => new Nav
            {
                Code = NavaidRules.Norm(n.Code), Kind = NavaidRules.Norm(n.Kind),
                Channel = NavaidRules.Valore(n.Channel),
            })
            .ToList(),
        Bearing = r.Bearing is { } b and >= 0 and <= 360 ? b : null,
        Distance = r.Distance is { } d and >= 0 and <= 9999 ? d : null,
    };

    /// <summary>
    /// Da quel che si vede a quel che si salva: la riga di ritorno, pronta per un'altra scrittura.
    ///
    /// <para>⚠️ <b>Il canale si porta indietro</b>, e non è un dettaglio: l'identità di una radioassistenza è
    /// la terna <c>codice+famiglia+canale</c> (vedi <see cref="NavaidKey"/>), e una riga rimessa insieme
    /// senza canale cita un impianto che non esiste — <c>GRO|VHF|null</c> quando in anagrafica c'è
    /// <c>GRO|VHF|35Y</c>. La risoluzione non lo trova, lo scarta in silenzio, e la radioassistenza sparisce
    /// dalla tabella al primo salvataggio successivo.</para>
    /// <para>⚠️ Un posto solo, perché di rimontaggi ce n'è uno per ogni gesto dell'editor — aggiungere uno
    /// scalo, spostarlo, scrivere un rilevamento — e ognuno di quelli riscriveva <b>tutte</b> le righe.</para>
    /// </summary>
    public static Riga Da(MilDiversionView v) => new()
    {
        Icao = v.Icao,
        Name = v.Name,
        Navaids = v.Navaids
            .Select(n => new Nav { Code = n.Code, Kind = n.Kind, Channel = n.Channel })
            .ToList(),
        Bearing = v.Bearing,
        Distance = v.DistanceNm,
    };

    /// <summary>Le identità di radioassistenza citate da tutte le righe, senza ripetizioni: è la lettura che
    /// serve a risolverle in <b>una</b> interrogazione invece di una per riga.</summary>
    public static IReadOnlyList<NavaidKey> ChiaviNavaid(IEnumerable<Riga> righe) =>
        righe.SelectMany(r => r.Navaids).Select(n => new NavaidKey(n.Code, n.Kind, n.Channel))
            .Distinct().ToList();
}
