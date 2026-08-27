using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain.Entities;

namespace Vipi.Application.Translation;

/// <summary>
/// Quanto di un documento è davvero tradotto, e quanto è stato riletto da una persona.
/// </summary>
/// <param name="Segmenti">Segmenti traducibili trovati nel documento.</param>
/// <param name="Tradotti">Quanti hanno una traduzione in memoria.</param>
/// <param name="Riletti">Quanti l'hanno <b>rivista da una persona</b>.</param>
public sealed record TranslationCoverage(int Segmenti, int Tradotti, int Riletti)
{
    public static readonly TranslationCoverage Nessuna = new(0, 0, 0);

    /// <summary>Quanti mancano: il documento si legge <b>a chiazze</b>, con dei pezzi nella lingua sorgente.</summary>
    public int Mancanti => Segmenti - Tradotti;

    /// <summary>Vero se non manca niente.</summary>
    public bool Completa => Segmenti > 0 && Mancanti == 0;

    /// <summary>
    /// Vero se qualcosa non è stato riletto da nessuno: la vista va <b>marcata</b>.
    /// <para>⚠️ La marcatura non è una formalità. Misurato contro il servizio vero: «riporta sottovento»
    /// torna «bring it back downwind» — grammatica giusta, identificatori intatti, e <b>non è
    /// fraseologia</b>. Plausibile e sbagliato è peggio di assente, perché nessuno se ne accorge leggendo.</para>
    /// </summary>
    public bool DaRileggere => Tradotti > Riletti;
}

/// <summary>Un documento tradotto, con quanto se ne può dire al lettore.</summary>
public sealed record TranslatedDocument(DocumentView View, TranslationCoverage Coverage);

/// <summary>
/// Traduce un <see cref="DocumentView"/> nella lingua di chi legge (carta
/// <c>docs/feature/2026-08-27-documenti-bilingue.md</c> §7).
///
/// <para>
/// ⚠️ <b>Il documento resta UNO.</b> Questa classe non produce un secondo documento: rende lo stesso, con le
/// stesse sezioni nello stesso ordine, in un'altra lingua. È l'invariante deciso dal committente — «quel che
/// è scritto in italiano c'è in inglese e viceversa» — e qui è strutturale: non c'è modo di far dire alla
/// vista tradotta qualcosa che l'originale non dice, perché ogni pezzo di testo viene <b>dalla stessa
/// impronta</b>.
/// </para>
///
/// <para>
/// ⚠️ <b>Quel che manca resta nella lingua sorgente</b>, non sparisce e non diventa vuoto. Un documento a
/// chiazze si legge male ma si legge; un documento con dei buchi mente.
/// </para>
///
/// <para>
/// ⚠️ Le sezioni <b>rese dalla pagina</b> (derivate e strutturate) qui non hanno corpo — <c>Body</c> e
/// <c>BodyJson</c> sono vuoti nel view, perché le disegna il componente. Quindi non si toccano, ed è giusto:
/// la loro prosa è generata da codice e si localizza con le risorse, non col traduttore automatico.
/// </para>
/// </summary>
public sealed class DocumentTranslator
{
    private readonly ITranslationMemory _memoria;

    public DocumentTranslator(ITranslationMemory memoria) => _memoria = memoria;

