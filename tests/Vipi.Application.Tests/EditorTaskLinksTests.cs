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
/// <c>/services/vsop/versions</c>. Tre tipi su quattro, un tasto che diceva «Apri documento» e mostrava un elenco.</para>
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

        Assert.Equal("/services/vsop/lirr/airports/editor?icao=LIRF", mappa[(ReleaseTargetType.Airport, "LIRF")].Url);
        Assert.Equal("/services/vsop/limm/apps/editor?app=LIML_APP", mappa[(ReleaseTargetType.App, "LIML_APP")].Url);
        Assert.Equal("/services/vsop/lirr/vloa/editor?acc=LFFF", mappa[(ReleaseTargetType.Vloa, "7")].Url);
        Assert.Equal("/services/vsop/lirr/editor", mappa[(ReleaseTargetType.AccVipi, "LIRR|")].Url);
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


    /// <summary>
    /// ⚠️ Il difetto N12, visto guardando la pagina e non da un'asserzione: la tendina si costruiva la chiave
    /// da se' — <c>"{acc}|"</c> per la vIPI ACC — mentre la chiave vera e' <c>{acc}|{callsign primario}</c>.
    /// L'incarico nasceva puntando a un documento che non esiste, il collegamento non si risolveva mai, e la
    /// pagina diceva «il documento collegato non esiste piu'»: falso, e su un documento che era li'.
    /// Chi sceglie e chi ritrova devono leggere la STESSA chiave, dallo stesso elenco.
    /// </summary>
    [Fact]
    public async Task La_chiave_che_si_sceglie_e_la_stessa_che_ritrova_il_documento()
    {
        var accVipi = Doc(ManagedDocKind.AccVipi, ReleaseTargetType.AccVipi, "LIBB|LIBB_CTR", acc: "LIBB", id: 9);
        var servizio = Servizio(accVipi);

        var opzioni = await servizio.OpzioniAsync();
        var scelta = opzioni.Single(o => o.Type == ReleaseTargetType.AccVipi);

        // La chiave scelta e' proprio quella dell'elenco, e con quella il link si risolve.
        Assert.Equal("LIBB|LIBB_CTR", scelta.Key);
        var mappa = await servizio.ForAsync(new[] { Incarico(scelta.Type, scelta.Key) });
        Assert.Equal("/services/vsop/libb/editor", mappa[(scelta.Type, scelta.Key)].Url);
    }

    /// <summary>Un documento nascosto non si assegna: l'incarico punterebbe a qualcosa che non si vede.</summary>
    [Fact]
    public async Task I_documenti_nascosti_non_sono_fra_le_opzioni()
    {
        var visibile = Doc(ManagedDocKind.AirportVipi, ReleaseTargetType.Airport, "LIRF", "LIRR", 3);
        var nascosto = new ManagedDoc(ManagedDocKind.AirportVipi, "Nascosto", "LIMC", "LIMM",
            IsPublished: true, HasDraft: false, IsHidden: true, ReleaseTargetType.Airport, "LIMC", 4);

        var opzioni = await Servizio(visibile, nascosto).OpzioniAsync();

        Assert.Single(opzioni);
        Assert.Equal("LIRF", opzioni[0].Key);
    }


    /// <summary>⚠️ Visto guidando la pagina: fra le opzioni compariva «Airport:» — chiave VUOTA. Un incarico
    /// creato su quella non si sarebbe risolto mai, perche' non c'e' niente da cercare.</summary>
    [Fact]
    public async Task Un_documento_senza_chiave_non_si_puo_collegare()
    {
        var buono = Doc(ManagedDocKind.AirportVipi, ReleaseTargetType.Airport, "LIRF", "LIRR", 3);
        var senzaChiave = new ManagedDoc(ManagedDocKind.AirportVipi, "Senza chiave", "", "LIMM",
            IsPublished: true, HasDraft: false, IsHidden: false, ReleaseTargetType.Airport, "", 5);

        var opzioni = await Servizio(buono, senzaChiave).OpzioniAsync();

        Assert.Single(opzioni);
        Assert.Equal("LIRF", opzioni[0].Key);
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
