using Vipi.Application.Coordinates;

namespace Vipi.SectorLab.Core.Modifiche;

/// <summary>
/// Il testo di «incolla da testo» letto dal convertitore di F1, PRIMA di toccare il record: è l'anteprima che la scheda
/// e la mappa mostrano mentre l'AOD scrive (chiesta dal committente il 23 settembre, per vedere come cambia la forma
/// cambiando la densità degli archi), e insieme la lettura che l'incolla usa davvero — una sola, così l'anteprima è
/// esattamente quello che si incollerà.
/// </summary>
/// <param name="Punti">I vertici che l'incolla scriverebbe; vuoto se il testo si rifiuta.</param>
/// <param name="Archi">Quanti archi il testo descrive (i cerchi a parte).</param>
/// <param name="Cerchi">Quanti cerchi interi.</param>
/// <param name="Centri">I centri di archi e cerchi: l'anteprima li segna, perché un centro sbagliato si vede lì.</param>
/// <param name="Rifiuto">Perché non si può incollare; null se si può.</param>
public sealed record TestoDaIncollare(
    IReadOnlyList<(double Lat, double Lon)> Punti,
    int Archi,
    int Cerchi,
    IReadOnlyList<(double Lat, double Lon)> Centri,
    string? Rifiuto)
{
    public static TestoDaIncollare Leggi(string? testo, double puntiPerGrado)
    {
        var letto = CoordinateParser.Parse(testo, puntiPerGrado);
        var centri = letto.CentriAip.Select(c => (c.Lat, c.Lon)).ToList();
        var aree = letto.Aree.Where(a => a.Punti.Count > 0).ToList();
        string? rifiuto = aree.Count switch
        {
            0 => "In quel testo non c'è nessuna coordinata che si possa leggere.",
            > 1 => $"Quel testo contiene {aree.Count} aree: incollane una sola.",
            _ => null,
        };

        return new TestoDaIncollare(rifiuto is null ? aree[0].Punti : [], letto.Archi, letto.Cerchi, centri, rifiuto);
    }
}
