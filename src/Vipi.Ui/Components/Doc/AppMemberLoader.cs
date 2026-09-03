using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Application.Translation;
using Vipi.Domain;
using Vipi.Ui.Shared;

namespace Vipi.Ui.Components.Doc;

/// <summary>
/// Una vIPI di APP non remotizzato <b>già caricata</b>: il documento nella lingua di lettura, le derivate
/// risolte, e i tre fatti che servono al chrome della pagina (ciclo dell'anteprima, lingua bloccata,
/// copertura di traduzione).
///
/// <para>Esiste perché lo stesso documento va reso in due posti: la sua pagina, e la pagina <b>unita</b> che
/// lo mostra insieme ad altri (carta <c>docs/feature/2026-09-03-documenti-uniti.md</c>).</para>
/// </summary>
public sealed record AppMemberDocument(
    string Callsign,
    string DisplayName,
    DocumentView View,
    PreviewMode Mode,
    string? RelCycle,
    string? Bloccata,
    TranslationCoverage Copertura,
    bool HaMarcate,
    SectionAudience? Vista,
    AppViewDerived Derived,
    IReadOnlyList<AccSpecialAreaView> Areas)
{
    /// <summary>La release che questa vista mostra: quella dell'anteprima, o null = la effettiva adesso.</summary>
    public int? ReleaseIdShown => Mode.Kind == PreviewKind.Release ? Mode.ReleaseId : null;
}

/// <summary>
/// Il caricamento di una vIPI di APP per la lettura: <b>tutto</b> quello che stava in
/// <c>AppnPage.OnParametersSetAsync</c>, spostato di peso perché non appartiene a <i>quella</i> pagina.
///
/// <para>
/// ⚠️ <b>L'ordine dei passi è la correttezza</b>, e i commenti che lo dicono viaggiano col codice: la lingua
/// del documento si decide <b>subito</b> (vale anche per le derivazioni), la traduzione va <b>in fondo</b>
/// (le derivate leggono gli id dall'originale), i titoli di catalogo <b>dopo</b> la traduzione, il filtro di
/// lettura <b>per ultimo</b>.
/// </para>
/// <para>
/// ⚠️ Il degrado di un'anteprima non autorizzata o non corrispondente ricade sulla vista pubblica
/// <b>derivate congelate comprese</b> (<c>useFrozen: true</c>): senza, il congelamento AIRAC sarebbe
/// aggirabile scrivendo un <c>?as=rel:</c> qualsiasi nell'indirizzo.
/// </para>
/// </summary>
public sealed class AppMemberLoader
{
    private readonly IVipiViewService _viewService;
    private readonly IAppDocumentService _appDoc;
    private readonly IAppViewDerivationService _appView;
    private readonly IReleaseService _releases;
    private readonly IEditAuthorizationService _authz;
    private readonly DocumentTranslator _translator;
    private readonly ReadingLanguageContext _lingua;

    public AppMemberLoader(IVipiViewService viewService, IAppDocumentService appDoc,
                           IAppViewDerivationService appView, IReleaseService releases,
                           IEditAuthorizationService authz, DocumentTranslator translator,
                           ReadingLanguageContext lingua)
    {
        _viewService = viewService;
        _appDoc = appDoc;
        _appView = appView;
        _releases = releases;
        _authz = authz;
        _translator = translator;
        _lingua = lingua;
    }

