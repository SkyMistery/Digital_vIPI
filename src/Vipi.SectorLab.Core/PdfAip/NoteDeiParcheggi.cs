using System.Globalization;
using System.Text.RegularExpressions;
using Aip;
using Aip.Pdf;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Una pagina di note di una carta dei parcheggi, com'è letta: la carta di AD 2.24 a cui appartiene, il riferimento della
/// pagina, la sua data e le righe del testo, senza la testata e il piè di pagina.
/// </summary>
public sealed record PaginaDiNote(string Carta, string Pagina, DateOnly? Data, IReadOnlyList<string> Righe);

/// <summary>
/// Le note dei parcheggi (carta «Gli aeroporti nel Lab», §2 D, controllo 3): le regole che AD 2 stampa come testo dopo le
/// carte dei parcheggi — gli stand col VDGS, quelli fuori dalla vista della torre, gli ingressi da una taxiway sola, le
/// apertura alari massime, il push-back. Si leggono e si mostrano: non c'è un dato del sector da confrontare.
/// <para>Misura sul 2611: delle carte dei parcheggi di AD 2.24 una ventina di scali ha pagine di testo, in due forme. La
/// tabella «STAND · ACFT CODE · DIMENSIONS RESTRICTIONS · SELF MANOEUVRING · PUSH BACK · REMARKS» (LIRN, LIBD, LIBR, LIPQ…),
/// che letta come testo ha le colonne sfalsate (uno stand cade accanto alle restrizioni di un altro): per questo si mostra
/// riga per riga com'è stampata, e non si divide stand per stand. E le note in prosa (LIRF, LIMC, LIML, LIMF, LIRA), anche
/// su più pagine. Una carta occupa le pagine fino alla carta seguente dello stesso file (come le carte di avvicinamento, S2);
/// le pagine col disegno hanno la legenda («Bearings are magnetic») e restano fuori, tranne quelle che stampano una tabella
/// di coordinate: i punti d'arresto degli stand di LIPE, LIEE, LIPY e LIRP («110-stop2 44° 31' 47.16" 011° 17' 44.29"»).</para>
/// </summary>
public static partial class NoteDeiParcheggi
{
    /// <summary>Una pagina che continua le note ha almeno tante parole: un disegno senza legenda ne ha una ventina al più.</summary>
    private const int ParoleDiUnaPaginaDiTesto = 40;

    [GeneratedRegex(@"PARKING|DOCKING", RegexOptions.CultureInvariant)]
    private static partial Regex RxCartaDeiParcheggi();

