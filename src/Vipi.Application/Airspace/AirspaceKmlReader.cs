using System.Xml.Linq;
using Vipi.Application.Coordinates;
using Vipi.Domain;

namespace Vipi.Application.Airspace;

/// <summary>
/// Legge gli spazi aerei da un KML/KMZ prodotto da <b>AirspaceConverter</b> (il file dell'AIP italiano che il
/// committente carica). Puro: gli si dà il testo o uno stream già aperto, e non tocca il disco. L'apertura
/// dello zip, la sicurezza dell'XML e la lettura della terna <c>lon,lat,alt</c> le fa
/// <see cref="KmlReader"/>, che è il posto dove quelle tre cose vivono.
///
/// <para>⚠️ <b>Il file non contiene contorni: contiene scatole.</b> Un volume con la base staccata da terra è
/// scritto come <b>tetto + pavimento + una parete per lato</b>: <c>TMA MILANO Z1</c> sono 147 poligoni per una
/// sola area, e in tutto il file ci sono 26 989 poligoni per 1 536 volumi. Chi legge un <c>MultiGeometry</c>
/// come un elenco di aree — che è quel che fa <see cref="KmlReader"/>, e per il suo caso d'uso è giusto — si
/// ritrova <c>TMA MILANO Z1 (1)…(147)</c>.</para>
///
/// <para><b>La regola che ne esce</b>, verificata su tutti e 1 536 i volumi del file del 15 luglio 2026: si
/// tengono i poligoni i cui vertici hanno <b>una sola quota</b> — le pareti ne hanno due — e si <b>deduplica
/// l'anello 2D</b>, perché il pavimento ripete il tetto. Esito: esattamente un anello per volume, sempre.
/// 397 volumi hanno il pavimento, 1 139 no, e in tutti e due i casi resta un anello.</para>
///
/// <para>⚠️ <b>Uno spazio aereo si riconosce dalla <c>Category</c></b>, che AirspaceConverter scrive su ognuno
/// dei 1 536 e su nessuno dei 684 punti d'appoggio (aeroporti, VOR, NDB). Non dal fatto che abbia un poligono:
/// anche i campi ne hanno uno, ed è la loro pista.</para>
/// </summary>
public static class AirspaceKmlReader
{
    /// <summary>Gli spazi aerei di un KMZ (uno zip con dentro <c>doc.kml</c>).</summary>
    public static AirspaceReadResult LeggiKmz(Stream zip)
    {
        var xml = KmlReader.ApriKmz(zip, out var guasto);
        return xml is null ? Guasto(guasto ?? "kmz") : LeggiKml(xml);
    }

    /// <summary>Gli spazi aerei di un KML come testo.</summary>
    public static AirspaceReadResult LeggiKml(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return AirspaceReadResult.Vuoto;

        var doc = KmlReader.CaricaXml(xml, out var errore);
        if (doc is null) return Guasto(errore ?? "xml");

        var volumi = new List<AirspaceVolumeRead>();
        var segnalazioni = new List<AirspaceIssue>();
        var visti = new Dictionary<string, int>(StringComparer.Ordinal);
        var quanti = 0;

        foreach (var placemark in doc.Descendants().Where(e => e.Name.LocalName == "Placemark"))
        {
            var dati = DatiEstesi(placemark);
            if (!dati.TryGetValue("Category", out var categoria) || string.IsNullOrWhiteSpace(categoria))
                continue;   // non è uno spazio aereo: è un aeroporto, un VOR, un NDB

            quanti++;

            var nome = Testo(dati, "Name")
                       ?? placemark.Elements().FirstOrDefault(e => e.Name.LocalName == "name")?.Value.Trim();
            if (string.IsNullOrWhiteSpace(nome))
            {
                segnalazioni.Add(new AirspaceIssue(AirspaceIssueKind.VolumeSenzaNome, "", categoria));
                continue;
            }

            var anelli = Anelli(placemark);
            if (anelli.Count == 0)
            {
                segnalazioni.Add(new AirspaceIssue(AirspaceIssueKind.VolumeSenzaAnello, nome));
                continue;
            }
            if (anelli.Count > 1)
                segnalazioni.Add(new AirspaceIssue(AirspaceIssueKind.VolumeAPiuAnelli, nome, $"{anelli.Count} anelli"));

            var famiglia = AirspaceFamilies.Classify(categoria, nome);
            var baseQ = Quota(dati, "Base", nome, famiglia, segnalazioni);
            var tetto = Quota(dati, "Top", nome, famiglia, segnalazioni);

            var chiave = ChiaveNaturale(famiglia, nome, baseQ, tetto);
            visti.TryGetValue(chiave, out var ordinale);
            visti[chiave] = ordinale + 1;
            if (ordinale > 0)
                segnalazioni.Add(new AirspaceIssue(AirspaceIssueKind.ChiaveDuplicata, nome, chiave));

            volumi.Add(new AirspaceVolumeRead(
                famiglia, nome.Trim(), categoria.Trim(), AirspaceFamilies.ClassOf(categoria),
                baseQ, tetto, anelli, chiave, ordinale));
        }

        if (quanti == 0)
            segnalazioni.Add(new AirspaceIssue(AirspaceIssueKind.FileNonLetto, "", "nessuno spazio aereo nel file"));

        return new AirspaceReadResult(volumi, segnalazioni, quanti, DataDiGenerazione(xml));
    }

