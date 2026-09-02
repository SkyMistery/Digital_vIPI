using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Vipi.Application.Import;

/// <summary>Com'e' andata la lettura di UNA cella. E' cio' che l'anteprima colora.</summary>
public enum EsitoCella
{
    /// <summary>Non c'era niente da leggere.</summary>
    Vuota,

    /// <summary>Presa com'era: testo, o un numero letto senza doverlo cercare da nessuna parte.</summary>
    Letta,

    /// <summary>Trovata su un catalogo: il valore che si salvera' viene dall'archivio, non dal testo incollato.</summary>
    Risolta,

    /// <summary>Il codice esiste piu' d'una volta: la scelta la fa una persona, e i candidati sono qui.</summary>
    DaScegliere,

    /// <summary>Non si e' letta, e il perche' e' nella nota.</summary>
    NonLetta,
}

/// <summary>
/// Una delle scelte possibili per una cella ambigua.
/// <para>⚠️ Porta la sua <b>identita'</b> e non solo il testo: sceglierne una deve bastare a scriverla, e un
/// elenco di sole etichette costringerebbe a ricercarla — cioe' a poter scrivere qualcosa di diverso da
/// quello che e' stato scelto.</para>
/// </summary>
public sealed record Candidato(string Valore, string? Chiave = null);

/// <summary>Una cella dopo la lettura.</summary>
/// <param name="Grezzo">Il testo com'era: l'anteprima lo mostra accanto all'esito.</param>
/// <param name="Valore">Il valore che si salvera'. Per una cella risolta e' quello del catalogo.</param>
/// <param name="Chiave">L'identita' sul catalogo, quando la cella ne ha una (l'ICAO, la terna di una
/// radioassistenza serializzata): serve a chi applica, che non deve cercare una seconda volta.</param>
/// <param name="Nota">Perche' non si e' letta, o quale scelta manca. Null se non c'e' niente da dire.</param>
/// <param name="Candidati">Le alternative, quando l'esito e' <see cref="EsitoCella.DaScegliere"/>.</param>
public sealed record CellaProposta(
    string Grezzo,
    string Valore,
    EsitoCella Esito,
    string? Chiave = null,
    string? Nota = null,
    IReadOnlyList<Candidato>? Candidati = null)
{
    public static CellaProposta Vuota(string grezzo = "") => new(grezzo, "", EsitoCella.Vuota);

    /// <summary>La cella con la scelta fatta: diventa risolta, e si porta dietro l'identita' scelta.</summary>
    public CellaProposta Scelta(Candidato c) =>
        this with { Valore = c.Valore, Chiave = c.Chiave, Esito = EsitoCella.Risolta, Nota = null, Candidati = null };
}

/// <summary>Una riga letta: le sue celle, e da quale riga del testo incollato viene.</summary>
/// <param name="Numero">Il numero di riga nel testo incollato (da 1): dice <b>dove</b>, non solo <b>cosa</b>.</param>
public sealed record RigaProposta(int Numero, string Grezza, IReadOnlyList<CellaProposta> Celle)
{
    /// <summary>Vero se questa riga si puo' importare: nessuna cella illeggibile o in attesa di scelta.</summary>
    public bool Ok => Celle.All(c => c.Esito != EsitoCella.NonLetta && c.Esito != EsitoCella.DaScegliere);

    /// <summary>Vero se la riga non ha niente dentro: si conta a parte, non e' un errore.</summary>
    public bool Vuota => Celle.All(c => c.Esito == EsitoCella.Vuota);
}

