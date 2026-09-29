namespace Vipi.Sectorfile.IO;

/// <summary>
/// I campi facoltativi in coda a una riga dei NAVAIDS (lotto «Subito» slice 10a): si scrivono fino all'ultimo che c'è,
/// e uno che manca prima di lui resta vuoto al suo posto (<c>ALB;116.95;…;;;;HLD-ALB;</c>: l'attesa è l'8° campo).
/// </summary>
internal static class CampiFacoltativi
{
    internal static void Aggiungi(List<string> campi, params string?[] facoltativi)
    {
        int ultimo = Array.FindLastIndex(facoltativi, c => c is not null);
        for (int i = 0; i <= ultimo; i++)
        {
            campi.Add(facoltativi[i] ?? string.Empty);
        }
    }
}
