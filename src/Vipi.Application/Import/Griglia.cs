using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vipi.Application.Import;

/// <summary>Da dove viene la griglia: serve a dirlo a chi importa, non a decidere qualcosa.</summary>
public enum FormaGriglia
{
    /// <summary>Niente da leggere.</summary>
    Vuota,

    /// <summary>Colonne separate da tabulazione: quel che esce da un foglio di calcolo.</summary>
    Tabulazioni,

    /// <summary>Colonne separate da <c>;</c> o <c>,</c>, con le virgolette RFC 4180.</summary>
    Csv,

    /// <summary>Tabella Markdown, <c>| a | b |</c>.</summary>
    Markdown,

    /// <summary>Tabella HTML: quel che Excel, Word e il browser mettono davvero in clipboard.</summary>
    Html,

    /// <summary>Colonne tagliate a posizioni fisse, scelte a mano.</summary>
    LarghezzaFissa,

    /// <summary>Un foglio di un file <c>.xlsx</c>.</summary>
    Xlsx,

    /// <summary>Nessun separatore riconosciuto: ogni riga e' una cella sola, e chi importa decide come
    /// spezzarla (le ancore della sua specifica, oppure i tagli a larghezza fissa).</summary>
    RigaIntera,

    /// <summary>
    /// La stessa tabella presa da un ALTRO documento: le celle sono gia' quelle, non c'e' niente da leggere.
    ///
    /// <para>⚠️ Prima queste griglie si dichiaravano <see cref="RigaIntera"/>, che vuol dire l'opposto —
    /// «una cella sola per riga, spezzala tu». Il pannello lo scrive in una pastiglia, e diceva «righe
    /// intere» su una tabella che di celle ne aveva quattro. La forma serve a <b>dirlo</b> a chi importa, e
    /// dirlo sbagliato e' peggio che non dirlo (4 settembre 2026).</para>
    /// </summary>
    AltroDocumento,
}

/// <summary>
/// Un testo o un file letti come <b>griglia di celle</b>, e nient'altro: qui non si sa che tabella sia, non
/// si risolve niente su un catalogo e non si scrive da nessuna parte.
///
/// <para>
/// ⚠️ Le righe possono essere <b>irregolari</b> — lunghezze diverse — e restano tali. Portarle tutte alla
/// stessa lunghezza e' un lavoro della specifica che le riceve, perche' solo lei sa quante colonne vuole:
/// una riga corta pareggiata qui nasconderebbe che la lettura ha perso un pezzo.
/// </para>
/// <para>
/// ⚠️ <b>La virgola e' l'ultimo separatore che si prova.</b> Dentro una cella separa gia' altro (i punti di
/// un accordo, «EKMUR, PISIP»), e sceglierla quando c'e' un'alternativa rende le due cose indistinguibili.
/// E' la lezione gia' pagata in <c>ClausePaste</c>.
/// </para>
/// </summary>
public sealed record Griglia(IReadOnlyList<IReadOnlyList<string>> Righe, FormaGriglia Forma)
{
    /// <summary>La griglia senza niente dentro.</summary>
    public static readonly Griglia Vuota = new(Array.Empty<IReadOnlyList<string>>(), FormaGriglia.Vuota);

    /// <summary>Quante celle ha la riga piu' lunga.</summary>
    public int Colonne => Righe.Count == 0 ? 0 : Righe.Max(r => r.Count);

    public bool Piena => Righe.Count > 0;

    /// <summary>
    /// Legge un testo incollato, riconoscendo da solo la forma: HTML, Markdown, tabulazioni, CSV; e quando
    /// non riconosce niente restituisce una riga per riga (<see cref="FormaGriglia.RigaIntera"/>) invece di
    /// indovinare uno spezzamento per spazi che sbaglierebbe le celle multi-parola.
    /// </summary>
    /// <param name="virgola">
    /// Se la virgola possa fare da separatore. ⚠️ Va spenta per le tabelle in cui la virgola separa gia'
    /// qualcosa <b>dentro</b> una cella — i punti di una clausola, «EKMUR, PISIP» — perche' li' le due cose
    /// diventerebbero indistinguibili. E' la lezione pagata dall'incolla delle clausole.
    /// </param>
    public static Griglia Leggi(string? testo, bool virgola = true)
    {
        var t = TestoTabellare.Normalizza(testo);
        if (t.Trim().Length == 0) return Vuota;

        if (t.IndexOf("<table", StringComparison.OrdinalIgnoreCase) >= 0) return TabellaHtml.Leggi(t);

        var righe = t.Split('\n').Where(r => r.Trim().Length > 0).ToList();
        if (righe.Count == 0) return Vuota;

        if (righe.All(EMarkdown)) return DaMarkdown(righe);
        if (righe.Any(r => r.Contains('\t'))) return DaSeparatore(righe, '\t', FormaGriglia.Tabulazioni);

        // ⚠️ Il CSV si legge sul testo INTERO e non riga per riga: una cella fra virgolette puo' contenere
        // un a-capo, e spezzare prima le righe lo trasformerebbe in due righe monche.
        foreach (var sep in virgola ? new[] { ';', '|', ',' } : new[] { ';', '|' })
            if (Coerente(righe, sep))
                return new Griglia(LeggiCsv(t, sep), FormaGriglia.Csv);

        return new Griglia(righe.Select(r => (IReadOnlyList<string>)new[] { r.Trim() }).ToList(),
            FormaGriglia.RigaIntera);
    }

