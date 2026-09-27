namespace Vipi.Sectorfile.IO;

/// <summary>
/// I commenti in coda a una riga di dati (<c>T;BREAK;RIVAM;RIVAM; //discontinuity</c>): carta «file per file» §C,
/// lotto «Subito» slice 2. Il committente, 24 settembre 2026: «dopo una riga letta da Aurora non vanno commenti
/// <c>//</c>»; lo sviluppatore di Aurora dice che una riga così si legge in circa il doppio del tempo. Sul fork del 27
/// settembre sono 713 righe in 70 file (<c>limm.mva</c> 374 <c>//Coast</c>). Il Lab non ne scrive mai, li segnala
/// (avviso) e li sposta sopra la riga.
/// </summary>
public static class CommentiInCoda
{
    /// <summary>
    /// Dove comincia il commento in coda (l'indice del <c>//</c>), o null se la riga non ne ha: una riga vuota, un
    /// commento intero (anche un record disattivato o un tag <c>//@</c>) o una riga di soli dati.
    /// </summary>
    public static int? Dove(string riga)
    {
        ArgumentNullException.ThrowIfNull(riga);
        string t = riga.TrimStart();
        if (t.Length == 0 || t.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        int i = riga.IndexOf("//", StringComparison.Ordinal);
        return i < 0 ? null : i;
    }

    /// <summary>
    /// Le due righe che prendono il posto di una riga col commento in coda: il commento sopra (col rientro della riga),
    /// i dati sotto (senza gli spazi prima del <c>//</c>). Null se la riga non ne ha.
    /// </summary>
    /// <remarks>
    /// 🔴 <c>limc.sid</c>: <c>…;0;OSKOR; //SUPER-HEAVY-A321</c> — il commento sta nell'8° campo; spostato, il campo resta
    /// vuoto (i dati finiscono col <c>;</c> che c'era).
    /// </remarks>
    public static (string Commento, string Dati)? Separa(string riga)
    {
        if (Dove(riga) is not { } i)
        {
            return null;
        }

        string rientro = riga[..(riga.Length - riga.TrimStart().Length)];
        return (rientro + riga[i..].TrimEnd(), riga[..i].TrimEnd());
    }

    /// <summary>
    /// Il testo con i commenti in coda spostati sopra la loro riga: tutti, o solo quelli delle righe
    /// <paramref name="soloLeRighe"/> (numeri da 1, del testo di partenza). Le altre righe restano com'erano.
    /// </summary>
    public static IReadOnlyList<string> SpostaSopra(IReadOnlyList<string> righe, IReadOnlySet<int>? soloLeRighe = null)
    {
        ArgumentNullException.ThrowIfNull(righe);
        var fuori = new List<string>(righe.Count);
        for (int i = 0; i < righe.Count; i++)
        {
            if ((soloLeRighe is null || soloLeRighe.Contains(i + 1)) && Separa(righe[i]) is { } separata)
            {
                fuori.Add(separata.Commento);
                fuori.Add(separata.Dati);
            }
            else
            {
                fuori.Add(righe[i]);
            }
        }

        return fuori;
    }

    /// <summary>I numeri (da 1) delle righe che hanno un commento in coda.</summary>
    public static IReadOnlyList<int> Righe(IReadOnlyList<string> righe)
    {
        ArgumentNullException.ThrowIfNull(righe);
        return [.. Enumerable.Range(1, righe.Count).Where(n => Dove(righe[n - 1]) is not null)];
    }
}
