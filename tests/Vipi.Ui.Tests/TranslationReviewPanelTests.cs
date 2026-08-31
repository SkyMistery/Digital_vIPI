using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Translation;
using Vipi.Domain.Entities;
using Vipi.Ui;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il correttore delle traduzioni dentro l'editor, e le due regole sul <c>DbContext</c> che questo file
/// aveva perso.
///
/// <para>🔴 <b>Il guasto.</b> Il 31 agosto 2026, su <c>/services/vsop/libb/editor</c>, due volte in dieci
/// secondi (codici <c>00-bea549b1…</c> e <c>00-a0ff0695…</c>):</para>
/// <code>
/// A second operation was started on this context instance
///    at EfEditingRepository.LoadForEditAsync ← DocumentTranslationReview.RigheAsync
///    ← TranslationReviewPanel.OnParametersSetAsync
/// </code>
///
/// <para>Due difetti nello stesso metodo, ognuno già scritto altrove e nessuno dei due applicato qui:</para>
/// <list type="number">
///   <item><b>Il pannello non era isolato</b>: leggeva sul contesto del circuito, cioè quello che l'editor
///         genitore sta usando mentre monta i figli. È la stessa cosa per cui sei componenti hanno un loro
///         scope dal 30 luglio 2026.</item>
///   <item><b>La lettura non era condizionata al cambio dei parametri</b>:
///         <c>OnParametersSetAsync</c> scatta a ogni ridisegno del genitore, e l'editor si ridisegna al
///         primo <c>await</c> di qualunque suo gestore. È la regola pagata il 1 agosto 2026 con
///         <c>ReleasePanel</c>.</item>
/// </list>
///
/// <para>⚠️ E il costo non era una query: <c>RigheAsync</c> carica il <b>documento intero</b>, e il pannello
/// lo caricava <b>due volte</b> (la seconda con l'altra lingua, solo per capire se il vuoto significasse
/// «stessa lingua»). Per un blocco che è chiuso di suo.</para>
/// </summary>
public class TranslationReviewPanelTests : TestContext
{
    /// <summary>Conta le letture e sa dire con quali argomenti sono arrivate.</summary>
    private sealed class RevisioneFinta : IDocumentTranslationReview
    {
        public int Letture { get; private set; }
        public List<string> Lingue { get; } = new();

        public Task<RevisioneDocumento> RevisioneAsync(int documentId, string targetLang, CancellationToken ct = default)
        {
            Letture++;
            Lingue.Add(targetLang);
            return Task.FromResult(new RevisioneDocumento("it", new[]
            {
                new RigaDaRivedere("Contatta la torre.", "Contact the tower.",
                    TranslationOrigin.Machine, false, "Regole piste"),
            }));
        }

        public Task<int> DocumentiToccatiAsync(string sorgente, CancellationToken ct = default) =>
            Task.FromResult(1);

        public Task CorreggiAsync(int documentId, string targetLang, string sorgente, string tradotto,
            CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Enumerable.Empty<LocalizedString>();
    }

    private RevisioneFinta Arrangia()
    {
        var revisione = new RevisioneFinta();
        // ⚠️ Registrato come SCOPED, non singleton: il pannello lo risolve dal proprio ScopedServices, e un
        // servizio che non fosse risolvibile da uno scope figlio farebbe fallire il test per il motivo
        // sbagliato. Registrando l'istanza come scoped si copre entrambe le strade.
        Services.AddScoped<IDocumentTranslationReview>(_ => revisione);
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        JSInterop.Mode = JSRuntimeMode.Loose;
        return revisione;
    }

    /// <summary>
    /// Il documento si carica <b>una volta</b> al montaggio: prima erano due, la seconda solo per dedurre la
    /// lingua sorgente.
    /// </summary>
    [Fact]
    public void Al_montaggio_il_documento_si_legge_una_volta_sola()
    {
        var revisione = Arrangia();

        RenderComponent<TranslationReviewPanel>(p => p.Add(x => x.DocumentId, 7));

        Assert.Equal(1, revisione.Letture);
    }

    /// <summary>
    /// ⚠️ Il ridisegno del genitore <b>non</b> rilegge. È il difetto vero: nell'editor ogni salvataggio, ogni
    /// sezione aggiunta, ogni blocco aperto ridisegna la pagina, e ogni ridisegno ricaricava il documento.
    /// Non è l'<c>@if</c> sui dati caricati a proteggere: il pericolo è il RI-render, non il montaggio.
    /// </summary>
    [Fact]
    public void Un_ridisegno_del_genitore_non_rilegge_il_documento()
    {
        var revisione = Arrangia();

        var cut = RenderComponent<TranslationReviewPanel>(p => p.Add(x => x.DocumentId, 7));
        Assert.Equal(1, revisione.Letture);

        // Tre giri di parametri identici: è quel che fa il genitore quando si ridisegna.
        for (var i = 0; i < 3; i++) cut.SetParametersAndRender(p => p.Add(x => x.DocumentId, 7));

        Assert.Equal(1, revisione.Letture);
    }

    /// <summary>Ma se il documento cambia davvero, si rilegge: la guardia non deve diventare una cache cieca.</summary>
    [Fact]
    public void Se_cambia_il_documento_si_rilegge()
    {
        var revisione = Arrangia();

        var cut = RenderComponent<TranslationReviewPanel>(p => p.Add(x => x.DocumentId, 7));
        cut.SetParametersAndRender(p => p.Add(x => x.DocumentId, 9));

        Assert.Equal(2, revisione.Letture);
    }
}
