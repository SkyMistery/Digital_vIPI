namespace Vipi.Application.Translation;

/// <summary>
/// Lo stato della traduzione di un documento, in una parola (carta
/// <c>docs/feature/2026-09-04-stato-traduzione.md</c> §3.3).
///
/// <para>⚠️ <b>I vuoti non sono uno solo</b>, ed è la stessa lezione già scritta in
/// <see cref="RevisioneDocumento"/>: «non c'è niente da tradurre» e «il documento si legge in una lingua
/// sola» si somigliano a schermo e vogliono dire cose opposte. Un documento a lingua bloccata <b>non è allo
/// 0%</b>: è fuori dal giro per decisione editoriale, e sta fuori dal corpus per la stessa ragione.</para>
/// </summary>
public enum StatoTraduzione
{
    /// <summary>Lingua bloccata: non si traduce in nessuna direzione, e non c'è niente da rivedere.</summary>
    Bloccata,

    /// <summary>Nessun segmento traducibile: un documento vuoto, o fatto di sole tabelle di identificatori.</summary>
    NienteDaTradurre,

    /// <summary>Ci sono segmenti e la memoria non ne ha nessuno.</summary>
    NonCominciata,

    /// <summary>Qualcosa c'è e qualcosa manca: il documento si legge <b>a chiazze</b>.</summary>
    AChiazze,

    /// <summary>Tutto tradotto, ma non tutto l'ha guardato una persona.</summary>
    DaRileggere,

    /// <summary>Tutto tradotto e tutto riletto.</summary>
    Completa,
}

/// <summary>
/// A che punto è la traduzione di <b>un</b> documento: due coperture, non una.
///
/// <para>
/// 🔴 <b>Perché due e non una media.</b> <see cref="Bozza"/> è quel che chi scrive sta per pubblicare
/// (versione di lavoro contro memoria viva); <see cref="Pubblicato"/> è quel che un lettore vede
/// <i>adesso</i> (snapshot della release efficace contro congelato ∪ memoria). «Bozza 100%, pubblicato 40%»
/// è il guasto §Q18 in persona — chi scrive prosa nuova e pubblica subito congela una traduzione incompleta
/// — e una media direbbe «70%», che è un numero che non descrive niente e non fa agire nessuno.
/// </para>
/// </summary>
/// <param name="DocumentId">Il documento. È la chiave con cui la UI si aggancia al suo <c>ManagedDoc</c>
/// per comporre i collegamenti: le rotte le sa il registry, non questo read-model.</param>
/// <param name="Titolo">Il titolo del documento, per chi legge la tabella.</param>
/// <param name="LinguaSorgente">La lingua in cui il documento è scritto.</param>
/// <param name="LinguaLettura">L'altra: quella in cui questo conto è fatto. Le lingue sono due e la
/// direzione di un documento è sempre «l'opposta della sua», quindi non è un parametro di chi chiede.</param>
/// <param name="Bloccata">Il documento si legge in una lingua sola.</param>
/// <param name="Bozza">La copertura della versione di lavoro contro la memoria viva.</param>
/// <param name="AMano">Quanti dei mancanti della bozza <b>il protettore rifiuta</b>: portano un dato
/// personale e non partiranno mai verso il motore. 🔴 Contarli insieme agli altri farebbe un contatore che
/// non può arrivare a zero, cioè un allarme che si impara a saltare.</param>
/// <param name="Pubblicato">La copertura della release efficace. <see cref="TranslationCoverage.Nessuna"/>
/// quando non c'è release: ⚠️ «nessuna release» non è «0% tradotto», e la UI deve dirlo con parole diverse
/// (<paramref name="HaReleaseEfficace"/>).</param>
/// <param name="HaReleaseEfficace">C'è una release in vigore adesso.</param>
/// <param name="ReleaseCongela">Lo snapshot della release efficace porta traduzioni congelate per la lingua
/// di lettura. ⚠️ Misurato il 4 settembre 2026: <b>nessuna</b> delle 17 release efficaci ne portava — il
/// congelamento riparato il 31 agosto vale dalla prossima pubblicazione, e fino ad allora quel che il
/// pubblico legge viene tutto dalla memoria viva. Era invisibile, e questa è la riga che lo mostra.</param>
public sealed record RigaStatoTraduzione(
    int DocumentId,
    string Titolo,
    string LinguaSorgente,
    string LinguaLettura,
    bool Bloccata,
    TranslationCoverage Bozza,
    int AMano,
    TranslationCoverage Pubblicato,
    bool HaReleaseEfficace,
    bool ReleaseCongela)
{
    /// <summary>Quanti mancanti li prenderà il giro da sé, entro un quarto d'ora.</summary>
    public int InAttesa => Math.Max(0, Bozza.Mancanti - AMano);

    /// <summary>Lo stato in una parola.</summary>
    public StatoTraduzione Stato =>
        Bloccata ? StatoTraduzione.Bloccata
        : Bozza.Segmenti == 0 ? StatoTraduzione.NienteDaTradurre
        : Bozza.Tradotti == 0 ? StatoTraduzione.NonCominciata
        : Bozza.Mancanti > 0 ? StatoTraduzione.AChiazze
        : Bozza.DaRileggere ? StatoTraduzione.DaRileggere
        : StatoTraduzione.Completa;

    /// <summary>Percentuale della bozza, arrotondata. 100 solo se non manca <b>niente</b>: ⚠️ un
    /// arrotondamento che scrive «100%» con una frase mancante è il modo di dire il falso senza mentire.</summary>
    public int PercentualeBozza => Percento(Bozza);

    /// <inheritdoc cref="PercentualeBozza"/>
    public int PercentualePubblicato => Percento(Pubblicato);

    internal static int Percento(TranslationCoverage c)
    {
        if (c.Segmenti == 0) return 0;
        if (c.Mancanti == 0) return 100;
        // Il troncamento, non l'arrotondamento: 99,6% deve leggersi «99», o il 100 non vuol dire più niente.
        return (int)(100L * c.Tradotti / c.Segmenti);
    }
}

