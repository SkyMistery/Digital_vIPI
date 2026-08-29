using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Vipi.Application.Coordinates;

/// <summary>
/// Legge le aree di un KML (Google Earth) o di un KMZ, che è un KML dentro uno zip. Puro: gli si dà il testo o
/// uno stream già aperto, e non tocca il disco.
///
/// <para>⚠️ <b>In KML le coordinate sono <c>lon,lat,alt</c></b> — longitudine prima, come in GeoJSON e come nel
/// <c>regionMapPolygon</c> di IVAO. La quota si ignora: il nostro dominio è 2D, e le bande FL stanno altrove
/// (<see cref="Vipi.Application.Aor.AorFlBand"/>).</para>
///
/// <para>⚠️ <b>Il buco si scarta, e lo si dice</b> (deciso dal committente il 29 agosto 2026): di un poligono
/// con <c>innerBoundaryIs</c> si tiene il contorno esterno. Un buco perso in silenzio è una zona che sembra
/// vietata e non lo è.</para>
/// </summary>
public static class KmlReader
{
    /// <summary>Il KMZ caricato, compresso. Otto mega sono un KML gigantesco: il nostro caso d'uso sono aree.</summary>
    public const int MaxByteFile = 8 * 1024 * 1024;

    /// <summary>Quanto si accetta di leggere una volta APERTO lo zip.</summary>
    public const int MaxByteDecompresso = 32 * 1024 * 1024;

    /// <summary>Quante voci può avere lo zip.</summary>
    public const int MaxVociZip = 200;

