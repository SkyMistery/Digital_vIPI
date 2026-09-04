namespace Vipi.Application.Translation;

/// <summary>Com'è finita una pressione del tasto «traduci ora».</summary>
public enum EsitoDellaPressione
{
    /// <summary>Il motore ha risposto e la memoria si è riempita.</summary>
    Fatto,

    /// <summary>Non mancava niente: il giro automatico era già passato, o la frase era già di un altro documento.</summary>
    NienteDaFare,

    /// <summary>Il documento si legge in una lingua sola: non c'è niente da tradurre, e non è un errore.</summary>
    Bloccata,

    /// <summary>
    /// Un giro sta già girando. ⚠️ Non si mette in fila: chi ha premuto sta guardando lo schermo, e
    /// «riprova fra poco» è una risposta, mentre un tasto appeso per la durata di una chiamata di rete
    /// sembra rotto.
    /// </summary>
    GiroInCorso,

    /// <summary>La traduzione è spenta (<c>Translation:Enabled</c> falso).</summary>
    Spenta,

    /// <summary>Accesa ma senza chiave: nessun motore configurato. Vuole una riga di configurazione, non un ritentativo.</summary>
    SenzaMotore,

    /// <summary>Il tetto di spesa dei motori è finito.</summary>
    TettoFinito,

    /// <summary>I motori non hanno risposto, o hanno risposto male.</summary>
    MotoreGiu,
}

/// <summary>
/// Che cosa dire a chi ha premuto.
///
/// <para>⚠️ <b>Non basta «fatto».</b> Un giro può riuscire e buttare via metà di quel che ha pagato: i
/// segmenti che tornano rotti — un segnaposto mangiato — non si salvano, e senza dirli qui la pressione
/// direbbe «tradotte 3» di cinque frasi spedite, con le altre due sparite in silenzio. È la stessa lezione
/// del registro (§Q16): <b>un avviso che non dice QUALE è un avviso inservibile</b>.</para>
/// </summary>
/// <param name="Esito">Com'è finita.</param>
/// <param name="Tradotti">Quante frasi sono entrate in memoria adesso.</param>
/// <param name="Mancavano">Quante ne mancavano prima di premere.</param>
/// <param name="Scartati">Quante sono tornate rotte: pagate e buttate, e il prossimo giro le rispedirà.</param>
/// <param name="AMano">Quante il protettore ha rifiutato: vogliono una persona.</param>
/// <param name="Motore">Chi ha risposto davvero — con la catena non è scontato che sia il primo.</param>
/// <param name="Rotti">Le frasi tornate rotte, per esteso: sono quelle su cui una persona deve guardare.</param>
/// <param name="Dettaglio">Che cosa ha detto il motore quando non è andata. Mai la chiave.</param>
public sealed record RispostaTraduciOra(
    EsitoDellaPressione Esito,
    int Tradotti = 0,
    int Mancavano = 0,
    int Scartati = 0,
    int AMano = 0,
    string? Motore = null,
    IReadOnlyList<string>? Rotti = null,
    string? Dettaglio = null);

/// <summary>
/// «Traduci ora» le frasi di <b>questo</b> documento (carta
/// <c>docs/feature/2026-09-04-stato-traduzione.md</c> §4-bis).
///
/// <para>
/// ⚠️ <b>Non rompe la regola «non si traduce al salvataggio»</b>, e la differenza non è formale: quella
/// regola esiste perché un disservizio del motore non deve poter impedire a un controllore di <b>salvare</b>
/// il suo lavoro. Qui non salva nessuno — è un gesto a parte, che una persona fa quando vuole vedere subito
/// come viene letto quel che ha appena scritto, e che può fallire senza portarsi dietro niente.
/// </para>
///
/// <para>
/// 🔴 <b>Il raggio è del documento</b>: partono i suoi segmenti e nessun altro. Chiamare il giro intero da un
/// tasto vorrebbe dire che la pressione di una persona paga la prosa in attesa di tutti gli altri.
/// </para>
///
/// <para>
/// ⚠️ <b>Il permesso è quello del DOCUMENTO</b> (<c>Editor</c>), non quello dell'amministratore: è la stessa
/// porta della correzione a mano nel pannello, e per la stessa ragione — chi può scrivere quel documento è
/// chi deve poterne guardare la resa.
/// </para>
/// </summary>
public interface ITraduciOra
{
    /// <summary>Chiede al motore le frasi mancanti di questo documento, adesso.</summary>
    Task<RispostaTraduciOra> EseguiAsync(int documentId, CancellationToken ct = default);
}
