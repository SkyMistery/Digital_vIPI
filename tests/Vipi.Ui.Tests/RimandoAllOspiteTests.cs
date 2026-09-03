using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Routing;
using Vipi.Domain;
using Vipi.Domain.Services;
using Vipi.Ui;
using Vipi.Ui.Components.Doc;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il rimando dalla pagina di un MEMBRO a quella dell'ospite (carta
/// <c>docs/feature/2026-09-03-documenti-uniti.md</c> §4), e la sola domanda che non si faceva: <b>l'ospite
/// ha qualcosa da mostrare?</b>
///
/// <para>
/// 🔴 Trovato in supervisione il 3 settembre 2026. Unire un APP <b>già pubblicato</b> sotto una vIPI
/// d'aeroporto ancora <b>in bozza</b> è un gesto di due clic, e senza guardia la pagina pubblica dell'APP
/// mandava chi la apriva su una pagina che dice «niente da mostrare»: un documento in vigore sparito dal web
/// per un gesto editoriale che non lo riguardava.
/// </para>
///
/// <para>⚠️ La visibilità pubblica <b>È</b> la release effettiva (<c>EfContentRepository</c>): non basta
/// che l'ospite esista, e nemmeno che abbia release — deve averne una <b>in vigore adesso</b>.</para>
/// </summary>
public class RimandoAllOspiteTests
{
    private static ReleaseInfo Rel(string ciclo, bool inVigore) =>
        new(1, VersionNumber: 1, ReleaseAiracCycle: ciclo,
            ReleaseEffectiveUtc: new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc),
            Status: inVigore ? ReleaseStatus.Effective : ReleaseStatus.Scheduled,
            CreatedByUserId: 1, CreatedUtc: new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc),
            Note: null, IsEffectiveNow: inVigore);

    private static ManagedDoc Doc(int id, ReleaseTargetType tipo, string chiave, string titolo, bool nascosto = false) =>
        new(tipo, titolo, chiave, "LIRR", IsPublished: true, HasDraft: false, IsHidden: nascosto, tipo, chiave, id);

    /// <summary>L'unione di prova: aeroporto LIBA ospite, LIBA_APP membro.</summary>
    private static UnionView Unione(bool ospiteNascosto = false) =>
        new(1, new[]
        {
            new UnionMemberView(1, 0, IsHost: true,  Doc(26, ReleaseTargetType.Airport, "LIBA", "vIPI — LIBA", ospiteNascosto)),
            new UnionMemberView(2, 1, IsHost: false, Doc(3,  ReleaseTargetType.App, "LIBA_APP", "Amendola Approach")),
        });

    private static UnionLoader Loader(ReleaseFinte rel) =>
        new(new ServiceCollection().BuildServiceProvider(), rel, new RotteFinte());

    [Fact]
    public async Task Con_l_ospite_IN_VIGORE_il_membro_rimanda_alla_pagina_unita()
    {
        var rel = new ReleaseFinte();
        rel.Releases.Add(Rel("2609", inVigore: true));

        var url = await Loader(rel).IndirizzoDellOspiteAsync(Unione(), ReleaseTargetType.App, "LIBA_APP");

        Assert.Equal("/pubblica/Airport/LIBA#doc-3", url);
    }

    /// <summary>🔴 Il caso del difetto: l'ospite non ha ancora pubblicato niente.</summary>
    [Fact]
    public async Task Se_l_ospite_non_ha_NIENTE_in_vigore_il_membro_resta_dov_e()
    {
        var rel = new ReleaseFinte();   // nessuna release

        var url = await Loader(rel).IndirizzoDellOspiteAsync(Unione(), ReleaseTargetType.App, "LIBA_APP");

        Assert.Null(url);
    }

    /// <summary>⚠️ E una release PROGRAMMATA non basta: al pubblico non si vede ancora.</summary>
    [Fact]
    public async Task Una_release_solo_PROGRAMMATA_dell_ospite_non_basta()
    {
        var rel = new ReleaseFinte();
        rel.Releases.Add(Rel("2610", inVigore: false));

        var url = await Loader(rel).IndirizzoDellOspiteAsync(Unione(), ReleaseTargetType.App, "LIBA_APP");

        Assert.Null(url);
    }

    /// <summary>⚠️ Un ospite NASCOSTO ha release in vigore e non si vede lo stesso: il flag lo toglie
    /// dagli elenchi pubblici, e mandarci qualcuno sarebbe mandarlo su una pagina vuota.</summary>
    [Fact]
    public async Task Un_ospite_NASCOSTO_non_riceve_nessuno()
    {
        var rel = new ReleaseFinte();
        rel.Releases.Add(Rel("2609", inVigore: true));

        var url = await Loader(rel).IndirizzoDellOspiteAsync(Unione(ospiteNascosto: true),
                                                             ReleaseTargetType.App, "LIBA_APP");

        Assert.Null(url);
    }

    /// <summary>⚠️ Chi È l'ospite non rimanda a se stesso, e la domanda non costa nemmeno una lettura.</summary>
    [Fact]
    public async Task L_ospite_non_rimanda_a_se_stesso()
    {
        var url = await Loader(new ReleaseFinte())
            .IndirizzoDellOspiteAsync(Unione(), ReleaseTargetType.Airport, "LIBA");

        Assert.Null(url);
    }

    /// <summary>Rotte finte: un indirizzo pubblico riconoscibile, per non dipendere da quelle vere.</summary>
    private sealed class RotteFinte : IDocRoutesRegistry
    {
        public IDocKindRoutes For(ReleaseTargetType type) => new Rotta(type);

        private sealed class Rotta : IDocKindRoutes
        {
            public Rotta(ReleaseTargetType t) => Target = t;
            public ReleaseTargetType Target { get; }
            public string? PublicUrl(string acc, string key, string? neighbourCode) => $"/pubblica/{Target}/{key}";
            public string? ViewerUrl(string acc, string key, string? neighbourCode, int releaseId) => null;
            public string? DraftUrl(string acc, string key, string? neighbourCode) => null;
            public string? EditorUrl(string acc, string key, string? neighbourCode, int? documentId) => null;
        }
    }

    private sealed class ReleaseFinte : IReleaseService
    {
        public Task<IReadOnlyList<ReleaseDiffRow>> DriftFromEffectiveAsync(ReleaseTargetType type, string key, string? alCiclo = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ReleaseDiffRow>>(Array.Empty<ReleaseDiffRow>());
        public AiracCycleInfo NextCycle() =>
            new("2609", new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc));

        public List<ReleaseInfo> Releases { get; } = new();
        public ReleaseDiff Diff { get; set; } =
            new(true, "2607", new[] { new ReleaseDiffRow("Separazioni", ReleaseChangeKind.Modified, 3, 5) });

        public int Published, PublishedNow, Canceled, DiffCalls;
        public string? LastCycle, LastNote;

        /// <summary>Le release di un ALTRO bersaglio, per chiave. ⚠️ Serve a distinguere «il membro ha
        /// pubblicato a quel ciclo» da «esiste»: la domanda dell'annullamento conta le sorelle vere, non i
        /// membri, e un doppio che risponde lo stesso a tutte le chiavi non vedrebbe mai la differenza.</summary>
        public Dictionary<string, List<ReleaseInfo>> PerChiave { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<ReleaseInfo>> ListAsync(ReleaseTargetType type, string key, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ReleaseInfo>>(
                PerChiave.TryGetValue(key ?? "", out var sue) ? sue.ToList() : Releases.ToList());

        public Task PublishAsync(ReleaseTargetType type, string key, string releaseCycle, string? note, CancellationToken ct = default)
        {
            Published++; LastCycle = releaseCycle; LastNote = note;
            return Task.CompletedTask;
        }

        public Task PublishNowAsync(ReleaseTargetType type, string key, string? note, CancellationToken ct = default)
        {
            PublishedNow++; LastNote = note;
            return Task.CompletedTask;
        }

        /// <summary>I membri dell'unione di questo documento. Vuoto = documento solo, che e' il caso
        /// normale: `PublishAsync` e `PublishNowAsync` allora pubblicano lui e basta.</summary>
        public Task<IReadOnlyList<Vipi.Application.Content.BersaglioUnito>> BersagliUnitiAsync(
            ReleaseTargetType type, string key, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Vipi.Application.Content.BersaglioUnito>>(Uniti);

        /// <summary>I membri che il pannello deve annunciare. Vuoto = documento solo.</summary>
        public List<Vipi.Application.Content.BersaglioUnito> Uniti { get; } = new();

        public Task CancelReleaseAsync(int releaseId, CancellationToken ct = default)
        {
            Canceled++;
            return Task.CompletedTask;
        }

        public Task<ReleaseDiff> DiffAsync(int releaseId, CancellationToken ct = default)
        {
            DiffCalls++;
            return Task.FromResult(Diff);
        }

        public string CurrentCycle() => "2607";

        public IReadOnlyList<AiracCycleInfo> UpcomingCycles(int count) => new[]
        {
            new AiracCycleInfo("2608", new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc)),
            new AiracCycleInfo("2609", new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc)),
        };

        // Non usati dal pannello.
        public Task<int> BackfillMissingReleasesAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<ReleasePreview?> GetPreviewAsync(int releaseId, ReleaseTargetType expectedType, string expectedKey, CancellationToken ct = default) => Task.FromResult<ReleasePreview?>(null);
        public Task<ReleaseLocation?> GetLocationAsync(int releaseId, CancellationToken ct = default) => Task.FromResult<ReleaseLocation?>(null);
        public Task<IReadOnlyDictionary<(ReleaseTargetType Type, string Key), ReleaseSummary>> SummariesAsync(
            IReadOnlyList<(ReleaseTargetType Type, string Key)> targets, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<(ReleaseTargetType, string), ReleaseSummary>>(
                new Dictionary<(ReleaseTargetType, string), ReleaseSummary>());
        public Task<int> PruneAllAsync(CancellationToken ct = default) => Task.FromResult(0);
    }
}
