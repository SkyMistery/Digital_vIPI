namespace Vipi.Application.Abstractions;

/// <summary>Che cosa si chiede alla sorgente, e con quale chiave.</summary>
public enum SourceProbeKind
{
    /// <summary>Un settore di ACC (subcenter): la chiave è il callsign, il proprietario l'ACC.</summary>
    AccSector,

    /// <summary>Una postazione d'aeroporto (DEL/GND/TWR/APP…): la chiave è il callsign, il proprietario l'ICAO.</summary>
    AirportSector,

    /// <summary>Un aeroporto: la chiave è l'ICAO.</summary>
    Airport,

    /// <summary>Un ente ACC: la chiave è il codice (es. <c>LIRR</c>).</summary>
    Acc,
}

/// <summary>
/// L'indirizzo di un elemento <b>nella sorgente</b>.
///
/// <para>⚠️ Non è un gemello di <c>DeletionTarget</c>, ed è per questo che sono due tipi. Quello indirizza
/// una riga del <b>nostro</b> archivio (un Id, una chiave di catalogo); questo indirizza ciò che la sorgente
/// espone, con le chiavi che la sorgente conosce — e che sono altre: il callsign invece dell'Id, e il
/// proprietario, che da noi è una FK e lì è un pezzo dell'URL. Un tipo solo per due indirizzari costringerebbe
/// uno dei due a portarsi dietro campi che non gli appartengono.</para>
/// </summary>
/// <param name="Key">La chiave naturale nella sorgente: callsign, ICAO, codice ACC.</param>
/// <param name="Owner">Chi la contiene: l'ACC per un subcenter, l'ICAO per una postazione d'aeroporto.
/// È ciò che permette la <b>controprova</b> — vedi <see cref="ISourcePresenceProbe"/>.</param>
public sealed record SourceProbeTarget(SourceProbeKind Kind, string Key, string? Owner = null);

/// <summary>
/// Il verdetto, a <b>tre</b> valori. Il terzo non è un dettaglio implementativo: è la ragione per cui questo
/// meccanismo si può usare per autorizzare una cancellazione.
/// </summary>
public enum SourcePresence
{
    /// <summary>La sorgente lo manda ancora. Non sblocca niente — ma chiude la questione subito.</summary>
    Presente,

    /// <summary>La sorgente ha risposto, ha nominato altro, e questo non c'è. È un <b>fatto</b>.</summary>
    Assente,

    /// <summary>
    /// Non si sa: la sorgente non ha risposto, ha risposto male, o ha risposto una cosa ambigua (un elenco
    /// vuoto). Non è «assente», e non deve mai comportarsi come tale.
    /// </summary>
    NonSiSa,
}

/// <summary>Che cosa ha risposto la sorgente, e come lo si racconta.</summary>
/// <param name="Motivo">La frase per lo schermo: si legge accanto al tasto che ha fatto la domanda.</param>
/// <param name="Tracce">Le chiamate fatte e i loro esiti, per il registro di audit. Non è per lo schermo.</param>
public sealed record SourceProbeResult(SourcePresence Esito, string Motivo, string Tracce = "")
{
    /// <summary>L'unico valore che autorizza a saltare l'attesa dei due giri.</summary>
    public bool ProvaLAssenza => Esito == SourcePresence.Assente;

    public static SourceProbeResult Assente(string motivo, string tracce = "") =>
        new(SourcePresence.Assente, motivo, tracce);

    public static SourceProbeResult Presente(string motivo, string tracce = "") =>
        new(SourcePresence.Presente, motivo, tracce);

    public static SourceProbeResult NonSiSa(string motivo, string tracce = "") =>
        new(SourcePresence.NonSiSa, motivo, tracce);
}

/// <summary>
/// Chiede alla sorgente, <b>adesso</b>, se un singolo elemento c'è ancora.
///
/// <para><b>Perché una porta nuova e non <c>IAirportDirectory</c>.</b> Le porte anagrafiche esistenti sono
/// <i>best-effort</i> per costruzione: <c>IvaoHttp.GetJsonAsync</c> e <c>GetStringAsync</c> ritornano
/// <c>null</c> per <b>qualunque</b> risposta non-2xx, quindi un «non l'ho trovato» e un «401, token scaduto»
/// arrivano al chiamante indistinguibili. Va benissimo per riempire un nome in un editor; è disastroso per
/// autorizzare una cancellazione, perché un'ora storta della sorgente diventerebbe il permesso di svuotare
/// il catalogo. Questa porta esiste per dire la differenza, e per questo il suo verdetto ha tre valori.</para>
///
/// <para><b>La controprova.</b> Un «non trovato» da solo non basta neanche quando è un 404 vero. La regola
/// dei due giri (<see cref="Vipi.Application.Content.SogliaEliminazione"/>) nasce perché <i>una risposta a
/// zero elementi non è un errore</i>: un giro può riuscire e tornare vuoto per un ente. Una domanda puntuale
/// che riceve un elenco vuoto ricrea identica quell'ambiguità, solo più in fretta. Quindi ogni
/// implementazione deve fare <b>due</b> chiamate — la puntuale e una di controllo sul contenitore — e può
/// rispondere <see cref="SourcePresence.Assente"/> soltanto se la seconda ha risposto <b>e ha nominato
/// altro</b>. Elenco vuoto, errore, credenziali mancanti: <see cref="SourcePresence.NonSiSa"/>.</para>
///
/// <para>Carta: <c>docs/feature/2026-08-26-chiedere-alla-sorgente.md</c>.</para>
/// </summary>
public interface ISourcePresenceProbe
{
    /// <summary>
    /// Interroga la sorgente sul singolo elemento. Non lancia: un guasto è un verdetto
    /// (<see cref="SourcePresence.NonSiSa"/>), non un'eccezione — chi chiama sta già mostrando una finestra.
    /// </summary>
    Task<SourceProbeResult> ChiediAsync(SourceProbeTarget bersaglio, CancellationToken ct = default);
}

/// <summary>
/// La sorgente non c'è: nessun adapter registrato (l'Host non ha chiamato <c>AddVipiIvao</c>, o gira senza
/// sorgente esterna). Risponde sempre «non si sa», che è la verità.
///
/// <para>È un null-object e non un <c>null</c> iniettato perché la differenza fra «non lo so» e «non c'è
/// nessuno a cui chiedere» non deve costringere il chiamante a un <c>if</c>: il verdetto è lo stesso, e a
/// schermo la frase pure.</para>
/// </summary>
public sealed class SorgenteNonInterrogabile : ISourcePresenceProbe
{
    public Task<SourceProbeResult> ChiediAsync(SourceProbeTarget bersaglio, CancellationToken ct = default) =>
        Task.FromResult(SourceProbeResult.NonSiSa(
            "nessuna sorgente esterna configurata: non c'è a chi chiedere",
            "nessun adapter registrato"));
}
