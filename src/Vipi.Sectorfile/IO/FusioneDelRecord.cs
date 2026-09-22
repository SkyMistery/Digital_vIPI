using System.Globalization;
using System.Text.RegularExpressions;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// «Riga come campi» (carta F2 §9.5, slice 3): come si riscrive un record toccato senza perdere niente di ciò
/// che il modello non conosce.
/// </summary>
/// <remarks>
/// <para>Tre versioni dello stesso record: le righe <b>grezze</b> del file, la <b>base</b> (ciò che lo scrittore
/// produceva dal record appena letto, fotografata all'apertura) e le righe <b>nuove</b> (ciò che lo scrittore
/// produce dal record modificato). Base e nuove escono dallo stesso scrittore, quindi si confrontano alla lettera:
/// la loro differenza è esattamente la modifica. Le grezze sono la verità del file, e restano dove la modifica
/// non arriva.</para>
/// <para>Perché serve: gli scrittori di A ricostruiscono la riga dal modello. Toccando tutti i record dell'albero
/// (22 settembre 2026) cambiavano 60 750 righe su 257 435 — campi in coda persi (<c>54Y</c>, <c>HLD-ABBOZ</c>),
/// commenti riattivati, <c>T;DUMMY</c> spariti, spazi e zeri normalizzati. Qui niente di tutto questo può
/// succedere: un campo che base e nuove hanno uguale si riscrive coi byte grezzi, e una riga che lo scrittore
/// non produce (un commento, un terminatore) non viene mai toccata.</para>
/// <list type="number">
///   <item>Si allineano le grezze alla base, riga per riga, con un'uguaglianza <i>di significato</i>
///   (<see cref="Equivalenti"/>): spazi, zeri davanti, frazioni dei secondi, campi in coda.</item>
///   <item>Si confrontano base e nuove alla lettera: righe uguali, tolte, aggiunte, cambiate.</item>
///   <item>Una riga uguale esce grezza; una cambiata esce grezza <b>tranne</b> i campi cambiati; una aggiunta esce
///   nuova, nella forma dei punti del record; una tolta sparisce. Le grezze che la base non ha restano al loro
///   posto, attaccate alla riga che le segue.</item>
/// </list>
/// </remarks>
internal static class FusioneDelRecord
{
    // Oltre questo prodotto di righe il confronto completo costerebbe troppo: si confrontano solo testa e coda
    // comuni e il resto si prende come cambiato in blocco. Nessun record dell'albero ci arriva vicino.
    private const long TettoDelConfronto = 4_000_000;

    private static readonly Regex PuntatoScritto = new(@"^([NSEW])(\d{3})\.(\d{2})\.(\d{2})\.(\d{3})$", RegexOptions.CultureInvariant);

    internal static List<string> Unisci(
        IReadOnlyList<string> grezze, IReadOnlyList<string> base_, IReadOnlyList<string> nuove, FormaDelPunto.Forma forma)
    {
        if (base_.SequenceEqual(nuove, StringComparer.Ordinal))
        {
            return grezze.ToList();
        }

        int[] grezzaDellaBase = Allinea(grezze, base_);
        var uscita = new List<string>(Math.Max(grezze.Count, nuove.Count));
        int prossimaGrezza = 0;

        void CopiaFinoA(int indiceGrezza)
        {
            while (prossimaGrezza < indiceGrezza)
            {
                uscita.Add(grezze[prossimaGrezza++]);
            }
        }

        foreach (var (tipo, b, n) in Confronta(base_, nuove))
        {
            int g = b >= 0 ? grezzaDellaBase[b] : -1;
            switch (tipo)
            {
                case Passo.Uguale when g >= 0:
                    CopiaFinoA(g);
                    uscita.Add(grezze[g]);
                    prossimaGrezza = g + 1;
                    break;

                case Passo.Uguale:
                    // Lo scrittore produce una riga che il file non ha alla lettera (per esempio un commento che A
                    // riscrive attivo): invariata, vale il file, e il file è già nelle grezze.
                    break;

                case Passo.Tolta when g >= 0:
                    CopiaFinoA(g);
                    prossimaGrezza = g + 1;
                    break;

                case Passo.Cambiata when g >= 0:
                    CopiaFinoA(g);
                    uscita.Add(UnisciCampi(grezze[g], base_[b], nuove[n], forma));
                    prossimaGrezza = g + 1;
                    break;

                case Passo.Cambiata:
                case Passo.Aggiunta:
                    uscita.Add(FormaDelPunto.In(new[] { nuove[n] }, forma).Single());
                    break;
            }
        }

        CopiaFinoA(grezze.Count);
        return uscita;
    }

