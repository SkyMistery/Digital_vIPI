using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Domain;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il banner di ciò che resta da fare su un documento.
///
/// ⚠️ Dal 26 agosto 2026 legge il read-model unico (<c>IWorkListService</c>) e non più il solo servizio
/// degli impatti: nello stesso banner stanno le segnalazioni del sistema E gli incarichi assegnati su quel
/// documento (carta <c>2026-08-26-da-fare-una-lista-sola.md</c> §2/D4). Quel che si prova qui non cambia —
/// cambia da dove arrivano le righe.
///
/// Il banner delle segnalazioni aperte su un documento. Tre cose devono reggere a schermo, e nessuna è
/// verificabile dai test del servizio: che le righe siano <b>più di una</b> (fino al 25 agosto 2026 il motivo
/// era uno solo e il secondo evento cancellava il primo), che il ✓ <b>non compaia</b> sulle righe calcolate —
/// il giro notturno le riaprirebbe, e l'utente spunterebbe la stessa riga ogni giorno — e che la frase sia
/// <b>ricomposta</b> da chiave e argomenti invece di essere letta da una colonna di testo.
/// </summary>
public class DocReviewBarTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class FakeImpacts : IDocumentImpactService
    {
        public List<DocumentImpactRow> Righe { get; } = new();
        public List<int> Chiusi { get; } = new();

        public Task<IReadOnlyList<DocumentImpactRow>> ListOpenAsync(int documentId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentImpactRow>>(Righe.Where(r => r.DocumentId == documentId).ToList());

        public Task ClearAsync(int impactId, CancellationToken ct = default)
        {
            Chiusi.Add(impactId);
            Righe.RemoveAll(r => r.Id == impactId);
            return Task.CompletedTask;
        }

        public Task<int> RaiseForSectorAsync(ImpactKind kind, string composePosition, string accCode, CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyList<int>> FindDocumentsForSectorAsync(string composePosition, string accCode,
            CancellationToken ct = default) => Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());
        public Task<int> RaiseForAreaAsync(ImpactKind kind, string ivaoId, string areaName, CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> RaiseForDocumentsAsync(ImpactKind kind, IReadOnlyCollection<int> documentIds, string sourceKey, IReadOnlyList<string> args, CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyList<RaiseImpactInput>> PrepareForSectorAsync(ImpactKind kind, string composePosition, string accCode, IReadOnlyList<string> args, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RaiseImpactInput>>(Array.Empty<RaiseImpactInput>());
        public Task<int> ClearBySourceAsync(IReadOnlyCollection<ImpactKind> kinds, string sourceKey, CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> ListOpenByKindCountAsync(ImpactKind kind, CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyDictionary<int, ImpactBadge>> CountOpenAsync(IReadOnlyCollection<int> documentIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, ImpactBadge>>(new Dictionary<int, ImpactBadge>());
        public Task<(int Aperti, int Chiusi)> ReconcileAsync(ImpactKind kind, IReadOnlyCollection<RaiseImpactInput> attuali, CancellationToken ct = default) =>
            Task.FromResult((0, 0));
        public Task<int> PruneClearedBeforeAsync(DateTime cutoffUtc, CancellationToken ct = default) => Task.FromResult(0);
    }

    private FakeImpacts Predisponi(params DocumentImpactRow[] righe)
    {
        var fake = new FakeImpacts();
        fake.Righe.AddRange(righe);
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddScoped<IDocumentImpactService>(_ => fake);
        Services.AddScoped<IEditorTaskService>(_ => new IncarichiFinti());
        // Il banner passa dal read-model: qui gli si dà una vista che traduce le stesse righe di prova,
        // così i test continuano a parlare di IMPATTI e non di infrastruttura.
        Services.AddScoped<IWorkListService>(_ => new LavoroFinto(fake));
        return fake;
    }

    private static DocumentImpactRow Riga(int id, ImpactKind kind, string arg = "LIRR_TS_CTR", bool pubblico = false) =>
        new(id, 7, "vIPI Roma ACC", kind, arg, "Impact_" + kind, new[] { arg }, pubblico, DateTime.UtcNow);

    [Fact]
    public void Senza_Segnalazioni_Il_Banner_Non_Compare()
    {
        Predisponi();
        var cut = RenderComponent<DocReviewBar>(p => p.Add(x => x.DocumentId, 7));

        Assert.Empty(cut.FindAll(".callout"));
    }

    [Fact]
    public void Tre_Segnalazioni_Tre_Righe()
    {
        Predisponi(
            Riga(1, ImpactKind.SectorGone),
            Riga(2, ImpactKind.AreaChanged, "LI D20"),
            Riga(3, ImpactKind.SectorHidden, "LIRR_NE_CTR"));

        var cut = RenderComponent<DocReviewBar>(p => p.Add(x => x.DocumentId, 7));

        Assert.Equal(3, cut.FindAll(".impact-list > li.wi").Count);
        // La frase è composta da chiave + argomenti: il localizzatore di prova restituisce «chiave arg».
        Assert.Contains("Impact_SectorGone LIRR_TS_CTR", cut.Markup);
        Assert.Contains("Impact_AreaChanged LI D20", cut.Markup);
    }

    /// <summary>
    /// ⚠️ <b>Una frase con TRE segnaposto.</b> La composizione si fermava a due argomenti e buttava via il
    /// resto: «l'aeroporto {0} è passato da {1} a {2}» alzava <c>FormatException</c> e — siccome questa riga
    /// vive dentro il banner dell'editor — non rompeva la riga, <b>non faceva partire la pagina</b>. Trovato
    /// guidando l'editor d'aeroporto il 5 settembre 2026, con la suite verde.
    /// </summary>
    [Fact]
    public void Una_frase_con_tre_argomenti_li_prende_tutti()
    {
        Predisponi(new DocumentImpactRow(9, 7, "vIPI Roma ACC", ImpactKind.AirportAccChanged, "LIBD",
            "Impact_AirportAccChanged", new[] { "LIBD", "LIBB", "LIRR" }, false, DateTime.UtcNow));

        var cut = RenderComponent<DocReviewBar>(p => p.Add(x => x.DocumentId, 7));

        Assert.Contains("Impact_AirportAccChanged LIBD LIBB LIRR", cut.Markup);
    }

    /// <summary>⚠️ Il ✓ su una riga calcolata sarebbe un ping-pong con il giro notturno: l'utente la chiude,
    /// il controllo la riapre. Le calcolate si chiudono togliendo la causa.</summary>
    [Fact]
    public void Le_Righe_Calcolate_Non_Hanno_Il_Tasto_Di_Chiusura()
    {
        Predisponi(Riga(1, ImpactKind.ReleaseDrift, "AoR"), Riga(2, ImpactKind.SectorGone));

        var cut = RenderComponent<DocReviewBar>(p => p.Add(x => x.DocumentId, 7));

        Assert.Single(cut.FindAll(".impact-list button"));   // solo quella del settore sparito
    }

    [Fact]
    public async Task Chiudere_Una_Riga_Lascia_Le_Altre()
    {
        var fake = Predisponi(Riga(1, ImpactKind.SectorGone), Riga(2, ImpactKind.SectorHidden, "LIRR_NE_CTR"));
        var cut = RenderComponent<DocReviewBar>(p => p.Add(x => x.DocumentId, 7));

        // ⚠️ Find e non FindAll[0]: l'indicizzatore della collezione rinfrescabile di bUnit sbatte contro
        // l'AngleSharp pinnato a 1.5.0 (MissingMethodException su IHtmlCollection.get_Item).
        await cut.InvokeAsync(() => cut.Find(".impact-list button").Click());

        Assert.Equal(new[] { 1 }, fake.Chiusi);
        Assert.Single(cut.FindAll(".impact-list > li.wi"));
    }

    [Fact]
    public void Una_Sezione_Viva_Si_Vede_Subito()
    {
        Predisponi(Riga(1, ImpactKind.AreaChanged, "LI D20", pubblico: true));

        var cut = RenderComponent<DocReviewBar>(p => p.Add(x => x.DocumentId, 7));

        Assert.Contains("Impact_PublicNow", cut.Markup);
        Assert.Single(cut.FindAll(".pill.red"));
    }

    /// <summary>Traduce le righe di prova in righe di lavoro, con le stesse regole del servizio vero.</summary>
    private sealed class LavoroFinto : IWorkListService
    {
        private readonly FakeImpacts _impatti;
        public LavoroFinto(FakeImpacts impatti) => _impatti = impatti;

        public async Task<IReadOnlyList<WorkItem>> PerDocumentoAsync(int documentId, CancellationToken ct = default)
        {
            var righe = await _impatti.ListOpenAsync(documentId, ct);
            return WorkOrdering.Ordina(righe.Select(r => new WorkItem(
                WorkOrigin.Sistema, $"imp:{r.Id}", r.DocumentId, r.DocumentTitle, "LIRR",
                "/services/vsop/lirr/editor", r.ReasonKey, r.ReasonArgs,
                r.Kind.Severita(r.IsPublicNow), r.Kind.AzioneCheChiude(), r.RaisedUtc, ImpactId: r.Id)));
        }

        public Task<IReadOnlyList<WorkItem>> MieAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> PrendiInCaricoAsync(int i, int a, string? n, string? c, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class IncarichiFinti : IEditorTaskService
    {
        public Task UpdateStatusAsync(int id, EditorTaskStatus s, CancellationToken ct = default) => Task.CompletedTask;
        public bool IsOverdue(EditorTask t) => false;
        public string CurrentCycle() => "2609";
        public IReadOnlyList<AiracCycleInfo> UpcomingCycles(int count) => Array.Empty<AiracCycleInfo>();
        public Task<IReadOnlyList<EditorTask>> ListMineAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<EditorTask>> ListAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CreateAsync(EditorTaskInput input, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AssignAsync(int id, int u, string? n, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
