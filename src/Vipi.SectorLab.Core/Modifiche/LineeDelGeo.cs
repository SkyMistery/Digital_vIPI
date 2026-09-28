using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Modifiche;

/// <summary>
/// Una linea di un <c>.geo</c> (lotto «Subito» slice 5d, «file per file» G1): i segmenti di fila nel file, attaccati (la
/// fine di uno è l'inizio del prossimo), dello stesso tipo e della stessa area, senza righe in mezzo. Nel file sono N
/// segmenti; all'AOD sono una linea di N+1 punti.
/// </summary>
/// <param name="Record">I record dei segmenti, in ordine.</param>
/// <param name="Punti">I punti: l'inizio del primo segmento, poi la fine di ognuno.</param>
/// <param name="Prima">Il record della linea che finisce dove questa comincia, staccata solo da righe vuote: si riunisce.</param>
/// <param name="Dopo">Il record della linea che comincia dove questa finisce, staccata solo da righe vuote.</param>
public sealed record LineaDelGeo(IReadOnlyList<int> Record, IReadOnlyList<Coordinate> Punti, int? Prima, int? Dopo);

/// <summary>
/// La vista a linea dei <c>.geo</c> (G1): cambiare un punto riscrive i due segmenti che lo toccano, così la catena non si
/// rompe; «spezza» e «unisci» mettono e tolgono la riga vuota fra due segmenti (R-3), sul testo come nella slice 5b.
/// </summary>
public static class LineeDelGeo
{
    /// <summary>La linea del segmento <paramref name="indice"/>, nel file com'è adesso; null se non è un segmento.</summary>
    public static LineaDelGeo? Di(FileAperto file, int indice, IReadOnlyList<string> righe, IReadOnlyList<(int Da, int Quante)> posti)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(righe);
        ArgumentNullException.ThrowIfNull(posti);
        if (file is not IFileConRecord conRecord || indice < 0 || indice >= conRecord.RecordDelModello.Count
            || conRecord.RecordDelModello[indice] is not Line)
            return null;
        var record = conRecord.RecordDelModello;

        int primo = indice, ultimo = indice;
        while (primo > 0 && Legati(record, posti, righe, primo - 1, soloVuote: false))
            primo--;
        while (ultimo + 1 < record.Count && Legati(record, posti, righe, ultimo, soloVuote: false))
            ultimo++;

        var segmenti = Enumerable.Range(primo, ultimo - primo + 1).ToList();
        var punti = new List<Coordinate> { ((Line)record[primo]).Start };
        punti.AddRange(segmenti.Select(r => ((Line)record[r]).End));
        int? prima = primo > 0 && Legati(record, posti, righe, primo - 1, soloVuote: true) ? primo - 1 : null;
        int? dopo = ultimo + 1 < record.Count && Legati(record, posti, righe, ultimo, soloVuote: true) ? ultimo + 1 : null;
        return new LineaDelGeo(segmenti, punti, prima, dopo);
    }

    /// <summary>
    /// Vero se i segmenti <paramref name="r"/> e r+1 sono la stessa linea: attaccati, stesso tipo e area, e fra loro
    /// niente (<paramref name="soloVuote"/> falso) o solo righe vuote, almeno una (vero: una linea spezzata).
    /// </summary>
    private static bool Legati(IReadOnlyList<object> record, IReadOnlyList<(int Da, int Quante)> posti, IReadOnlyList<string> righe,
                               int r, bool soloVuote)
    {
        if (record[r] is not Line a || record[r + 1] is not Line b || a.End != b.Start
            || !string.Equals(a.Color.Trim(), b.Color.Trim(), StringComparison.OrdinalIgnoreCase) || a.Nome != b.Nome)
            return false;
        int da = posti[r].Da + posti[r].Quante, a2 = posti[r + 1].Da;
        if (!soloVuote)
            return da == a2;
        return a2 > da && Enumerable.Range(da, a2 - da).All(i => righe[i].Trim().Length == 0);
    }

    /// <summary>
    /// Le righe da cambiare per spezzare la linea al suo punto <paramref name="punto"/> (un punto in mezzo: da 1 al
    /// penultimo): una riga vuota prima del segmento che comincia lì.
    /// </summary>
    internal static Dictionary<int, IReadOnlyList<string>>? Spezza(LineaDelGeo linea, int punto, IReadOnlyList<string> righe,
                                                                 IReadOnlyList<(int Da, int Quante)> posti, out string? perche)
    {
        perche = null;
        if (punto < 1 || punto >= linea.Record.Count)
        {
            perche = "La linea si spezza in un punto in mezzo: al primo e all'ultimo è già finita.";
            return null;
        }

        int da = posti[linea.Record[punto]].Da;
        return new Dictionary<int, IReadOnlyList<string>> { [da + 1] = ["", righe[da]] };
    }

    /// <summary>Le righe da cambiare per riunire la linea con quella dopo (<paramref name="dopo"/>) o prima: via le righe vuote fra loro.</summary>
    internal static Dictionary<int, IReadOnlyList<string>>? Unisci(LineaDelGeo linea, bool dopo, IReadOnlyList<(int Da, int Quante)> posti, out string? perche)
    {
        perche = null;
        int? altro = dopo ? linea.Dopo : linea.Prima;
        if (altro is not { } r)
        {
            perche = "Da quella parte non c'è una linea staccata da una riga vuota.";
            return null;
        }

        int primo = dopo ? linea.Record[^1] : r;
        int da = posti[primo].Da + posti[primo].Quante, a = posti[primo + 1].Da;
        return Enumerable.Range(da, a - da).ToDictionary(i => i + 1, _ => (IReadOnlyList<string>)[]);
    }

    /// <summary>Un punto come si scrive nella scheda: le coordinate in DMS puntato.</summary>
    public static string Scrivi(Coordinate punto) => CoordinateConverter.ToDottedDms(punto);
}
