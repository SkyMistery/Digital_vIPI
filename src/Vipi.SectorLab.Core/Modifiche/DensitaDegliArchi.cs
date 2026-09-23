using Vipi.Application.Coordinates;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Modifiche;

/// <summary>
/// Quanto sono fitti gli archi che un file ha già (prove a mano del committente, 23 settembre): il convertitore di F1
/// ne mette un punto per grado, chi ha disegnato <c>twrs.tfl</c> uno ogni 8-10° — incollare l'arco di un CTR faceva
/// crescere il file di 240 righe e il diff di altrettante, per la stessa forma. Qui si stima il passo dagli archi che
/// ci sono, e l'«incolla da testo» lo usa (l'AOD lo può cambiare).
/// <para>Il passo di un arco disegnato a punti è l'angolo di cui gira la spezzata a ogni vertice (è l'angolo al centro
/// fra due punti consecutivi). Si guardano solo i vertici «da arco»: svolta piccola (0,5-30°) e due lati di lunghezza
/// quasi uguale; i vertici dei lati dritti e gli spigoli non contano. Si prende la mediana: un vertice storto non
/// sposta la stima.</para>
/// </summary>
public static class DensitaDegliArchi
{
    /// <summary>Quanti vertici «da arco» servono perché la stima valga qualcosa.</summary>
    private const int VerticiMinimi = 3;

    /// <summary>La densità (punti per grado) degli archi di questi punti, o null se non ci sono archi da misurare.</summary>
    public static double? Stima(IEnumerable<IReadOnlyList<Coordinate>> elenchi)
    {
        ArgumentNullException.ThrowIfNull(elenchi);
        var passi = new List<double>();
        foreach (var punti in elenchi)
        {
            for (int i = 1; i + 1 < punti.Count; i++)
            {
                if (PassoDArco(punti[i - 1], punti[i], punti[i + 1]) is { } passo)
                    passi.Add(passo);
            }
        }

        if (passi.Count < VerticiMinimi)
            return null;

        passi.Sort();
        double mediana = passi[passi.Count / 2];
        return Math.Clamp(1 / mediana, ArcGeometry.DensitaMinima, ArcGeometry.DensitaMassima);
    }

    /// <summary>L'angolo di svolta in b, in gradi, se b sembra un vertice d'arco; null altrimenti.</summary>
    private static double? PassoDArco(Coordinate a, Coordinate b, Coordinate c)
    {
        // Piano locale: basta per angoli e rapporti su lati di qualche miglio.
        double k = Math.Cos(b.LatitudeDeg * Math.PI / 180);
        double x1 = (b.LongitudeDeg - a.LongitudeDeg) * k, y1 = b.LatitudeDeg - a.LatitudeDeg;
        double x2 = (c.LongitudeDeg - b.LongitudeDeg) * k, y2 = c.LatitudeDeg - b.LatitudeDeg;
        double l1 = Math.Sqrt((x1 * x1) + (y1 * y1)), l2 = Math.Sqrt((x2 * x2) + (y2 * y2));
        if (l1 <= 0 || l2 <= 0)
            return null;

        double rapporto = l1 / l2;
        if (rapporto is < 0.8 or > 1.25)
            return null;

        double coseno = Math.Clamp(((x1 * x2) + (y1 * y2)) / (l1 * l2), -1, 1);
        double svolta = Math.Acos(coseno) * 180 / Math.PI;
        return svolta is >= 0.5 and <= 30 ? svolta : null;
    }
}