    // AirspaceConverter lo scrive in un commento in testa al file: «This file was created on: Wed 15 July
    // 2026 at 18:30:49 UTC». Non e' un dato del dominio ma la risposta a «di quando e' questo file», e
    // chiederla a chi carica quando il file ce l'ha gia' scritta sarebbe un modo di farsela dire sbagliata.
    private static readonly System.Text.RegularExpressions.Regex Generato =
        new(@"created on:\s*(?:\w{3}\s+)?(\d{1,2}\s+\w+\s+\d{4})\s+at\s+(\d{2}:\d{2}:\d{2})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static DateTime? DataDiGenerazione(string xml)
    {
        var m = Generato.Match(xml.Length > 4096 ? xml[..4096] : xml);   // sta in testa, non si scandisce 12 MB
        if (!m.Success) return null;
        return DateTime.TryParse($"{m.Groups[1].Value} {m.Groups[2].Value}",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
            out var quando) ? quando : null;
    }

    /// <summary>
    /// Gli anelli <b>distinti</b> del volume: i poligoni a quota costante, deduplicati sul contorno 2D.
    ///
    /// <para>Il confronto per il doppione è sui punti <b>arrotondati</b> alla quinta cifra decimale (circa un
    /// metro): tetto e pavimento nascono dallo stesso contorno, ma passano da una conversione di quota per uno,
    /// e pretendere l'uguaglianza bit a bit su un <c>double</c> significherebbe tenerli tutti e due.</para>
    /// </summary>
    private static List<IReadOnlyList<(double Lat, double Lon)>> Anelli(XElement placemark)
    {
        var anelli = new List<IReadOnlyList<(double Lat, double Lon)>>();
        var chiavi = new HashSet<string>(StringComparer.Ordinal);

        foreach (var poligono in placemark.Descendants().Where(e => e.Name.LocalName == "Polygon"))
        {
            var esterno = poligono.Descendants().FirstOrDefault(e => e.Name.LocalName == "outerBoundaryIs");
            var testo = esterno?.Descendants().FirstOrDefault(e => e.Name.LocalName == "coordinates")?.Value;
            var punti = KmlReader.CoordinateConQuota(testo);
            if (punti.Count < 3) continue;

            // Una parete ha due quote: non è un contorno, è un lato della scatola.
            if (punti.Select(p => p.Alt).Distinct().Count() > 1) continue;

            var anello = punti.Select(p => (p.Lat, p.Lon)).ToList();
            ChiudiSeRipetuto(anello);
            if (anello.Count < 3) continue;

            if (chiavi.Add(Impronta(anello))) anelli.Add(anello);
        }
        return anelli;
    }

    private static string Impronta(IReadOnlyList<(double Lat, double Lon)> anello) =>
        string.Join(";", anello.Select(p =>
            $"{p.Lat.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)},{p.Lon.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)}"));

    /// <summary>L'ultimo vertice che ripete il primo è la chiusura dell'anello, non un punto in più.</summary>
    private static void ChiudiSeRipetuto(List<(double Lat, double Lon)> punti)
    {
        if (punti.Count <= 2) return;
        if (Math.Abs(punti[0].Lat - punti[^1].Lat) > 1e-9 || Math.Abs(punti[0].Lon - punti[^1].Lon) > 1e-9) return;
        punti.RemoveAt(punti.Count - 1);
    }

    /// <summary>I campi <c>SimpleData</c> del Placemark: nome → valore.</summary>
    private static Dictionary<string, string> DatiEstesi(XElement placemark)
    {
        var dati = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var campo in placemark.Descendants().Where(e => e.Name.LocalName == "SimpleData"))
        {
            var nome = campo.Attribute("name")?.Value;
            if (!string.IsNullOrWhiteSpace(nome)) dati[nome] = campo.Value;
        }
        return dati;
    }

