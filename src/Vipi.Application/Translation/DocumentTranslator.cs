using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
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
public sealed record TranslatedDocument(DocumentView View, TranslationCoverage Coverage)
{
    /// <summary>
    /// La passata che ha tradotto questo documento, per i testi che la <b>pagina</b> mette accanto al
    /// documento e che quindi il traduttore non ha visto.
    ///
    /// <para>⚠️ Esiste per un guasto misurato il 28 agosto 2026 sulla vIPI di Crotone: il viewer d'aeroporto
    /// non mostra i titoli del DOCUMENTO ma quelli del CATALOGO (<c>SectionCatalog</c>), che sono stringhe
    /// italiane cablate. A traduzione completa la pagina restava con le testate in italiano — «Regole piste»
    /// in mezzo a un documento inglese — e nessun avviso lo diceva, perché per il traduttore era tutto
    /// tradotto: quei titoli non gli erano mai passati davanti.</para>
    /// </summary>
    public TranslationPass Pass { get; init; } = TranslationPass.Nessuna;
}

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
    /// Il codice a due lettere della lingua <b>sorgente</b> di un documento: quella in cui è stato redatto.
    ///
    /// <para>⚠️ <b>Si chiede al documento, non alla famiglia.</b> Fino al 28 agosto 2026 le due pagine
    /// bilingui scrivevano la sorgente a mano — «it» il vSOP militare, «en» la vLOA — e finché ogni famiglia
    /// nasce in una lingua sola la cosa regge. Ma la lingua sta già <b>sul documento</b>
    /// (<see cref="DocumentView.Language"/>), e un secondo posto che la dichiara è un posto che può
    /// contraddire il primo: una vLOA redatta in italiano sarebbe stata tradotta come se fosse inglese, e il
    /// lettore avrebbe visto la memoria mancare su ogni frase senza capire perché.</para>
    ///
    /// <para><paramref name="predefinita"/> serve ai documenti salvati <b>prima</b> che il campo esistesse,
    /// che arrivano con la lingua nulla: è la lingua in cui quella famiglia nasce.</para>
    /// </summary>
    public static string CodiceSorgente(Language? lingua, Language predefinita) =>
        (lingua ?? predefinita) == Language.En ? "en" : "it";

    /// <summary>
    /// Il documento nella lingua di chi legge, con la lingua sorgente <b>presa dal documento</b>
    /// (<see cref="CodiceSorgente"/>). È la forma che usano le pagine: nessuna di loro sa, né deve sapere,
    /// in che lingua è scritto il documento che sta mostrando.
    /// </summary>
    public Task<TranslatedDocument> TranslateAsync(
        DocumentView view, Language predefinita, string targetLang, CancellationToken ct = default) =>
        TranslateAsync(view, CodiceSorgente(view.Language, predefinita), targetLang, ct);

    /// <summary>
    /// Il documento nella lingua di chi legge. Se le due lingue coincidono, torna l'originale <b>senza
    /// toccare il database</b>: leggere un documento italiano in italiano non deve costare una query.
    /// </summary>
    public async Task<TranslatedDocument> TranslateAsync(
        DocumentView view, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        // Tutti i segmenti del documento, in una passata; poi UNA lettura di memoria per tutti.
        // ⚠️ Una query per segmento sarebbe una corsa sul DbContext del circuito Blazor — il guasto
        // «second operation» già pagato sei volte su questo prodotto.
        var passata = await PreparaAsync(Segmenti(view), sourceLang, targetLang, Congelate(view, targetLang), ct)
            .ConfigureAwait(false);

        // Niente da tradurre (stessa lingua, o documento senza prosa): l'originale, intatto.
        if (passata.Coverage.Segmenti == 0) return new TranslatedDocument(view, TranslationCoverage.Nessuna);

        var tradotto = new DocumentView
        {
            // ⚠️ IL TITOLO DEL DOCUMENTO NON SI TRADUCE, ed è una regola del committente
            // (docs/design/regole-lingua.md R4): «vIPI — LIBC Crotone» è il NOME di quel documento, quello
            // che sta nell'elenco, nella briciola di pane e in bocca a chi lo cita in frequenza. Un nome
            // che cambia con la lingua di chi guarda non è più un nome.
            Title = view.Title,
            AiracCycle = view.AiracCycle,   // un ciclo AIRAC non si traduce
            Sections = view.Sections.Select(passata.Sezione).ToList(),
            // La vista tradotta resta una vista dello STESSO documento, e deve continuare a sapere in che
            // lingua e' scritto l'originale e che cosa la release aveva congelato.
            // ⚠️ Anche il BLOCCO, per la stessa ragione per cui si ricopia `Audience` sulle sezioni: ogni campo
            // che questa classe non ricopia lo azzera in silenzio, e il default è sempre quello «buono», quindi
            // la pagina continua a rendersi e nessun test cade. Qui vorrebbe dire dire «non bloccato» di un
            // documento bloccato, alla riga dopo averlo tradotto.
            Language = view.Language,
            LanguageLocked = view.LanguageLocked,
            Translations = view.Translations,
        };

        return new TranslatedDocument(tradotto, passata.Coverage) { Pass = passata };
    }

    /// <summary>
    /// Carica la memoria per una passata di traduzione e restituisce <b>la funzione che traduce</b>, più
    /// quanto se ne può dire al lettore.
    ///
    /// <para>
    /// Serve a chi ha della prosa da tradurre ma <b>non un <see cref="DocumentView"/></b>: la vIPI ACC, che
    /// vive come blocchi (<c>AccVipiData</c>) e non come vista documentale. Senza questo ingresso l'unico
    /// modo di tradurla sarebbe una seconda copia di questa logica — la lettura di memoria, il conteggio
    /// della copertura, la preferenza per le congelate — e due copie divergono.
    /// </para>
    ///
    /// <para>⚠️ <b>Una lettura sola, per tutti i segmenti.</b> Chi chiama deve passarli TUTTI in una volta:
    /// una query per segmento sarebbe una corsa sul DbContext del circuito Blazor.</para>
    /// </summary>
    /// <param name="segmenti">Ogni testo traducibile, anche ripetuto: il dedup lo fa l'impronta.</param>
    /// <param name="congelate">Le traduzioni congelate dalla release, se il documento ne porta.</param>
    public async Task<TranslationPass> PreparaAsync(
        IEnumerable<string> segmenti, string sourceLang, string targetLang,
        IReadOnlyDictionary<string, FrozenTranslation>? congelate = null, CancellationToken ct = default)
    {
        if (string.Equals(sourceLang, targetLang, StringComparison.OrdinalIgnoreCase))
            return TranslationPass.Nessuna;

        var impronte = segmenti.Select(TranslationText.Hash).Distinct(StringComparer.Ordinal).ToList();
        if (impronte.Count == 0) return TranslationPass.Nessuna;

        var note = await NoteAsync(impronte, sourceLang, targetLang, congelate, ct).ConfigureAwait(false);

        var copertura = new TranslationCoverage(
            Segmenti: impronte.Count,
            Tradotti: impronte.Count(note.ContainsKey),
            Riletti: impronte.Count(h => note.TryGetValue(h, out var t) && t.Reviewed));

        return new TranslationPass(
            testo =>
            {
                if (string.IsNullOrWhiteSpace(testo)) return testo;
                // ⚠️ Quel che manca resta com'è: un testo non tradotto si legge nella lingua d'origine, non
                // sparisce. Un documento a chiazze si legge male ma si legge; uno con dei buchi mente.
                return note.TryGetValue(TranslationText.Hash(testo), out var t) ? t.TargetText : testo;
            },
            copertura);
    }

    /// <summary>
    /// Che cosa si sa di queste impronte: <b>prima il congelato della release, poi la memoria viva per
    /// quel che il congelato non copre</b>.
    ///
    /// <para>
    /// ⚠️ <b>Il congelato si SOVRAPPONE alla memoria, non la sostituisce</b>, e la differenza si vede solo
    /// a regime. Fino al 28 agosto 2026 bastava che lo snapshot portasse una traduzione qualsiasi perché la
    /// memoria non venisse più letta: ma la fotografia la scatta <c>ReleaseService</c> nell'istante della
    /// pubblicazione, e il giro che riempie la memoria passa <b>ogni quarto d'ora</b>. Chi scriveva prosa
    /// nuova e pubblicava subito — cioè il caso normale, non quello raro — congelava una traduzione
    /// incompleta, e quel documento restava <b>a chiazze per sempre</b>: il motore traduceva le frasi
    /// mancanti dieci minuti dopo, la memoria le aveva, e nessuno le andava più a prendere. L'avviso
    /// «mancano N frasi su M» rimaneva acceso fino alla ripubblicazione successiva.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>La ragione del congelamento resta intatta.</b> Il congelato serve a limitare il raggio d'azione
    /// di una correzione: una resa cambiata oggi su un altro documento non deve riscrivere quello che
    /// questo ha <b>già pubblicato</b>. Ma dove lo snapshot <i>non ha niente</i> non c'è niente di
    /// pubblicato da proteggere: quella frase, nella release, si legge nella lingua sorgente. Prendere la
    /// memoria viva lì non cambia una parola già pubblicata — riempie un buco.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>A congelato completo il database non si tocca</b>, esattamente come prima: si legge solo se
    /// resta qualche impronta scoperta, e solo per QUELLE. Su una release pubblicata con calma è zero
    /// query, che è il caso per cui quella scorciatoia era stata scritta.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyDictionary<string, KnownTranslation>> NoteAsync(
        IReadOnlyList<string> impronte, string sourceLang, string targetLang,
        IReadOnlyDictionary<string, FrozenTranslation>? congelate, CancellationToken ct)
    {
        if (congelate is null || congelate.Count == 0)
            return await _memoria.LookupAsync(sourceLang, targetLang, impronte, ct).ConfigureAwait(false);

        var note = new Dictionary<string, KnownTranslation>(StringComparer.Ordinal);
        var scoperte = new List<string>();

        foreach (var impronta in impronte)
        {
            // ⚠️ `HasText` e non `ContainsKey`: una voce arrivata vuota — snapshot troncato, forma che il
            // lettore non riconosce — non deve valere come «congelata», o cancellerebbe la frase invece di
            // tradurla. Vuota = scoperta, e si prova la memoria.
            if (congelate.TryGetValue(impronta, out var c) && c.HasText)
                // ⚠️ Il timbro viene dallo snapshot, non da un default. Riletta ⇒ l'ha scritta una persona:
                // `ReviewedUtc` lo mette solo `SaveHumanAsync`, quindi l'origine si deduce e non si
                // inventa. Senza timbro (release pubblicate prima del 28 agosto 2026) resta «da rileggere»,
                // che è tutto quello che quello snapshot può dire di sé.
                note[impronta] = new KnownTranslation(
                    c.Text,
                    c.Reviewed ? TranslationOrigin.Human : TranslationOrigin.Machine,
                    c.Reviewed);
            else
                scoperte.Add(impronta);
        }

        if (scoperte.Count == 0) return note;

        // Una lettura sola, e solo per quel che manca: le impronte già congelate non si richiedono.
        var viva = await _memoria.LookupAsync(sourceLang, targetLang, scoperte, ct).ConfigureAwait(false);
        foreach (var kv in viva) note[kv.Key] = kv.Value;

        return note;
    }

    /// <summary>
    /// Le traduzioni che la <b>release</b> ha congelato per la lingua di lettura, se ce ne sono.
    ///
    /// <para>⚠️ DOVE LA RELEASE HA CONGELATO, VINCE IL CONGELATO. Senza questa preferenza, una correzione
    /// fatta oggi su un'altra vLOA cambierebbe l'inglese già pubblicato di questa, sotto gli occhi di chi
    /// lo sta leggendo e senza che il suo editor abbia pubblicato niente. Congelato, il raggio d'azione di
    /// una correzione resta limitato: gli altri documenti la vedono alla LORO prossima ripubblicazione,
    /// quando il loro editor guarda il diff.</para>
    ///
    /// <para>⚠️ <b>«Dove», non «se».</b> Le impronte che lo snapshot non porta le riempie la memoria viva
    /// (<see cref="NoteAsync"/>): lì non c'è niente di pubblicato da proteggere, e prima del 28 agosto 2026
    /// restavano non tradotte per sempre.</para>
    /// </summary>
    public static IReadOnlyDictionary<string, FrozenTranslation>? Congelate(DocumentView view, string targetLang) =>
        Congelate(view.Translations, targetLang);

    /// <inheritdoc cref="Congelate(DocumentView, string)"/>
    /// <param name="traduzioni">Le traduzioni congelate dello snapshot, per lingua di lettura.</param>
    /// <param name="targetLang">La lingua di chi legge.</param>
    public static IReadOnlyDictionary<string, FrozenTranslation>? Congelate(
        IReadOnlyDictionary<string, Dictionary<string, FrozenTranslation>>? traduzioni, string targetLang) =>
        traduzioni is not null && traduzioni.TryGetValue(targetLang, out var perLingua) ? perLingua : null;

    internal static SectionView TraduciSezione(SectionView s, Func<string?, string?> traduci) => new()
    {
        Id = s.Id,
        Title = traduci(s.Title) ?? s.Title,
        Depth = s.Depth,
        SectionKey = s.SectionKey,
        IsHidden = s.IsHidden,
        BeforeParentBody = s.BeforeParentBody,
        LeadSentence = s.LeadSentence,
        // ⚠️ Anche il destinatario: questa classe RICOSTRUISCE le sezioni, e ogni flag per-sezione che non
        // si ricopia qui viene azzerato dalla traduzione — in silenzio, perché il default è quello «buono»
        // (Both) e la pagina continua a rendersi. Costato una prova live: la chip non compariva mai su un
        // documento tradotto, e il filtro non filtrava.
        Audience = s.Audience,
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

    /// <summary>
    /// Ogni testo traducibile del documento, nell'ordine in cui si incontra.
    /// <para>⚠️ <b>Il titolo del documento non c'è</b>, e non è una dimenticanza: non si traduce (R4), e
    /// quindi non deve nemmeno finire nel conto della copertura — dire «manca una frase» di una frase che
    /// non si tradurrà mai vorrebbe dire lasciare l'avviso acceso per sempre.</para>
    /// </summary>
    private static IEnumerable<string> Segmenti(DocumentView view)
    {
        foreach (var sezione in view.Sections)
            foreach (var s in SegmentiSezione(sezione))
                yield return s;
    }

    /// <summary>Ogni testo traducibile di una sezione e delle sue figlie. Pubblico perché la vIPI ACC porta
    /// le sue sezioni editoriali dentro i blocchi, non dentro un <see cref="DocumentView"/>.</summary>
    public static IEnumerable<string> SegmentiSezione(SectionView sezione)
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

    /// <summary>
    /// Ogni testo traducibile di uno <b>snapshot</b> di release (carta
    /// <c>docs/feature/2026-09-04-stato-traduzione.md</c> §3.1).
    ///
    /// <para>⚠️ <b>Esiste perché la definizione di «segmento» dev'essere UNA.</b> Lo stato della traduzione
    /// confronta quel che la release ha pubblicato con quel che la memoria sa: se contasse i segmenti dello
    /// snapshot con un ciclo suo, basterebbe una virgola di differenza nella segmentazione perché la
    /// percentuale del pubblicato non fosse più confrontabile con quella della bozza — e nessun test lo
    /// direbbe, perché tutt'e due sarebbero «un numero».</para>
    ///
    /// <para>⚠️ E lo snapshot è <b>uniforme per tutte le famiglie</b> (misurato il 4 settembre 2026 sulle 17
    /// release efficaci: vLOA, aeroporto, aeroporto militare, APP e vIPI ACC portano tutte un
    /// <c>Doc.Roots</c>), quindi qui non serve nessun descrittore per-tipo e nessuno
    /// <c>switch (TargetType)</c>.</para>
    /// </summary>
    public static IEnumerable<string> SegmentiGrezzi(RawDocument doc)
    {
        foreach (var radice in doc.Roots)
            foreach (var s in SegmentiSezione(radice))
                yield return s;
    }

    /// <inheritdoc cref="SegmentiGrezzi"/>
    public static IEnumerable<string> SegmentiSezione(RawSection sezione)
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

    /// <summary>Un titolo, se c'è qualcosa da tradurre. Pubblico per la stessa ragione di
    /// <see cref="SegmentiSezione(SectionView)"/>: i titoli dei blocchi della vIPI ACC stanno fuori dalle
    /// sezioni.</summary>
    public static IEnumerable<string> Aggiungi(string? testo)
    {
        var norm = TranslationText.Normalize(testo);
        if (TranslationText.HasSomethingToTranslate(norm)) yield return norm;
    }
}

