using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Il glossario di fraseologia su database (<c>lavori-aperti §Q3</c>).
/// </summary>
public sealed class EfGlossaryStore : IGlossaryStore
{
    private readonly VipiDbContext _db;
    public EfGlossaryStore(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<GlossaryTerm>> ListAsync(
        string sourceLang, string targetLang, string? cerca = null, CancellationToken ct = default)
    {
        var q = _db.GlossaryTerms.AsNoTracking()
            .Where(t => t.SourceLang == sourceLang && t.TargetLang == targetLang);

        // ⚠️ Si cerca nei DUE lati. Chi cura il glossario ricorda a volte la formula e a volte come l'ha
        // resa, e una ricerca che guardasse solo la sorgente direbbe «non c'è» di una voce che c'è.
        var ago = (cerca ?? "").Trim().ToLowerInvariant();
        if (ago.Length > 0)
            q = q.Where(t => t.SourceText.ToLower().Contains(ago) || t.TargetText.ToLower().Contains(ago));

        return await q
            // Le più recenti in cima: chi apre la pagina vuole rivedere quello che ha appena scritto.
            .OrderByDescending(t => t.UpdatedUtc ?? t.CreatedUtc)
            .ThenBy(t => t.SourceKey)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> UpsertAsync(
        string sourceLang, string targetLang, string sourceText, string targetText,
        int? userId, CancellationToken ct = default)
    {
        var chiave = Chiave(sourceText);

        // ⚠️ Si cerca per SourceKey e non per SourceText: la chiave è la frase senza distinzione di
        // maiuscole, e cercare per il testo così com'è stato scritto creerebbe una seconda riga per
        // «Riporta sottovento» — due rese della stessa frase, e a decidere quale vince sarebbe l'ordine
        // della query.
        var riga = await _db.GlossaryTerms
            .FirstOrDefaultAsync(
                t => t.SourceLang == sourceLang && t.TargetLang == targetLang && t.SourceKey == chiave, ct)
            .ConfigureAwait(false);

        var nuova = riga is null;
        if (riga is null)
        {
            riga = new GlossaryTerm
            {
                SourceLang = sourceLang,
                TargetLang = targetLang,
                CreatedUtc = DateTime.UtcNow,
            };
            _db.GlossaryTerms.Add(riga);
        }

        riga.SourceText = sourceText.Trim();
        riga.SourceKey = chiave;
        riga.TargetText = targetText.Trim();

        // ⚠️ Solo quando c'è davvero una persona. Il seme passa null, e la riga resta riconoscibile come
        // «contenuto di partenza»: la pagina lo mostra, e senza questa distinzione chi cura il glossario non
        // saprebbe dire che cosa ha scelto lui e che cosa ha trovato lì.
        if (userId is not null)
        {
            riga.UpdatedUtc = DateTime.UtcNow;
            riga.UpdatedByUserId = userId;
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return nuova;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var riga = await _db.GlossaryTerms.FirstOrDefaultAsync(t => t.Id == id, ct).ConfigureAwait(false);
        if (riga is null) return false;

        _db.GlossaryTerms.Remove(riga);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    public Task<int> ContaAsync(string sourceLang, string targetLang, CancellationToken ct = default) =>
        _db.GlossaryTerms.AsNoTracking()
            .CountAsync(t => t.SourceLang == sourceLang && t.TargetLang == targetLang, ct);

    /// <summary>La forma della colonna che porta l'indice unico. ⚠️ <c>Invariant</c> e non la cultura
    /// corrente: in turco «I» minuscola non è «i», e una voce scritta su un server con quella cultura
    /// finirebbe su una chiave che nessun altro server ritrova.</summary>
    private static string Chiave(string sourceText) => sourceText.Trim().ToLowerInvariant();
}
