namespace Vipi.SectorLab.Core.Modifiche;

/// <summary>Che cosa è successo a una riga.</summary>
public enum SegnoDelDiff
{
    Uguale,
    Tolta,
    Aggiunta,
}

/// <summary>Una riga del diff, col suo numero nel file di prima (tolte e uguali) o in quello di dopo (aggiunte).</summary>
public sealed record RigaDelDiff(SegnoDelDiff Segno, int Numero, string Testo);

/// <summary>Un pezzo di diff: le righe cambiate con tre righe di contesto intorno.</summary>
public sealed record PezzoDiDiff(int DaPrima, int DaDopo, IReadOnlyList<RigaDelDiff> Righe)
{
    public int Tolte => Righe.Count(r => r.Segno == SegnoDelDiff.Tolta);

    public int Aggiunte => Righe.Count(r => r.Segno == SegnoDelDiff.Aggiunta);
}

/// <summary>
/// Il diff unificato fra le righe di prima e quelle di dopo (carta F3 §2.2 passo 6, slice 6).
/// <para>È l'algoritmo di Myers, non un confronto riga per riga: quel che conta qui è che <b>una modifica di un
/// campo esca come una riga tolta e una aggiunta</b>, e non come «da qui in giù è tutto diverso». Myers costa
/// O((N+M)·D) dove D è quanto i due testi differiscono: nei nostri file N è fino a 13 560 righe ma D è qualche
/// riga, quindi è lavoro da niente.</para>
/// <para>Oltre un tetto di differenze ci si ferma e si dice che il file è cambiato in blocco: un diff di migliaia
/// di righe non lo legge nessuno, e vorrebbe dire che è successo qualcosa di diverso da una modifica.</para>
/// </summary>
public static class Diff
{
    /// <summary>Le righe di contesto attorno a ogni pezzo, come in un diff unificato.</summary>
    public const int Contesto = 3;

    /// <summary>Oltre questo numero di righe diverse non si fa più il confronto fine.</summary>
    public const int TettoDelleDifferenze = 2_000;

    /// <summary>Vero se il file è cambiato in blocco: il confronto fine si è arreso (vedi il tetto).</summary>
    public sealed record Esito(IReadOnlyList<PezzoDiDiff> Pezzi, bool InBlocco)
    {
        public int Tolte => Pezzi.Sum(p => p.Tolte);

        public int Aggiunte => Pezzi.Sum(p => p.Aggiunte);
    }

    public static Esito Fra(IReadOnlyList<string> prima, IReadOnlyList<string> dopo)
    {
        ArgumentNullException.ThrowIfNull(prima);
        ArgumentNullException.ThrowIfNull(dopo);

        var righe = Confronta(prima, dopo, out bool inBlocco);
        return new Esito(Pezzi(righe), inBlocco);
    }

    /// <summary>
    /// Dove è finita ogni riga di prima: per la riga numero N (da 1) di <paramref name="prima"/>, il suo numero in
    /// <paramref name="dopo"/>, o null se è stata tolta o cambiata. Serve a un problema del validatore, che dà il numero
    /// della riga SUL DISCO, quando il file di adesso ha record aggiunti o tolti sopra di lei (committente, 24 settembre:
    /// clic sulla riga 30, si apriva la 29).
    /// </summary>
    public static int?[] Allinea(IReadOnlyList<string> prima, IReadOnlyList<string> dopo)
    {
        ArgumentNullException.ThrowIfNull(prima);
        ArgumentNullException.ThrowIfNull(dopo);

        var dove = new int?[prima.Count + 1];
        int numeroDopo = 0;
        foreach (var riga in Confronta(prima, dopo, out _))
        {
            switch (riga.Segno)
            {
                case SegnoDelDiff.Uguale:
                    dove[riga.Numero] = ++numeroDopo;
                    break;
                case SegnoDelDiff.Aggiunta:
                    numeroDopo++;
                    break;
            }
        }

        return dove;
    }

