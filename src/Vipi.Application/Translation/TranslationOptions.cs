namespace Vipi.Application.Translation;

/// <summary>
/// Configurazione dei documenti bilingue (sezione <c>Translation</c>).
///
/// <para>
/// ⚠️ <b>La chiave del motore non sta MAI in un <c>appsettings</c> versionato</b>: user-secrets in sviluppo,
/// variabile d'ambiente o cartella dei segreti in produzione — la stessa regola già in vigore per le
/// credenziali IVAO.
/// </para>
/// </summary>
public sealed class TranslationOptions
{
    public const string SectionName = "Translation";

    /// <summary>
    /// Spento di default. Un sito senza motore configurato non è rotto: mostra i documenti nella loro lingua
    /// sorgente, che è quello che fa oggi.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Lingue in cui si offre la lettura, oltre a quella sorgente del documento. ⚠️ Codici brevi
    /// (<c>it</c>, <c>en</c>): la traduzione fra le due direzioni è la stessa macchina, perché la vLOA nasce
    /// in inglese e per lei l'italiano è il bersaglio.
    /// </summary>
    public string[] Targets { get; set; } = { "it", "en" };

    public DeepLOptions DeepL { get; set; } = new();
}

/// <summary>Il motore DeepL. Vive qui e non in Infrastructure perché è configurazione, non implementazione.</summary>
public sealed class DeepLOptions
{
    /// <summary>La chiave. Vuota = motore non configurato, e il sito non traduce.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Il glossario di fraseologia della divisione, se ne esiste uno.
    /// <para>⚠️ È la difesa che conta davvero sulla qualità: «riporta sottovento» tradotto in modo
    /// <i>plausibile ma non standard</i> è peggio di non tradotto, perché nessuno se ne accorge. Va costruito
    /// e curato da un controllore, non da chi scrive il codice — e finché è vuoto la vista tradotta resta
    /// marcata «non revisionata».</para>
    /// </summary>
    public string? GlossaryId { get; set; }

    /// <summary>
    /// Base dell'API. Vuota = <b>dedotta dalla chiave</b>: le chiavi del piano gratuito finiscono in
    /// <c>:fx</c> e vogliono <c>api-free.deepl.com</c>, le altre <c>api.deepl.com</c>. Puntare al server
    /// sbagliato dà 403, che somiglia a una chiave scaduta e manda a cercare dalla parte opposta.
    /// </summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>
    /// Variante d'inglese chiesta al motore. <c>EN-GB</c> di default: l'inglese aeronautico è quello, e
    /// <c>EN</c> secco è deprecato come bersaglio.
    /// </summary>
    public string EnglishVariant { get; set; } = "EN-GB";

    /// <summary>Quanti testi per chiamata. DeepL ne accetta 50; oltre, si spezza in più lotti.</summary>
    public int MaxTextsPerCall { get; set; } = 50;
}
