using Vipi.Application.Abstractions;
using Vipi.Domain.Entities;

namespace Vipi.Application.Translation;

/// <summary>Che cosa ha fatto un giro di riempimento, e che cosa non ha potuto fare.</summary>
/// <param name="Segmenti">Segmenti distinti trovati nel corpus.</param>
/// <param name="GiaInMemoria">Quanti erano già tradotti: il dedup che si vede.</param>
/// <param name="Tradotti">Quanti ne ha aggiunti questo giro.</param>
/// <param name="DaTradurreAMano">Segmenti <b>rifiutati dal cancello</b> sui dati personali. Non è un errore
/// del giro: è il cancello che ha funzionato, e quei segmenti vogliono una persona.</param>
/// <param name="Scartati">Segmenti che il motore ha restituito rotti (un segnaposto mangiato o inventato).</param>
/// <param name="Esito">Come è finita la parte automatica.</param>
/// <param name="Dettaglio">Che cosa ha detto il motore, per il registro. Non contiene mai la chiave.</param>
/// <param name="Rotti">I segmenti tornati rotti, come sono partiti. ⚠️ Senza questo elenco l'avviso dice
/// «1 segmento» e nessuno può fare niente: il corpus ne ha decine, e trovare quello giusto vorrebbe dire
/// interrogare il database a mano. Il testo ce l'abbiamo in mano proprio nel punto in cui lo buttiamo.</param>
/// <param name="CaratteriScartati">I caratteri spesi per i segmenti tornati rotti: <b>pagati e buttati</b>.
/// ⚠️ Non entrano nel conto della spesa (quello si deduce da quel che è rimasto in memoria, e questi in
/// memoria non ci vanno) e il giro dopo si rispediscono. Finché sono zero non c'è niente da dire; quando
/// non lo sono, questo numero è l'unico posto in cui la perdita si vede. Vedi lavori-aperti §Q16.</param>
/// <param name="Motore">Chi ha tradotto davvero. Con una catena non e' scontato che sia il primo: se Azure
/// ha finito la quota, qui c'e' scritto «deepl», ed e' l'informazione che dice all'amministratore che il
/// primario e' fermo <b>senza</b> che il servizio si sia fermato con lui.</param>
/// <param name="InQuarantena">Segmenti che <b>non sono partiti</b> perché il motore li aveva già resi rotti
/// abbastanza volte (<see cref="TranslationQuarantena.Soglia"/>). ⚠️ Non sono né tradotti né scartati: non
/// hanno mai lasciato casa, e non sono costati niente. È il numero che dice quanto sta risparmiando il freno.</param>
/// <param name="AppenaFermati">I segmenti che con <b>questo</b> giro hanno raggiunto la soglia e da adesso
/// non partono più. ⚠️ Solo il passaggio di stato, non l'elenco di quelli fermi: è il passaggio a volere un
/// avviso: uno che si ripete a ogni giro è il rumore che il freno esiste per spegnere.</param>
public sealed record TranslationFillReport(
    int Segmenti, int GiaInMemoria, int Tradotti, int DaTradurreAMano, int Scartati,
    TranslationOutcome Esito, string? Dettaglio = null, string? Motore = null, long CaratteriScartati = 0,
    IReadOnlyList<string>? Rotti = null, int InQuarantena = 0, IReadOnlyList<string>? AppenaFermati = null)
{
    /// <summary>Quanti mancano ancora, dopo questo giro.</summary>
    public int Mancanti => Segmenti - GiaInMemoria - Tradotti;
}

/// <summary>
/// Il giro che riempie la memoria (carta <c>docs/feature/2026-08-27-documenti-bilingue.md</c> §6).
///
/// <para>
/// ⚠️ <b>Non si traduce al salvataggio.</b> Bloccherebbe l'editor su un'attesa di rete, e un disservizio del
/// motore bloccherebbe il <i>salvataggio</i> — che è inaccettabile per una ragione semplice: il testo
/// italiano è il documento, la traduzione è un servizio. Il salvataggio non deve mai dipendere da un terzo.
/// </para>
///
/// <para>
/// ⚠️ <b>L'ordine dei cancelli non è negoziabile.</b> Prima il protettore (un segmento non sicuro non parte
/// nemmeno), poi il budget, poi la rete. Invertire i primi due farebbe uscire un dato personale nel giro
/// che poi si sarebbe fermato per quota.
/// </para>
/// </summary>
public sealed class TranslationFillUseCase
{
    private readonly ITranslatableCorpus _corpus;
    private readonly ITranslationMemory _memoria;
    private readonly ITranslationQuarantine _quarantena;
    private readonly IReadOnlyList<ITranslationEngine> _catena;
    private readonly TextProtector _protettore;
    private readonly TranslationOptions _opt;

