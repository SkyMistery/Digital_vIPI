using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Application.Translation;

/// <summary>
/// Una frase di <b>questo</b> documento, con la sua resa nella lingua di lettura.
/// </summary>
/// <param name="Sorgente">Il testo come è scritto nel documento. ⚠️ È <b>la chiave</b> della memoria e non
/// si modifica da qui: cambiare quel che il documento dice è un'edit del documento, non una revisione della
/// traduzione.</param>
/// <param name="Tradotto">La resa attuale, o vuota se non c'è ancora.</param>
/// <param name="Origine">Macchina o persona.</param>
/// <param name="Riletta">Vero se una persona l'ha già guardata.</param>
/// <param name="Dove">Dove compare nel documento — il titolo della sezione — per ritrovarla.</param>
public sealed record RigaDaRivedere(
    string Sorgente, string Tradotto, TranslationOrigin Origine, bool Riletta, string Dove);

/// <summary>
/// Le frasi di un documento e la loro traduzione, per chi lo sta scrivendo (carta bilingue §5, slice 7).
///
/// <para>
/// ⚠️ <b>Perché nell'editor e non solo nel Registro.</b> Il Registro delle traduzioni elenca le frasi di
/// TUTTA la divisione, in ordine di quanto sono state riviste: è il posto giusto per chi fa un giro di
/// revisione, ed è quello sbagliato per chi ha appena scritto un documento e vuole vedere <b>come viene
/// letto</b> quel documento lì. Chi scrive conosce la fraseologia del suo scalo, ed è l'unico che può dire
/// se «riporta sottovento» è diventato «report downwind» o «bring it back downwind».
/// </para>
///
/// <para>
/// ⚠️ <b>La correzione tocca la FRASE, non il documento</b>: la memoria è indicizzata sull'impronta, quindi
/// quel che si corregge qui vale per ogni documento che contiene la stessa frase, anche futuro. È il
/// superpotere della forma e insieme il suo trabocchetto — per questo il conto dei documenti toccati si
/// mostra <b>prima</b> di salvare, non dopo.
/// </para>
/// </summary>
public interface IDocumentTranslationReview
{
    /// <summary>
    /// Le frasi del documento in lavorazione, con la loro resa in <paramref name="targetLang"/>. Vuoto se
    /// la lingua di lettura è quella in cui il documento è scritto: lì non c'è niente da rivedere.
    /// </summary>
    Task<IReadOnlyList<RigaDaRivedere>> RigheAsync(int documentId, string targetLang, CancellationToken ct = default);

    /// <summary>Quanti altri documenti contengono questa frase: si dice a chi corregge, prima che salvi.</summary>
    Task<int> DocumentiToccatiAsync(string sorgente, CancellationToken ct = default);

    /// <summary>Salva la correzione di una persona. Da qui in avanti la macchina non la tocca più.</summary>
    Task CorreggiAsync(int documentId, string targetLang, string sorgente, string tradotto,
        CancellationToken ct = default);
}

/// <inheritdoc cref="IDocumentTranslationReview"/>
public sealed class DocumentTranslationReview : IDocumentTranslationReview
{
    private readonly IDocumentForReview _editing;
    private readonly ITranslationMemory _memoria;
    private readonly Auth.IEditAuthorizationService _authz;

    public DocumentTranslationReview(IDocumentForReview editing, ITranslationMemory memoria,
        Auth.IEditAuthorizationService authz)
    {
        _editing = editing;
        _memoria = memoria;
        _authz = authz;
    }

