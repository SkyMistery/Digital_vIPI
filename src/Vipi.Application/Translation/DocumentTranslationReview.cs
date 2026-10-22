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
/// <param name="Comune">La frase è del <b>testo comune</b> delle torri radar (S127), non del documento: correggerla
/// cambia la resa in tutte le torri radar insieme, e la corregge solo un admin — come il testo stesso.</param>
public sealed record RigaDaRivedere(
    string Sorgente, string Tradotto, TranslationOrigin Origine, bool Riletta, string Dove, bool Comune = false);

/// <summary>
/// Le righe di un documento <b>più</b> la lingua in cui il documento è scritto, da una lettura sola.
///
/// <para>⚠️ La seconda metà non è un lusso. Chi mostra le righe deve saper distinguere due vuoti che si
/// somigliano — «non c'è niente da tradurre» e «lo stai già leggendo nella sua lingua» — e fino al 31 agosto
/// 2026 li distingueva <b>richiamando <see cref="IDocumentTranslationReview.RigheAsync"/> con l'altra
/// lingua</b>: cioè ricaricando il documento intero una seconda volta, a ogni ridisegno dell'editor, per
/// dedurre un dato che la prima lettura aveva già in mano. Oltre a costare il doppio dava anche la risposta
/// sbagliata su un documento vuoto, dove nessuna delle due lingue ha righe.</para>
/// </summary>
/// <param name="LinguaSorgente">Il codice di due lettere della lingua in cui il documento è scritto.</param>
/// <param name="Righe">Le frasi da rivedere. Vuoto se non c'è niente da tradurre <b>o</b> se la lingua di
/// lettura è già <paramref name="LinguaSorgente"/>.</param>
/// <param name="Bloccata">Il documento si legge in una lingua sola (carta
/// <c>docs/feature/2026-08-31-lingua-bloccata.md</c>): non c'è nessun giro di revisione da fare, ed è un
/// TERZO vuoto — diverso da «niente da tradurre» e da «lo stai leggendo nella sua lingua». Detto con le
/// parole di quegli altri due, chi guarda penserebbe che il pannello sia rotto.</param>
public sealed record RevisioneDocumento(string LinguaSorgente, IReadOnlyList<RigaDaRivedere> Righe,
    bool Bloccata = false)
{
    /// <summary>Vero se si sta leggendo nella lingua in cui il documento è scritto: lì non c'è niente da rivedere.</summary>
    public bool StessaLingua(string linguaDiLettura) =>
        string.Equals(LinguaSorgente, linguaDiLettura, StringComparison.OrdinalIgnoreCase);
}

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
    /// Le frasi del documento in lavorazione, con la loro resa in <paramref name="targetLang"/>, <b>e</b> la
    /// lingua in cui il documento è scritto. È la porta da usare: una lettura del documento, non due.
    /// </summary>
    Task<RevisioneDocumento> RevisioneAsync(int documentId, string targetLang, CancellationToken ct = default);

    /// <summary>
    /// Le sole frasi. Vuoto se la lingua di lettura è quella in cui il documento è scritto: lì non c'è
    /// niente da rivedere. ⚠️ Chi ha bisogno anche di sapere <i>quale</i> sia quella lingua deve chiamare
    /// <see cref="RevisioneAsync"/>, non questa due volte con due lingue diverse.
    /// </summary>
    async Task<IReadOnlyList<RigaDaRivedere>> RigheAsync(int documentId, string targetLang, CancellationToken ct = default)
        => (await RevisioneAsync(documentId, targetLang, ct).ConfigureAwait(false)).Righe;

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

    /// <summary>
    /// Il testo comune delle torri radar (S127). Opzionale: senza, il pannello mostra le sole frasi del documento,
    /// com'era prima.
    /// </summary>
    private readonly ITestoComuneStore? _comune;

    public DocumentTranslationReview(IDocumentForReview editing, ITranslationMemory memoria,
        Auth.IEditAuthorizationService authz, ITestoComuneStore? comune = null)
    {
        _editing = editing;
        _memoria = memoria;
        _authz = authz;
        _comune = comune;
    }

    public async Task<RevisioneDocumento> RevisioneAsync(
        int documentId, string targetLang, CancellationToken ct = default)
    {
        var doc = await _editing.LoadForEditAsync(documentId, ct);
        // Senza documento non c'è nemmeno una lingua sorgente: si risponde con quella di lettura, che è il
        // modo di dire «non c'è niente da rivedere» senza far credere a chi guarda di aver sbagliato lingua.
        if (doc is null) return new RevisioneDocumento(targetLang, Array.Empty<RigaDaRivedere>());

        var sourceLang = DocumentTranslator.CodiceSorgente(doc.Language, Language.It);
        // Lingua bloccata: il documento non si traduce in nessuna direzione, quindi non c'è niente da
        // rivedere — e nemmeno una lettura di memoria da fare.
        if (doc.LanguageLocked)
            return new RevisioneDocumento(sourceLang, Array.Empty<RigaDaRivedere>(), Bloccata: true);

        if (string.Equals(sourceLang, targetLang, StringComparison.OrdinalIgnoreCase))
            return new RevisioneDocumento(sourceLang, Array.Empty<RigaDaRivedere>());

        // ⚠️ Le frasi si raccolgono col LORO POSTO: senza, chi corregge vede un elenco di frasi sciolte e
        // non sa quale sezione sta guardando. Il titolo della sezione è l'ancora più economica che c'è.
        var segmenti = new List<(string Testo, string Dove)>();
        foreach (var sezione in doc.Sections) Raccogli(sezione, sezione.Title, segmenti);

        // ⚠️ Il titolo del DOCUMENTO non c'è, e non è una dimenticanza: non si traduce (regole-lingua R4).

        var distinti = segmenti
            .GroupBy(x => x.Testo, StringComparer.Ordinal)
            .Select(g => (Testo: g.Key, Dove: g.First().Dove, Comune: false))
            .ToList();

        // Il testo comune delle torri radar (S127), in coda: non sta nel documento, ma il lettore lo trova in testa alla
        // «Tecnica operativa», ed era tradotto dalla macchina senza un posto dove rileggerlo (committente, 22 ottobre
        // 2026: «non sapevo dove si traducesse»). Le frasi che il documento ha già restano sue.
        var proprie = distinti.Select(x => x.Testo).ToHashSet(StringComparer.Ordinal);
        foreach (var s in await FrasiComuniAsync(doc, sourceLang, targetLang, ct).ConfigureAwait(false))
            if (proprie.Add(s)) distinti.Add((s, DoveComune, true));

        if (distinti.Count == 0) return new RevisioneDocumento(sourceLang, Array.Empty<RigaDaRivedere>());

        // Una lettura sola per tutto il documento: una query per frase sarebbe una corsa sul DbContext.
        // ⚠️ La lingua del testo comune è la stessa del documento: `FrasiComuniAsync` lo porta solo lì.
        var note = await _memoria.LookupAsync(sourceLang, targetLang,
            distinti.Select(x => TranslationText.Hash(x.Testo)).Distinct(StringComparer.Ordinal).ToList(), ct)
            .ConfigureAwait(false);

        var righe = distinti.Select(x =>
        {
            note.TryGetValue(TranslationText.Hash(x.Testo), out var t);
            return new RigaDaRivedere(
                x.Testo,
                t?.TargetText ?? "",
                t?.Origin ?? TranslationOrigin.Machine,
                t?.Reviewed ?? false,
                x.Dove,
                x.Comune);
        }).ToList();

        return new RevisioneDocumento(sourceLang, righe);
    }

    /// <summary>Dove sta una frase del testo comune: non in una sezione del documento, ma in tutte le torri radar.</summary>
    private static string DoveComune => Messaggio.Lingua(
        "Testo comune delle torri radar (in testa alla Tecnica operativa)",
        "Common text of the radar towers (at the top of the Operating technique)");

    /// <summary>
    /// Le frasi del testo comune che QUESTO documento mostra: se ha la «Tecnica operativa» del radar di torre, e se si
    /// legge in un'altra lingua da quella in cui il testo comune è scritto.
    ///
    /// <para>⚠️ Solo nei documenti scritti nella lingua del testo comune (l'italiano): la memoria si legge con la
    /// lingua del documento, e in un documento inglese le frasi italiane del testo comune non sono «la sua lingua».
    /// Lì si rivedono da un qualunque documento italiano: la resa è una sola, la stessa per tutti.</para>
    ///
    /// <para>⚠️ Anche dove la torre è senza radar o la sezione è nascosta, e quindi il lettore oggi non lo vede: chi
    /// scrive lo scalo può comunque volerlo rileggere, e la resa è la stessa in tutte le torri.</para>
    /// </summary>
    private async Task<IReadOnlyList<string>> FrasiComuniAsync(EditableDocument doc, string sourceLang, string targetLang,
        CancellationToken ct)
    {
        var lingua = TestoComune.Sorgente == Language.En ? "en" : "it";
        if (_comune is null
            || !string.Equals(sourceLang, lingua, StringComparison.OrdinalIgnoreCase)
            || string.Equals(targetLang, lingua, StringComparison.OrdinalIgnoreCase)
            || !HaLaSezione(doc.Sections, SectionKeys.RadarTowerTechnique))
            return Array.Empty<string>();

        // ⚠️ La stessa segmentazione del giro che traduce (`EfTranslatableCorpus`, `EfStatoTraduzione`): con un'altra,
        // le impronte non coinciderebbero e il pannello direbbe «manca» a frasi già tradotte.
        var blocchi = await _comune.LeggiAsync(SectionKeys.RadarTowerTechnique, ct).ConfigureAwait(false);
        return DocumentTranslator.SegmentiSezione(TestoComune.ComeSezione(blocchi)).Distinct(StringComparer.Ordinal).ToList();
    }

    private static bool HaLaSezione(IReadOnlyList<EditableSection> sezioni, string chiave) =>
        sezioni.Any(s => string.Equals(s.SectionKey, chiave, StringComparison.OrdinalIgnoreCase)
                         || HaLaSezione(s.Children, chiave));

    public Task<int> DocumentiToccatiAsync(string sorgente, CancellationToken ct = default) =>
        _memoria.DocumentiToccatiAsync(TranslationText.Normalize(sorgente), ct);

    public async Task CorreggiAsync(int documentId, string targetLang, string sorgente, string tradotto,
        CancellationToken ct = default)
    {
        // ⚠️ Il permesso è quello del DOCUMENTO: correggere la traduzione è un atto editoriale su ciò che
        // quel documento dice a chi legge, e chi non lo può scrivere non lo può nemmeno ridire in un'altra
        // lingua. (Il Registro, che tocca tutte le frasi della divisione, lo apre anche lui all'Editor: AdminNav e
        // GlossarioPage — il «resta agli admin» che stava qui era superato, U-113.)
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

        // Le SID citate e i link agli allegati (§A73) devono restare quelli del sorgente: sono sintassi, non
        // parole, e una resa che li perde li perde solo in questa lingua.
        if (!TextProtector.StessiRiferimenti(sorgente, tradotto))
            throw new Aor.ValidationException(RiferimentiCambiati);

        // ⚠️ Una frase del testo comune (S127) la corregge solo un admin, come il testo stesso: la resa vale per tutte
        // le torri radar insieme. Si guarda la frase e non il clic: chi arriva da un'altra scheda non passa lo stesso.
        // Se la frase sta anche nel documento, è sua e resta all'Editor — com'è sempre stato.
        var norm = TranslationText.Normalize(sorgente);
        var proprie = new List<(string Testo, string Dove)>();
        foreach (var sezione in doc.Sections) Raccogli(sezione, sezione.Title, proprie);
        if (!proprie.Any(p => string.Equals(p.Testo, norm, StringComparison.Ordinal))
            && (await FrasiComuniAsync(doc, sourceLang, targetLang, ct).ConfigureAwait(false)).Contains(norm, StringComparer.Ordinal))
            _authz.EnsureAtLeast(VipiRole.Admin);

        await _memoria.SaveHumanAsync(sourceLang, targetLang,
            TranslationText.Normalize(sorgente), TranslationText.Normalize(tradotto),
            _authz.CurrentUserId ?? 0, ct).ConfigureAwait(false);
    }

    /// <summary>Il rifiuto di una resa scritta a mano che ha perso o cambiato un riferimento (§A73).</summary>
    public static string RiferimentiCambiati => Messaggio.Lingua(
        "La traduzione deve lasciare identici i riferimenti del testo originale: le procedure citate ([[SID …]], [[STAR …]]), i link agli allegati (allegato:…) e gli indirizzi dei link (https://…). Copiali così come sono.",
        "The translation must keep the references of the original text unchanged: cited procedures ([[SID …]], [[STAR …]]), attachment links (allegato:…) and link addresses (https://…). Copy them as they are.");

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