    /// <summary>
    /// Una riga cambiata: campo per campo, i byte grezzi dove base e nuova coincidono, la nuova dove differiscono.
    /// I campi che il modello non conosce (oltre la fine di base e nuova) restano.
    /// </summary>
    internal static string UnisciCampi(string grezza, string base_, string nuova, FormaDelPunto.Forma forma)
    {
        // Un record disattivato (la grezza commentata, la base no): si unisce il contenuto e il `//` resta.
        int inizio = grezza.Length - grezza.TrimStart().Length;
        if (grezza.AsSpan(inizio).StartsWith("//", StringComparison.Ordinal)
            && !base_.TrimStart().StartsWith("//", StringComparison.Ordinal)
            && !nuova.TrimStart().StartsWith("//", StringComparison.Ordinal))
        {
            return grezza[..(inizio + 2)] + UnisciCampi(grezza[(inizio + 2)..], base_, nuova, forma);
        }

        string[] g = grezza.Split(';');
        string[] b = base_.Split(';');
        string[] n = nuova.Split(';');

        var campi = new List<(string Testo, bool Nuovo)>();
        int quanti = Math.Max(g.Length, Math.Max(b.Length, n.Length));
        for (int j = 0; j < quanti; j++)
        {
            bool inG = j < g.Length, inB = j < b.Length, inN = j < n.Length;
            if (inB && inN)
            {
                if (string.Equals(b[j], n[j], StringComparison.Ordinal))
                {
                    if (inG)
                    {
                        campi.Add((g[j], false));
                    }
                }
                else
                {
                    campi.Add((n[j], true));
                }
            }
            else if (inN)
            {
                campi.Add((n[j], true));
            }
            else if (!inB && inG)
            {
                campi.Add((g[j], false));
            }

            // inB e non inN: il modello ha tolto il campo, e si toglie.
        }

        return string.Join(";", NellaForma(campi, forma));
    }

    private static IEnumerable<string> NellaForma(List<(string Testo, bool Nuovo)> campi, FormaDelPunto.Forma forma)
    {
        for (int j = 0; j < campi.Count; j++)
        {
            var (testo, nuovo) = campi[j];
            if (!nuovo)
            {
                yield return testo;
                continue;
            }

            if (forma == FormaDelPunto.Forma.Compatta)
            {
                yield return PuntatoScritto.Replace(testo, "$1$2$3$4$5");
            }
            else if (forma == FormaDelPunto.Forma.Decimale && PuntatoScritto.IsMatch(testo))
            {
                // Campo per campo: se è cambiata la sola latitudine, la longitudine resta coi byte del file.
                yield return ValoreDms(testo)!.Value.ToString("F8", CultureInfo.InvariantCulture);
            }
            else
            {
                yield return testo;
            }
        }
    }

