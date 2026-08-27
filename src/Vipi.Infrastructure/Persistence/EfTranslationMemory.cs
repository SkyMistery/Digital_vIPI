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

    public async Task<int> DocumentiToccatiAsync(string sourceText, CancellationToken ct = default)
    {
        // Il numero che si mostra a chi corregge PRIMA che salvi. Si conta sui documenti, non sui blocchi:
        // «tocca 4 blocchi» non dice niente a nessuno, «tocca 3 documenti» sì.
        var testo = TranslationText.Normalize(sourceText);
        if (testo.Length == 0) return 0;

        // ⚠️ Confronto per CONTENUTO e non per impronta: l'impronta ce l'ha solo la memoria, i blocchi no.
        // Il testo del blocco può avere grafia diversa (a-capo Windows, apostrofo tipografico), quindi si
        // filtra grossolanamente sul database e si conferma in memoria con la normalizzazione.
        var candidati = await _db.ContentBlocks.AsNoTracking()
            .Where(b => b.Body != null && b.Body.Contains(testo))
            .Select(b => new { b.DocumentVersion!.DocumentId })
            .ToListAsync(ct).ConfigureAwait(false);

        return candidati.Select(c => c.DocumentId).Distinct().Count();
    }

    public async Task<long> CaratteriSpesiStimatiAsync(string engine, CancellationToken ct = default) =>
        await _db.TranslationUnits.AsNoTracking()
            .Where(u => u.Engine == engine && u.Origin == TranslationOrigin.Machine)
            .SumAsync(u => (long)u.SourceText.Length, ct).ConfigureAwait(false);
}