/// <summary>
/// Una passata di traduzione <b>già caricata</b>: la funzione che traduce un testo, e la copertura di quel
/// che si sta traducendo.
///
/// <para>
/// Esiste perché la lettura della memoria si fa <b>una volta sola</b> (⚠️ una query per segmento sarebbe una
/// corsa sul DbContext del circuito Blazor), mentre i testi da tradurre si incontrano a mano a mano che si
/// ricostruisce quel che la pagina mostrerà.
/// </para>
/// </summary>
/// <param name="Traduci">Il testo nella lingua di lettura, o quello di partenza se la memoria non ce l'ha.</param>
/// <param name="Coverage">Quanto è tradotto, e quanto l'ha riletto una persona.</param>
public sealed record TranslationPass(Func<string?, string?> Traduci, TranslationCoverage Coverage)
{
    /// <summary>La passata che non traduce niente: stessa lingua, o niente da tradurre.</summary>
    public static readonly TranslationPass Nessuna = new(static t => t, TranslationCoverage.Nessuna);

    /// <inheritdoc cref="Traduci"/>
    public string? Testo(string? testo) => Traduci(testo);

    /// <summary>La sezione tradotta, con tutte le figlie e i blocchi.</summary>
    public SectionView Sezione(SectionView sezione) => DocumentTranslator.TraduciSezione(sezione, Traduci);
}
