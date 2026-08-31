using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Translation;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// La memoria di traduzione su database (carta <c>docs/feature/2026-08-27-documenti-bilingue.md</c> §1).
/// </summary>
public sealed class EfTranslationMemory : ITranslationMemory
{
    private readonly VipiDbContext _db;
    public EfTranslationMemory(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<string, KnownTranslation>> LookupAsync(
        string sourceLang, string targetLang, IReadOnlyCollection<string> hashes, CancellationToken ct = default)
    {
        if (hashes.Count == 0)
            return new Dictionary<string, KnownTranslation>(StringComparer.Ordinal);

        var righe = await _db.TranslationUnits.AsNoTracking()
            .Where(u => u.SourceLang == sourceLang && u.TargetLang == targetLang && hashes.Contains(u.SourceHash))
            .Select(u => new { u.SourceHash, u.TargetText, u.Origin, u.ReviewedUtc })
            .ToListAsync(ct).ConfigureAwait(false);

        return righe.ToDictionary(
            r => r.SourceHash,
            r => new KnownTranslation(r.TargetText, r.Origin, r.ReviewedUtc is not null),
            StringComparer.Ordinal);
    }

    public async Task<int> SaveMachineAsync(
        string sourceLang, string targetLang, string engine,
        IReadOnlyList<(string SourceText, string TargetText)> tradotte, CancellationToken ct = default)
    {
        if (tradotte.Count == 0) return 0;

        var perImpronta = new Dictionary<string, (string Sorgente, string Bersaglio)>(StringComparer.Ordinal);
        foreach (var (sorgente, bersaglio) in tradotte)
            perImpronta[TranslationText.Hash(sorgente)] = (TranslationText.Normalize(sorgente), bersaglio);

        var impronte = perImpronta.Keys.ToList();
        var esistenti = await _db.TranslationUnits
            .Where(u => u.SourceLang == sourceLang && u.TargetLang == targetLang && impronte.Contains(u.SourceHash))
            .ToListAsync(ct).ConfigureAwait(false);

        var adesso = DateTime.UtcNow;
        var scritte = 0;

        foreach (var riga in esistenti)
        {
            // ⚠️ LA PROMESSA DELLA FUNZIONE: una correzione umana non si sovrascrive MAI dalla macchina,
            // nemmeno se il motore cambia versione. Una persona ha già deciso come si dice quella frase.
            if (riga.Origin == TranslationOrigin.Human) continue;
            if (!perImpronta.TryGetValue(riga.SourceHash, out var nuovo)) continue;
            riga.TargetText = nuovo.Bersaglio;
            riga.Engine = engine;
            riga.CreatedUtc = adesso;
            scritte++;
        }

        var giaVisti = esistenti.Select(r => r.SourceHash).ToHashSet(StringComparer.Ordinal);
        foreach (var (impronta, testi) in perImpronta)
        {
            if (giaVisti.Contains(impronta)) continue;
            _db.TranslationUnits.Add(new TranslationUnit
            {
                SourceLang = sourceLang,
                TargetLang = targetLang,
                SourceHash = impronta,
                SourceText = testi.Sorgente,
                TargetText = testi.Bersaglio,
                Origin = TranslationOrigin.Machine,
                Engine = engine,
                CreatedUtc = adesso,
            });
            scritte++;
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return scritte;
    }

    public async Task SaveHumanAsync(
        string sourceLang, string targetLang, string sourceText, string targetText,
        int reviewerUserId, CancellationToken ct = default)
    {
        var impronta = TranslationText.Hash(sourceText);
        var adesso = DateTime.UtcNow;

        var riga = await _db.TranslationUnits
            .FirstOrDefaultAsync(u => u.SourceLang == sourceLang && u.TargetLang == targetLang
                                      && u.SourceHash == impronta, ct).ConfigureAwait(false);

        if (riga is null)
        {
            // Una correzione può arrivare prima che la macchina abbia mai tradotto quella frase: è il caso
            // di un segmento che il cancello sui dati personali ha rifiutato, e che vuole una persona.
            riga = new TranslationUnit
            {
                SourceLang = sourceLang,
                TargetLang = targetLang,
                SourceHash = impronta,
                SourceText = TranslationText.Normalize(sourceText),
                CreatedUtc = adesso,
            };
            _db.TranslationUnits.Add(riga);
        }

        riga.TargetText = targetText;
        riga.Origin = TranslationOrigin.Human;
        riga.ReviewedUtc = adesso;
        riga.ReviewedByUserId = reviewerUserId;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TranslationReviewRow>> ListForReviewAsync(
        string sourceLang, string targetLang, bool soloDaRileggere, int limite,
        string? cerca = null, int salta = 0, CancellationToken ct = default) =>
        // ⚠️ Le mai riviste PRIME, non le piu' recenti: chi apre la pagina di revisione vuole vedere cio'
        // che nessuno ha ancora guardato. Ordinare per data di inserimento gli metterebbe in cima le
        // ultime tradotte, che non sono ne' le piu' urgenti ne' le piu' lette.
        //
        // ⚠️ L'ordinamento deve essere TOTALE, o il «carica altre» salta e ripete righe: `ThenBy(Id)` non
        // è cosmesi, è ciò che rende `Skip` ripetibile.
        await PerRevisione(sourceLang, targetLang, soloDaRileggere, cerca)
            .OrderBy(u => u.ReviewedUtc == null ? 0 : 1)
            .ThenBy(u => u.Id)
            .Skip(salta)
            .Take(limite)
            .Select(u => new TranslationReviewRow(
                u.Id, u.SourceText, u.TargetText, u.Origin, u.ReviewedUtc, u.ReviewedByUserId))
            .ToListAsync(ct).ConfigureAwait(false);

    public Task<int> ContaPerRevisioneAsync(
        string sourceLang, string targetLang, bool soloDaRileggere, string? cerca = null,
        CancellationToken ct = default) =>
        PerRevisione(sourceLang, targetLang, soloDaRileggere, cerca).CountAsync(ct);

    /// <summary>
    /// La stessa domanda per l'elenco e per il conteggio: due filtri scritti due volte divergono, e il
    /// «N di M» direbbe un M che non è il numero delle righe che si possono scorrere.
    ///
    /// <para>⚠️ Si cerca nei <b>due</b> lati — la frase e la sua resa: chi rivede ricorda a volte l'una e a
    /// volte l'altra. E si cerca <b>sul database</b>: la pagina carica un lotto per volta, e un filtro
    /// applicato al lotto direbbe «non c'è» di una frase che sta alla riga 101.</para>
    /// </summary>
    private IQueryable<TranslationUnit> PerRevisione(
        string sourceLang, string targetLang, bool soloDaRileggere, string? cerca)
    {
        var q = _db.TranslationUnits.AsNoTracking()
            .Where(u => u.SourceLang == sourceLang && u.TargetLang == targetLang);

        if (soloDaRileggere)
            q = q.Where(u => u.ReviewedUtc == null);

        var ago = (cerca ?? "").Trim().ToLowerInvariant();
        if (ago.Length > 0)
            q = q.Where(u => u.SourceText.ToLower().Contains(ago) || u.TargetText.ToLower().Contains(ago));

        return q;
    }

    public async Task<IReadOnlyDictionary<string, string>> LoadAllAsync(
        string sourceLang, string targetLang, CancellationToken ct = default)
    {
        var righe = await _db.TranslationUnits.AsNoTracking()
            .Where(u => u.SourceLang == sourceLang && u.TargetLang == targetLang)
            .Select(u => new { u.SourceHash, u.TargetText })
            .ToListAsync(ct).ConfigureAwait(false);

        return righe.ToDictionary(r => r.SourceHash, r => r.TargetText, StringComparer.Ordinal);
    }

    public async Task<IReadOnlySet<string>> LoadHumanHashesAsync(
        string sourceLang, string targetLang, CancellationToken ct = default) =>
        (await _db.TranslationUnits.AsNoTracking()
            .Where(u => u.SourceLang == sourceLang && u.TargetLang == targetLang
                        && u.Origin == TranslationOrigin.Human)
            .Select(u => u.SourceHash)
            .ToListAsync(ct).ConfigureAwait(false))
        .ToHashSet(StringComparer.Ordinal);

    public async Task<(int Totale, int DaRileggere)> ContaAsync(
        string sourceLang, string targetLang, CancellationToken ct = default)
    {
        // Un giro solo sul database: due Count separati sarebbero due passate sulla stessa tabella.
        var righe = await _db.TranslationUnits.AsNoTracking()
            .Where(u => u.SourceLang == sourceLang && u.TargetLang == targetLang)
            .GroupBy(u => u.ReviewedUtc == null)
            .Select(g => new { DaRileggere = g.Key, Quante = g.Count() })
            .ToListAsync(ct).ConfigureAwait(false);

        return (righe.Sum(r => r.Quante), righe.Where(r => r.DaRileggere).Sum(r => r.Quante));
    }

    /// <summary>
    /// Quanti documenti contengono questa frase: il numero che si mostra a chi corregge <b>prima</b> che
    /// salvi. Si conta sui documenti e non sui blocchi — «tocca 4 blocchi» non dice niente a nessuno,
    /// «tocca 3 documenti» sì.
    ///
    /// <para>
    /// ⚠️ <b>Si confronta l'IMPRONTA, e i segmenti si tagliano come li taglia il corpus.</b> Fino al 28
    /// agosto 2026 questo metodo faceva un <c>Body.Contains(testo)</c> e guardava <b>solo</b>
    /// <c>ContentBlock.Body</c>: una frase che sta in un <b>titolo di sezione</b> o in una <b>cella di
    /// tabella</b> (<c>BodyJson</c>) contava <b>zero</b>. E il pannello mostra l'avviso solo sopra il primo
    /// documento, quindi proprio le correzioni più diffuse passavano <b>mute</b>: chi correggeva salvava
    /// credendo di toccare il documento che aveva davanti.
    /// </para>
    ///
    /// <para>
    /// ⚠️ Il commento che stava qui prometteva una «conferma in memoria con la normalizzazione» che nel
    /// codice <b>non c'era</b>: il <c>Contains</c> era l'ultima parola. Quindi il conto sbagliava anche
    /// dall'altro lato — un corpo con l'apostrofo tipografico o l'a-capo di Windows non corrispondeva al
    /// testo normalizzato che arriva dalla memoria, e quel documento non si contava.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>Si legge tutto e si conta in memoria</b>, senza <c>LIKE</c>. Un prefiltro sul database non può
    /// essere corretto — la normalizzazione avviene <i>dopo</i>, e quel che il database confronta è la
    /// grafia — quindi sarebbe un filtro che scarta risposte giuste. Il corpus editoriale è stato
    /// <b>misurato</b>: 499 campi per 23 344 caratteri in tutto il <c>vipi.db</c> reale, e questo giro parte
    /// solo quando una persona apre <b>una</b> riga del pannello di revisione.
    /// </para>
    ///
    /// <para>
    /// ⚠️ Si guardano <b>tutte le versioni</b>, bozze e archiviate comprese, ed è la stessa portata di
    /// <c>EfTranslatableCorpus</c>: una frase entra in memoria se una qualunque versione la contiene, e il
    /// conto deve dire la stessa cosa che dice il corpus. Due portate diverse sulla stessa domanda sono due
    /// risposte diverse alla stessa domanda.
    /// </para>
    /// </summary>
    public async Task<int> DocumentiToccatiAsync(string sourceText, CancellationToken ct = default) =>
        // ⚠️ Poggia sulla stessa passata di <see cref="DoveSiUsanoAsync"/>, e non ne ha una sua: il numero
        // e l'elenco devono venire dallo stesso conto, o un giorno la pastiglia dirà «2» e il pannello
        // aprirà tre righe.
        (await DoveSiUsanoAsync(new[] { sourceText }, ct).ConfigureAwait(false))
            .TryGetValue(TranslationText.Hash(sourceText), out var usi) ? usi.Count : 0;

    /// <inheritdoc />
    /// <remarks>
    /// ⚠️ <b>Il corpus si legge UNA volta per tutte le frasi chieste.</b> È la stessa lettura che serviva a
    /// contare i documenti di una frase sola — 499 campi per 23 344 caratteri, misurati sul <c>vipi.db</c>
    /// reale — e ripeterla per ognuna delle cento righe a schermo sarebbe cento volte quel giro. È anche il
    /// motivo per cui la pastiglia col numero si può mostrare in elenco.
    ///
    /// <para>⚠️ Le chiavi tornate sono le <b>impronte</b>, non i testi: la stessa frase scritta con
    /// l'apostrofo tipografico è la stessa frase, ed è tutto il punto della memoria. Chi chiama ritrova la
    /// sua riga con <c>TranslationText.Hash</c>.</para>
    /// </remarks>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<UsoInDocumento>>> DoveSiUsanoAsync(
        IReadOnlyCollection<string> sourceTexts, CancellationToken ct = default)
    {
        var cercate = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in sourceTexts)
            if (TranslationText.Normalize(t).Length > 0)
                cercate.Add(TranslationText.Hash(t));

        // Le frasi vuote non si cercano, ma la chiave torna lo stesso: «zero» è una risposta.
        var esito = new Dictionary<string, Dictionary<int, UsoInDocumento>>(StringComparer.Ordinal);
        foreach (var impronta in cercate) esito[impronta] = new Dictionary<int, UsoInDocumento>();
        if (cercate.Count == 0) return Vuoto(esito);

        // Due letture in blocco, come le fa il corpus: non una query per documento. Il titolo del documento
        // arriva con loro — senza, servirebbe una terza query per dare un nome alle righe del pannello.
        var blocchi = await _db.ContentBlocks.AsNoTracking()
            .Where(b => b.Body != null || b.BodyJson != null)
            .Select(b => new
            {
                b.Body,
                b.BodyJson,
                b.DocumentVersion!.DocumentId,
                Titolo = b.DocumentVersion!.Document!.Title,
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var titoli = await _db.DocumentSections.AsNoTracking()
            .Select(s => new
            {
                s.Title,
                s.DocumentVersion!.DocumentId,
                Documento = s.DocumentVersion!.Document!.Title,
            })
            .ToListAsync(ct).ConfigureAwait(false);

        // ⚠️ Un documento compare UNA volta per frase, e con il primo posto in cui la si è trovata: la
        // domanda è «quali documenti tocco», non «quante volte».
        void Segna(string impronta, int documentId, string titolo, UsoDelTesto dove)
        {
            if (!esito.TryGetValue(impronta, out var perDocumento)) return;
            if (perDocumento.ContainsKey(documentId)) return;
            perDocumento[documentId] = new UsoInDocumento(documentId, titolo, dove);
        }

        foreach (var b in blocchi)
        {
            foreach (var seg in TextSegmenter.SplitProse(b.Body))
                Segna(TranslationText.Hash(seg), b.DocumentId, b.Titolo, UsoDelTesto.Prosa);
            foreach (var seg in TextSegmenter.SplitJson(b.BodyJson))
                Segna(TranslationText.Hash(seg), b.DocumentId, b.Titolo, UsoDelTesto.Tabella);
        }

        foreach (var t in titoli)
            Segna(TranslationText.Hash(t.Title), t.DocumentId, t.Documento, UsoDelTesto.Titolo);

        return Vuoto(esito);
    }

    /// <summary>Dai dizionari di lavoro alle liste che escono dalla porta, ordinate per titolo.</summary>
    private static IReadOnlyDictionary<string, IReadOnlyList<UsoInDocumento>> Vuoto(
        Dictionary<string, Dictionary<int, UsoInDocumento>> lavoro) =>
        lavoro.ToDictionary(
            r => r.Key,
            r => (IReadOnlyList<UsoInDocumento>)r.Value.Values
                .OrderBy(u => u.Titolo, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(u => u.DocumentId)
                .ToList(),
            StringComparer.Ordinal);

    public async Task<IReadOnlyList<FraseConFormula>> FrasiConLaFormulaAsync(
        string sourceLang, string targetLang, string formula, int limite, CancellationToken ct = default)
    {
        var ago = formula.Trim().ToLowerInvariant();
        if (ago.Length == 0) return Array.Empty<FraseConFormula>();

        return await _db.TranslationUnits.AsNoTracking()
            .Where(u => u.SourceLang == sourceLang && u.TargetLang == targetLang
                        && u.SourceText.ToLower().Contains(ago))
            .OrderByDescending(u => u.CreatedUtc)
            .ThenBy(u => u.Id)
            .Take(limite)
            .Select(u => new FraseConFormula(u.SourceText, u.TargetText, u.Origin))
            .ToListAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// ⚠️ <b>Una lettura sola, e il conto si fa in memoria.</b> Una query per formula sarebbe una query per
    /// riga del glossario; e un <c>LIKE</c> per ognuna, messe in <c>OR</c>, non direbbe comunque QUALE
    /// formula ha corrisposto. I testi sorgente di una coppia sono decine — misurati: 274 righe per una
    /// media di 77 caratteri — e stanno in una lista.
    /// </remarks>
    public async Task<IReadOnlyDictionary<string, int>> ContaFrasiPerFormuleAsync(
        string sourceLang, string targetLang, IReadOnlyCollection<string> formule,
        CancellationToken ct = default)
    {
        var esito = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in formule) esito[f] = 0;
        if (esito.Count == 0) return esito;

        var sorgenti = await _db.TranslationUnits.AsNoTracking()
            .Where(u => u.SourceLang == sourceLang && u.TargetLang == targetLang)
            .Select(u => u.SourceText)
            .ToListAsync(ct).ConfigureAwait(false);

        foreach (var testo in sorgenti)
            foreach (var f in formule)
                if (f.Trim().Length > 0 && testo.Contains(f.Trim(), StringComparison.OrdinalIgnoreCase))
                    esito[f]++;

        return esito;
    }

    /// <summary>
    /// I caratteri già spesi con questo motore: la somma dei testi sorgente delle voci che <b>lui</b> ha
    /// prodotto.
    ///
    /// <para>
    /// ⚠️ <b>Non si filtra più su <c>Origin</c>, ed è la correzione di una deriva vera.</b> Fino al 28
    /// agosto 2026 qui c'era anche <c>Origin == Machine</c>. Ma quando una persona corregge una resa,
    /// <c>SaveHumanAsync</c> ribalta <c>Origin</c> a <c>Human</c> e <b>lascia intatto <c>Engine</c></b>:
    /// quei caratteri erano stati spesi davvero, e sparivano dal conto. Più si revisionava, più il tetto
    /// si allargava — cioè la difesa si allentava proprio mentre il lavoro andava avanti.
    /// </para>
    ///
    /// <para>
    /// La colonna <c>Engine</c> è la domanda giusta: dice <b>chi ha tradotto</b>, e non cambia quando
    /// cambia chi ha l'ultima parola sul testo. Una riga nata da una correzione umana senza che nessun
    /// motore l'avesse mai tradotta ha <c>Engine</c> nullo, e non conta per nessuno: giusto, non è stata
    /// pagata.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>Resta una stima, e per una ragione che non si chiude qui</b>: i segmenti che il motore
    /// restituisce rotti (un segnaposto mangiato) vengono <b>pagati e non salvati</b>, quindi non entrano
    /// in questa somma — e il giro successivo li rispedisce. Vedi <c>docs/lavori-aperti.md</c> §Q16: quella
    /// parte vuole un contatore suo, non una somma dedotta da ciò che è rimasto in tabella.
    /// </para>
    /// </summary>
    public Task<int> ContaConLaFormulaAsync(
        string sourceLang, string targetLang, string formula, CancellationToken ct = default)
    {
        var ago = formula.Trim().ToLowerInvariant();
        if (ago.Length == 0) return Task.FromResult(0);

        return AutomaticheCon(sourceLang, targetLang, ago).AsNoTracking().CountAsync(ct);
    }

    public async Task<int> DimenticaAutomaticheConLaFormulaAsync(
        string sourceLang, string targetLang, string formula, CancellationToken ct = default)
    {
        var ago = formula.Trim().ToLowerInvariant();
        if (ago.Length == 0) return 0;

        var righe = await AutomaticheCon(sourceLang, targetLang, ago).ToListAsync(ct).ConfigureAwait(false);
        if (righe.Count == 0) return 0;

        // ⚠️ RemoveRange e non ExecuteDelete: il secondo scavalca il change-tracker e lo lascia convinto che
        // le righe ci siano ancora. È la regola già scritta per gli altri repository, e vale anche qui.
        _db.TranslationUnits.RemoveRange(righe);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return righe.Count;
    }

    /// <summary>
    /// Le voci automatiche di questa coppia che contengono la formula.
    ///
    /// <para>⚠️ <c>ToLower().Contains()</c> e non <c>Like</c> con le percentuali intorno: <c>Contains</c>
    /// diventa <c>instr</c> su SQLite e <c>LOCATE</c> su MySQL, che cercano una <b>sottostringa</b> — una
    /// formula che contenesse un <c>%</c> o un <c>_</c> con <c>Like</c> diventerebbe un carattere jolly, e
    /// cancellerebbe molto più di quel che il curatore ha chiesto.</para>
    /// <para>⚠️ Il <c>ToLower</c> su entrambi i lati perché il glossario cerca senza distinguere le
    /// maiuscole mentre i due database, ognuno a modo suo, distinguono: senza, «Riporta sottovento» a inizio
    /// frase resterebbe in memoria com'era e il documento non cambierebbe.</para>
    /// </summary>
    private IQueryable<TranslationUnit> AutomaticheCon(string sourceLang, string targetLang, string agoMinuscolo) =>
        _db.TranslationUnits
            .Where(u => u.SourceLang == sourceLang
                        && u.TargetLang == targetLang
                        && u.Origin == TranslationOrigin.Machine
                        && u.SourceText.ToLower().Contains(agoMinuscolo));

    public async Task<long> CaratteriSpesiAsync(string engine, CancellationToken ct = default) =>
        await _db.TranslationSpends.AsNoTracking()
            .Where(s => s.Engine == engine)
            .SumAsync(s => s.Characters, ct).ConfigureAwait(false);

    public async Task RegistraSpesaAsync(
        string engine, string sourceLang, string targetLang, long caratteri, int segmenti,
        int scartati, long caratteriScartati, DateTime nowUtc, CancellationToken ct = default)
    {
        _db.TranslationSpends.Add(new TranslationSpend
        {
            Engine = engine,
            Kind = TranslationSpendKind.Dispatch,
            SourceLang = sourceLang,
            TargetLang = targetLang,
            Characters = caratteri,
            Segments = segmenti,
            Discarded = scartati,
            DiscardedCharacters = caratteriScartati,
            AtUtc = nowUtc,
        });
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> FotografaSpesaPregressaAsync(
        IReadOnlyList<string> engines, DateTime nowUtc, CancellationToken ct = default)
    {
        // ⚠️ «Una volta sola per motore» si chiede al DATABASE, non a un flag in memoria: il giro gira in
        // un processo che si riavvia, e un flag ricomincerebbe da capo a ogni riavvio scrivendo una
        // fotografia in più — cioè gonfiando la spesa, che è il verso opposto ma altrettanto sbagliato.
        var gia = await _db.TranslationSpends.AsNoTracking()
            .Where(s => s.Kind == TranslationSpendKind.Baseline)
            .Select(s => s.Engine)
            .ToListAsync(ct).ConfigureAwait(false);

        var scritte = 0;
        foreach (var engine in engines.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (gia.Contains(engine, StringComparer.OrdinalIgnoreCase)) continue;

            // La sola misura disponibile per il passato: la somma dei sorgenti che quel motore ha tradotto.
            // ⚠️ È una STIMA per difetto — non conta quel che è tornato rotto, che è proprio ciò che il
            // registro esiste per vedere — e la riga lo dice, perché si chiama Baseline.
            var stima = await _db.TranslationUnits.AsNoTracking()
                .Where(u => u.Engine == engine)
                .SumAsync(u => (long)u.SourceText.Length, ct).ConfigureAwait(false);

            _db.TranslationSpends.Add(new TranslationSpend
            {
                Engine = engine,
                Kind = TranslationSpendKind.Baseline,
                Characters = stima,
                AtUtc = nowUtc,
            });
            scritte++;
        }

        if (scritte > 0) await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return scritte;
    }
}
