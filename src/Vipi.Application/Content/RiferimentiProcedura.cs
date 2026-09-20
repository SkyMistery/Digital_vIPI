using System.Text.RegularExpressions;
using Vipi.Domain.Entities;

namespace Vipi.Application.Content;

/// <summary>
/// Una procedura citata nel testo di un documento — una <b>SID</b> o una <b>STAR</b> — col nome che la segue
/// quando si aggiorna dal sectorfile (carte <c>2026-09-18-riferimenti-sid-nel-testo.md</c> §A73 e
/// <c>2026-09-20-star-e-altri-riferimenti.md</c> §A80).
///
/// <para>Nel testo salvato sta <c>[[SID LIRF OST1E]]</c> o <c>[[STAR LIRF ELKA3A]]</c>: il verso, lo scalo e
/// l'<b>ultimo nome visto</b>. ⚠️ Il verso sta nel riferimento e non si deduce dal nome: <c>OST1E</c> può
/// essere una partenza a uno scalo e un arrivo a un altro, e cercarlo nella tabella sbagliata darebbe il nome
/// di un'altra procedura. Al momento di
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
public static class RiferimentiProcedura
{
    /// <summary>Il riferimento nel testo: <c>[[SID</c> o <c>[[STAR</c>, quattro lettere di scalo, il nome,
    /// <c>]]</c>.</summary>
    /// <remarks>Il nome ammette solo maiuscole, cifre, spazi e trattini — le forme vere dell'archivio
    /// (<c>OST1E</c>, <c>BRL1Z-ARL1K</c>, <c>GOLF 1</c>). ⚠️ Nessuna virgoletta né barra rovescia: il
    /// riferimento vive anche dentro le stringhe del JSON delle tabelle, e si sostituisce sul testo del JSON.
    /// <para>⚠️ <c>internal</c> perché la protezione dalla traduzione (<c>TextProtector</c>) usa la STESSA regola:
    /// un riferimento che il renderer riconosce e la protezione no partirebbe verso il motore.</para></remarks>
    // ⚠️ Le parentesi sono ammesse (revisione del 18 settembre 2026): LICZ ha `NELD6V(NSY)` e `NELD6Z(NSY)` in
    // produzione, e senza la regola il selettore inseriva un riferimento che poi nessuno riconosceva — in pagina
    // usciva grezzo. Il selettore offre comunque solo nomi che questa regola accetta (`ElencoAsync`).
    internal static readonly Regex Riferimento = new(
        @"\[\[(SID|STAR) ([A-Z]{4}) ([A-Z0-9](?:[A-Z0-9 \-()]{0,38}[A-Z0-9)])?)\]\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Il verso scritto nel riferimento: <c>SID</c> o <c>STAR</c>.</summary>
    internal static ProcedureKind VersoDi(Match m) =>
        m.Groups[1].Value == "STAR" ? ProcedureKind.Star : ProcedureKind.Sid;

    /// <summary>La parola del verso, come si scrive nel riferimento.</summary>
    public static string Parola(ProcedureKind kind) => kind == ProcedureKind.Star ? "STAR" : "SID";

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
        // ⚠️ Solo un punto fatto di LETTERE: i punti VFR militari hanno un prefisso — `BV-VICTOR` per la SID
        // `VICTOR6A` di LIBV, misurato il 18 settembre 2026 — e «BV-VICTOR 6A» non è il nome di nessuna procedura.
        // Il nome esce dall'archivio e va a finire anche dentro il JSON delle tabelle: vedi ValoreScrivibile.
        if (!m.Success || f.Length == 0 || !f.All(c => c is >= 'A' and <= 'Z')) return ValoreScrivibile(n);
        return ValoreScrivibile($"{f} {m.Groups[1].Value}");
    }

    /// <summary>Il testo del riferimento per quel verso, quello scalo e quel nome.</summary>
    public static string Scrivi(ProcedureKind kind, string icao, string nome) =>
        $"[[{Parola(kind)} {Norm(icao)} {Norm(nome)}]]";

    /// <summary>Vero se il testo contiene almeno un riferimento. La via breve: quasi nessun testo ne ha, e
    /// due <c>Contains</c> costano molto meno di una regex su ogni corpo di ogni blocco.</summary>
    public static bool Contiene(string? testo) =>
        !string.IsNullOrEmpty(testo)
        && (testo.Contains("[[SID ", StringComparison.Ordinal) || testo.Contains("[[STAR ", StringComparison.Ordinal))
        && Riferimento.IsMatch(testo);

