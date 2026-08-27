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
public sealed record TranslationFillReport(
    int Segmenti, int GiaInMemoria, int Tradotti, int DaTradurreAMano, int Scartati,
    TranslationOutcome Esito, string? Dettaglio = null)
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
    private readonly ITranslationEngine _motore;
    private readonly TextProtector _protettore;
    private readonly TranslationOptions _opt;

    public TranslationFillUseCase(
        ITranslatableCorpus corpus, ITranslationMemory memoria, ITranslationEngine motore,
        TextProtector protettore, TranslationOptions opt)
    {
        _corpus = corpus;
        _memoria = memoria;
        _motore = motore;
        _protettore = protettore;
        _opt = opt;
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
        var aMano = 0;
        foreach (var s in mancanti)
        {
            var protetto = _protettore.Protect(s);
            if (!protetto.Safe) { aMano++; continue; }
            daSpedire.Add((s, protetto));
        }

        if (daSpedire.Count == 0)
            return new TranslationFillReport(segmenti.Count, giaInMemoria, 0, aMano, 0, TranslationOutcome.Ok);

        // ---- Cancello 2: il budget. ----
        // ⚠️ Serve perché la franchigia del motore può essere UNA TANTUM e non rinnovarsi: scoprire a cose
        // fatte che è finita costerebbe la funzione, non un giro. Si controlla PRIMA di spendere.
        var superato = await BudgetSuperatoAsync(daSpedire.Sum(d => d.Protetto.Text.Length), ct).ConfigureAwait(false);
        if (superato is not null)
            return new TranslationFillReport(segmenti.Count, giaInMemoria, 0, aMano, 0,
                TranslationOutcome.QuotaExceeded, superato);

        // ---- Il motore. ----
        var esito = await _motore
            .TranslateAsync(daSpedire.Select(d => d.Protetto.Text).ToList(), sourceLang, targetLang, ct)
            .ConfigureAwait(false);

        if (esito.Outcome != TranslationOutcome.Ok)
            return new TranslationFillReport(segmenti.Count, giaInMemoria, 0, aMano, 0, esito.Outcome, esito.Detail);

        // ---- Ripristino, e chi non torna intero si butta. ----
        var buone = new List<(string, string)>();
        var scartati = 0;
        for (var i = 0; i < daSpedire.Count; i++)
        {
            var (originale, protetto) = daSpedire[i];
            if (TextProtector.TryRestore(esito.Texts![i], protetto.Tokens, out var tradotto))
                buone.Add((originale, tradotto));
            else
                // Una frase a cui manca il callsign è PEGGIO della frase non tradotta: sembra giusta e non
                // lo è. Non si salva, così il giro dopo ci riprova.
                scartati++;
        }

        var scritte = buone.Count == 0
            ? 0
            : await _memoria.SaveMachineAsync(sourceLang, targetLang, _motore.Name, buone, ct).ConfigureAwait(false);

        return new TranslationFillReport(segmenti.Count, giaInMemoria, scritte, aMano, scartati, TranslationOutcome.Ok);
    }

    /// <summary>
    /// Il tetto di spesa, se ne è stato messo uno. Torna il motivo se questo giro lo sfonderebbe, altrimenti
    /// null.
    /// <para>⚠️ La stima è locale e per difetto (non conta i tentativi falliti). Il dato autorevole lo dà
    /// il motore, ed è quello che l'amministratore deve guardare: questa guardia serve a non partire, non a
    /// fare la contabilità.</para>
    /// </summary>
    private async Task<string?> BudgetSuperatoAsync(int caratteriDiQuestoGiro, CancellationToken ct)
    {
        var tetto = _opt.MaxCaratteriTotali;
        if (tetto <= 0) return null;   // nessun tetto configurato

        var spesi = await _memoria.CaratteriSpesiStimatiAsync(_motore.Name, ct).ConfigureAwait(false);
        if (spesi + caratteriDiQuestoGiro <= tetto) return null;

        return $"tetto di {tetto} caratteri: stimati {spesi} già spesi, questo giro ne chiede {caratteriDiQuestoGiro}";
    }
}