    /// <param name="motori">I motori <b>in ordine di preferenza</b>. Il primo che risponde vince.</param>
    /// <param name="quarantena">Il freno dei segmenti che il motore non sa rendere.
    /// <para>🔴 <b>Obbligatorio, e non con un valore di comodo.</b> Un parametro facoltativo che di default
    /// non frena vuol dire che una registrazione dimenticata nel contenitore spegne il freno <b>in
    /// silenzio</b>, e il modo in cui ce ne accorgeremmo sarebbe la bolletta fra un mese. Chi costruisce
    /// questo giro deve dire esplicitamente chi frena.</para></param>
    public TranslationFillUseCase(
        ITranslatableCorpus corpus, ITranslationMemory memoria, IEnumerable<ITranslationEngine> motori,
        TextProtector protettore, TranslationOptions opt, ITranslationQuarantine quarantena)
    {
        _corpus = corpus;
        _memoria = memoria;
        _quarantena = quarantena;
        _protettore = protettore;
        _opt = opt;

        // L'ordine lo detta la configurazione, non l'ordine di registrazione nel contenitore: un motore
        // aggiunto in fondo al file di DI non deve diventare il primario per sbaglio.
        var perNome = motori.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
        _catena = opt.Order
            .Where(perNome.ContainsKey)
            .Select(n => perNome[n])
            .ToList();
    }

    /// <summary>
    /// Il giro su <b>tutto il corpus</b>: quel che fa il servizio ogni quarto d'ora.
    /// </summary>
    public async Task<TranslationFillReport> EseguiAsync(
        string sourceLang, string targetLang, CancellationToken ct = default) =>
        await EseguiSuAsync(await _corpus.SegmentiAsync(sourceLang, ct).ConfigureAwait(false),
            sourceLang, targetLang, TranslationSpendKind.Dispatch, ct).ConfigureAwait(false);

