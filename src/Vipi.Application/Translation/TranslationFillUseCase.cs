using Vipi.Application.Abstractions;

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
/// <param name="CaratteriScartati">I caratteri spesi per i segmenti tornati rotti: <b>pagati e buttati</b>.
/// ⚠️ Non entrano nel conto della spesa (quello si deduce da quel che è rimasto in memoria, e questi in
/// memoria non ci vanno) e il giro dopo si rispediscono. Finché sono zero non c'è niente da dire; quando
/// non lo sono, questo numero è l'unico posto in cui la perdita si vede. Vedi lavori-aperti §Q16.</param>
/// <param name="Motore">Chi ha tradotto davvero. Con una catena non e' scontato che sia il primo: se Azure
/// ha finito la quota, qui c'e' scritto «deepl», ed e' l'informazione che dice all'amministratore che il
/// primario e' fermo <b>senza</b> che il servizio si sia fermato con lui.</param>
public sealed record TranslationFillReport(
    int Segmenti, int GiaInMemoria, int Tradotti, int DaTradurreAMano, int Scartati,
    TranslationOutcome Esito, string? Dettaglio = null, string? Motore = null, long CaratteriScartati = 0)
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
    private readonly IReadOnlyList<ITranslationEngine> _catena;
    private readonly TextProtector _protettore;
    private readonly TranslationOptions _opt;

    /// <param name="motori">I motori <b>in ordine di preferenza</b>. Il primo che risponde vince.</param>
    public TranslationFillUseCase(
        ITranslatableCorpus corpus, ITranslationMemory memoria, IEnumerable<ITranslationEngine> motori,
        TextProtector protettore, TranslationOptions opt)
    {
        _corpus = corpus;
        _memoria = memoria;
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

    public async Task<TranslationFillReport> EseguiAsync(
        string sourceLang, string targetLang, CancellationToken ct = default)
    {
        var segmenti = (await _corpus.SegmentiAsync(sourceLang, ct).ConfigureAwait(false))
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

        // ---- Cancello 1: i dati personali. Prima di tutto, budget compreso. ----
        var daSpedire = new List<(string Originale, ProtectedText Protetto)>();

        // I segmenti che non hanno niente da tradurre: la loro «traduzione» è se stessi.
        var identiche = new List<(string, string)>();
        var aMano = 0;
        foreach (var s in mancanti)
        {
            var protetto = _protettore.Protect(s);
            if (!protetto.Safe) { aMano++; continue; }

            // ⚠️ Del testo protetto non resta che segnaposto: la «traduzione» è il testo stesso, e si scrive
            // in memoria senza chiamare nessuno. Vale per le celle che sono solo un identificatore — un
            // punto («MARTE»), un fix («CHI»), un callsign — e senza questo passaggio partirebbero a ogni
            // giro per tornare cambiate e farsi scartare.
            if (TextProtector.SoloSegnaposti(protetto.Text))
            {
                identiche.Add((s, TranslationText.Normalize(s)));
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
                segmenti.Count, giaInMemoria, soleIdentiche, aMano, 0, TranslationOutcome.Ok, null, "nessuno");
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
                var spesi = await _memoria.CaratteriSpesiStimatiAsync(motore.Name, ct).ConfigureAwait(false);
                if (spesi + caratteri > tetto)
                {
                    ultimoEsito = TranslationOutcome.QuotaExceeded;
                    ultimoDettaglio = $"{motore.Name}: tetto di {tetto} caratteri, stimati {spesi} gia' spesi, "
                                      + $"questo giro ne chiede {caratteri}";
                    continue;
                }
            }

            var tentativo = await motore.TranslateAsync(testi, sourceLang, targetLang, ct).ConfigureAwait(false);
            if (tentativo.Outcome == TranslationOutcome.Ok) { riuscito = tentativo; break; }

            // Qualunque esito diverso da Ok fa passare al motore dopo. Anche AuthFailed: una chiave
            // sbagliata vuole una persona, ma nel frattempo il documento si traduce lo stesso, e il
            // rapporto porta il motivo.
            ultimoEsito = tentativo.Outcome;
            ultimoDettaglio = $"{motore.Name}: {tentativo.Detail}";
        }

        if (riuscito is null)
            return new TranslationFillReport(segmenti.Count, giaInMemoria, 0, aMano, 0, ultimoEsito, ultimoDettaglio);

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
        for (var i = 0; i < daSpedire.Count; i++)
        {
            var (originale, protetto) = daSpedire[i];
            if (TextProtector.TryRestore(riuscito.Texts![i], protetto.Tokens, out var tradotto))
                // ⚠️ Il grassetto si ripara PRIMA di salvare, non alla resa: quel che entra in memoria è
                // quello che leggeranno tutti finché una persona non lo corregge.
                buone.Add((originale, TranslationText.RiparaGrassetto(originale, tradotto)));
            else
            {
                // Una frase a cui manca il callsign e' PEGGIO della frase non tradotta: sembra giusta e non
                // lo e'. Non si salva, cosi' il giro dopo ci riprova.
                // ⚠️ E siccome ci riprova ogni quarto d'ora, questi caratteri si ripagano ogni volta senza
                // comparire da nessuna parte: qui si contano almeno, o la perdita resta invisibile.
                scartati++;
                caratteriScartati += protetto.Text.Length;
            }
        }

        var scritte = buone.Count == 0
            ? 0
            : await _memoria.SaveMachineAsync(sourceLang, targetLang, motoreUsato, buone, ct).ConfigureAwait(false);

        if (identiche.Count > 0)
            scritte += await _memoria.SaveMachineAsync(sourceLang, targetLang, "nessuno", identiche, ct)
                .ConfigureAwait(false);

        return new TranslationFillReport(
            segmenti.Count, giaInMemoria, scritte, aMano, scartati, TranslationOutcome.Ok, null, motoreUsato,
            caratteriScartati);
    }
}