/// <summary>
/// Il quadro di tutta la divisione, da <b>una</b> passata.
///
/// <para>⚠️ <b>Si calcola, non si registra</b>, ed è una misura, non un'opinione: il 4 settembre 2026 una
/// passata su tutto il <c>vipi.db</c> — 26 documenti, 696 titoli di sezione, 218 blocchi, incrocio con le
/// 313 voci di memoria — è costata <b>45 ms</b>. Una tabella di stato sarebbe il secondo posto dove sapere
/// una cosa che si deduce, e si disallineerebbe al primo documento eliminato. È la stessa scelta, e lo
/// stesso motivo, di <see cref="Abstractions.ITranslatableCorpus"/> («nessuna coda, per scelta»).</para>
/// </summary>
/// <param name="Righe">Un documento per riga, in nessun ordine particolare: ordina chi mostra.</param>
/// <param name="FuoriDaiDocumenti">I testi che non appartengono a nessun documento — descrizioni e
/// attivazioni delle <b>aree regolamentate</b> (la loro lingua è quella di IVAO, inglese) e le <b>intro di
/// pagina</b>. ⚠️ Non si attribuiscono a un documento e non si sommano alle righe: comparirebbero N volte,
/// una per ogni documento che li mostra.</param>
public sealed record QuadroStatoTraduzione(
    IReadOnlyList<RigaStatoTraduzione> Righe,
    TranslationCoverage FuoriDaiDocumenti)
{
    /// <summary>Il quadro vuoto: nessun documento, niente fuori.</summary>
    public static readonly QuadroStatoTraduzione Vuoto =
        new(Array.Empty<RigaStatoTraduzione>(), TranslationCoverage.Nessuna);

    /// <summary>I documenti che aspettano qualcosa dalla macchina.</summary>
    public int DocumentiInAttesa => Righe.Count(r => r.InAttesa > 0);

    /// <summary>I documenti che aspettano una <b>persona</b>: nessun giro li chiuderà.</summary>
    public int DocumentiAMano => Righe.Count(r => r.AMano > 0);

    /// <summary>
    /// I documenti in vigore la cui release <b>non congela niente</b> per la lingua di lettura: quel che il
    /// pubblico legge lì viene dalla memoria viva, e cambierà sotto i suoi occhi alla prima correzione fatta
    /// su un altro documento che contiene la stessa frase.
    /// </summary>
    public int ReleaseSenzaCongelato => Righe.Count(r => r is { HaReleaseEfficace: true, Bloccata: false, ReleaseCongela: false });
}

/// <summary>
/// A che punto è la traduzione (carta <c>docs/feature/2026-09-04-stato-traduzione.md</c>).
///
/// <para>
/// ⚠️ <b>Una passata per tutti, mai una domanda per riga.</b> Chi mostra una tabella di documenti chiede
/// <see cref="QuadroAsync"/> una volta e si serve: una query per riga sarebbe la stessa forma che
/// <c>DoveSiUsanoAsync</c> ha già dovuto correggere (cento righe a schermo = cento letture del corpus), e su
/// una pagina Blazor sarebbe anche una corsa sul <c>DbContext</c> del circuito.
/// </para>
/// </summary>
public interface IStatoTraduzione
{
    /// <summary>Il quadro di tutti i documenti, da una passata sola.</summary>
    Task<QuadroStatoTraduzione> QuadroAsync(CancellationToken ct = default);

    /// <summary>
    /// La riga di <b>un</b> documento, per l'editor che sta guardando quello e basta. null se il documento
    /// non esiste o non ha una versione di lavoro.
    /// </summary>
    Task<RigaStatoTraduzione?> DocumentoAsync(int documentId, CancellationToken ct = default);
}