    /// <summary>Tutte le righe, nell'ordine, con il loro segno: è la base da cui si ritagliano i pezzi.</summary>
    private static List<RigaDelDiff> Confronta(IReadOnlyList<string> prima, IReadOnlyList<string> dopo, out bool inBlocco)
    {
        inBlocco = false;
        var righe = new List<RigaDelDiff>(prima.Count + dopo.Count);

        // Testa e coda uguali si tolgono subito: è il caso vero (un campo cambiato in mezzo a un file intatto).
        int testa = 0;
        while (testa < prima.Count && testa < dopo.Count && prima[testa] == dopo[testa])
        {
            righe.Add(new RigaDelDiff(SegnoDelDiff.Uguale, testa + 1, prima[testa]));
            testa++;
        }

        int coda = 0;
        while (coda < prima.Count - testa && coda < dopo.Count - testa
               && prima[prima.Count - 1 - coda] == dopo[dopo.Count - 1 - coda])
        {
            coda++;
        }

        var mezzoPrima = prima.Skip(testa).Take(prima.Count - testa - coda).ToList();
        var mezzoDopo = dopo.Skip(testa).Take(dopo.Count - testa - coda).ToList();

        var camminata = Myers(mezzoPrima, mezzoDopo);
        if (camminata is null)
        {
            inBlocco = true;
            foreach (var (riga, i) in mezzoPrima.Select((r, i) => (r, i)))
                righe.Add(new RigaDelDiff(SegnoDelDiff.Tolta, testa + i + 1, riga));
            foreach (var (riga, i) in mezzoDopo.Select((r, i) => (r, i)))
                righe.Add(new RigaDelDiff(SegnoDelDiff.Aggiunta, testa + i + 1, riga));
        }
        else
        {
            int daPrima = testa;
            int daDopo = testa;
            foreach (var passo in camminata)
            {
                switch (passo)
                {
                    case SegnoDelDiff.Uguale:
                        righe.Add(new RigaDelDiff(SegnoDelDiff.Uguale, ++daPrima, prima[daPrima - 1]));
                        daDopo++;
                        break;
                    case SegnoDelDiff.Tolta:
                        righe.Add(new RigaDelDiff(SegnoDelDiff.Tolta, ++daPrima, prima[daPrima - 1]));
                        break;
                    case SegnoDelDiff.Aggiunta:
                        righe.Add(new RigaDelDiff(SegnoDelDiff.Aggiunta, ++daDopo, dopo[daDopo - 1]));
                        break;
                }
            }
        }

        for (int i = prima.Count - coda; i < prima.Count; i++)
            righe.Add(new RigaDelDiff(SegnoDelDiff.Uguale, i + 1, prima[i]));

        return righe;
    }

    /// <summary>
    /// Myers: la strada più corta fra i due testi, come sequenza di passi. Null se le differenze passano il tetto.
    /// </summary>
    private static List<SegnoDelDiff>? Myers(IReadOnlyList<string> prima, IReadOnlyList<string> dopo)
    {
        int n = prima.Count;
        int m = dopo.Count;
        int massimo = Math.Min(n + m, TettoDelleDifferenze);

        var v = new int[2 * massimo + 2];
        var tracce = new List<int[]>(massimo + 1);
        int scarto = massimo;

        for (int d = 0; d <= massimo; d++)
        {
            tracce.Add((int[])v.Clone());
            for (int k = -d; k <= d; k += 2)
            {
                int x = k == -d || (k != d && v[scarto + k - 1] < v[scarto + k + 1])
                    ? v[scarto + k + 1]
                    : v[scarto + k - 1] + 1;
                int y = x - k;

                while (x < n && y < m && prima[x] == dopo[y])
                {
                    x++;
                    y++;
                }

                v[scarto + k] = x;
                if (x >= n && y >= m)
                    return Ricostruisci(tracce, prima, dopo, d, scarto);
            }
        }

        return null;
    }

    private static List<SegnoDelDiff> Ricostruisci(
        List<int[]> tracce, IReadOnlyList<string> prima, IReadOnlyList<string> dopo, int d, int scarto)
    {
        var passi = new List<SegnoDelDiff>();
        int x = prima.Count;
        int y = dopo.Count;

        for (int passo = d; passo > 0; passo--)
        {
            var v = tracce[passo];
            int k = x - y;
            bool dallAlto = k == -passo || (k != passo && v[scarto + k - 1] < v[scarto + k + 1]);
            int kPrima = dallAlto ? k + 1 : k - 1;
            int xPrima = v[scarto + kPrima];
            int yPrima = xPrima - kPrima;

            while (x > xPrima && y > yPrima)
            {
                passi.Add(SegnoDelDiff.Uguale);
                x--;
                y--;
            }

            if (dallAlto)
            {
                passi.Add(SegnoDelDiff.Aggiunta);
                y--;
            }
            else
            {
                passi.Add(SegnoDelDiff.Tolta);
                x--;
            }
        }

        while (x > 0 && y > 0)
        {
            passi.Add(SegnoDelDiff.Uguale);
            x--;
            y--;
        }

        passi.Reverse();
        return passi;
    }

    /// <summary>Ritaglia i pezzi: le righe cambiate con al più tre righe di contesto attaccate.</summary>
    private static IReadOnlyList<PezzoDiDiff> Pezzi(List<RigaDelDiff> righe)
    {
        var pezzi = new List<PezzoDiDiff>();
        int i = 0;

        while (i < righe.Count)
        {
            if (righe[i].Segno == SegnoDelDiff.Uguale)
            {
                i++;
                continue;
            }

            int inizio = i;
            while (inizio > 0 && righe[inizio - 1].Segno == SegnoDelDiff.Uguale && i - inizio < Contesto)
                inizio--;

            int fine = i;
            int uguali = 0;
            while (fine < righe.Count && (righe[fine].Segno != SegnoDelDiff.Uguale || uguali < Contesto))
            {
                uguali = righe[fine].Segno == SegnoDelDiff.Uguale ? uguali + 1 : 0;
                fine++;
            }

            var dentro = righe.GetRange(inizio, fine - inizio);
            pezzi.Add(new PezzoDiDiff(
                dentro.First(r => r.Segno != SegnoDelDiff.Aggiunta).Numero,
                dentro.First(r => r.Segno != SegnoDelDiff.Tolta).Numero,
                dentro));
            i = fine;
        }

        return pezzi;
    }
}