    /// <summary>
    /// Il KML come testo. ⚠️ Il confronto sui nomi degli elementi è per <b>nome locale</b>: un KML dichiara lo
    /// spazio dei nomi OGC, un altro quello di Google, e legare il lettore all'uno o all'altro significa
    /// rifiutare metà dei file veri senza una ragione.
    /// </summary>
    public static CoordinateReadResult LeggiKml(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return CoordinateReadResult.Vuoto;
        xml = xml.TrimStart('﻿', '​');   // stesso motivo: un BOM incollato a mano fa lo stesso danno

        XDocument doc;
        try
        {
            // ⚠️ DTD spenta e nessun resolver: un XML arriva da fuori, e le entità esterne sono la via classica
            // per farsi leggere un file del server.
            using var reader = XmlReader.Create(new StringReader(xml),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            doc = XDocument.Load(reader);
        }
        catch (XmlException e)
        {
            return new CoordinateReadResult([],
                [new CoordinateIssue(CoordinateIssueKind.FileNonLetto, 0, "", e.Message)], 0, 0);
        }

        var aree = new List<CoordinateArea>();
        var segnalazioni = new List<CoordinateIssue>();

        foreach (var placemark in doc.Descendants().Where(e => e.Name.LocalName == "Placemark"))
        {
            var nome = placemark.Elements().FirstOrDefault(e => e.Name.LocalName == "name")?.Value.Trim();
            if (string.IsNullOrWhiteSpace(nome)) nome = null;

            var geometrie = 0;
            foreach (var (punti, chiuso, buchi) in Geometrie(placemark))
            {
                if (punti.Count == 0) continue;
                geometrie++;

                // Più geometrie nello stesso Placemark (MultiGeometry) diventano aree distinte, numerate: sono
                // aree distinte davvero, e fonderle le renderebbe un poligono che nel file non c'è.
                var etichetta = geometrie > 1 ? $"{nome ?? "Area"} ({geometrie})" : nome;
                aree.Add(new CoordinateArea(etichetta, punti, chiuso));

                for (var i = 0; i < buchi; i++)
                    segnalazioni.Add(new CoordinateIssue(CoordinateIssueKind.BucoScartato, 0, etichetta ?? ""));
            }
        }

        if (aree.Count == 0 && segnalazioni.Count == 0)
            segnalazioni.Add(new CoordinateIssue(CoordinateIssueKind.FileNonLetto, 0, "", "0 Placemark"));

        // Un KML non ha righe: al loro posto si contano i PUNTI, che è ciò che la pagina mostra («letti N
        // punti»). Dire «0 righe su 0» a chi ha appena caricato un file sarebbe una bugia con l'aria di un guasto.
        var quantiPunti = aree.Sum(a => a.Punti.Count);
        return new CoordinateReadResult(aree, segnalazioni, quantiPunti, quantiPunti);
    }

    /// <summary>Il KMZ: uno zip che dentro ha <c>doc.kml</c>, o in sua assenza il primo <c>*.kml</c>.</summary>
    public static CoordinateReadResult LeggiKmz(Stream zip)
    {
        try
        {
            using var archivio = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);

            // ⚠️ I tre tetti stanno QUI e non nella pagina: uno zip che si dichiara piccolo e si apre enorme è
            // il più vecchio dei trucchi, e questo codice non sa chi gli ha passato il file.
            if (archivio.Entries.Count > MaxVociZip)
                return Guasto($"{archivio.Entries.Count} > {MaxVociZip} voci");

            var voce = archivio.Entries.FirstOrDefault(e => e.Name.Equals("doc.kml", StringComparison.OrdinalIgnoreCase))
                       ?? archivio.Entries.FirstOrDefault(e => e.Name.EndsWith(".kml", StringComparison.OrdinalIgnoreCase));
            if (voce is null) return Guasto("nessun .kml nello zip");
            if (voce.Length > MaxByteDecompresso) return Guasto($"{voce.Length} byte decompressi");

            using var flusso = voce.Open();
            using var limitato = new MemoryStream();
            var buffer = new byte[81920];
            int letti;
            while ((letti = flusso.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (limitato.Length + letti > MaxByteDecompresso) return Guasto("supera il tetto decompresso");
                limitato.Write(buffer, 0, letti);
            }

            // ⚠️ Il BOM: `Encoding.UTF8.GetString` lo lascia in testa alla stringa, e un XML che comincia con
            // U+FEFF non si apre («Data at the root level is invalid»). Google Earth e mezzo mondo lo scrivono.
            // Lo StreamReader col riconoscimento dell'ordine dei byte lo toglie, e in più legge gli UTF-16.
            limitato.Position = 0;
            using var lettore = new StreamReader(limitato, System.Text.Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true);
            return LeggiKml(lettore.ReadToEnd());
        }
        catch (InvalidDataException e)
        {
            return Guasto(e.Message);
        }
    }

    private static CoordinateReadResult Guasto(string dettaglio) =>
        new([], [new CoordinateIssue(CoordinateIssueKind.FileNonLetto, 0, "", dettaglio)], 0, 0);

    /// <summary>Le geometrie di un Placemark: poligoni (contorno esterno), linee e punti.</summary>
    private static IEnumerable<(List<(double Lat, double Lon)> Punti, bool Chiuso, int Buchi)> Geometrie(XElement placemark)
    {
        foreach (var geo in placemark.Descendants().Where(e => e.Name.LocalName is "Polygon" or "LineString" or "Point"))
        {
            if (geo.Name.LocalName == "Polygon")
            {
                var esterno = geo.Descendants().FirstOrDefault(e => e.Name.LocalName == "outerBoundaryIs");
                var buchi = geo.Descendants().Count(e => e.Name.LocalName == "innerBoundaryIs");
                var testo = esterno?.Descendants().FirstOrDefault(e => e.Name.LocalName == "coordinates")?.Value;
                var punti = Coordinate(testo);
                ChiudiSeRipetuto(punti);
                yield return (punti, punti.Count > 2, buchi);   // un Polygon KML è chiuso per definizione
            }
            else
            {
                var testo = geo.Elements().FirstOrDefault(e => e.Name.LocalName == "coordinates")?.Value;
                var punti = Coordinate(testo);
                yield return (punti, ChiudiSeRipetuto(punti), 0);
            }
        }
    }

    /// <summary>L'ultimo vertice che ripete il primo è la chiusura dell'anello, non un punto in più.</summary>
    private static bool ChiudiSeRipetuto(List<(double Lat, double Lon)> punti)
    {
        if (punti.Count <= 2) return false;
        if (Math.Abs(punti[0].Lat - punti[^1].Lat) > 1e-9 || Math.Abs(punti[0].Lon - punti[^1].Lon) > 1e-9)
            return false;
        punti.RemoveAt(punti.Count - 1);
        return true;
    }

    /// <summary>Il contenuto di <c>&lt;coordinates&gt;</c>: terne <c>lon,lat[,alt]</c> separate da spazi o a capo.</summary>
    private static List<(double Lat, double Lon)> Coordinate(string? testo)
    {
        var punti = new List<(double Lat, double Lon)>();
        if (string.IsNullOrWhiteSpace(testo)) return punti;

        foreach (var terna in testo.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var pezzi = terna.Split(',');
            if (pezzi.Length < 2) continue;
            if (!double.TryParse(pezzi[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)) continue;
            if (!double.TryParse(pezzi[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)) continue;
            if (Math.Abs(lat) > 90 || Math.Abs(lon) > 180) continue;
            punti.Add((lat, lon));
        }
        return punti;
    }
}
