using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Routing;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// «Apri documento» deve aprire il documento (carta <c>2026-08-22-incarichi-cosa-sono.md</c>, N3).
///
/// <para>⚠️ Fino al 22 agosto 2026 il link lo costruiva <c>TaskDocLink</c> dalla sola chiave di release, e la
/// chiave contiene il codice ACC <b>solo</b> per la vIPI ACC: per aeroporti, APP e vLOA il tasto portava a
/// <c>/vsop/versioni</c>. Tre tipi su quattro, un tasto che diceva «Apri documento» e mostrava un elenco.</para>
/// </summary>
public class EditorTaskLinksTests
{
    [Fact]
    public async Task Ogni_tipo_di_documento_ha_il_link_del_suo_editor()
    {
        var servizio = Servizio(
            Doc(ManagedDocKind.AirportVipi, ReleaseTargetType.Airport, "LIRF", acc: "LIRR", id: 3),
            Doc(ManagedDocKind.AppVipi, ReleaseTargetType.App, "LIML_APP", acc: "LIMM", id: 4),
            Doc(ManagedDocKind.Vloa, ReleaseTargetType.Vloa, "7", acc: "LIRR", id: 7, vicino: "LFFF"),
            Doc(ManagedDocKind.AccVipi, ReleaseTargetType.AccVipi, "LIRR|", acc: "LIRR", id: 9));

        var mappa = await servizio.ForAsync(new[]
        {
            Incarico(ReleaseTargetType.Airport, "LIRF"), Incarico(ReleaseTargetType.App, "LIML_APP"),
            Incarico(ReleaseTargetType.Vloa, "7"), Incarico(ReleaseTargetType.AccVipi, "LIRR|"),
        });

        Assert.Equal("/vsop/lirr/airports/editor?icao=LIRF", mappa[(ReleaseTargetType.Airport, "LIRF")].Url);
        Assert.Equal("/vsop/limm/apps/editor?app=LIML_APP", mappa[(ReleaseTargetType.App, "LIML_APP")].Url);
        Assert.Equal("/vsop/lirr/vloa/editor?acc=LFFF", mappa[(ReleaseTargetType.Vloa, "7")].Url);
        Assert.Equal("/vsop/lirr/editor", mappa[(ReleaseTargetType.AccVipi, "LIRR|")].Url);
    }

    /// <summary>Il titolo corrente serve al <c>title</c> del link; l'etichetta a schermo resta quella scritta
    /// nell'incarico, che è come il documento si chiamava quando l'incarico fu dato.</summary>
    [Fact]
    public async Task Il_link_porta_anche_il_titolo_che_il_documento_ha_adesso()
    {
        var servizio = Servizio(Doc(ManagedDocKind.AirportVipi, ReleaseTargetType.Airport, "LIRF", "LIRR", 3, titolo: "vIPI Fiumicino"));

        var mappa = await servizio.ForAsync(new[] { Incarico(ReleaseTargetType.Airport, "LIRF") });

        Assert.Equal("vIPI Fiumicino", mappa[(ReleaseTargetType.Airport, "LIRF")].TitoloCorrente);
    }

    /// <summary>Un incarico il cui documento è stato eliminato non ha un posto dove andare: la mappa non
    /// inventa un ripiego, e la pagina non mostra un tasto che mente.</summary>
    [Fact]
    public async Task Un_documento_sparito_non_ha_link()
    {
        var servizio = Servizio(Doc(ManagedDocKind.AirportVipi, ReleaseTargetType.Airport, "LIRF", "LIRR", 3));

        var mappa = await servizio.ForAsync(new[] { Incarico(ReleaseTargetType.Airport, "LIMC") });

        Assert.Empty(mappa);
    }

    /// <summary>⚠️ Una query per pagina, e zero se non serve: gli incarichi liberi non sono legati a niente.</summary>
    [Fact]
    public async Task Senza_incarichi_legati_a_un_documento_non_si_interroga_niente()
    {
        var repo = new DocumentiFinti(Doc(ManagedDocKind.AirportVipi, ReleaseTargetType.Airport, "LIRF", "LIRR", 3));
        var servizio = new EditorTaskLinksService(repo, Rotte());

        var mappa = await servizio.ForAsync(new[] { new EditorTask { Id = 1, Title = "Incarico libero" } });

        Assert.Empty(mappa);
        Assert.Equal(0, repo.Letture);
    }

    // ---- impalcatura -----------------------------------------------------------------------------------

    private static EditorTask Incarico(ReleaseTargetType tipo, string chiave) =>
        new() { Id = 1, Title = "x", TargetType = tipo, TargetKey = chiave };

    private static ManagedDoc Doc(ManagedDocKind kind, ReleaseTargetType target, string chiave, string acc,
        int id, string? vicino = null, string titolo = "Documento") =>
        new(kind, titolo, chiave, acc, IsPublished: true, HasDraft: false, IsHidden: false, target, chiave, id, vicino);

    private static IDocRoutesRegistry Rotte() => new DocRoutesRegistry(new IDocKindRoutes[]
    {
        new AccVipiDocRoutes(), new AirportDocRoutes(), new AppDocRoutes(), new VloaDocRoutes(),
    });

    private static EditorTaskLinksService Servizio(params ManagedDoc[] docs) =>
        new(new DocumentiFinti(docs), Rotte());

    private sealed class DocumentiFinti : IDocumentAdminRepository
    {
        private readonly ManagedDoc[] _docs;
        public DocumentiFinti(params ManagedDoc[] docs) => _docs = docs;

        /// <summary>Quante volte l'elenco è stato letto: è il conto che difende «una query per pagina».</summary>
        public int Letture { get; private set; }

        public Task<IReadOnlyList<ManagedDoc>> ListAsync(CancellationToken ct = default)
        {
            Letture++;
            return Task.FromResult<IReadOnlyList<ManagedDoc>>(_docs);
        }

        public Task<IReadOnlyDictionary<int, string>> GetTitlesAsync(IReadOnlyCollection<int> documentIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> GetAccCodeAsync(ManagedDocRef doc, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetHiddenAsync(ManagedDocRef doc, bool hidden, int actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(ManagedDocRef doc, int actorUserId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
