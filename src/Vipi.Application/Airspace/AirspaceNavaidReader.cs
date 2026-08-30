using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Vipi.Application.Coordinates;

namespace Vipi.Application.Airspace;

/// <summary>
/// Una radioassistenza <b>come la dice l'AIP</b>. È un dato di sola lettura, da confrontare: non entra in
/// anagrafica e non la corregge (decisione 9 del committente, 29 agosto 2026 — <i>solo segnalando, perché poi
/// vanno corrette nel sectorfile e poi importate</i>).
/// </summary>
/// <param name="Kind">La <b>famiglia</b>, come la intende <c>Navaid.Kind</c>: <c>VHF</c> o <c>NDB</c>.</param>
/// <param name="Type">Il tipo che il file dichiara: <c>TACAN</c>, <c>VORTAC</c>, <c>VOR-DME</c>, <c>NDB</c>…</param>
public sealed record AipNavaid(
    string Code, string Name, string Kind, string? Type,
    string? Frequency, string? Channel, double? Latitude, double? Longitude);

/// <summary>
/// Legge le <b>radioassistenze</b> dallo stesso file dell'AIP. Sono 115 — 78 in VHF e 37 NDB — e portano
/// codice, tipo, frequenza, canale e posizione.
///
/// <para>⚠️ <b>Nessuna di queste righe entra in anagrafica.</b> Servono solo a dire dove i due archivi non
/// vanno d'accordo: la correzione si fa nel <b>sectorfile</b>, e da lì si reimporta. Un secondo posto da cui
/// nascono radioassistenze sarebbe esattamente la seconda anagrafica che il 30 agosto è costata la
/// riscrittura del modello.</para>
///
/// <para>Un punto d'appoggio si riconosce dal campo <c>Type</c> (<c>VOR</c> o <c>NDB</c>), non dalla
/// <c>Category</c>, che è quel che distingue gli spazi aerei: sono i due mondi del file, e nessun Placemark
/// ha tutti e due i campi.</para>
/// </summary>
public static class AirspaceNavaidReader
{
    /// <summary>Il tipo sta in testa alla descrizione: <c>«TACAN, Frequency: …»</c>.</summary>
    private static readonly Regex Canale = new(@"Channel:\s*(?<c>[0-9]{1,3}[XYxy])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Le radioassistenze di un KMZ.</summary>
    public static IReadOnlyList<AipNavaid> LeggiKmz(Stream zip)
    {
        var xml = KmlReader.ApriKmz(zip, out _);
        return xml is null ? Array.Empty<AipNavaid>() : LeggiKml(xml);
    }

    /// <summary>Le radioassistenze di un KML come testo.</summary>
    public static IReadOnlyList<AipNavaid> LeggiKml(string? xml)
    {
        var doc = KmlReader.CaricaXml(xml, out _);
        if (doc is null) return Array.Empty<AipNavaid>();

        var righe = new List<AipNavaid>();
        foreach (var placemark in doc.Descendants().Where(e => e.Name.LocalName == "Placemark"))
        {
            var dati = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var campo in placemark.Descendants().Where(e => e.Name.LocalName == "SimpleData"))
            {
                var nome = campo.Attribute("name")?.Value;
                if (!string.IsNullOrWhiteSpace(nome)) dati[nome] = campo.Value;
            }

            var tipoFile = Testo(dati, "Type");
            if (tipoFile is not "VOR" and not "NDB") continue;   // non è un punto d'appoggio radio

            var codice = Testo(dati, "Code");
            if (codice is null) continue;   // senza codice non ha un'identità che si possa confrontare

            var descrizione = Testo(dati, "Desc") ?? "";
            var (lat, lon) = Posizione(placemark);

            righe.Add(new AipNavaid(
                Code: codice.ToUpperInvariant(),
                Name: Testo(dati, "Name") ?? codice,
                Kind: tipoFile == "NDB" ? "NDB" : "VHF",
                Type: Tipo(descrizione),
                Frequency: Testo(dati, tipoFile),   // il campo si chiama come il tipo: <VOR>109.300</VOR>
                Channel: Canale.Match(descrizione) is { Success: true } m ? m.Groups["c"].Value.ToUpperInvariant() : null,
                Latitude: lat,
                Longitude: lon));
        }
        return righe;
    }

    /// <summary>Il tipo dichiarato, che è la prima voce della descrizione. Null se non ne dice nessuno.</summary>
    private static string? Tipo(string descrizione)
    {
        var testa = descrizione.Split(',')[0].Trim();
        return testa.Length is > 0 and <= 16 ? testa.ToUpperInvariant() : null;
    }

    private static (double? Lat, double? Lon) Posizione(XElement placemark)
    {
        var testo = placemark.Descendants().FirstOrDefault(e => e.Name.LocalName == "coordinates")?.Value;
        var punti = KmlReader.CoordinateConQuota(testo);
        return punti.Count > 0 ? (punti[0].Lat, punti[0].Lon) : (null, null);
    }

    private static string? Testo(IReadOnlyDictionary<string, string> dati, string campo) =>
        dati.TryGetValue(campo, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    /// <summary>La frequenza come si scrive di solito: <c>109.300</c> → <c>109.30</c>, <c>420.0</c> resta.</summary>
    public static string? NormalizzaFrequenza(string? f)
    {
        if (string.IsNullOrWhiteSpace(f)) return null;
        var t = f.Trim().Replace(',', '.');
        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v.ToString("0.###", CultureInfo.InvariantCulture)
            : t;
    }
}
