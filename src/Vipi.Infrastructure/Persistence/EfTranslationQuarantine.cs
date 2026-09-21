using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Il freno dei segmenti irrecuperabili, su database (vedi <see cref="TranslationQuarantine"/>).
/// </summary>
public sealed class EfTranslationQuarantine : ITranslationQuarantine
{
    private readonly VipiDbContext _db;
    public EfTranslationQuarantine(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<string, int>> StrikeAsync(
        string sourceLang, string targetLang, IReadOnlyCollection<string> hashes, CancellationToken ct = default)
    {
        // ⚠️ L'uscita a vuoto non è cosmesi: il giro normale non ha niente in quarantena, e senza questa
        // riga pagherebbe una query a ogni quarto d'ora per sentirsi dire «niente».
        if (hashes.Count == 0)
            return new Dictionary<string, int>(StringComparer.Ordinal);

        var righe = await _db.TranslationQuarantines.AsNoTracking()
            .Where(q => q.SourceLang == sourceLang && q.TargetLang == targetLang && hashes.Contains(q.SourceHash))
            .Select(q => new { q.SourceHash, q.Strikes })
            .ToListAsync(ct).ConfigureAwait(false);

        return righe.ToDictionary(r => r.SourceHash, r => r.Strikes, StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<string>> SegnaRottiAsync(
        string sourceLang, string targetLang, IReadOnlyList<TentativoRotto> rotti, string? engine,
        DateTime nowUtc, CancellationToken ct = default)
    {
        if (rotti.Count == 0) return Array.Empty<string>();

        // ⚠️ Lo stesso segmento due volte nello stesso elenco sarebbe due righe con la stessa terna, cioè
        // l'indice unico che salta. Non dovrebbe succedere — il giro lavora su segmenti distinti — ma «non
        // dovrebbe» non è una difesa: qui si sommano, e restano un tentativo solo.
        var perImpronta = new Dictionary<string, TentativoRotto>(StringComparer.Ordinal);
        foreach (var r in rotti)
            perImpronta[r.Hash] = perImpronta.TryGetValue(r.Hash, out var gia)
                ? gia with { Characters = gia.Characters + r.Characters }
                : r;

        var impronte = perImpronta.Keys.ToList();
        var esistenti = await _db.TranslationQuarantines
            .Where(q => q.SourceLang == sourceLang && q.TargetLang == targetLang && impronte.Contains(q.SourceHash))
            .ToListAsync(ct).ConfigureAwait(false);

        var appenaFermati = new List<string>();

        foreach (var riga in esistenti)
        {
            if (!perImpronta.TryGetValue(riga.SourceHash, out var tentativo)) continue;

            // 🔴 Il passaggio di stato si guarda PRIMA di incrementare: chi era già sotto soglia e la
            // raggiunge adesso è l'unico che merita un avviso. Guardandolo dopo, un segmento già fermo da
            // settimane rientrerebbe nell'elenco a ogni giro — e l'avviso «non parte più» tornerebbe a
            // essere le novantasei righe al giorno che questa tabella esiste per spegnere.
            var eraSotto = riga.Strikes < TranslationQuarantena.Soglia;

            riga.Strikes++;
            riga.CharactersWasted += tentativo.Characters;
            riga.Engine = engine;
            riga.LastUtc = nowUtc;
            // ⚠️ Il testo si riscrive: la normalizzazione può essere cambiata fra una versione e l'altra, e
            // una riga che mostra il testo vecchio manda a cercare una frase che nel documento non c'è più.
            riga.SourceText = tentativo.SourceText;

            if (eraSotto && riga.Strikes >= TranslationQuarantena.Soglia)
                appenaFermati.Add(tentativo.SourceText);
        }

        var giaVisti = esistenti.Select(r => r.SourceHash).ToHashSet(StringComparer.Ordinal);
        foreach (var (impronta, tentativo) in perImpronta)
        {
            if (giaVisti.Contains(impronta)) continue;

            var nuova = new TranslationQuarantine
            {
                SourceLang = sourceLang,
                TargetLang = targetLang,
                SourceHash = impronta,
                SourceText = tentativo.SourceText,
                Strikes = 1,
                CharactersWasted = tentativo.Characters,
                Engine = engine,
                FirstUtc = nowUtc,
                LastUtc = nowUtc,
            };
            _db.TranslationQuarantines.Add(nuova);

            // Con soglia 1 la prima rottura è già la condanna: l'avviso deve uscire lo stesso.
            if (nuova.Strikes >= TranslationQuarantena.Soglia) appenaFermati.Add(tentativo.SourceText);
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return appenaFermati;
    }

    public async Task DimenticaAsync(
        string sourceLang, string targetLang, IReadOnlyCollection<string> hashes, CancellationToken ct = default)
    {
        if (hashes.Count == 0) return;

        var righe = await _db.TranslationQuarantines
            .Where(q => q.SourceLang == sourceLang && q.TargetLang == targetLang && hashes.Contains(q.SourceHash))
            .ToListAsync(ct).ConfigureAwait(false);

        if (righe.Count == 0) return;

        _db.TranslationQuarantines.RemoveRange(righe);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SegmentoInQuarantena>> FermiAsync(
        string sourceLang, string targetLang, CancellationToken ct = default) =>
        await _db.TranslationQuarantines.AsNoTracking()
            .Where(q => q.SourceLang == sourceLang && q.TargetLang == targetLang
                        && q.Strikes >= TranslationQuarantena.Soglia)
            // Dal più costoso: chi deve scrivere le rese a mano comincia da dove si perde di più.
            .OrderByDescending(q => q.CharactersWasted)
            .Select(q => new SegmentoInQuarantena(q.SourceText, q.Strikes, q.CharactersWasted, q.Engine, q.LastUtc))
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task<int> SvuotaAsync(string sourceLang, string targetLang, CancellationToken ct = default)
    {
        var righe = await _db.TranslationQuarantines
            .Where(q => q.SourceLang == sourceLang && q.TargetLang == targetLang)
            .ToListAsync(ct).ConfigureAwait(false);

        if (righe.Count == 0) return 0;

        _db.TranslationQuarantines.RemoveRange(righe);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return righe.Count;
    }
}