    // ── 1. grezze ↔ base ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>Per ogni riga della base, l'indice della grezza che le corrisponde (−1 se nessuna).</summary>
    /// <remarks>
    /// Due passi. Il primo allinea con <see cref="Equivalenti"/> e basta. Il secondo guarda solo i buchi rimasti
    /// — righe della base senza grezza, fra due righe già allineate — e lì lascia che una riga della base
    /// trovi la sua versione <b>commentata</b>: il lettore di A legge <c>//N037…;PIER;</c> come un record
    /// disattivato, e il suo scrittore lo riscrive senza <c>//</c>. Farlo al primo passo sarebbe pericoloso: un
    /// AOD che tiene la versione vecchia commentata sopra la nuova vedrebbe la modifica finire sul commento.
    /// </remarks>
    private static int[] Allinea(IReadOnlyList<string> grezze, IReadOnlyList<string> base_)
    {
        var mappa = Enumerable.Repeat(-1, base_.Count).ToArray();
        foreach (var (tipo, b, g) in Lcs(base_, grezze, (rigaBase, rigaGrezza) => Equivalenti(rigaGrezza, rigaBase)))
        {
            if (tipo == Passo.Uguale)
            {
                mappa[b] = g;
            }
        }

        int bInizio = 0;
        while (bInizio < base_.Count)
        {
            if (mappa[bInizio] >= 0)
            {
                bInizio++;
                continue;
            }

            int bFine = bInizio;
            while (bFine < base_.Count && mappa[bFine] < 0)
            {
                bFine++;
            }

            int gDa = bInizio > 0 ? mappa[bInizio - 1] + 1 : 0;
            int gA = bFine < base_.Count ? mappa[bFine] : grezze.Count;
            var basiDelBuco = Enumerable.Range(bInizio, bFine - bInizio).ToList();
            var grezzeDelBuco = Enumerable.Range(gDa, Math.Max(0, gA - gDa)).ToList();
            foreach (var (tipo, i, k) in Lcs(
                basiDelBuco.Select(x => base_[x]).ToList(),
                grezzeDelBuco.Select(x => grezze[x]).ToList(),
                (rigaBase, rigaGrezza) => Disattivata(rigaGrezza, rigaBase) || StessiPunti(rigaGrezza, rigaBase)))
            {
                if (tipo == Passo.Uguale)
                {
                    mappa[basiDelBuco[i]] = grezzeDelBuco[k];
                }
            }

            bInizio = bFine;
        }

        return mappa;
    }

