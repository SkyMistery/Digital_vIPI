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
        string sourceLang, string targetLang, bool soloDaRileggere, int limite, CancellationToken ct = default)
    {
        var q = _db.TranslationUnits.AsNoTracking()
            .Where(u => u.SourceLang == sourceLang && u.TargetLang == targetLang);

        if (soloDaRileggere)
            q = q.Where(u => u.ReviewedUtc == null);

        // ⚠️ Le mai riviste PRIME, non le piu' recenti: chi apre la pagina di revisione vuole vedere cio'
        // che nessuno ha ancora guardato. Ordinare per data di inserimento gli metterebbe in cima le
        // ultime tradotte, che non sono ne' le piu' urgenti ne' le piu' lette.
        return await q
            .OrderBy(u => u.ReviewedUtc == null ? 0 : 1)
            .ThenBy(u => u.Id)
            .Take(limite)
            .Select(u => new TranslationReviewRow(
                u.Id, u.SourceText, u.TargetText, u.Origin, u.ReviewedUtc, u.ReviewedByUserId))
            .ToListAsync(ct).ConfigureAwait(false);
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
    public async Task<int> DocumentiToccatiAsync(string sourceText, CancellationToken ct = default)
    {
        if (TranslationText.Normalize(sourceText).Length == 0) return 0;
        var impronta = TranslationText.Hash(sourceText);

        // Due letture in blocco, come le fa il corpus: non una query per documento.
        var blocchi = await _db.ContentBlocks.AsNoTracking()
            .Where(b => b.Body != null || b.BodyJson != null)
            .Select(b => new { b.Body, b.BodyJson, b.DocumentVersion!.DocumentId })
            .ToListAsync(ct).ConfigureAwait(false);

        var titoli = await _db.DocumentSections.AsNoTracking()
            .Select(s => new { s.Title, s.DocumentVersion!.DocumentId })
            .ToListAsync(ct).ConfigureAwait(false);

        var documenti = new HashSet<int>();

        foreach (var b in blocchi)
        {
            if (documenti.Contains(b.DocumentId)) continue;   // un documento si conta una volta sola
            if (Contiene(TextSegmenter.SplitProse(b.Body), impronta) ||
                Contiene(TextSegmenter.SplitJson(b.BodyJson), impronta))
                documenti.Add(b.DocumentId);
        }

        foreach (var t in titoli)
            if (!documenti.Contains(t.DocumentId) && TranslationText.Hash(t.Title) == impronta)
                documenti.Add(t.DocumentId);

        return documenti.Count;
    }

    /// <summary>Vero se uno di questi segmenti ha questa impronta. <c>Hash</c> normalizza da sé, quindi la
    /// grafia non conta — che è tutto il punto.</summary>
    private static bool Contiene(IEnumerable<string> segmenti, string impronta) =>
        segmenti.Any(s => TranslationText.Hash(s) == impronta);

    public async Task<long> CaratteriSpesiStimatiAsync(string engine, CancellationToken ct = default) =>
        await _db.TranslationUnits.AsNoTracking()
            .Where(u => u.Engine == engine && u.Origin == TranslationOrigin.Machine)
            .SumAsync(u => (long)u.SourceText.Length, ct).ConfigureAwait(false);
}