    /// <summary>
    /// La radice del nome: la cifra di revisione di ogni pezzo diventa <c>?</c> (<c>OST1E</c> → <c>OST?E</c>,
    /// <c>BRL1Z-ARL1K</c> → <c>BRL?Z-ARL?K</c>). Un nome senza pezzi di quella forma — i militari
    /// <c>GOLF 1</c>, <c>OMNI</c>, <c>FRASCA DEP16</c> — non ha revisioni ed è radice di sé stesso.
    /// </summary>
    public static string Radice(string? nome) => Pezzo.Replace(Norm(nome), "$1?$3");

    /// <summary>
    /// Le tabelle citate: una coppia <b>verso + scalo</b> per ogni famiglia nominata, senza doppioni.
    /// ⚠️ Non «gli scali»: un documento che cita una SID di LIRF e una STAR di LIRF ha bisogno di DUE tabelle,
    /// e chiederne una sola darebbe un riferimento irrisolto su metà del testo.
    /// </summary>
    public static IReadOnlySet<(ProcedureKind Kind, string Icao)> TabelleCitate(IEnumerable<string?> testi)
    {
        var citate = new HashSet<(ProcedureKind, string)>();
        foreach (var testo in testi)
        {
            if (!Contiene(testo)) continue;
            foreach (Match m in Riferimento.Matches(testo!)) citate.Add((VersoDi(m), m.Groups[2].Value));
        }
        return citate;
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
    /// Il testo con ogni riferimento sostituito dal nome di oggi. <paramref name="nomi"/> <c>null</c>, o una
    /// procedura che non si trova più, = l'ultimo nome visto, scritto nel riferimento.
    /// </summary>
    public static string? Sostituisci(string? testo, NomiProcedura? nomi)
    {
        if (!Contiene(testo)) return testo;
        return Riferimento.Replace(testo!, m =>
        {
            var ultimo = m.Groups[3].Value;
            return nomi?.NomePer(VersoDi(m), m.Groups[2].Value, ultimo) ?? ultimo;
        });
    }

    /// <summary>Maiuscolo, spazi ridotti a uno, niente spazi ai bordi. Vale per ICAO e nomi.</summary>
    public static string Norm(string? s) => Spazi.Replace((s ?? "").Trim(), " ").ToUpperInvariant();

    /// <summary>
    /// Il valore come si può SCRIVERE al posto di un riferimento: senza virgolette doppie né barre rovesce.
    ///
    /// <para>🔴 Un riferimento si sostituisce anche <b>dentro il JSON</b> dei blocchi tabella — è per questo
    /// che la sua forma non ammette quei due caratteri — ma il <b>valore</b> che prende il suo posto arriva
    /// dall'archivio, e uno di quei campi è testo libero di sorgente esterna: il nominativo radio viene dal
    /// catalogo IVAO. Una virgoletta lì dentro spaccherebbe il JSON del blocco, che smetterebbe di rendersi
    /// — in una pagina sola, senza un errore che lo dica. Nessun nominativo, nessuna frequenza e nessun nome
    /// di procedura le contiene davvero: toglierle non perde niente di vero.</para>
    /// </summary>
    public static string ValoreScrivibile(string? s) =>
        (s ?? "").Trim().Replace("\"", "", StringComparison.Ordinal).Replace("\\", "", StringComparison.Ordinal);

    /// <summary>Le cifre di revisione di un nome, in fila: servono a scegliere fra due righe con la stessa radice.</summary>
    internal static string Revisione(string nome) =>
        string.Concat(Pezzo.Matches(Norm(nome)).Select(m => m.Groups[2].Value));
}

/// <summary>
/// I nomi di oggi delle procedure citate da un documento: per <b>verso</b>, scalo e radice, il nome da
/// scrivere. Si costruisce dalle tabelle che il lettore vede (<see cref="AirportSidView"/>, congelate o vive),
/// quindi il testo dice sempre lo stesso nome della tabella.
/// <para>⚠️ Il verso è dentro la chiave e non è un dettaglio: le due famiglie di uno stesso scalo possono avere
/// due procedure con la stessa radice, e una chiave senza verso farebbe scrivere il nome dell'altra.</para>
/// </summary>
public sealed class NomiProcedura
{
    // Per verso e radice, il CODICE che ha vinto (serve a scegliere fra due revisioni) e il nome da scrivere.
    private readonly Dictionary<(ProcedureKind Kind, string Icao, string Radice), (string Codice, string Esteso)> _nomi = new();
    // Solo per le radici con più di un nome vivo: TUTTI i nomi, come si scrivono. Servono all'avviso dell'editor.
    private readonly Dictionary<(ProcedureKind Kind, string Icao, string Radice), SortedSet<string>> _ambigue = new();
    // Ogni nome vivo, esatto → come si scrive. Serve a preferire il nome CITATO quando è ancora vivo.
    private readonly Dictionary<(ProcedureKind Kind, string Icao, string Codice), string> _perCodice = new();