    /// <summary>
    /// Lo stesso giro, ma su <b>questi</b> segmenti soltanto: il tasto «traduci ora» di chi ha appena
    /// scritto un documento (carta <c>docs/feature/2026-09-04-stato-traduzione.md</c> §4-bis).
    ///
    /// <para>🔴 <b>Il raggio è la ragione per cui questo ingresso esiste.</b> Chiamare il giro intero da un
    /// tasto vorrebbe dire che la pressione di una persona paga la prosa in attesa di <b>tutti</b> gli altri
    /// documenti — compresa quella che il suo autore non ha ancora finito di scrivere. Qui passano i
    /// segmenti di quel documento e basta.</para>
    ///
    /// <para>⚠️ Si passano <b>tutti</b> i segmenti del documento, non i soli mancanti: il confronto con la
    /// memoria lo fa questo metodo, ed è lo stesso confronto del giro automatico. Una selezione fatta fuori
    /// sarebbe un secondo posto in cui si decide che cosa manca.</para>
    /// </summary>
    /// <param name="kind">Come si registra la spesa: <see cref="TranslationSpendKind.ManualDispatch"/> per
    /// una pressione, così nel registro le due sorgenti restano distinguibili.</param>
    public async Task<TranslationFillReport> EseguiSuAsync(
        IEnumerable<string> daTradurre, string sourceLang, string targetLang,
        TranslationSpendKind kind = TranslationSpendKind.Dispatch, CancellationToken ct = default)
    {
        var segmenti = daTradurre
            .Where(TranslationText.HasSomethingToTranslate)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (segmenti.Count == 0)
            return new TranslationFillReport(0, 0, 0, 0, 0, TranslationOutcome.Ok);

        // Una lettura sola per tutto il giro: la memoria si interroga di gruppo.
        var impronte = segmenti.ToDictionary(s => s, TranslationText.Hash, StringComparer.Ordinal);
        var note = await _memoria
            .LookupAsync(sourceLang, targetLang, impronte.Values.Distinct().ToList(), ct)
            .ConfigureAwait(false);

        var mancanti = segmenti.Where(s => !note.ContainsKey(impronte[s])).ToList();
        var giaInMemoria = segmenti.Count - mancanti.Count;

        if (mancanti.Count == 0)
            return new TranslationFillReport(segmenti.Count, giaInMemoria, 0, 0, 0, TranslationOutcome.Ok);

        // ---- Cancello 0: il freno. Prima di tutto, protettore compreso. ----
        // ⚠️ PRIMA del protettore e non dopo, e non è un'inversione dell'ordine che la carta dichiara non
        // negoziabile: quell'ordine riguarda ciò che PARTE — prima il protettore, poi il budget, poi la
        // rete — e di qui non parte niente. Un segmento fermo non va nemmeno protetto: sarebbe lavoro per
        // decidere di non fare lavoro.
        var strike = await _quarantena
            .StrikeAsync(sourceLang, targetLang, mancanti.Select(s => impronte[s]).ToList(), ct)
            .ConfigureAwait(false);

        var inQuarantena = 0;
        if (strike.Count > 0)
        {
            var prima = mancanti.Count;
            // ⚠️ `Where` e non `Except`: Except è un'operazione d'insieme, riordina e toglie i doppioni —
            // qui l'ordine dei segmenti è quello in cui partono al motore, e cambiarlo cambierebbe i lotti.
            mancanti = mancanti
                .Where(s => !(strike.TryGetValue(impronte[s], out var n) && n >= TranslationQuarantena.Soglia))
                .ToList();
            inQuarantena = prima - mancanti.Count;
        }

        // Tutto quel che mancava è fermo: non c'è niente da chiedere a nessuno, e soprattutto niente da
        // pagare. È il caso normale una volta che il freno ha fatto il suo lavoro.
        if (mancanti.Count == 0)
            return new TranslationFillReport(
                segmenti.Count, giaInMemoria, 0, 0, 0, TranslationOutcome.Ok, InQuarantena: inQuarantena);

        // ---- Cancello 1: i dati personali. Prima di tutto, budget compreso. ----
        var daSpedire = new List<(string Originale, ProtectedText Protetto)>();

        // I segmenti che non hanno niente da tradurre: la loro «traduzione» è se stessi.
        var identiche = new List<(string, string)>();
        var aMano = 0;
        foreach (var s in mancanti)
        {
            var protetto = _protettore.Protect(s);
            if (!protetto.Safe) { aMano++; continue; }

            // ⚠️ Del testo protetto non resta che segnaposto: non c'è niente da chiedere a nessuno, e la
            // traduzione si compone qui. Vale per le celle che sono solo un identificatore — un punto
            // («MARTE»), un fix («CHI»), un callsign — e senza questo passaggio partirebbero a ogni giro per
            // tornare cambiate e farsi scartare.
            if (TextProtector.SoloSegnaposti(protetto.Text))
            {
                // ⚠️ La traduzione è il testo protetto RIPRISTINATO, non il sorgente ricopiato. Per un
                // identificatore le due cose coincidono — torna esattamente quello che era partito — ma per
                // una cella che è tutta una voce di glossario NO: lì il ripristino mette la resa inglese,
                // mentre ricopiare il sorgente scriverebbe in memoria l'ITALIANO spacciandolo per inglese, e
                // lo scriverebbe come voce definitiva che nessun giro successivo riproverebbe.
                identiche.Add(TextProtector.TryRestore(protetto.Text, protetto, out var comeVa)
                    ? (s, comeVa)
                    : (s, TranslationText.Normalize(s)));
                continue;
            }

            daSpedire.Add((s, protetto));
        }

        if (daSpedire.Count == 0)
        {
            var soleIdentiche = identiche.Count == 0
                ? 0
                : await _memoria.SaveMachineAsync(sourceLang, targetLang, "nessuno", identiche, ct).ConfigureAwait(false);
            // «nessuno» e non null: il registro dice CHI ha tradotto, e «(null)» non lo dice a nessuno.
            return new TranslationFillReport(
                segmenti.Count, giaInMemoria, soleIdentiche, aMano, 0, TranslationOutcome.Ok, null, "nessuno",
                InQuarantena: inQuarantena);
        }

        // ---- Cancello 2 e la catena: si prova un motore per volta, in ordine di preferenza. ----
        var testi = daSpedire.Select(d => d.Protetto.Text).ToList();
        var caratteri = testi.Sum(t => t.Length);

        TranslationBatch? riuscito = null;
        var ultimoEsito = TranslationOutcome.NotConfigured;
        string? ultimoDettaglio = null;

        foreach (var motore in _catena)
        {
            if (!motore.IsConfigured) continue;

            // ⚠️ Il tetto e' PER MOTORE, e si controlla PRIMA di spendere. Un motore oltre il suo tetto non
            // ferma il giro: si passa al successivo. E' tutta la ragione per cui esiste una catena -- la
            // franchigia di DeepL e' una tantum, e quando finisce il servizio deve continuare, non fermarsi.
            var tetto = _opt.TettoDi(motore.Name);
            if (tetto > 0)
            {
                var spesi = await _memoria.CaratteriSpesiAsync(motore.Name, ct).ConfigureAwait(false);
                if (spesi + caratteri > tetto)
                {
                    ultimoEsito = TranslationOutcome.QuotaExceeded;
                    ultimoDettaglio = $"{motore.Name}: tetto di {tetto} caratteri, {spesi} gia' spesi, "
                                      + $"questo giro ne chiede {caratteri}";
                    continue;
                }
            }

            var tentativo = await motore.TranslateAsync(testi, sourceLang, targetLang, ct).ConfigureAwait(false);
            if (tentativo.Outcome == TranslationOutcome.Ok) { riuscito = tentativo; break; }

            // 🔴 T-045 (revisione del 13 settembre 2026): un lotto successivo al primo è fallito, ma quelli prima sono
            // partiti e sono pagati. Si registrano come spesa scartata — le traduzioni non tornano intere — o il
            // tetto del motore non li vede e un 400 deterministico li fa ripagare a ogni giro.
            if (tentativo.BilledChars > 0)
                await _memoria.RegistraSpesaAsync(
                    motore.Name, sourceLang, targetLang, tentativo.BilledChars, tentativo.BilledTexts,
                    tentativo.BilledTexts, tentativo.BilledChars, DateTime.UtcNow, kind, ct).ConfigureAwait(false);

            // Qualunque esito diverso da Ok fa passare al motore dopo. Anche AuthFailed: una chiave
            // sbagliata vuole una persona, ma nel frattempo il documento si traduce lo stesso, e il
            // rapporto porta il motivo.
            ultimoEsito = tentativo.Outcome;
            ultimoDettaglio = $"{motore.Name}: {tentativo.Detail}";
        }

        if (riuscito is null)
            // ⚠️ Nessuno strike: il motore non ha risposto, quindi non ha reso rotto niente. Contarlo come
            // tentativo fallito del SEGMENTO vorrebbe dire condannare frasi sane per un disservizio di rete
            // — e bastarebbero tre quarti d'ora di Azure giù per fermare mezzo corpus.
            return new TranslationFillReport(
                segmenti.Count, giaInMemoria, 0, aMano, 0, ultimoEsito, ultimoDettaglio,
                InQuarantena: inQuarantena);

        // ⚠️ Chi ha tradotto DAVVERO, non chi e' stato chiamato per primo: la voce in memoria e il contatore
        // dei caratteri appartengono a lui, o il tetto di un motore verrebbe consumato dal lavoro dell'altro.
        var motoreUsato = riuscito.Engine ?? _catena[0].Name;

        // ---- Ripristino, e chi non torna intero si butta. ----
        // ⚠️ Le identiche NON vanno nel mucchio del motore: verrebbero salvate col suo nome, e i loro
        // caratteri conterebbero nel suo tetto — caratteri che non ha mai speso. Il tetto è una difesa vera,
        // e una difesa tarata su una misura falsa non difende.
        var buone = new List<(string, string)>();
        var scartati = 0;
        // I caratteri di quel che si butta: gia' spesi, e il giro dopo si rispendono.
        var caratteriScartati = 0L;
        // ⚠️ E QUALI sono: il testo ce l'abbiamo qui, e senza portarlo fuori l'avviso resta inservibile.
        var rotti = new List<string>();
        // Gli stessi, nella forma che il freno registra: impronta, testo e caratteri buttati.
        var tentativiRotti = new List<TentativoRotto>();
        for (var i = 0; i < daSpedire.Count; i++)
        {
            var (originale, protetto) = daSpedire[i];
            // ⚠️ Il ripristino COMPLETO, non solo i segnaposto: i marcatori di elenco sono stati tolti prima di
            // spedire, e senza rimetterli la traduzione di un elenco tornerebbe un capoverso qualunque.
            if (TextProtector.TryRestore(riuscito.Texts![i], protetto, out var tradotto))
                // ⚠️ Il grassetto si ripara PRIMA di salvare, non alla resa: quel che entra in memoria è
                // quello che leggeranno tutti finché una persona non lo corregge.
                buone.Add((originale, TranslationText.RiparaGrassetto(originale, tradotto)));
            else
            {
                // Una frase a cui manca il callsign e' PEGGIO della frase non tradotta: sembra giusta e non
                // lo e'. Non si salva, cosi' il giro dopo ci riprova.
                // ⚠️ E siccome ci riprova ogni quarto d'ora, questi caratteri si ripagano ogni volta senza
                // comparire da nessuna parte: qui si contano almeno, o la perdita resta invisibile.
                // ⚠️ E dalla terza volta non ci riprova più: il guasto è deterministico (§A64.3), quindi
                // «il giro dopo ci riprova» era una speranza che costava 170 506 caratteri in cinque
                // giorni. Vedi TranslationQuarantine.
                scartati++;
                caratteriScartati += protetto.Text.Length;
                rotti.Add(originale);
                tentativiRotti.Add(new TentativoRotto(impronte[originale], originale, protetto.Text.Length));
            }
        }

        // ⚠️ La spesa si registra PRIMA di salvare le buone, e comprende anche le rotte: sono partite, e
        // sono state pagate. È tutta la ragione per cui questo registro esiste invece di dedurre la spesa da
        // quel che è rimasto in memoria — le rotte in memoria non ci arrivano mai.
        await _memoria.RegistraSpesaAsync(
            motoreUsato, sourceLang, targetLang, caratteri, daSpedire.Count, scartati, caratteriScartati,
            DateTime.UtcNow, kind, ct).ConfigureAwait(false);

        var scritte = buone.Count == 0
            ? 0
            : await _memoria.SaveMachineAsync(sourceLang, targetLang, motoreUsato, buone, ct).ConfigureAwait(false);

        if (identiche.Count > 0)
            scritte += await _memoria.SaveMachineAsync(sourceLang, targetLang, "nessuno", identiche, ct)
                .ConfigureAwait(false);

        // ---- Il freno: si impara da com'è andata. ----
        var adesso = DateTime.UtcNow;

        IReadOnlyList<string> appenaFermati = Array.Empty<string>();
        if (tentativiRotti.Count > 0)
            appenaFermati = await _quarantena
                .SegnaRottiAsync(sourceLang, targetLang, tentativiRotti, motoreUsato, adesso, ct)
                .ConfigureAwait(false);

        // ⚠️ E chi è tornato INTERO si perdona: un ripristino può fallire una volta per un motivo
        // passeggero, e tre incidenti sparsi in tre mesi non sono un segmento irrecuperabile. Si chiede al
        // database solo per chi aveva davvero una storia — il giro normale non ne ha nessuna, e qui non
        // deve pagare niente.
        var risanati = buone
            .Select(b => impronte[b.Item1])
            .Where(strike.ContainsKey)
            .ToList();
        if (risanati.Count > 0)
            await _quarantena.DimenticaAsync(sourceLang, targetLang, risanati, ct).ConfigureAwait(false);

        return new TranslationFillReport(
            segmenti.Count, giaInMemoria, scritte, aMano, scartati, TranslationOutcome.Ok, null, motoreUsato,
            caratteriScartati, rotti, inQuarantena, appenaFermati);
    }
}
