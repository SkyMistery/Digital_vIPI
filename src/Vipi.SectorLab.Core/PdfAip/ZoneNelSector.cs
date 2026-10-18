using Aip;
using Aip.Aurora;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Uno spazio dell'AIP (CTR, ATZ) scritto come lo tiene il sector (slice 1c, «Porta nel sector» e «Sostituisci»): una
/// voce del <c>MAPS</c> dello <c>.str</c> di scalo, <c>LIPE;MAPS;LIPE CTR; ; ;1;</c> o <c>LIPE;MAPS;LIPE ATZ; ; ;5;</c>, e
/// sotto le righe dei punti — una zona dopo l'altra, ognuna chiusa (l'ultimo punto ripete il primo), la penna che si alza
/// fra una zona e la prossima (<c>&lt;br&gt;</c> sul primo punto della zona). Come le voci che ci sono (<c>lipe.str</c>,
/// <c>lipq.str</c>). Un settore di torre di <c>twrs.tfl</c> è invece un poligono solo, che si chiude da sé: una zona,
/// e l'ultimo punto non ripete il primo.
/// <list type="bullet">
/// <item>🔴 I vertici dell'AIP escono identici (<see cref="Outline"/>); archi e cerchi alla densità data.</item>
/// <item>Un commento <c>//zona N</c> su una riga sua prima di ogni zona, se le zone sono più d'una: mai in coda a una
/// riga di dati (Aurora la legge nel doppio del tempo).</item>
/// <item>Un tratto di confine o di costa (l'AIP lo nomina, non lo disegna) si prende dalla linea che il sector ha già:
/// i punti fra i due capi, se i capi ci stanno entro <see cref="TolleranzaDelConfine"/>. Senza una linea da cui
/// prenderlo lo spazio non si scrive: una retta al posto di una costa sarebbe un dato sbagliato.</item>
/// </list>
/// </summary>
public static class ZoneNelSector
{
    /// <summary>Quanto un capo del tratto di confine può stare lontano da un punto della linea del sector, in NM.</summary>
    public const double TolleranzaDelConfine = 0.5;

    /// <summary>
    /// La testata di una voce nuova del <c>MAPS</c>: <c>LIPE;MAPS;LIPE CTR; ; ;1;</c> (tipo 1, come tutte le 45 del sector)
    /// o <c>LIPE;MAPS;LIPE ATZ; ; ;5;</c> (tipo 5, come 85 delle 87); null per i settori delle torri, che non si portano.
    /// </summary>
    public static string? Testata(string icao, FamigliaDelConfronto famiglia = FamigliaDelConfronto.Ctr) => famiglia switch
    {
        FamigliaDelConfronto.Ctr => $"{icao};MAPS;{icao} CTR; ; ;1;",
        FamigliaDelConfronto.Atz => $"{icao};MAPS;{icao} ATZ; ; ;5;",
        _ => null,
    };

    /// <summary>Le zone di uno spazio dell'AIP (<see cref="ConfrontoDelleForme.Gruppi"/>), nell'ordine del modello.</summary>
    public static IReadOnlyList<Airspace> ZoneDi(IEnumerable<Airspace> aip, string nome, FamigliaDelConfronto famiglia = FamigliaDelConfronto.Ctr) =>
        ConfrontoDelleForme.Gruppi(famiglia, aip).FirstOrDefault(g => string.Equals(g.Nome, nome, StringComparison.OrdinalIgnoreCase)).Zone ?? [];

    /// <summary>
    /// Le righe dei punti, senza la testata. <paramref name="vecchia"/>: la linea che il sector ha adesso (i suoi tratti),
    /// da cui si prendono i tratti di confine; vuota per uno spazio che nel sector manca. <paramref name="poligono"/>: un
    /// settore di <c>twrs.tfl</c> (una zona sola, non si ripete il primo punto). Null se non si può scrivere, e
    /// <paramref name="perche"/> lo dice.
    /// </summary>
    public static IReadOnlyList<string>? Corpo(IReadOnlyList<Airspace> zone, IReadOnlyList<IReadOnlyList<Coordinate>> vecchia,
                                               double puntiPerGrado, out string? perche, bool poligono = false, BorderTracer? riserva = null)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(vecchia);
        perche = null;
        if (zone.Count == 0)
        {
            perche = "Nell'AIP espanso questo spazio non ha zone.";
            return null;
        }
        if (poligono && zone.Count > 1)
        {
            perche = $"Nell'AIP ha {zone.Count} zone: un settore della torre è un poligono solo, si sistema a mano.";
            return null;
        }