    /// <summary>
    /// Il documento nella lingua di chi legge. Se le due lingue coincidono, torna l'originale <b>senza
    /// toccare il database</b>: leggere un documento italiano in italiano non deve costare una query.
    /// </summary>
    public async Task<TranslatedDocument> TranslateAsync(
        DocumentView view, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        if (string.Equals(sourceLang, targetLang, StringComparison.OrdinalIgnoreCase))
            return new TranslatedDocument(view, TranslationCoverage.Nessuna);

        // Tutti i segmenti del documento, in una passata; poi UNA lettura di memoria per tutti.
        // ⚠️ Una query per segmento sarebbe una corsa sul DbContext del circuito Blazor — il guasto
        // «second operation» già pagato sei volte su questo prodotto.
        var segmenti = Segmenti(view).ToList();
        if (segmenti.Count == 0) return new TranslatedDocument(view, TranslationCoverage.Nessuna);

        var impronte = segmenti.Distinct(StringComparer.Ordinal)
            .ToDictionary(s => s, TranslationText.Hash, StringComparer.Ordinal);

        var note = await _memoria
            .LookupAsync(sourceLang, targetLang, impronte.Values.Distinct().ToList(), ct)
            .ConfigureAwait(false);

        string? Traduci(string? testo)
        {
            var norm = TranslationText.Normalize(testo);
            if (norm.Length == 0) return testo;
            return note.TryGetValue(TranslationText.Hash(norm), out var t) ? t.TargetText : testo;
        }

        var copertura = new TranslationCoverage(
            Segmenti: impronte.Count,
            Tradotti: impronte.Values.Distinct().Count(note.ContainsKey),
            Riletti: impronte.Values.Distinct().Count(h => note.TryGetValue(h, out var t) && t.Reviewed));

        var tradotto = new DocumentView
        {
            Title = Traduci(view.Title) ?? view.Title,
            AiracCycle = view.AiracCycle,   // un ciclo AIRAC non si traduce
            Sections = view.Sections.Select(s => TraduciSezione(s, Traduci)).ToList(),
        };

        return new TranslatedDocument(tradotto, copertura);
    }

    private static SectionView TraduciSezione(SectionView s, Func<string?, string?> traduci) => new()
    {
        Id = s.Id,
        Title = traduci(s.Title) ?? s.Title,
        Depth = s.Depth,
        SectionKey = s.SectionKey,
        IsHidden = s.IsHidden,
        BeforeParentBody = s.BeforeParentBody,
        LeadSentence = s.LeadSentence,
        Blocks = s.Blocks.Select(b => TraduciBlocco(b, traduci)).ToList(),
        Children = s.Children.Select(c => TraduciSezione(c, traduci)).ToList(),
    };

    private static BlockView TraduciBlocco(BlockView b, Func<string?, string?> traduci) => new()
    {
        Id = b.Id,
        Format = b.Format,
        State = b.State,
        CollapseLabel = b.CollapseLabel,
        // La prosa si taglia in paragrafi, si traduce paragrafo per paragrafo e si rimette insieme: così un
        // paragrafo non ancora tradotto resta nella sua lingua invece di far ricadere tutto il blocco.
        Body = b.Body is null
            ? null
            : TextSegmenter.JoinProse(TextSegmenter.SplitProse(b.Body).Select(p => traduci(p) ?? p).ToList()),
        BodyJson = TextSegmenter.MapJson(b.BodyJson, s => traduci(s) ?? s),
        CalloutKind = b.CalloutKind,
    };

    /// <summary>Ogni testo traducibile del documento, nell'ordine in cui si incontra.</summary>
    private static IEnumerable<string> Segmenti(DocumentView view)
    {
        foreach (var s in Aggiungi(view.Title)) yield return s;
        foreach (var sezione in view.Sections)
            foreach (var s in SegmentiSezione(sezione))
                yield return s;
    }

    private static IEnumerable<string> SegmentiSezione(SectionView sezione)
    {
        foreach (var s in Aggiungi(sezione.Title)) yield return s;

        foreach (var b in sezione.Blocks)
        {
            foreach (var p in TextSegmenter.SplitProse(b.Body))
                if (TranslationText.HasSomethingToTranslate(p)) yield return p;

            foreach (var c in TextSegmenter.SplitJson(b.BodyJson))
            {
                var norm = TranslationText.Normalize(c);
                if (TranslationText.HasSomethingToTranslate(norm)) yield return norm;
            }
        }

        foreach (var figlia in sezione.Children)
            foreach (var s in SegmentiSezione(figlia))
                yield return s;
    }

    private static IEnumerable<string> Aggiungi(string? testo)
    {
        var norm = TranslationText.Normalize(testo);
        if (TranslationText.HasSomethingToTranslate(norm)) yield return norm;
    }
}
