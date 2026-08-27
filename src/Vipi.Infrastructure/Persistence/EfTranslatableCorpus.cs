using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Translation;
using Vipi.Domain;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Da dove escono i testi da tradurre: i campi editoriali dei documenti in una data lingua sorgente, tagliati
/// in segmenti (carta <c>docs/feature/2026-08-27-documenti-bilingue.md</c> §2).
///
/// <para>
/// ⚠️ <b>Nessuna coda, per scelta.</b> «Che cosa manca» è la differenza fra questi segmenti e le impronte già
/// in memoria: si calcola, non si registra. Una tabella di coda sarebbe un secondo posto dove sapere che una
/// frase esiste, e i due si sarebbero disallineati al primo documento eliminato o ripristinato. Così il giro
/// è auto-riparante.
/// </para>
///
/// <para>
/// Il conto regge perché è stato <b>misurato</b>, non supposto: 499 campi per 23.344 caratteri in tutto il
/// <c>vipi.db</c> reale. Scandirli per intero costa meno che tenere sincronizzata una coda.
/// </para>
///
/// <para>
/// ⚠️ La lingua è quella del <b>documento</b>, non della sezione: <c>Document.Language</c> è la lingua
/// SORGENTE, ed è per questo che la vLOA — che nasce <c>En</c> — passa da qui col verso invertito.
/// </para>
/// </summary>
public sealed class EfTranslatableCorpus : ITranslatableCorpus
{
    private readonly VipiDbContext _db;
    public EfTranslatableCorpus(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<string>> SegmentiAsync(string sourceLang, CancellationToken ct = default)
    {
        var lingua = sourceLang.Equals("en", StringComparison.OrdinalIgnoreCase) ? Language.En : Language.It;

        // Tre letture, non una per documento: il corpus si prende in blocco.
        var blocchi = await _db.ContentBlocks.AsNoTracking()
            .Where(b => b.DocumentVersion!.Document!.Language == lingua)
            .Select(b => new { b.Body, b.BodyJson })
            .ToListAsync(ct).ConfigureAwait(false);

        var titoliSezione = await _db.DocumentSections.AsNoTracking()
            .Where(s => s.DocumentVersion!.Document!.Language == lingua)
            .Select(s => s.Title)
            .ToListAsync(ct).ConfigureAwait(false);

        var titoliDocumento = await _db.Documents.AsNoTracking()
            .Where(d => d.Language == lingua)
            .Select(d => d.Title)
            .ToListAsync(ct).ConfigureAwait(false);

        var segmenti = new HashSet<string>(StringComparer.Ordinal);

        foreach (var b in blocchi)
        {
            foreach (var s in TextSegmenter.SplitProse(b.Body)) segmenti.Add(s);
            foreach (var s in TextSegmenter.SplitJson(b.BodyJson)) segmenti.Add(TranslationText.Normalize(s));
        }
        foreach (var t in titoliSezione) segmenti.Add(TranslationText.Normalize(t));
        foreach (var t in titoliDocumento) segmenti.Add(TranslationText.Normalize(t));

        segmenti.Remove("");
        return segmenti.ToList();
    }
}
