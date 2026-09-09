using System.Globalization;

namespace Vipi.Ui;

/// <summary>
/// La frase che accompagna un guasto di <b>render</b>: che cosa c'era, quando è successo.
///
/// <para>🔴 <b>Perché serve.</b> Una <c>NullReferenceException</c> dentro <c>BuildRenderTree</c> arriva nuda:
/// «riga N», e basta. In Release i metodi corti finiscono dentro il chiamante e la loro riga sparisce, quindi
/// la riga incolpata è spesso quella del <b>chiamante</b> e non del guasto. Sull'editor APP questo è costato
/// tre occorrenze (7 settembre 2026 alle 14:21:36, 8 settembre alle 13:58:40 e alle 14:00:22) senza mai
/// arrivare a un colpevole: sulla riga incolpata, letta al commit che girava, non c'è <b>niente</b> che possa
/// essere nullo — il ramo «ACC sconosciuto» c'è, il nome è una stringa inizializzata, la stazione militare si
/// legge con <c>?.</c>. Vedi <c>docs/lavori-aperti.md</c> §CF.</para>
///
/// <para>⚠️ E le voci nuove arrivano dal <b>circuito</b> (<c>CircuitUnhandledException</c>), non da una GET:
/// non portano né rotta né VID. Il contesto deve metterlo il codice, o non c'è.</para>
/// </summary>
public static class ContestoDiRender
{
    /// <summary>
    /// La testata dell'editor APP.
    ///
    /// <para>🔴 <b>Non deve poter fallire</b>, ed è la sola regola che conta qui: una frase di contesto che
    /// solleva a sua volta mentre racconta un guasto sostituisce l'errore vero con il proprio, e si perde
    /// anche quel poco che si sapeva. Per questo prende <b>tutto</b> come già letto e già annullabile — non
    /// dereferenzia niente — e ogni pezzo ha la sua parola per «non c'era».</para>
    /// </summary>
    public static string TestataApp(
        string? app, string? acc, bool documentoCaricato, bool stazioneMilitareNota, string? nomeMostrato) =>
        "Testata dell'editor APP: " +
        $"app={Pezzo(app)}, acc={Pezzo(acc)}, " +
        $"documento={(documentoCaricato ? "caricato" : "NON caricato")}, " +
        $"stazione militare={(stazioneMilitareNota ? "nota" : "sconosciuta")}, " +
        $"nome={Pezzo(nomeMostrato)}, " +
        $"lingua di lettura={Lingua()}";

    /// <summary>
    /// L'editor documentale CONDIVISO (<c>DocumentSectionsEditor</c>), che è quello che rende le sezioni di
    /// tutte e cinque le famiglie.
    ///
    /// <para>🔴 <b>Perché serve anche qui.</b> Il 9 settembre 2026, fra le 13:47 e le 14:10 UTC, questo
    /// componente ha dato <b>quattro</b> <c>NullReferenceException</c> di render — tre da <c>BuildToc()</c>
    /// e una da <c>SectionsBody</c> — e una di loro ha abbattuto il circuito. Sulle righe incolpate, lette
    /// al commit che girava (<c>30ca658</c>), non c'è niente che possa essere nullo <b>di per sé</b>: sono
    /// righe che chiamano membri corti — <c>LinguaDelDocumento</c>, <c>Titolo</c>, <c>Offset</c> — che in
    /// Release finiscono <b>dentro</b> il chiamante e si portano via la propria riga. È la stessa diagnosi
    /// che si era fermata tre volte sulla testata dell'APP prima di <see cref="TestataApp"/>.</para>
    ///
    /// <para>⚠️ <b>Il sospetto misurabile è <c>Doc</c>.</b> È dichiarato
    /// <c>[Parameter, EditorRequired] EditableDocument Doc { get; set; } = default!</c>: <c>EditorRequired</c>
    /// è un avviso al <b>chiamante</b> in compilazione, non una garanzia a runtime, e <c>default!</c> mette a
    /// tacere il compilatore per sempre. Quindi <c>Doc.Sections</c> e <c>Doc.Language</c> sono
    /// dereferenziamenti nudi su un riferimento che il tipo dichiara non nullo e che nasce nullo. Questa
    /// frase lo dice: se alla prossima occorrenza c'è scritto «documento=(NON caricato)», la diagnosi è
    /// chiusa e il rimedio è una guardia, non un'altra rete.</para>
    ///
    /// <para>⚠️ Come <see cref="TestataApp"/>: prende tutto <b>già letto</b>, non dereferenzia niente, e ogni
    /// pezzo ha la sua parola per «non c'era».</para>
    /// </summary>
    /// <param name="dove">Quale dei due punti stava rendendo: «indice» o «corpo delle sezioni».</param>
    public static string EditoreDiSezioni(
        string dove, bool documentoCaricato, int? quanteSezioniDelDocumento, int? quanteRadiciProprie,
        bool profiloNoto, bool conIndice, int? radiceId, string? gruppo) =>
        $"Editor delle sezioni ({dove}): " +
        $"documento={(documentoCaricato ? "caricato" : "NON caricato")}, " +
        $"sezioni del documento={Conta(quanteSezioniDelDocumento)}, " +
        $"radici proprie={Conta(quanteRadiciProprie)}, " +
        $"profilo={(profiloNoto ? "noto" : "sconosciuto")}, " +
        $"indice={(conIndice ? "acceso" : "spento")}, " +
        $"radice={(radiceId is { } r ? r.ToString(CultureInfo.InvariantCulture) : "(documento)")}, " +
        $"gruppo={Pezzo(gruppo)}, " +
        $"lingua di lettura={Lingua()}";

    /// <summary>«(null)» o il numero: qui la differenza fra «non c'era l'elenco» e «l'elenco era vuoto» è
    /// esattamente la domanda a cui si vuole rispondere.</summary>
    private static string Conta(int? n) =>
        n is { } v ? v.ToString(CultureInfo.InvariantCulture) : "(null)";

    /// <summary>«(null)», «(vuoto)» o il valore fra virgolette: tre casi diversi che a occhio si somigliano.</summary>
    private static string Pezzo(string? v) =>
        v is null ? "(null)" : v.Length == 0 ? "(vuoto)" : $"«{v}»";

    /// <summary>
    /// ⚠️ Anche questa non può fallire: si legge la cultura corrente, che è la cosa più probabile che cambi
    /// fra un render che regge e uno che cade — l'editor si ridisegna anche quando si gira la lingua.
    /// </summary>
    private static string Lingua()
    {
        try { return CultureInfo.CurrentUICulture.Name is { Length: > 0 } n ? n : "(invariante)"; }
        catch (Exception) { return "(non leggibile)"; }
    }
}
