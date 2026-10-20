namespace Vipi.SectorLab.Core.Testo;

/// <summary>
/// Dal testo scritto nella pagina Testo (riordino, D12) alle righe da cambiare nel file com'è adesso, nella forma di
/// <see cref="Modifiche.ModificheInSospeso.CambiaRighe"/>: per numero di riga (da 1) le righe che vanno al suo posto. Così
/// quel che l'AOD scrive è una modifica in sospeso come le altre — diff, Annulla, problemi nuovi, «Salva» —, e le righe
/// che non ha toccato restano quelle (i tag <c>//@</c> compresi, che lì non si possono toccare senza sbloccarli, D13).
/// </summary>
public static class RigheCambiate
{
    /// <summary>
    /// Oltre questo prodotto (righe cambiate di prima × di dopo, tolte le uguali in testa e in coda) il confronto riga per
    /// riga costerebbe troppo: il pezzo in mezzo si sostituisce in blocco. Un file del sector ne ha al più qualche decina di
    /// migliaia (<c>italy.prohibit</c> 10 143 linee, misura R8), ma chi scrive tocca di solito poche righe vicine.
    /// </summary>
    public const long TettoDelConfronto = 4_000_000;

    /// <summary>
    /// Le sostituzioni che portano <paramref name="prima"/> a <paramref name="dopo"/>; vuote se sono uguali. Le righe
    /// uguali restano dove sono; una riga nuova fra due uguali si attacca a quella prima (in coda) o, se quella è un tag
    /// <c>//@</c> (<paramref name="eUnTag"/>), a quella dopo (in testa): così un tag che non cambia non entra nelle sostituzioni.
    /// </summary>
    public static Dictionary<int, IReadOnlyList<string>> Di(IReadOnlyList<string> prima, IReadOnlyList<string> dopo, Func<string, bool> eUnTag)
    {
        ArgumentNullException.ThrowIfNull(prima);
        ArgumentNullException.ThrowIfNull(dopo);
        ArgumentNullException.ThrowIfNull(eUnTag);

        // Le uguali in testa e in coda non si guardano.
        int testa = 0;
        while (testa < prima.Count && testa < dopo.Count && prima[testa] == dopo[testa])
            testa++;
        int coda = 0;
        while (coda < prima.Count - testa && coda < dopo.Count - testa && prima[prima.Count - 1 - coda] == dopo[dopo.Count - 1 - coda])
            coda++;
        int n = prima.Count - testa - coda, m = dopo.Count - testa - coda;
        if (n == 0 && m == 0)
            return [];

        // Per ogni riga di prima (indice da 0): le righe nuove davanti, se resta, e quelle dopo.
        var davanti = new Dictionary<int, List<string>>();
        var dietro = new Dictionary<int, List<string>>();
        var tolte = new HashSet<int>();
        var sostituite = new Dictionary<int, List<string>>();

        // Le coppie di righe uguali in mezzo (la più lunga sequenza comune), o nessuna se il pezzo è troppo grande.
        var uguali = (long)n * m <= TettoDelConfronto ? SequenzaComune(prima, testa, n, dopo, testa, m) : [];

        int i = testa, j = testa;
        foreach (var (pi, dj) in uguali.Append((testa + n, testa + m)))
        {
            // Fra la coppia di prima e questa: le righe di prima da pi in giù sono cambiate (o tolte), quelle di dopo nuove.
            var nuove = Enumerable.Range(j, dj - j).Select(k => dopo[k]).ToList();
            if (pi > i)
            {
                // La prima riga del pezzo prende tutte le nuove; le altre si tolgono.
                sostituite[i] = nuove;
                for (int k = i + 1; k < pi; k++)
                    tolte.Add(k);
            }
            else if (nuove.Count > 0)
            {
                // Righe solo aggiunte, prima della riga pi di prima.
                if (i - 1 >= 0 && !eUnTag(prima[i - 1]))
                    Lista(dietro, i - 1).AddRange(nuove);
                else if (pi < prima.Count && !eUnTag(prima[pi]))
                    Lista(davanti, pi).AddRange(nuove);
                else if (i - 1 >= 0)
                    Lista(dietro, i - 1).AddRange(nuove);   // fra due tag: la rifiuterà CambiaRighe, se i tag sono bloccati
                else
                    Lista(davanti, pi).AddRange(nuove);
            }
            i = pi + 1;
            j = dj + 1;
        }

        var sostituzioni = new Dictionary<int, IReadOnlyList<string>>();
        foreach (int k in davanti.Keys.Concat(dietro.Keys).Concat(tolte).Concat(sostituite.Keys).Distinct().Order())
        {
            var righe = new List<string>(davanti.GetValueOrDefault(k) ?? []);
            if (sostituite.TryGetValue(k, out var alPosto))
                righe.AddRange(alPosto);
            else if (!tolte.Contains(k) && k < prima.Count)   // in un file vuoto non c'è una riga a cui attaccarsi
                righe.Add(prima[k]);
            righe.AddRange(dietro.GetValueOrDefault(k) ?? []);
            sostituzioni[k + 1] = righe;
        }
        return sostituzioni;
    }

    private static List<string> Lista(Dictionary<int, List<string>> d, int k) => d.TryGetValue(k, out var l) ? l : d[k] = [];

    /// <summary>La più lunga sequenza di righe uguali (indici in <paramref name="a"/> e in <paramref name="b"/>), in ordine.</summary>
    private static List<(int A, int B)> SequenzaComune(IReadOnlyList<string> a, int da, int n, IReadOnlyList<string> b, int db, int m)
    {
        var lunghezza = new int[n + 1, m + 1];
        for (int x = n - 1; x >= 0; x--)
            for (int y = m - 1; y >= 0; y--)
                lunghezza[x, y] = a[da + x] == b[db + y] ? lunghezza[x + 1, y + 1] + 1 : Math.Max(lunghezza[x + 1, y], lunghezza[x, y + 1]);
        var coppie = new List<(int, int)>();
        for (int x = 0, y = 0; x < n && y < m;)
        {
            if (a[da + x] == b[db + y])
            {
                coppie.Add((da + x, db + y));
                x++;
                y++;
            }
            else if (lunghezza[x + 1, y] >= lunghezza[x, y + 1])
                x++;
            else
                y++;
        }
        return coppie;
    }
}
