namespace Vipi.Sectorfile.Shared;

/// <summary>
/// L'ordine di disegno dei riempimenti di terra in un <c>.pol</c> (lotto «Subito» slice 12d, «file per file» I3): in
/// Aurora vince l'ultimo poligono del file, quindi si scrive prima quel che sta sotto.
/// </summary>
/// <remarks>
/// Deciso dal committente il 4 ottobre 2026: vale l'ordine che il fork ha già — erba, taxiway, cemento, piazzale,
/// edifici, pista — e non quello scritto in carta il 25 settembre (erba, cemento, piazzale, taxiway, pista, edifici,
/// buchi), che solo 16 file su 93 seguivano. Il posto di <c>HOLE</c> viene dalla misura: sul fork i buchi stanno fra i
/// piazzali e gli edifici (55 volte dopo un altro buco, 8 dopo un piazzale; prima di un edificio 7), e lì i poligoni
/// fuori posto sono 49 in 14 file — in ogni altro posto 109 o più.
/// </remarks>
public static class OrdineDeiRiempimenti
{
    /// <summary>Dal primo che si scrive (sotto a tutti) all'ultimo (sopra a tutti).</summary>
    public static IReadOnlyList<string> Ordine { get; } = ["GRASS", "TAXIWAY", "CONCRETE", "APRON", "HOLE", "BUILDING", "RUNWAY"];

    /// <summary>Lo stesso ordine con le parole dell'AOD, per i messaggi.</summary>
    public const string InParole = "erba → taxiway → cemento → piazzale → buchi → edifici → pista";

    /// <summary>Il posto di un riempimento nell'ordine (da 0), o null se non è uno di quelli di terra.</summary>
    public static int? Posto(string? riempimento)
    {
        string nome = (riempimento ?? string.Empty).Trim();
        for (int i = 0; i < Ordine.Count; i++)
        {
            if (string.Equals(Ordine[i], nome, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return null;
    }
}