    /// <summary>
    /// Legge un CSV con un separatore <b>dichiarato</b> (il file caricato, dove non si indovina: lo dice
    /// l'estensione e, se serve, l'utente).
    /// </summary>
    public static Griglia LeggiCsvEsplicito(string? testo, char separatore)
    {
        var t = TestoTabellare.Normalizza(testo);
        return t.Trim().Length == 0 ? Vuota : new Griglia(LeggiCsv(t, separatore), FormaGriglia.Csv);
    }

    /// <summary>
    /// Taglia ogni riga alle <paramref name="tagli"/> date: e' il modo a larghezza fissa, quello con le
    /// maniglie trascinabili sull'anteprima.
    /// <para>⚠️ I tagli fuori posto non sono un errore da rifiutare: una riga piu' corta del taglio produce
    /// una cella vuota, che e' esattamente cio' che si vede sullo schermo mentre si trascina.</para>
    /// </summary>
    public static Griglia LeggiLarghezzaFissa(string? testo, IReadOnlyList<int> tagli)
    {
        var righe = TestoTabellare.Righe(testo);
        if (righe.Count == 0) return Vuota;

        var punti = (tagli ?? Array.Empty<int>()).Where(x => x > 0).Distinct().OrderBy(x => x).ToList();
        var fuori = new List<IReadOnlyList<string>>();
        foreach (var riga in righe)
        {
            var celle = new List<string>();
            var da = 0;
            foreach (var a in punti)
            {
                celle.Add(Fetta(riga, da, a));
                da = a;
            }
            celle.Add(Fetta(riga, da, riga.Length));
            fuori.Add(celle);
        }
        return new Griglia(fuori, FormaGriglia.LarghezzaFissa);
    }

    /// <summary>
    /// Piega una griglia di <b>una colonna sola</b> in <paramref name="colonne"/> colonne.
    ///
    /// <para>
    /// ⚠️ E' la cura per il caso peggiore del copia-incolla da PDF: certi estrattori non emettono righe,
    /// emettono <b>una cella per riga</b> — «NAME / Apron ALPHA / Apron OSCAR / NUMBERS / …». Non c'e'
    /// nessuna struttura da riconoscere, perche' nel testo non c'e' piu': l'unica cosa che la puo' rimettere
    /// e' una persona che dice <b>quante colonne</b> erano e in che <b>verso</b> il PDF le ha sputate fuori.
    /// </para>
    /// <para>
    /// ⚠️ E per questo si vede subito nell'anteprima: piegare e' un'ipotesi, e un'ipotesi si guarda. Con
    /// <paramref name="perColonne"/> le celle si leggono <b>giu' per la prima colonna, poi la seconda</b>
    /// (l'ordine di certi estrattori); senza, si riempie <b>riga per riga</b>.
    /// </para>
    /// <para>
    /// ⚠️ L'ultima riga puo' restare <b>corta</b>, e resta corta: pareggiarla qui nasconderebbe che i conti
    /// non tornano — ed e' proprio il segno che il numero di colonne scelto e' sbagliato.
    /// </para>
    /// </summary>
    public Griglia Piega(int colonne, bool perColonne)
    {
        if (colonne < 2 || Righe.Count == 0) return this;

        var celle = Righe.SelectMany(r => r).ToList();
        if (celle.Count == 0) return this;

        var righe = (celle.Count + colonne - 1) / colonne;
        var fuori = new List<IReadOnlyList<string>>();
        for (var r = 0; r < righe; r++)
        {
            var riga = new List<string>();
            for (var c = 0; c < colonne; c++)
            {
                var i = perColonne ? c * righe + r : r * colonne + c;
                if (i < celle.Count) riga.Add(celle[i]);
            }
            if (riga.Count > 0) fuori.Add(riga);
        }
        return this with { Righe = fuori };
    }