/// <summary>
/// Quel che si vede <b>prima</b> di scrivere: le righe lette, com'e' andata cella per cella, e quante se ne
/// importerebbero.
///
/// <para>⚠️ Non e' un passaggio intermedio da saltare quando si ha fretta. Un incolla che salvasse
/// direttamente metterebbe in archivio la propria interpretazione di un testo che nessuno ha riletto — e
/// l'interpretazione di una tabella copiata da un PDF sbaglia, non «potrebbe sbagliare».</para>
/// </summary>
public sealed record Proposta(
    SpecImport Spec,
    IReadOnlyList<string> Colonne,
    MappaturaColonne Mappatura,
    IReadOnlyList<RigaProposta> Righe)
{
    public static Proposta Niente(SpecImport spec) =>
        new(spec, Array.Empty<string>(), new MappaturaColonne(Array.Empty<int>(), false),
            Array.Empty<RigaProposta>());

    /// <summary>Le righe che si importeranno.</summary>
    public IReadOnlyList<RigaProposta> Buone => Righe.Where(r => r.Ok && !r.Vuota).ToList();

    /// <summary>Le righe che restano fuori, con il loro perche' gia' dentro le celle.</summary>
    public IReadOnlyList<RigaProposta> Scartate => Righe.Where(r => !r.Ok).ToList();

    /// <summary>
    /// La proposta in cui la cella (<paramref name="riga"/>, <paramref name="colonna"/>) ha ricevuto la sua
    /// scelta. ⚠️ Non si ricostruisce niente: la lettura era gia' giusta, mancava solo <b>quale</b> dei due
    /// impianti — e ricostruire vorrebbe dire rifare le interrogazioni ai cataloghi a ogni tendina toccata.
    /// </summary>
    public Proposta ConScelta(int riga, int colonna, int candidato)
    {
        if (riga < 0 || riga >= Righe.Count) return this;
        var r = Righe[riga];
        if (colonna < 0 || colonna >= r.Celle.Count) return this;

        var cella = r.Celle[colonna];
        if (cella.Candidati is not { Count: > 0 } scelte
            || candidato < 0 || candidato >= scelte.Count) return this;

        var celle = r.Celle.ToArray();
        celle[colonna] = cella.Scelta(scelte[candidato]);

        var righe = Righe.ToArray();
        righe[riga] = r with { Celle = celle };
        return this with { Righe = righe };
    }
}

/// <summary>Che cosa il catalogo sa dire di un valore incollato.</summary>
/// <param name="Valore">Come si scrivera' nel documento (il nome dell'archivio, non quello incollato).</param>
/// <param name="Chiave">L'identita' sul catalogo, per chi poi applica.</param>
public sealed record EsitoRisoluzione(
    string Valore,
    EsitoCella Esito,
    string? Chiave = null,
    string? Nota = null,
    IReadOnlyList<Candidato>? Candidati = null);

/// <summary>
/// Chi sa cercare un valore incollato su un catalogo (aeroporti, radioassistenze).
///
/// <para>⚠️ <b>Si chiede a lotti</b>, un tipo per volta e tutti i valori insieme: una tabella di quaranta
/// righe con due colonne di catalogo farebbe ottanta interrogazioni una per cella, e le tabelle grosse sono
/// esattamente quelle per cui l'import esiste.</para>
/// <para>⚠️ <b>Non crea niente.</b> Un codice sconosciuto torna <see cref="EsitoCella.NonLetta"/> e si
/// segnala: l'import di <i>un</i> documento non aggiunge righe a un'anagrafica che e' di <i>tutti</i>.</para>
/// </summary>
public interface IRisolutoreCelle
{
    Task<IReadOnlyDictionary<string, EsitoRisoluzione>> RisolviAsync(
        TipoCella tipo, IReadOnlyCollection<string> valori, CancellationToken ct = default);
}

/// <summary>
/// Che cosa l'anteprima consegna all'editor quando qualcuno preme «importa»: la proposta approvata e dove
/// vanno le righe.
/// </summary>
/// <param name="Sostituisci">
/// Vero: le righe importate prendono il posto di quelle che c'erano. Falso: si aggiungono in coda.
/// <para>⚠️ Sono due tasti e non un'impostazione nascosta, perche' sono due gesti diversi e uno dei due
/// butta via del lavoro: chi importa deve poter vedere quale sta premendo.</para>
/// </param>
public sealed record RichiestaImport(Proposta Proposta, bool Sostituisci);

/// <summary>
/// Una tabella <b>gia' scritta altrove</b> da cui partire: il vSOP di un altro campo, per esempio.
///
/// <para>⚠️ Sul corpus vero e' il guadagno piu' grosso di tutto l'import, e non e' un formato: quindici SOP
/// militari hanno le stesse sezioni, e la meta' delle righe di uno somiglia a quelle di un altro. Qui non
/// c'e' niente da riconoscere e niente da spezzare — le celle sono gia' celle e i codici sono gia' risolti —
/// ma l'anteprima resta la stessa, perche' resta la stessa la domanda: <i>e' questo che volevi?</i></para>
/// </summary>
/// <param name="Chiave">Come chi ha aperto l'import ritrova la sorgente (un ICAO, un id).</param>
public sealed record SorgenteTabella(string Chiave, string Etichetta);
