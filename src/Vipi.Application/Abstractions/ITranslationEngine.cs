namespace Vipi.Application.Abstractions;

/// <summary>
/// Come è andata una chiamata al motore di traduzione.
///
/// <para>
/// ⚠️ <b>Perché un verdetto e non un <c>null</c>.</b> È la lezione già pagata su <c>IvaoHttp</c>, che
/// riduceva ogni risposta non riuscita a <c>null</c>: «non configurato», «quota finita» e «il servizio è giù
/// per due minuti» diventavano la stessa cosa, e chi guardava i log non poteva distinguere una chiave
/// scaduta da un guasto passeggero. Qui le tre risposte sono tre, perché le azioni sono tre: chiamare
/// l'amministratore, aspettare il mese nuovo, riprovare fra poco.
/// </para>
/// </summary>
public enum TranslationOutcome
{
    /// <summary>Tradotto.</summary>
    Ok,

    /// <summary>Nessun motore configurato. Non è un errore: è un sito che non traduce.</summary>
    NotConfigured,

    /// <summary>Chiave rifiutata. Serve una persona: riprovare non serve a niente.</summary>
    AuthFailed,

    /// <summary>Quota del periodo esaurita. La coda si <b>ferma</b>: insistere brucia solo tentativi.</summary>
    QuotaExceeded,

    /// <summary>Servizio momentaneamente indisponibile o troppe richieste. Si riprova al giro dopo.</summary>
    TemporaryFailure,

    /// <summary>Il motore ha risposto qualcosa che non sappiamo leggere. Non si riprova da soli.</summary>
    PermanentFailure,
}

/// <summary>Esito di una chiamata: i testi tradotti nell'ordine dei testi chiesti, oppure il perché no.</summary>
/// <param name="Texts">Tradotti, uno per ingresso e <b>nello stesso ordine</b>. null se <see cref="Outcome"/>
/// non è <see cref="TranslationOutcome.Ok"/>.</param>
/// <param name="Detail">Che cosa ha detto il motore, per il registro. ⚠️ Non contiene mai la chiave.</param>
/// <param name="Engine">CHI ha tradotto. ⚠️ Non è deducibile da chi è stato chiamato: con una catena di
/// motori il primo può cedere il passo al secondo, e sia la voce in memoria sia il contatore dei caratteri
/// spesi appartengono a quello che ha <b>davvero</b> risposto. Senza questo campo il tetto di spesa di un
/// motore verrebbe consumato dal lavoro dell'altro.</param>
public sealed record TranslationBatch(
    IReadOnlyList<string>? Texts,
    TranslationOutcome Outcome,
    string? Detail = null,
    string? Engine = null)
{
    public static TranslationBatch Ok(IReadOnlyList<string> testi, string? engine = null) =>
        new(testi, TranslationOutcome.Ok, null, engine);

    public static TranslationBatch Ko(TranslationOutcome esito, string? dettaglio = null, string? engine = null) =>
        new(null, esito, dettaglio, engine);
}

/// <summary>
/// Il motore di traduzione automatica, visto dall'applicazione (carta
/// <c>docs/feature/2026-08-27-documenti-bilingue.md</c> §4).
///
/// <para>
/// ⚠️ <b>Riceve testo GIÀ PROTETTO.</b> Chi implementa questa porta non decide che cosa si può mandare: lo
/// ha già deciso <c>TextProtector</c>, e i segmenti non sicuri non arrivano nemmeno qui. Un'implementazione
/// non deve mai reintrodurre l'originale «per avere più contesto».
/// </para>
/// </summary>
public interface ITranslationEngine
{
    /// <summary>Nome breve del motore, che finisce in <c>TranslationUnit.Engine</c>: <c>deepl</c>.</summary>
    string Name { get; }

    /// <summary>Falso se manca la configurazione: il sito funziona lo stesso, semplicemente non traduce.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Traduce un lotto di testi. L'ordine dell'uscita è quello dell'ingresso — è un contratto, non una
    /// gentilezza: chi chiama riaccoppia per posizione.
    /// </summary>
    Task<TranslationBatch> TranslateAsync(
        IReadOnlyList<string> testi, string sourceLang, string targetLang, CancellationToken ct = default);
}