    /// <summary>La riga <paramref name="i"/> come lista di celle, o vuota se non c'e'.</summary>
    public IReadOnlyList<string> Riga(int i) =>
        i >= 0 && i < Righe.Count ? Righe[i] : Array.Empty<string>();

    /// <summary>La griglia senza la prima riga: si usa quando la prima e' l'intestazione.</summary>
    public Griglia SenzaPrima() =>
        Righe.Count == 0 ? this : this with { Righe = Righe.Skip(1).ToList() };

    // ---- forme ---------------------------------------------------------------------------------------

    private static bool EMarkdown(string riga)
    {
        var r = riga.Trim();
        return r.StartsWith("|", StringComparison.Ordinal) && r.Length > 1;
    }

    private static Griglia DaMarkdown(IReadOnlyList<string> righe)
    {
        var fuori = new List<IReadOnlyList<string>>();
        foreach (var riga in righe)
        {
            var r = riga.Trim();
            if (r.StartsWith("|", StringComparison.Ordinal)) r = r.Substring(1);
            if (r.EndsWith("|", StringComparison.Ordinal)) r = r.Substring(0, r.Length - 1);

            var celle = r.Split('|').Select(c => c.Trim()).ToList();
            // La riga dei trattini (|---|:--:|) e' impaginazione, non dato.
            if (celle.Count > 0 && celle.All(SoloTrattini)) continue;
            fuori.Add(celle);
        }
        return fuori.Count == 0 ? Vuota : new Griglia(fuori, FormaGriglia.Markdown);
    }

    private static bool SoloTrattini(string cella)
    {
        var c = cella.Trim();
        return c.Length > 0 && c.All(ch => ch == '-' || ch == ':' || ch == ' ');
    }

    private static Griglia DaSeparatore(IReadOnlyList<string> righe, char sep, FormaGriglia forma) =>
        new(righe.Select(r => (IReadOnlyList<string>)r.Split(sep).Select(c => c.Trim()).ToList()).ToList(),
            forma);

    /// <summary>
    /// Vero se il separatore produce <b>lo stesso</b> numero di colonne (almeno due) sulla maggioranza delle
    /// righe. E' il controllo che tiene fuori la virgola dei decimali e quella dentro le frasi: una sola riga
    /// con due virgole e le altre senza non fa un CSV.
    /// </summary>
    private static bool Coerente(IReadOnlyList<string> righe, char sep)
    {
        var conti = righe.Select(r => r.Count(c => c == sep) + 1).ToList();
        var moda = conti.GroupBy(n => n)
            .OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).First();

        // ⚠️ Serve la maggioranza STRETTA delle righe, non delle sole righe che contengono il separatore.
        // Con la soglia lasca «LIBA Amendola MNL TAC - 99Y, 115.25» piu' una riga senza virgole passava per
        // un CSV a due colonne: una riga su due basta a far vincere una forma che vale per meta' del testo.
        return moda.Key >= 2 && (righe.Count < 2 || moda.Count() * 2 > righe.Count);
    }

    /// <summary>
    /// Il lettore CSV: virgolette RFC 4180 (doppio apice = un apice dentro la cella, a-capo ammesso dentro
    /// le virgolette).
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<string>> LeggiCsv(string testo, char sep)
    {
        var righe = new List<IReadOnlyList<string>>();
        var celle = new List<string>();
        var cella = new StringBuilder();
        var dentro = false;

        for (var i = 0; i < testo.Length; i++)
        {
            var c = testo[i];
            if (dentro)
            {
                if (c != '"') { cella.Append(c); continue; }
                if (i + 1 < testo.Length && testo[i + 1] == '"') { cella.Append('"'); i++; continue; }
                dentro = false;
                continue;
            }

            if (c == '"') { dentro = true; continue; }
            if (c == sep) { celle.Add(cella.ToString().Trim()); cella.Clear(); continue; }
            if (c == '\n')
            {
                celle.Add(cella.ToString().Trim());
                cella.Clear();
                if (celle.Any(x => x.Length > 0)) righe.Add(celle);
                celle = new List<string>();
                continue;
            }
            cella.Append(c);
        }

        celle.Add(cella.ToString().Trim());
        if (celle.Any(x => x.Length > 0)) righe.Add(celle);
        return righe;
    }

    private static string Fetta(string riga, int da, int a)
    {
        if (da >= riga.Length) return "";
        var fine = Math.Min(a, riga.Length);
        return riga.Substring(da, fine - da).Trim();
    }
}