    public static NomiProcedura Vuoto { get; } =
        new(new Dictionary<(ProcedureKind, string), AirportSidView>());

    /// <param name="tabelle">Per ogni verso e scalo, la tabella come la vede il lettore.</param>
    public NomiProcedura(IReadOnlyDictionary<(ProcedureKind Kind, string Icao), AirportSidView> tabelle)
    {
        foreach (var ((kind, icao), tabella) in tabelle)
        {
            var scalo = RiferimentiProcedura.Norm(icao);
            foreach (var riga in tabella.Rows)
            {
                var nome = RiferimentiProcedura.Norm(riga.Name);
                if (nome.Length == 0) continue;
                var chiave = (kind, scalo, RiferimentiProcedura.Radice(nome));
                var voce = (nome, RiferimentiProcedura.NomeEsteso(riga.Fix, nome));
                _perCodice.TryAdd((kind, scalo, nome), voce.Item2);
                if (!_nomi.TryGetValue(chiave, out var gia)) { _nomi[chiave] = voce; continue; }
                if (gia.Codice == nome) continue;

                // Due nomi diversi per la stessa radice: due revisioni vive insieme (LIBG ROBO1H e ROBO5H, misurato
                // il 18 settembre 2026 — una su 1258). Per la radice vince la revisione più alta, e la coppia si
                // ricorda; ma un riferimento che cita un nome ancora vivo tiene il SUO (`NomePer`).
                if (!_ambigue.TryGetValue(chiave, out var tutti))
                    _ambigue[chiave] = tutti = new SortedSet<string>(StringComparer.Ordinal) { gia.Esteso };
                tutti.Add(voce.Item2);
                if (string.CompareOrdinal(RiferimentiProcedura.Revisione(nome), RiferimentiProcedura.Revisione(gia.Codice)) > 0)
                    _nomi[chiave] = voce;
            }
        }
    }

    /// <summary>I nomi vivi di una radice ambigua, come si scrivono; vuoto se la radice non è ambigua.</summary>
    public IReadOnlyCollection<string> Alternative(ProcedureKind kind, string icao, string radice) =>
        _ambigue.TryGetValue((kind, RiferimentiProcedura.Norm(icao), radice), out var tutti) ? tutti : Array.Empty<string>();

    /// <summary>Il nome di oggi come si scrive (<c>BANAV 9A</c>), o <c>null</c> se lo scalo non ha, in quel
    /// verso, una procedura con quella radice.</summary>
    public string? Nome(ProcedureKind kind, string icao, string radice) =>
        _nomi.TryGetValue((kind, RiferimentiProcedura.Norm(icao), radice), out var nome) ? nome.Esteso : null;

    /// <summary>
    /// Il nome da scrivere per una SID CITATA col codice <paramref name="codiceCitato"/>: quel nome stesso se è
    /// ancora vivo nella tabella, altrimenti il nome di oggi della sua radice. <c>null</c> = non si trova.
    /// <para>🔴 Deciso dal committente il 18 settembre 2026. Con due revisioni vive della stessa SID (LIBG ROBO1H e
    /// ROBO5H) vinceva sempre la cifra più alta: ma i numeri ricominciano dopo il 9, e con ROBO9H vecchia e ROBO1H
    /// nuova avrebbe vinto la vecchia. Il nome citato, se c'è ancora, è la scelta di chi ha scritto e non si indovina
    /// niente; la cifra più alta resta solo per il caso in cui il nome citato è sparito. L'editor segnala comunque
    /// la radice ambigua.</para>
    /// </summary>
    public string? NomePer(ProcedureKind kind, string icao, string codiceCitato)
    {
        var codice = RiferimentiProcedura.Norm(codiceCitato);
        return _perCodice.TryGetValue((kind, RiferimentiProcedura.Norm(icao), codice), out var esteso)
            ? esteso
            : Nome(kind, icao, RiferimentiProcedura.Radice(codice));
    }

