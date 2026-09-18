using System.Text.RegularExpressions;

namespace Vipi.Application.Content;

/// <summary>
/// Una SID citata nel testo di un documento, col nome che segue la SID quando si aggiorna dal sectorfile
/// (carta <c>docs/feature/2026-09-18-riferimenti-sid-nel-testo.md</c>, §A73).
///
/// <para>Nel testo salvato sta <c>[[SID LIRF OST1E]]</c>: lo scalo e l'<b>ultimo nome visto</b>. Al momento di
/// mostrare si cerca, fra le SID di quello scalo, quella con la stessa <see cref="Radice"/> — <c>OST?E</c> — e
/// si scrive il suo nome di oggi, <c>OST2E</c>. Il testo salvato non cambia mai: la memoria di traduzione, che
/// è indicizzata sull'impronta del segmento sorgente, resta valida a ogni revisione.</para>
///
/// <para>⚠️ <b>La chiave è la radice del NOME, non la <c>StableKey</c></b> (carta §2): quella contiene la pista
/// — <c>CDC6A</c> sono due righe, 14L e 14R —, il fix del parser — cambia con un alias —, è nulla sulle SID
/// manuali e non è unica. E le righe di una tabella SID congelata non la portano: portano il nome. Col nome
/// si confronta il riferimento con la tabella che il lettore vede già, congelata o viva.</para>
///
/// <para>Se la SID non si trova più, esce l'ultimo nome visto: un riferimento non esce mai grezzo, nemmeno
/// dove nessuno ha risolto i nomi (<see cref="Sostituisci"/> con <c>null</c>).</para>
/// </summary>
public static class RiferimentiSid
{
    /// <summary>Il riferimento nel testo: <c>[[SID</c>, quattro lettere di scalo, il nome, <c>]]</c>.</summary>
    /// <remarks>Il nome ammette solo maiuscole, cifre, spazi e trattini — le forme vere dell'archivio
    /// (<c>OST1E</c>, <c>BRL1Z-ARL1K</c>, <c>GOLF 1</c>). ⚠️ Nessuna virgoletta né barra rovescia: il
    /// riferimento vive anche dentro le stringhe del JSON delle tabelle, e si sostituisce sul testo del JSON.
    /// <para>⚠️ <c>internal</c> perché la protezione dalla traduzione (<c>TextProtector</c>) usa la STESSA regola:
    /// un riferimento che il renderer riconosce e la protezione no partirebbe verso il motore.</para></remarks>
    internal static readonly Regex Riferimento = new(
        @"\[\[SID ([A-Z]{4}) ([A-Z0-9](?:[A-Z0-9 \-]{0,38}[A-Z0-9])?)\]\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Un pezzo con revisione: da 2 a 7 lettere, UNA cifra, una lettera — <c>OST1E</c>, <c>SALENTO5A</c>, e
    /// ciascuna metà di <c>BRL1Z-ARL1K</c>. La cifra è la revisione.
    /// </summary>
    private static readonly Regex Pezzo = new(
        @"(?<![A-Z0-9])([A-Z]{2,7})([0-9])([A-Z])(?![A-Z0-9])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Spazi = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Un nome che è UN pezzo solo, <c>BANA8A</c>: il codice troncato del sectorfile.</summary>
    private static readonly Regex Semplice = new(
        @"^[A-Z]{2,7}([0-9][A-Z])$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Il nome come lo si SCRIVE nel testo: il punto per esteso più il designatore — <c>BANAV 9A</c>, non il
    /// codice troncato <c>BANA9A</c> (richiesta del committente, 18 settembre 2026). Il punto è quello della
    /// tabella, cioè quello effettivo: la correzione a mano se c'è (LIRF «SIV» → <c>SOSIV</c>).
    /// <para>Solo per la forma semplice. Un composto (<c>BRL1Z-ARL1K</c>) e un nome militare (<c>GOLF 1</c>)
    /// non hanno un punto unico da cui partire, e restano come sono; così pure una riga senza punto.</para>
    /// </summary>
    public static string NomeEsteso(string? fix, string? nome)
    {
        var n = Norm(nome);
        var f = Norm(fix);
        var m = Semplice.Match(n);
        if (!m.Success || f.Length == 0 || f == "—" || f.Contains(' ')) return n;
        return $"{f} {m.Groups[1].Value}";
    }

    /// <summary>Il testo del riferimento per quello scalo e quel nome.</summary>
    public static string Scrivi(string icao, string nome) => $"[[SID {Norm(icao)} {Norm(nome)}]]";

    /// <summary>Vero se il testo contiene almeno un riferimento. La via breve: quasi nessun testo ne ha.</summary>
    public static bool Contiene(string? testo) =>
        !string.IsNullOrEmpty(testo) && testo.Contains("[[SID ", StringComparison.Ordinal) && Riferimento.IsMatch(testo);

    /// <summary>
    /// La radice del nome: la cifra di revisione di ogni pezzo diventa <c>?</c> (<c>OST1E</c> → <c>OST?E</c>,
    /// <c>BRL1Z-ARL1K</c> → <c>BRL?Z-ARL?K</c>). Un nome senza pezzi di quella forma — i militari
    /// <c>GOLF 1</c>, <c>OMNI</c>, <c>FRASCA DEP16</c> — non ha revisioni ed è radice di sé stesso.
    /// </summary>
    public static string Radice(string? nome) => Pezzo.Replace(Norm(nome), "$1?$3");

    /// <summary>Gli scali citati, in maiuscolo e senza doppioni.</summary>
    public static IReadOnlySet<string> ScaliCitati(IEnumerable<string?> testi)
    {
        var scali = new HashSet<string>(StringComparer.Ordinal);
        foreach (var testo in testi)
        {
            if (!Contiene(testo)) continue;
            foreach (Match m in Riferimento.Matches(testo!)) scali.Add(m.Groups[1].Value);
        }
        return scali;
    }

    /// <summary>I testi di un documento che possono portare un riferimento: corpo e JSON di ogni blocco, a ogni
    /// profondità.</summary>
    public static IEnumerable<string?> TestiDi(IEnumerable<SectionView> sezioni)
    {
        foreach (var s in sezioni)
        {
            foreach (var b in s.Blocks)
            {
                yield return b.Body;
                yield return b.BodyJson;
            }
            foreach (var t in TestiDi(s.Children)) yield return t;
        }
    }

    /// <summary>
    /// Il testo con ogni riferimento sostituito dal nome di oggi. <paramref name="nomi"/> <c>null</c>, o una SID
    /// che non si trova più, = l'ultimo nome visto, scritto nel riferimento.
    /// </summary>
    public static string? Sostituisci(string? testo, NomiSid? nomi)
    {
        if (!Contiene(testo)) return testo;
        return Riferimento.Replace(testo!, m =>
        {
            var ultimo = m.Groups[2].Value;
            return nomi?.Nome(m.Groups[1].Value, Radice(ultimo)) ?? ultimo;
        });
    }

    /// <summary>Maiuscolo, spazi ridotti a uno, niente spazi ai bordi.</summary>
    internal static string Norm(string? s) => Spazi.Replace((s ?? "").Trim(), " ").ToUpperInvariant();

    /// <summary>Le cifre di revisione di un nome, in fila: servono a scegliere fra due righe con la stessa radice.</summary>
    internal static string Revisione(string nome) =>
        string.Concat(Pezzo.Matches(Norm(nome)).Select(m => m.Groups[2].Value));
}

/// <summary>
/// I nomi di oggi delle SID citate da un documento: per scalo e radice, il nome da scrivere. Si costruisce dalle
/// tabelle SID che il lettore vede (<see cref="AirportSidView"/>, congelate o vive), quindi il testo dice
/// sempre lo stesso nome della tabella.
/// </summary>
public sealed class NomiSid
{
    // Per radice, il CODICE che ha vinto (serve a scegliere fra due revisioni) e il nome da scrivere.
    private readonly Dictionary<(string Icao, string Radice), (string Codice, string Esteso)> _nomi = new();
    private readonly HashSet<(string Icao, string Radice)> _ambigue = new();

    public static NomiSid Vuoto { get; } = new(new Dictionary<string, AirportSidView>());

    /// <param name="tabelle">Per ogni scalo, la tabella SID come la vede il lettore.</param>
    public NomiSid(IReadOnlyDictionary<string, AirportSidView> tabelle)
    {
        foreach (var (icao, tabella) in tabelle)
        {
            var scalo = RiferimentiSid.Norm(icao);
            foreach (var riga in tabella.Rows)
            {
                var nome = RiferimentiSid.Norm(riga.Name);
                if (nome.Length == 0) continue;
                var chiave = (scalo, RiferimentiSid.Radice(nome));
                var voce = (nome, RiferimentiSid.NomeEsteso(riga.Fix, nome));
                if (!_nomi.TryGetValue(chiave, out var gia)) { _nomi[chiave] = voce; continue; }
                if (gia.Codice == nome) continue;

                // Due nomi diversi per la stessa radice: due revisioni vive insieme (LIBG ROBO1H e ROBO5H, misurato
                // il 18 settembre 2026 — una su 1258). Vince la revisione più alta, e la coppia si ricorda.
                _ambigue.Add(chiave);
                if (string.CompareOrdinal(RiferimentiSid.Revisione(nome), RiferimentiSid.Revisione(gia.Codice)) > 0)
                    _nomi[chiave] = voce;
            }
        }
    }

    /// <summary>Il nome di oggi come si scrive (<c>BANAV 9A</c>), o <c>null</c> se lo scalo non ha una SID con
    /// quella radice.</summary>
    public string? Nome(string icao, string radice) =>
        _nomi.TryGetValue((RiferimentiSid.Norm(icao), radice), out var nome) ? nome.Esteso : null;

    /// <summary>Vero se per quella radice lo scalo ha più di un nome vivo.</summary>
    public bool Ambigua(string icao, string radice) => _ambigue.Contains((RiferimentiSid.Norm(icao), radice));
}