    /// <summary>
    /// Stesso tipo di riga (primo campo), stesso numero di campi e <b>gli stessi punti</b>: è la stessa riga, anche
    /// se un altro campo differisce perché lo scrittore lo deduce invece di ricordarlo (l'etichetta dei
    /// <c>.mva</c> di rotta: <c>L;50;…</c> nella base, <c>L;LIMM;…</c> nel file). Solo al secondo passo, nei buchi.
    /// </summary>
    private static bool StessiPunti(string grezza, string base_)
    {
        if (grezza.TrimStart().StartsWith("//", StringComparison.Ordinal) || base_.TrimStart().StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        string[] g = grezza.Split(';');
        string[] b = base_.Split(';');

        // Il `;` finale non è un campo: `T;3500SE;N…;E…; //3500 SE` (lipe.mva, un commento in coda e niente `;`)
        // ha gli stessi cinque campi della sua base `T; //3500 SE;N…;E…; //3500 SE;`.
        static int Campi(string[] c) => c.Length > 1 && c[^1].Length == 0 ? c.Length - 1 : c.Length;
        if (Campi(g) != Campi(b) || !string.Equals(g[0].Trim(), b[0].Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        int punti = 0;
        for (int j = 0; j < b.Length; j++)
        {
            if (ValoreDms(b[j].Trim()) is not null)
            {
                if (!CampiEquivalenti(g[j], b[j]))
                {
                    return false;
                }

                punti++;
            }
        }

        return punti > 0;
    }

    /// <summary>La grezza è la versione commentata della riga della base (un record disattivato).</summary>
    private static bool Disattivata(string grezza, string base_)
    {
        string g = grezza.TrimStart();
        return g.StartsWith("//", StringComparison.Ordinal)
            && !base_.TrimStart().StartsWith("//", StringComparison.Ordinal)
            && Equivalenti(g[2..], base_);
    }

    /// <summary>
    /// Una riga grezza e una della base dicono la stessa cosa? Campo per campo, a meno di spazi, zeri davanti,
    /// scrittura della coordinata; i campi in più della grezza non contano (sono quelli che il modello perde),
    /// quelli in più della base devono essere vuoti, e un campo vuoto nella base vale come sconosciuto.
    /// </summary>
    internal static bool Equivalenti(string grezza, string base_)
    {
        bool commentoG = grezza.TrimStart().StartsWith("//", StringComparison.Ordinal);
        bool commentoB = base_.TrimStart().StartsWith("//", StringComparison.Ordinal);
        if (commentoG != commentoB)
        {
            return false;
        }

        // Due commenti (un record disattivato, `//LIBB;00;00;…` contro `//LIBB;0;0;…`) si confrontano come le
        // righe attive, tolto il `//`.
        if (commentoG)
        {
            grezza = grezza.TrimStart()[2..];
            base_ = base_.TrimStart()[2..];
        }

        string[] g = grezza.Split(';');
        string[] b = base_.Split(';');

        // Il `;` che chiude la riga non è un campo: senza toglierlo, il vuoto dopo l'ultimo `;` della base
        // dovrebbe coincidere col campo in più della grezza (`…;0;2;` contro `…;0;2;54Y;`).
        int campiBase = b.Length > 1 && b[^1].Length == 0 ? b.Length - 1 : b.Length;
        for (int j = 0; j < campiBase; j++)
        {
            if (j >= g.Length)
            {
                if (b[j].Trim().Length > 0)
                {
                    return false;
                }

                continue;
            }

            // Un campo che la base lascia vuoto è un campo che il modello non conosce (lo scrittore dei .mva
            // scrive `T;;N…` dove il file ha `T;LIPP;N…`): come i campi in coda, non conta.
            if (b[j].Trim().Length > 0 && !CampiEquivalenti(g[j], b[j]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CampiEquivalenti(string a, string b)
    {
        a = a.Trim();
        b = b.Trim();
        if (string.Equals(a, b, StringComparison.Ordinal))
        {
            return true;
        }

        bool numeroA = decimal.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal da);
        bool numeroB = decimal.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal db);
        if (numeroA && numeroB)
        {
            return da == db;
        }

        // Una coordinata, in qualunque delle tre forme: la grezza può essere decimale o compatta, la base è
        // sempre puntata. Si confronta il valore, entro ciò che il puntato a millesimi di secondo sa dire.
        double? va = numeroA ? (double)da : ValoreDms(a);
        double? vb = numeroB ? (double)db : ValoreDms(b);
        return va is not null && vb is not null && (ValoreDms(a) is not null || ValoreDms(b) is not null)
            && Math.Abs(va.Value - vb.Value) < 1e-6;
    }

    /// <summary>Il valore con segno di un token DMS (S e W negativi), o null se non è un DMS.</summary>
    private static double? ValoreDms(string token)
    {
        if (token.Length < 2 || !"NSEWnsew".Contains(token[0]) || !char.IsAsciiDigit(token[1]))
        {
            return null;
        }

        try
        {
            var punto = CoordinateConverter.Parse(token);
            return char.ToUpperInvariant(token[0]) is 'N' or 'S' ? punto.LatitudeDeg : punto.LongitudeDeg;
        }
        catch (CoordinateParseException)
        {
            return null;
        }
    }

    // ── 2. base ↔ nuove ──────────────────────────────────────────────────────────────────────────────────

    internal enum Passo { Uguale, Tolta, Aggiunta, Cambiata }

    /// <summary>
    /// Il confronto alla lettera, con le tolte e le aggiunte vicine accoppiate in «cambiate»: una riga di cui è
    /// cambiato un campo è una riga cambiata, non una tolta più una aggiunta.
    /// </summary>
    private static List<(Passo Tipo, int Base, int Nuova)> Confronta(IReadOnlyList<string> base_, IReadOnlyList<string> nuove)
    {
        var passi = Lcs(base_, nuove, (x, y) => string.Equals(x, y, StringComparison.Ordinal));
        var uscita = new List<(Passo, int, int)>(passi.Count);
        var tolte = new List<int>();
        var aggiunte = new List<int>();

        void Accoppia()
        {
            int coppie = Math.Min(tolte.Count, aggiunte.Count);
            for (int k = 0; k < coppie; k++)
            {
                uscita.Add((Passo.Cambiata, tolte[k], aggiunte[k]));
            }

            for (int k = coppie; k < tolte.Count; k++)
            {
                uscita.Add((Passo.Tolta, tolte[k], -1));
            }

            for (int k = coppie; k < aggiunte.Count; k++)
            {
                uscita.Add((Passo.Aggiunta, -1, aggiunte[k]));
            }

            tolte.Clear();
            aggiunte.Clear();
        }

        foreach (var (tipo, b, n) in passi)
        {
            switch (tipo)
            {
                case Passo.Tolta:
                    tolte.Add(b);
                    break;
                case Passo.Aggiunta:
                    aggiunte.Add(n);
                    break;
                default:
                    Accoppia();
                    uscita.Add((Passo.Uguale, b, n));
                    break;
            }
        }

        Accoppia();
        return uscita;
    }

    /// <summary>
    /// La sottosequenza comune più lunga fra <paramref name="a"/> e <paramref name="b"/>, come elenco di passi
    /// (Uguale i,j · Tolta i · Aggiunta j). Testa e coda comuni si tolgono prima: nel caso tipico (un punto
    /// spostato) il confronto vero si fa su una riga.
    /// </summary>
    private static List<(Passo Tipo, int A, int B)> Lcs(IReadOnlyList<string> a, IReadOnlyList<string> b, Func<string, string, bool> uguali)
    {
        var passi = new List<(Passo, int, int)>(Math.Max(a.Count, b.Count));

        int testa = 0;
        while (testa < a.Count && testa < b.Count && uguali(a[testa], b[testa]))
        {
            testa++;
        }

        int coda = 0;
        while (coda < a.Count - testa && coda < b.Count - testa && uguali(a[a.Count - 1 - coda], b[b.Count - 1 - coda]))
        {
            coda++;
        }

        for (int k = 0; k < testa; k++)
        {
            passi.Add((Passo.Uguale, k, k));
        }

        int da = testa, fa = a.Count - coda, db = testa, fb = b.Count - coda;
        int n = fa - da, m = fb - db;
        if ((long)n * m > TettoDelConfronto)
        {
            for (int i = da; i < fa; i++)
            {
                passi.Add((Passo.Tolta, i, -1));
            }

            for (int j = db; j < fb; j++)
            {
                passi.Add((Passo.Aggiunta, -1, j));
            }
        }
        else
        {
            var lunghezza = new int[n + 1, m + 1];
            for (int i = n - 1; i >= 0; i--)
            {
                for (int j = m - 1; j >= 0; j--)
                {
                    lunghezza[i, j] = uguali(a[da + i], b[db + j])
                        ? lunghezza[i + 1, j + 1] + 1
                        : Math.Max(lunghezza[i + 1, j], lunghezza[i, j + 1]);
                }
            }

            int x = 0, y = 0;
            while (x < n && y < m)
            {
                if (uguali(a[da + x], b[db + y]) && lunghezza[x, y] == lunghezza[x + 1, y + 1] + 1)
                {
                    passi.Add((Passo.Uguale, da + x, db + y));
                    x++;
                    y++;
                }
                else if (lunghezza[x + 1, y] >= lunghezza[x, y + 1])
                {
                    passi.Add((Passo.Tolta, da + x, -1));
                    x++;
                }
                else
                {
                    passi.Add((Passo.Aggiunta, -1, db + y));
                    y++;
                }
            }

            for (; x < n; x++)
            {
                passi.Add((Passo.Tolta, da + x, -1));
            }

            for (; y < m; y++)
            {
                passi.Add((Passo.Aggiunta, -1, db + y));
            }
        }

        for (int k = coda; k > 0; k--)
        {
            passi.Add((Passo.Uguale, a.Count - k, b.Count - k));
        }

        return passi;
    }
}