        // Il tratto di confine dalla linea che la voce ha già; se non c'è (una voce nuova), dal tracciato di riserva —
        // itgeo.geo del sector, l'eccezione del committente del 19 ottobre.
        var opzioni = new OutlineOptions
        {
            PointsPerDegree = puntiPerGrado,
            Border = (s, da, a) => DallaLinea(vecchia, da, a) ?? riserva?.Invoke(s, da, a),
        };
        var righe = new List<string>();
        for (var n = 0; n < zone.Count; n++)
        {
            var problemi = new List<AuroraIssue>();
            var punti = Outline.Points(zone[n].Geometry, opzioni, problemi, zone[n].Key);
            if (problemi.FirstOrDefault(p => p.Kind == AuroraIssueKind.BorderNotDrawn) is { } confine)
            {
                perche = vecchia.Count == 0
                    ? $"{zone[n].Designator}: il tratto «{confine.Detail}» l'AIP non lo disegna, e il sector non ha una linea da cui prenderlo."
                    : $"{zone[n].Designator}: il tratto «{confine.Detail}» non si ritrova sulla linea del sector (i suoi capi ne stanno a più di {TolleranzaDelConfine:0.0} NM).";
                return null;
            }

            if (zone.Count > 1)
                righe.Add($"//zona {n + 1}");
            for (var i = 0; i < punti.Count + (poligono ? 0 : 1); i++)
            {
                var riga = AuroraText.Coordinates(punti[i % punti.Count]) + ";";
                righe.Add(i == 0 && n > 0 ? riga + "<br>" : riga);
            }
        }

        return righe;
    }

    /// <summary>
    /// Le sostituzioni (numero di riga da 1 → righe) che mettono <paramref name="corpo"/> al posto delle righe dei punti
    /// del record che comincia alla riga <paramref name="da"/> (da 0) e ne ha <paramref name="quante"/>: la testata resta
    /// com'è (coi tag e i commenti sopra di lei), il resto se ne va.
    /// </summary>
    public static IReadOnlyDictionary<int, IReadOnlyList<string>> AlPostoDelCorpo(IReadOnlyList<string> righe, int da, int quante, IReadOnlyList<string> corpo)
    {
        ArgumentNullException.ThrowIfNull(righe);
        var sostituzioni = new Dictionary<int, IReadOnlyList<string>> { [da + 1] = [righe[da], .. corpo] };
        for (var i = da + 1; i < da + quante; i++)
            sostituzioni[i + 1] = [];
        return sostituzioni;
    }

    /// <summary>Il tratto di confine dalla linea del sector: i punti fra quelli più vicini ai due capi, nello stesso tratto.</summary>
    private static List<Position>? DallaLinea(IReadOnlyList<IReadOnlyList<Coordinate>> linea, Position da, Position a)
    {
        (int Tratto, int Da, int A, double Scarto)? migliore = null;
        for (var t = 0; t < linea.Count; t++)
        {
            var (iDa, dDa) = PiuVicino(linea[t], da);
            var (iA, dA) = PiuVicino(linea[t], a);
            var scarto = Math.Max(dDa, dA);
            if (iDa >= 0 && iA >= 0 && (migliore is null || scarto < migliore.Value.Scarto))
                migliore = (t, iDa, iA, scarto);
        }

        if (migliore is not { } m || m.Scarto > TolleranzaDelConfine)
            return null;
        var punti = linea[m.Tratto];
        var verso = m.A >= m.Da ? 1 : -1;
        var tracciato = new List<Position>();
        for (var i = m.Da + verso; i != m.A; i += verso)
            tracciato.Add(new Position(punti[i].LatitudeDeg, punti[i].LongitudeDeg));
        return tracciato;
    }

    private static (int Indice, double Distanza) PiuVicino(IReadOnlyList<Coordinate> punti, Position p)
    {
        var (indice, distanza) = (-1, double.MaxValue);
        for (var i = 0; i < punti.Count; i++)
        {
            var d = Sphere.DistanceNm(new Position(punti[i].LatitudeDeg, punti[i].LongitudeDeg), p);
            if (d < distanza)
                (indice, distanza) = (i, d);
        }
        return (indice, distanza);
    }

    /// <summary>La densità degli archi che la voce ha già (<see cref="DensitaDegliArchi"/>), o quella data se non ne ha.</summary>
    public static double DensitaDi(IReadOnlyList<IReadOnlyList<Coordinate>> vecchia, double diBase) =>
        DensitaDegliArchi.Stima(vecchia) ?? diBase;
}