    private static string? Testo(IReadOnlyDictionary<string, string> dati, string campo) =>
        dati.TryGetValue(campo, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    /// <summary>
    /// La quota del campo, col ripiego. ⚠️ Un tetto illeggibile <b>non</b> diventa il suolo: diventa
    /// l'illimitato, e una base illeggibile diventa il suolo. Il ripiego apre l'area invece di chiuderla —
    /// stessa regola del cancello AIRAC, che in caso di dubbio non nasconde niente: un'area disegnata più
    /// grande del vero si vede e si corregge, una rimpicciolita in silenzio no.
    /// </summary>
    private static AirspaceLevel Quota(
        IReadOnlyDictionary<string, string> dati, string campo, string nome,
        AirspaceFamily famiglia, List<AirspaceIssue> segnalazioni)
    {
        var grezzo = Testo(dati, campo);
        var letta = AirspaceLevelParser.Parse(grezzo);
        if (letta is not null) return letta;

        // La segnalazione si scrive solo per le famiglie che si usano davvero: un parco naturale con la quota
        // scritta storto non è una notizia, e cinquecento righe di rumore nascondono le tre che contano.
        if (AirspaceFamilies.IsUsable(famiglia))
            segnalazioni.Add(new AirspaceIssue(AirspaceIssueKind.QuotaNonLetta, nome, $"{campo}: {grezzo ?? "(vuoto)"}"));

        return campo.Equals("Top", StringComparison.OrdinalIgnoreCase)
            ? new AirspaceLevel(AirspaceDatum.Unlimited, null, grezzo ?? "")
            : new AirspaceLevel(AirspaceDatum.Gnd, 0, grezzo ?? "GND");
    }

    /// <summary>
    /// L'identità di un volume: <c>famiglia|nome|base|tetto</c>, maiuscola e con gli spazi ridotti a uno.
    /// ⚠️ Il nome da solo non basta — <c>GRAZZANISE CTR Z2</c> compare due volte con bande diverse — e la
    /// chiave si <b>scrive</b> invece di lasciarla dedurre a un confronto del database, come già fanno
    /// <c>Navaid.NaturalKey</c> e <c>GlossaryTerm.SourceKey</c>.
    /// </summary>
    public static string ChiaveNaturale(AirspaceFamily famiglia, string nome, AirspaceLevel baseQ, AirspaceLevel tetto) =>
        string.Join('|', famiglia.ToString().ToUpperInvariant(), Normale(nome), Normale(baseQ.Raw), Normale(tetto.Raw));

    private static string Normale(string? s) =>
        System.Text.RegularExpressions.Regex.Replace((s ?? "").Trim(), @"\s+", " ").ToUpperInvariant();

    private static AirspaceReadResult Guasto(string dettaglio) =>
        new([], [new AirspaceIssue(AirspaceIssueKind.FileNonLetto, "", dettaglio)], 0);
}
