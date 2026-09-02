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
    IReadOnlyList<string>? Candidati = null)
{
    public static CellaProposta Vuota(string grezzo = "") => new(grezzo, "", EsitoCella.Vuota);
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
}

/// <summary>Che cosa il catalogo sa dire di un valore incollato.</summary>
/// <param name="Valore">Come si scrivera' nel documento (il nome dell'archivio, non quello incollato).</param>
/// <param name="Chiave">L'identita' sul catalogo, per chi poi applica.</param>
public sealed record EsitoRisoluzione(
    string Valore,
    EsitoCella Esito,
    string? Chiave = null,
    string? Nota = null,
    IReadOnlyList<string>? Candidati = null);

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