    public async Task<IReadOnlyList<RigaDaRivedere>> RigheAsync(
        int documentId, string targetLang, CancellationToken ct = default)
    {
        var doc = await _editing.LoadForEditAsync(documentId, ct);
        if (doc is null) return Array.Empty<RigaDaRivedere>();

        var sourceLang = DocumentTranslator.CodiceSorgente(doc.Language, Language.It);
        if (string.Equals(sourceLang, targetLang, StringComparison.OrdinalIgnoreCase))
            return Array.Empty<RigaDaRivedere>();

        // ⚠️ Le frasi si raccolgono col LORO POSTO: senza, chi corregge vede un elenco di frasi sciolte e
        // non sa quale sezione sta guardando. Il titolo della sezione è l'ancora più economica che c'è.
        var segmenti = new List<(string Testo, string Dove)>();
        foreach (var sezione in doc.Sections) Raccogli(sezione, sezione.Title, segmenti);

        // ⚠️ Il titolo del DOCUMENTO non c'è, e non è una dimenticanza: non si traduce (regole-lingua R4).

        var distinti = segmenti
            .GroupBy(x => x.Testo, StringComparer.Ordinal)
            .Select(g => (Testo: g.Key, Dove: g.First().Dove))
            .ToList();
        if (distinti.Count == 0) return Array.Empty<RigaDaRivedere>();

        // Una lettura sola per tutto il documento: una query per frase sarebbe una corsa sul DbContext.
        var note = await _memoria.LookupAsync(sourceLang, targetLang,
            distinti.Select(x => TranslationText.Hash(x.Testo)).Distinct(StringComparer.Ordinal).ToList(), ct)
            .ConfigureAwait(false);

        return distinti.Select(x =>
        {
            note.TryGetValue(TranslationText.Hash(x.Testo), out var t);
            return new RigaDaRivedere(
                x.Testo,
                t?.TargetText ?? "",
                t?.Origin ?? TranslationOrigin.Machine,
                t?.Reviewed ?? false,
                x.Dove);
        }).ToList();
    }

    public Task<int> DocumentiToccatiAsync(string sorgente, CancellationToken ct = default) =>
        _memoria.DocumentiToccatiAsync(TranslationText.Normalize(sorgente), ct);

    public async Task CorreggiAsync(int documentId, string targetLang, string sorgente, string tradotto,
        CancellationToken ct = default)
    {
        // ⚠️ Il permesso è quello del DOCUMENTO: correggere la traduzione è un atto editoriale su ciò che
        // quel documento dice a chi legge, e chi non lo può scrivere non lo può nemmeno ridire in un'altra
        // lingua. (Il Registro, che tocca tutte le frasi della divisione, resta agli admin.)
        _authz.EnsureAtLeast(VipiRole.Editor);

        var doc = await _editing.LoadForEditAsync(documentId, ct)
            ?? throw new Aor.ValidationException(Messaggio.Lingua(
                $"Documento {documentId} senza versione di lavoro.",
                $"Document {documentId} has no working version."));

        var sourceLang = DocumentTranslator.CodiceSorgente(doc.Language, Language.It);
        if (string.Equals(sourceLang, targetLang, StringComparison.OrdinalIgnoreCase))
            throw new Aor.ValidationException(Messaggio.Lingua(
                "Questa è la lingua in cui il documento è scritto: si corregge nell'editor, non qui.",
                "This is the language the document is written in: it is corrected in the editor, not here."));

        await _memoria.SaveHumanAsync(sourceLang, targetLang,
            TranslationText.Normalize(sorgente), TranslationText.Normalize(tradotto),
            _authz.CurrentUserId ?? 0, ct).ConfigureAwait(false);
    }

    /// <summary>Le frasi di una sezione e delle sue figlie, ognuna col nome della sezione in cui sta.</summary>
    private static void Raccogli(EditableSection sezione, string dove, List<(string, string)> dentro)
    {
        foreach (var s in DocumentTranslator.Aggiungi(sezione.Title)) dentro.Add((s, dove));

        foreach (var b in sezione.Blocks)
        {
            foreach (var p in TextSegmenter.SplitProse(b.Body))
                if (TranslationText.HasSomethingToTranslate(p)) dentro.Add((p, dove));

            foreach (var c in TextSegmenter.SplitJson(b.BodyJson))
            {
                var norm = TranslationText.Normalize(c);
                if (TranslationText.HasSomethingToTranslate(norm)) dentro.Add((norm, dove));
            }
        }

        // ⚠️ Le figlie tengono il nome della sezione PADRE: nell'editor si naviga per sezione radice, ed è
        // quella che chi corregge deve ritrovare nell'indice.
        foreach (var figlia in sezione.Children) Raccogli(figlia, dove, dentro);
    }
}
