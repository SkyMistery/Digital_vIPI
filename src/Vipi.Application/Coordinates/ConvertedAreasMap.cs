using Vipi.Application.Aor;
using Vipi.Application.Content;

namespace Vipi.Application.Coordinates;

/// <summary>
/// Le aree convertite viste come una mappa AoR (<see cref="AccAorView"/>). Gemella di
/// <see cref="RegulatedAreasMap"/>, e per la stessa ragione: il 2D con Leaflet, il 3D, le chip e il
/// commutatore esistono già e sono guidati dal DOM, quindi <b>nessun motore di mappa nuovo</b> — costa una
/// traduzione di nomi, ed è tutta qui.
///
/// <para>⚠️ <b>La chiave della chip è l'INDICE</b>, non il nome: i nomi delle aree arrivano da un file
/// qualsiasi, contengono spazi e virgolette, e il JS li usa dentro un selettore <c>[data-sec="…"]</c>. Un
/// numero non ha niente da rompere. È la stessa lezione delle aree regolamentate, dove la chiave è l'id IVAO.</para>
///
/// PURA/deterministica, nessun I/O.
/// </summary>
public static class ConvertedAreasMap
{
    /// <summary>
    /// Il raggio del cerchietto che rappresenta un punto singolo. ⚠️ Serve perché
    /// <see cref="AorPolygonProjector"/> vuole almeno tre vertici, e convertire <b>una</b> coordinata è il
    /// caso d'uso più comune di tutti: senza, chi converte un punto non vedrebbe mai la mappa.
    /// </summary>
    public const double RaggioPuntoNm = 0.3;

    /// <summary>
    /// Colori cartografici, non di brand — stessa scelta di <see cref="SpecialAreaColorScheme"/>: i poligoni
    /// si riempiono al 16% e si sovrappongono, e i passi del brand a piena saturazione a quell'opacità
    /// diventano indistinguibili. Esadecimali veri: Leaflet li scrive in attributi SVG, che non sostituiscono
    /// <c>var()</c>.
    /// </summary>
    private static readonly string[] Colori =
    [
        "#2F6FB0",   // blu
        "#B0413E",   // rosso
        "#3E8E5A",   // verde
        "#C9A227",   // giallo
        "#7B4EA8",   // viola
        "#3E8E8E",   // teal
    ];

    /// <summary>
    /// La vista mappa delle aree indicate. <paramref name="etichetta"/> dà il testo della chip (la pagina lo
    /// sa già: è quello del selettore, e i due elenchi devono dire la stessa cosa).
    /// </summary>
    /// <param name="riconvertite">
    /// Per ogni area, la <b>stessa</b> area riletta dall'uscita. Quando c'è, si aggiunge una forma
    /// tratteggiata sopra l'originale: se le due non combaciano, la conversione ha perso qualcosa e si vede
    /// prima ancora di leggere l'errore in metri.
    /// </param>
    public static AccAorView Build(
        IReadOnlyList<(int Indice, CoordinateArea Area)> aree,
        Func<int, string> etichetta,
        IReadOnlyDictionary<int, IReadOnlyList<(double Lat, double Lon)>>? riconvertite = null)
    {
        if (aree.Count == 0) return AccAorView.Empty;

        var settori = new List<AccSectorAor>(aree.Count);
        foreach (var (indice, area) in aree)
        {
            var colore = Colori[indice % Colori.Length];
            settori.Add(new AccSectorAor(
                Callsign: indice.ToString(),
                Name: etichetta(indice),
                Color: colore,
                Polygons: Poligoni(area.Punti),
                Label: etichetta(indice)));

            if (riconvertite is not null && riconvertite.TryGetValue(indice, out var tornata))
            {
                settori.Add(new AccSectorAor(
                    Callsign: $"{indice}r",
                    Name: etichetta(indice),
                    Color: colore,
                    Polygons: Poligoni(tornata),
                    Label: $"{etichetta(indice)} ↺",
                    Dashed: true));
            }
        }

        return new AccAorView(settori, Array.Empty<AccConfigSelection>());
    }

    /// <summary>
    /// I poligoni disegnabili di un elenco di punti: uno solo se i punti bastano a fare un anello, altrimenti
    /// un cerchietto per punto — che è il modo di <b>mostrare</b> una coordinata singola senza inventare un
    /// disegnatore di segnaposti.
    /// </summary>
    private static IReadOnlyList<AppAorPolygon> Poligoni(IReadOnlyList<(double Lat, double Lon)> punti)
    {
        if (punti.Count == 0) return Array.Empty<AppAorPolygon>();

        if (punti.Count >= 3)
        {
            var poly = AorPolygonProjector.Project(Json(punti));
            return poly is null ? Array.Empty<AppAorPolygon>() : [poly];
        }

        var cerchi = new List<AppAorPolygon>(punti.Count);
        foreach (var p in punti)
        {
            var c = AorPolygonProjector.Project(CircleShapeBuilder.Build(p.Lat, p.Lon, RaggioPuntoNm));
            if (c is not null) cerchi.Add(c);
        }
        return cerchi;
    }

    /// <summary>
    /// Punti → JSON <c>[[lng,lat],…]</c>. ⚠️ <b>Longitudine prima</b>: è la forma di <c>RegionMapPolygon</c>
    /// di IVAO, ed è quella che il proiettore si aspetta. Invertirla darebbe un poligono ruotato di 90° di
    /// cui nessuno si lamenterebbe, perché si disegna benissimo.
    /// </summary>
    private static string Json(IReadOnlyList<(double Lat, double Lon)> punti) =>
        AuroraRingJson.Scrivi(punti);
}

/// <summary>
/// Anello (Lat, Lon) → JSON <c>[[lng,lat],…]</c>. È la stessa conversione che
/// <c>AuroraSectorfileParser.RingToPolygonJson</c> fa per l'import; vive qui perché l'ordine invertito è una
/// conoscenza del <b>formato IVAO</b>, e chi sta nella UI non può vedere l'infrastruttura.
/// </summary>
public static class AuroraRingJson
{
    public static string Scrivi(IReadOnlyList<(double Lat, double Lon)> anello)
    {
        var sb = new System.Text.StringBuilder("[");
        for (var i = 0; i < anello.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('[')
              .Append(Math.Round(anello[i].Lon, 6).ToString(System.Globalization.CultureInfo.InvariantCulture))
              .Append(',')
              .Append(Math.Round(anello[i].Lat, 6).ToString(System.Globalization.CultureInfo.InvariantCulture))
              .Append(']');
        }
        return sb.Append(']').ToString();
    }
}