    /// <summary>
    /// Carica la vIPI dell'APP <paramref name="callsign"/>. null = quel callsign non è un APP non remotizzato,
    /// oppure non ha un documento da mostrare: è la stessa risposta che dava la pagina, e chi chiama disegna
    /// il suo avviso.
    /// </summary>
    /// <param name="mode">La modalità chiesta dall'indirizzo. Quella <b>ottenuta</b> può essere diversa —
    /// il degrado — e sta in <see cref="AppMemberDocument.Mode"/>: chi disegna il banner deve leggere quella.</param>
    /// <param name="fissaLaPagina">Falso quando questo documento e' un MEMBRO di un'unione: la lingua
    /// della pagina la decide l'OSPITE. Vedi <see cref="LinguaDelDocumento.Prepara"/>.</param>
    public async Task<AppMemberDocument?> LoadAsync(string callsign, PreviewMode mode, string? vista,
                                                    bool fissaLaPagina = true,
                                                    CancellationToken ct = default)
    {
        var app = (callsign ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(app)) return null;

        var identity = await _appDoc.GetIdentityAsync(app);
        if (identity is null) return null;   // callsign non è un APP standalone

        var displayName = string.IsNullOrWhiteSpace(identity.Title) ? app : identity.Title;
        var canEdit = _authz.IsEditor;

        DocumentView? view = null;
        var useFrozen = false;
        string? relCycle = null;

        // Vista documentale (struttura + editoriale). Le derivate restano vuote nel view → rese live sotto.
        switch (mode.Kind)
        {
            case PreviewKind.Draft:
                if (canEdit)
                    view = await _viewService.BuildAppVipiAsync(app, BlockTier.Extended, live: false,
                                                                ignoreRelease: true, preferWorking: true);
                else { mode = default; (view, useFrozen) = await PubblicaAsync(app); }
                break;
            case PreviewKind.Release:
                var pv = await _releases.GetPreviewAsync(mode.ReleaseId, ReleaseTargetType.App, app, ct);
                if (pv?.Doc is not null)
                {
                    relCycle = pv.AiracCycle;
                    view = await _viewService.BuildFromRawAsync(pv.Doc, BlockTier.Extended);
                }
                else { mode = default; (view, useFrozen) = await PubblicaAsync(app); }
                break;
            default:
                (view, useFrozen) = await PubblicaAsync(app);
                break;
        }
        if (view is null) return null;

        // ---- In che lingua si legge QUESTO documento (carta 2026-08-31-lingua-bloccata.md §3-4) ---------
        // ⚠️ SUBITO, appena si sa qual è il documento, e non in fondo insieme alla traduzione: se è bloccato
        // la lingua vale anche per le DERIVAZIONI che partono qui sotto, che compongono prosa ed etichette.
        // Deciderla in fondo vorrebbe dire derivare nella lingua di chi guarda e tradurre il resto nell'altra
        // — mezza schermata per lingua, e nessun errore da nessuna parte.
        var lettore = LinguaDelDocumento.Prepara(_lingua, view.LanguageLocked, view.Language, Language.It,
                                                 fissaLaPagina: fissaLaPagina);
        var bloccata = view.LanguageLocked ? lettore : null;

        // Derivate: frozen dalla release effettiva nella vista pubblica, live in bozza/anteprima (doc 10 §3d).
        // Il documento mostrato viaggia col resolver: la tabella «Configurazioni» si deriva dalle configurazioni
        // di QUESTA versione, non da quella di lavoro.
        var derived = await _appView.ResolveForViewAsync(app, view, useFrozen, ct);

        // Aree regolamentate: gli id li porta il documento mostrato (pubblico/bozza/release), i dettagli e le
        // shape vengono dai cataloghi correnti — come nella vIPI ACC.
        var regulated = view.Sections.FirstOrDefault(
            s => string.Equals(s.SectionKey, "regulated", StringComparison.OrdinalIgnoreCase));
        var areas = regulated is null
            ? Array.Empty<AccSpecialAreaView>()
            : await _appDoc.ResolveRegulatedAreasAsync(SezioniDocumentali.LeggiRegulated(regulated), ct);

        // ---- Lettura bilingue (carta 2026-08-27 §7) --------------------------------------------------
        // ⚠️ IN FONDO, dopo le derivate e le aree: quelle si leggono dall'originale (gli id stanno nel JSON dei
        // blocchi), la traduzione porta la PROSA. Tradurre prima vorrebbe dire cercare «regulated» in un
        // documento che nel frattempo dice altro.
        var tradotto = await _translator.TranslateAsync(view, Language.It, lettore);
        view = tradotto.View;

        // I titoli delle sezioni di CATALOGO nella lingua di lettura, DOPO la traduzione.
        // ⚠️ Non è un doppione della passata: quei titoli stanno scritti nel documento nella lingua che aveva
        // alla NASCITA, e la passata li copre solo finché c'è una passata. Su un documento BLOCCATO la
        // traduzione è spenta per definizione (sorgente == bersaglio).
        view = SezioniDocumentali.ConSezioni(view, TitoliDiCatalogo.Applica(view.Sections, SectionProfile.App, lettore));

        // Il filtro di lettura, DOPO la traduzione: si filtra la vista che il lettore vedrà davvero.
        var letturaVista = AudienceFilter.Leggi(vista);
        var haMarcate = AudienceFilter.HaSezioniMarcate(view.Sections);
        view = SezioniDocumentali.ConSezioni(view, AudienceFilter.Filtra(view.Sections, letturaVista));

        return new AppMemberDocument(app, displayName, view, mode, relCycle, bloccata, tradotto.Coverage,
                                     haMarcate, letturaVista, derived, areas);
    }

    // Vista PUBBLICA (doc 11 §3d): documento e derivate frozen si impostano INSIEME. È anche il degrado di
    // un'anteprima non autorizzata/non corrispondente: senza, il fallback lasciava useFrozen=false e serviva
    // al pubblico le derivate LIVE (congelamento AIRAC bypassabile dall'URL con un ?as=rel: qualsiasi).
    private async Task<(DocumentView? View, bool UseFrozen)> PubblicaAsync(string app) =>
        (await _viewService.BuildAppVipiAsync(app, BlockTier.Extended, live: false), true);
}
