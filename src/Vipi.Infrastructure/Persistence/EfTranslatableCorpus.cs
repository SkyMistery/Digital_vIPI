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

        // Due letture, non una per documento: il corpus si prende in blocco.
        var blocchi = await _db.ContentBlocks.AsNoTracking()
            .Where(b => b.DocumentVersion!.Document!.Language == lingua)
            .Select(b => new { b.Body, b.BodyJson })
            .ToListAsync(ct).ConfigureAwait(false);

        var titoliSezione = await _db.DocumentSections.AsNoTracking()
            .Where(s => s.DocumentVersion!.Document!.Language == lingua)
            .Select(s => s.Title)
            .ToListAsync(ct).ConfigureAwait(false);

        // ⚠️ I TITOLI DEI DOCUMENTI NON SONO NEL CORPUS, ed è una regola del committente
        // (docs/design/regole-lingua.md R4): «vIPI — LIBC Crotone» è il NOME di quel documento e non si
        // traduce mai. Mandarlo al motore vorrebbe dire pagare dei caratteri per una risposta che nessuno
        // mostrerà — e riempire il Registro di righe che chi rivede non saprebbe dove vanno a finire.

        var segmenti = new HashSet<string>(StringComparer.Ordinal);

        foreach (var b in blocchi)
        {
            foreach (var s in TextSegmenter.SplitProse(b.Body)) segmenti.Add(s);
            foreach (var s in TextSegmenter.SplitJson(b.BodyJson)) segmenti.Add(TranslationText.Normalize(s));
        }
        foreach (var t in titoliSezione) segmenti.Add(TranslationText.Normalize(t));

        // ---- I testi che stanno FUORI dai documenti (carta §4) --------------------------------------
        // ⚠️ Descrizioni e dettagli di attivazione delle aree regolamentate vivono nell'ANAGRAFICA, non in
        // un documento: non hanno una `Document.Language`, e la loro lingua è quella della SORGENTE — IVAO,
        // che scrive in inglese. Entrano quindi nel giro «en», qualunque documento poi li mostri.
        //
        // Senza questo pezzo il lettore italiano vedrebbe il documento tradotto e le aree regolamentate
        // ancora in inglese: la stessa schermata a metà di prima, solo in un'altra sezione.
        //
        // Misurato il 28 agosto 2026: 230 aree, 35.056 caratteri in queste due colonne, ma appena
        // **9 descrizioni e 6 attivazioni DISTINTE**. Il dedup rende questo pezzo quasi gratuito.
        if (lingua == Language.En)
        {
            var aree = await _db.SpecialAreas.AsNoTracking()
                .Where(a => a.Description != null || a.ActivationDetails != null)
                .Select(a => new { a.Description, a.ActivationDetails })
                .Distinct()
                .ToListAsync(ct).ConfigureAwait(false);

            foreach (var a in aree)
            {
                segmenti.Add(TranslationText.Normalize(a.Description));
                segmenti.Add(TranslationText.Normalize(a.ActivationDetails));
            }
        }

        segmenti.Remove("");
        return segmenti.ToList();
    }
}