    /// <summary>Vero se per quella radice, in quel verso, lo scalo ha più di un nome vivo.</summary>
    public bool Ambigua(ProcedureKind kind, string icao, string radice) =>
        _ambigue.ContainsKey((kind, RiferimentiProcedura.Norm(icao), radice));
}

/// <summary>Perché una procedura citata va ricontrollata.</summary>
public enum ProceduraDaRivedereTipo
{
    /// <summary>Lo scalo non ha più, in quel verso, una procedura con quel nome: nel documento esce l'ultimo
    /// nome visto, così com'è.</summary>
    NonTrovata,

    /// <summary>Due revisioni vive con lo stesso nome: esce quella citata se c'è ancora, altrimenti la più alta — e
    /// chi scrive deve sapere che c'era una scelta.</summary>
    Ambigua,
}

/// <summary>
/// Una procedura citata che l'editor segnala in cima al documento (§A73 slice 4, §A80 per gli arrivi).
/// </summary>
/// <param name="Kind">Il verso: la SID e la STAR con lo stesso nome sono due voci diverse.</param>
/// <param name="Icao">Lo scalo.</param>
/// <param name="Codice">L'ultimo nome visto, scritto nel riferimento (<c>BANA8A</c>).</param>
/// <param name="Tipo">Perché va ricontrollata.</param>
/// <param name="Dove">Le sezioni in cui compare, per titolo.</param>
/// <param name="Esce">Il nome che il documento mostra oggi al suo posto.</param>
/// <param name="Alternative">Per una radice ambigua, tutti i nomi vivi; altrimenti vuoto.</param>
public sealed record ProceduraDaRivedere(
    ProcedureKind Kind, string Icao, string Codice, ProceduraDaRivedereTipo Tipo, IReadOnlyList<string> Dove,
    string Esce, IReadOnlyList<string> Alternative)
{
    /// <summary>Come si nomina la famiglia in pagina: <c>SID</c> o <c>STAR</c>.</summary>
    public string Parola => RiferimentiProcedura.Parola(Kind);
}

/// <summary>Il controllo delle procedure citate in un documento: quali non si trovano più, quali sono
/// ambigue. Vale per i due versi insieme — l'avviso è uno solo, come uno solo è il documento.</summary>
public static class ControlloProcedureCitate
{
    /// <summary>
    /// Le procedure da ricontrollare nei testi dati, ognuno con la sezione in cui sta. Una voce per riferimento
    /// (verso + scalo + codice), con tutte le sezioni che lo citano. ⚠️ Solo i nomi del documento contano: una
    /// procedura sparita dall'anagrafica ma non citata non è affare di questo documento.
    /// </summary>
    public static IReadOnlyList<ProceduraDaRivedere> Controlla(IEnumerable<(string Dove, string? Testo)> testi, NomiProcedura nomi)
    {
        var dove = new Dictionary<(ProcedureKind Kind, string Icao, string Codice), List<string>>();
        foreach (var (sezione, testo) in testi)
        {
            if (!RiferimentiProcedura.Contiene(testo)) continue;
            foreach (System.Text.RegularExpressions.Match m in RiferimentiProcedura.Riferimento.Matches(testo!))
            {
                var chiave = (RiferimentiProcedura.VersoDi(m), m.Groups[2].Value, m.Groups[3].Value);
                if (!dove.TryGetValue(chiave, out var sezioni)) dove[chiave] = sezioni = new List<string>();
                if (!sezioni.Contains(sezione)) sezioni.Add(sezione);
            }
        }

        var esito = new List<ProceduraDaRivedere>();
        // Le partenze prima degli arrivi, e dentro ogni verso per scalo e codice: un elenco che cambia ordine
        // a ogni apertura è un elenco che nessuno rilegge.
        foreach (var ((kind, icao, codice), sezioni) in dove.OrderBy(d => d.Key.Kind)
                                                             .ThenBy(d => d.Key.Icao, StringComparer.Ordinal)
                                                             .ThenBy(d => d.Key.Codice, StringComparer.Ordinal))
        {
            var radice = RiferimentiProcedura.Radice(codice);
            var nome = nomi.NomePer(kind, icao, codice);
            if (nome is null)
                esito.Add(new ProceduraDaRivedere(kind, icao, codice, ProceduraDaRivedereTipo.NonTrovata, sezioni, codice, Array.Empty<string>()));
            else if (nomi.Ambigua(kind, icao, radice))
                esito.Add(new ProceduraDaRivedere(kind, icao, codice, ProceduraDaRivedereTipo.Ambigua, sezioni, nome,
                    nomi.Alternative(kind, icao, radice).ToList()));
        }
        return esito;
    }
}