    [GeneratedRegex(@"REMARKS|DESCRIPTION\s+TABLE", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxNote();

    [GeneratedRegex(@"Bearings\s+are\s+magnetic", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxLegenda();

    [GeneratedRegex(@"^(AIP\s*-\s*Italia|ENAV\b|AIRAC\s+effective\s+date|AD\s*[23]\s+[A-Z]{4}\s+\d+\s*[-­]\s*\d+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxTestata();

    [GeneratedRegex(@"[A-Za-z]{3,}", RegexOptions.CultureInvariant)]
    private static partial Regex RxParola();

    /// <summary>Gradi, primi e secondi stampati: <c>44°31'47.16"</c>, <c>39° 14' 50''</c>.</summary>
    [GeneratedRegex(@"\b\d{2,3}\s?[°º]\s?\d{2}\s?['’]\s?\d{2}(?:[.,]\d+)?\s?(?:""|''|”|″)", RegexOptions.CultureInvariant)]
    private static partial Regex RxCoordinata();

    /// <summary>
    /// Tante coordinate su una pagina col disegno sono una tabella di punti (i punti d'arresto degli stand di LIPE, LIEE,
    /// LIPY, LIRP sul 2611): la pagina entra, perché la ricerca per stand li trovi. Le soglie di una carta sono poche.
    /// </summary>
    private const int CoordinateDiUnaTabella = 10;

    [GeneratedRegex(@"(?<!\d)(?<da>\d{1,4})\s*(?:-|–|\bto\b|\ba\b)\s*(?<a>\d{1,4})(?!\d)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxIntervallo();

    /// <summary>Una carta dei parcheggi di AD 2.24: «AIRCRAFT PARKING DOCKING CHART», anche le sue DESCRIPTION TABLES.</summary>
    public static bool EDeiParcheggi(AerodromeChart carta) => RxCartaDeiParcheggi().IsMatch(carta?.Title ?? "");

    /// <summary>
    /// Le pagine delle carte dei parcheggi dello scalo nell'indice delle pagine: dalla pagina della carta fino a quella della
    /// carta seguente di AD 2.24 nello stesso file, o fino alla fine della sezione (<c>AD 2 LIRN 2</c>) per l'ultima.
    /// </summary>
    public static IReadOnlyList<(AerodromeChart Carta, IndexedPage Pagina)> Pagine(Aerodrome scalo, IReadOnlyList<IndexedPage> indice)
    {
        ArgumentNullException.ThrowIfNull(scalo);
        ArgumentNullException.ThrowIfNull(indice);
        var carte = scalo.Charts ?? [];
        var perRiferimento = indice.Where(p => p.Reference is not null).GroupBy(p => p.Reference!).ToDictionary(g => g.Key, g => g.First());
        var pagine = new List<(AerodromeChart, IndexedPage)>();
        for (var i = 0; i < carte.Count; i++)
        {
            if (!EDeiParcheggi(carte[i]) || !perRiferimento.TryGetValue(carte[i].Page, out var prima))
                continue;
            var fine = carte.Skip(i + 1).Select(c => perRiferimento.GetValueOrDefault(c.Page)).FirstOrDefault(p => p is not null && p.File == prima.File)?.Position;
            foreach (var p in indice.Where(p => p.File == prima.File && p.Position >= prima.Position && (fine is null ? p.Section == prima.Section : p.Position < fine))
                         .OrderBy(p => p.Position))
                pagine.Add((carte[i], p));
        }
        return pagine;
    }

    /// <summary>
    /// Le pagine di note dello scalo. <paramref name="pdf"/> dà le pagine lette di un file della cartella del ciclo (dal nome
    /// nell'indice), o null se non c'è: si legge una volta sola per file.
    /// </summary>
    public static IReadOnlyList<PaginaDiNote> Leggi(Aerodrome scalo, IReadOnlyList<IndexedPage> indice, Func<string, IReadOnlyList<PdfPageText>?> pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        var letti = new Dictionary<string, IReadOnlyList<PdfPageText>?>(StringComparer.OrdinalIgnoreCase);
        var note = new List<PaginaDiNote>();
        string? cartaDiPrima = null;
        var continua = false;
        foreach (var (carta, pagina) in Pagine(scalo, indice))
        {
            if (cartaDiPrima != carta.Page)
                continua = false;
            cartaDiPrima = carta.Page;
            if (!letti.TryGetValue(pagina.File, out var pagine))
                letti[pagina.File] = pagine = pdf(pagina.File);
            if (pagine is null || pagina.Position < 1 || pagina.Position > pagine.Count)
                continue;
            var nota = Note(carta, pagina, Table.PlainLines(pagine[pagina.Position - 1]), continua);
            if (nota is not null)
                note.Add(nota);
            continua = nota is not null;
        }
        return note;
    }

    /// <summary>
    /// Una pagina, se è di note: con «REMARKS» o «DESCRIPTION TABLE», o di seguito a una pagina di note con abbastanza
    /// testo, e senza la legenda di un disegno; o una pagina, anche col disegno, che stampa una tabella di coordinate. Null
    /// altrimenti.
    /// </summary>
    public static PaginaDiNote? Note(AerodromeChart carta, IndexedPage pagina, IReadOnlyList<string> righe, bool continua)
    {
        ArgumentNullException.ThrowIfNull(carta);
        ArgumentNullException.ThrowIfNull(pagina);
        ArgumentNullException.ThrowIfNull(righe);
        var corpo = righe.Select(r => r.Trim()).Where(r => r.Length > 0 && !RxTestata().IsMatch(r)).ToList();
        var testo = string.Join('\n', corpo);
        var tabellaDiPunti = RxCoordinata().Matches(testo).Count >= CoordinateDiUnaTabella;
        if (RxLegenda().IsMatch(testo) && !tabellaDiPunti)
            return null;
        if (!tabellaDiPunti && !RxNote().IsMatch(testo) && !(continua && RxParola().Matches(testo).Count >= ParoleDiUnaPaginaDiTesto))
            return null;
        return new PaginaDiNote(carta.Title, pagina.Reference ?? $"{pagina.File} p.{pagina.Position}", pagina.Effective, corpo);
    }

    /// <summary>
    /// La riga cita lo stand: il suo numero da solo («401,», «stand 406»), non come misura («36 m»), o dentro un intervallo
    /// («from 307 to 317», «Stands 11-16»). Un intervallo conta solo fra numeri con le stesse cifre e vicini (al più 200):
    /// «0600-2200» è un orario.
    /// </summary>
    public static bool CitaLoStand(string riga, string stand)
    {
        ArgumentNullException.ThrowIfNull(riga);
        var s = stand?.Trim() ?? "";
        if (s.Length == 0)
            return false;
        // Un numero con l'unità dopo è una misura («Max wing-span 36 m»), non lo stand 36.
        if (Regex.IsMatch(riga, $@"(?<![\w/.]){Regex.Escape(s)}(?![\w/.])(?!\s*(?:m|ft|kg|mhz|nm|%)\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return true;
        if (!int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var numero))
            return false;
        foreach (Match m in RxIntervallo().Matches(riga))
        {
            string da = m.Groups["da"].Value, a = m.Groups["a"].Value;
            if (da.Length != a.Length || da.Length != s.Length)
                continue;
            int primo = int.Parse(da, CultureInfo.InvariantCulture), ultimo = int.Parse(a, CultureInfo.InvariantCulture);
            if (ultimo > primo && ultimo - primo <= 200 && numero >= primo && numero <= ultimo)
                return true;
        }
        return false;
    }
}
