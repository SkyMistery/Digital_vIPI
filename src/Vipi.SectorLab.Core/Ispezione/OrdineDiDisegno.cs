using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Core.Ispezione;

/// <summary>Il posto di un riempimento nell'ordine di disegno del suo file.</summary>
/// <param name="Posto">Da 1: il primo del file si disegna per primo, cioè sotto a tutti.</param>
/// <param name="Quanti">I riempimenti del file.</param>
/// <param name="Sopra">Cosa si disegna dopo di lui, quindi sopra: riempimento e quanti, nell'ordine del file.</param>
public sealed record PostoNelDisegno(int Posto, int Quanti, IReadOnlyList<(string Riempimento, int Quanti)> Sopra);

/// <summary>
/// L'ordine di disegno dei riempimenti di un <c>.pol</c> (lotto «Subito» slice 12c, «file per file» I3): in Aurora
/// <b>vince l'ultimo del file</b> — un poligono copre quelli scritti prima di lui, e <c>HOLE</c> buca quel che ha sotto.
/// La scheda dice a che posto sta un riempimento e cosa ha sopra; un poligono nuovo nasce dopo l'ultimo del suo
/// riempimento (<c>ModificheInSospeso.AggiungiRecord</c>).
/// </summary>
/// <remarks>
/// L'ordine giusto fra i riempimenti è quello del fork (committente, 4 ottobre 2026): erba → taxiway → cemento →
/// piazzale → buchi → edifici → pista (<c>Vipi.Sectorfile.Shared.OrdineDeiRiempimenti</c>). Chi è fuori posto lo dice
/// il validatore (<c>Regola.OrdineDiDisegno</c>, slice 12d); qui si mostra l'ordine che il file ha.
/// </remarks>
public static class OrdineDiDisegno
{
    /// <summary>Il posto del record nel disegno, o null se non è un riempimento.</summary>
    public static PostoNelDisegno? Di(FileAperto file, int indice)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file is not IFileConRecord conRecord || indice < 0 || indice >= conRecord.RecordDelModello.Count
            || conRecord.RecordDelModello[indice] is not Polygon)
            return null;

        var riempimenti = conRecord.RecordDelModello.Select((r, i) => (Record: r as Polygon, Indice: i)).Where(r => r.Record is not null).ToList();
        int posto = riempimenti.FindIndex(r => r.Indice == indice);
        var sopra = new List<(string, int)>();
        foreach (var (poligono, _) in riempimenti.Skip(posto + 1))
        {
            string suo = poligono!.FillColor.Trim().ToUpperInvariant();
            int gia = sopra.FindIndex(s => s.Item1 == suo);
            if (gia < 0)
                sopra.Add((suo, 1));
            else
                sopra[gia] = (suo, sopra[gia].Item2 + 1);
        }

        return new PostoNelDisegno(posto + 1, riempimenti.Count, sopra);
    }
}
