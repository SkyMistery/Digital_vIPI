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
    ///
    /// <para>🔴 <b>Senza doppioni, qualunque cosa arrivi</b> (17 settembre 2026). Il legame della configurazione
    /// <b>AGGIUNGE</b> gli elementi di un array a quelli che la proprietà ha già: il default <c>{ it, en }</c> più
    /// <c>"Targets": [ "it", "en" ]</c> di <c>appsettings.json</c> facevano <c>it, en, it, en</c>. Il giro dei quindici
    /// minuti percorre ogni coppia, quindi faceva <b>otto</b> passate invece di due — e i segmenti che Azure rende
    /// rotti si ripagavano quattro volte a giro (3 528 caratteri invece di 882). Visto nel primo
    /// <c>log-2026-09-17.txt</c> di produzione, §A59. La difesa sta nel setter perché è l'unico posto da cui passa
    /// ogni fonte — file, variabile d'ambiente, segreti.</para>
    /// </summary>
    public string[] Targets { get => _targets; set => _targets = SenzaDoppioni(value); }
    private string[] _targets = { "it", "en" };

    /// <summary>
    /// I motori da provare, <b>in ordine di preferenza</b>, per nome (<c>ITranslationEngine.Name</c>).
    /// Il primo che risponde vince; gli altri restano pronti.
    ///
    /// <para>
    /// ⚠️ <b>Perche' una catena e non un motore.</b> Le franchigie gratuite cambiano senza preavviso: quella
    /// di DeepL e' passata da 500k al mese a un milione UNA TANTUM mentre si scriveva questa carta. Con un
    /// motore solo, il giorno che la quota finisce la funzione si ferma e qualcuno deve accorgersene. Con la
    /// catena, il secondo subentra da se' e il rapporto dice chi ha tradotto.
    /// </para>
    /// <para>
    /// ⚠️ La <b>memoria non cambia</b> quando cambia il motore: e' indicizzata sul testo, non su chi l'ha
    /// tradotto. Passare dall'uno all'altro non ripaga niente di gia' fatto, e nel database resta scritto
    /// quale motore ha prodotto ogni voce.
    /// </para>
    /// </summary>
    /// <para>⚠️ Senza doppioni per la stessa ragione di <see cref="Targets"/>: <c>azure, deepl, azure, deepl</c>
    /// ritenterebbe due volte ogni motore che non risponde.</para>
    public string[] Order { get => _order; set => _order = SenzaDoppioni(value); }
    private string[] _order = { "azure", "deepl" };

    /// <summary>L'ordine della prima comparsa, senza distinguere maiuscole: «IT» e «it» sono la stessa lingua.</summary>
    private static string[] SenzaDoppioni(string[]? valori) =>
        (valori ?? Array.Empty<string>())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public AzureOptions Azure { get; set; } = new();

    public DeepLOptions DeepL { get; set; } = new();

    /// <summary>Il tetto di spesa configurato per un motore, per nome. 0 = nessun tetto.</summary>
    public long TettoDi(string engine) => engine switch
    {
        "azure" => Azure.MaxCaratteriTotali,
        "deepl" => DeepL.MaxCaratteriTotali,
        _ => 0,
    };
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

    /// <summary>
    /// Tetto di caratteri complessivi da spendere con QUESTO motore. <c>0</c> = nessun tetto.
    /// <para>⚠️ Per DeepL conta piu' che altrove, perche' la franchigia e' <b>una tantum e non si
    /// rinnova</b>: qui il tetto non protegge un mese, protegge la riserva. Superato, la catena passa al
    /// motore dopo invece di fermarsi.</para>
    /// </summary>
    public long MaxCaratteriTotali { get; set; }
}

/// <summary>
/// Il motore Azure AI Translator (Text Translation v3.0).
/// <para>⚠️ La <see cref="Region"/> non e' facoltativa quando la risorsa e' regionale o multi-servizio: senza
/// l'intestazione della regione, Azure risponde <b>401</b> — che somiglia a una chiave sbagliata e manda a
/// rigenerare una chiave che andava benissimo.</para>
/// </summary>
public sealed class AzureOptions
{
    /// <summary>La chiave della risorsa. Vuota = motore non configurato.</summary>
    public string? ApiKey { get; set; }

    /// <summary>La regione della risorsa (es. <c>westeurope</c>). Vedi l'avviso sulla classe.</summary>
    public string? Region { get; set; }

    /// <summary>Base dell'API. Il default e' l'endpoint globale, che va bene per la gran parte dei casi.</summary>
    public string BaseUrl { get; set; } = "https://api.cognitive.microsofttranslator.com";

    /// <summary>Quanti testi per chiamata. Azure ne accetta 100 (o 50.000 caratteri per richiesta).</summary>
    public int MaxTextsPerCall { get; set; } = 50;

    /// <summary>Tetto di caratteri complessivi con questo motore. <c>0</c> = nessun tetto.</summary>
    public long MaxCaratteriTotali { get; set; }
}
