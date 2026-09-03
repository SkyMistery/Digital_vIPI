using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Services;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il quadro del <b>ciclo entrante</b> e la programmazione in blocco (carta 2026-09-02 §AW3).
/// </summary>
public class ProssimoAiracServiceTests
{
    private const string Entrante = "2609";
    private static readonly DateTime EfficaceEntrante = new(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc);

    private static ManagedDoc Doc(string titolo, string chiave, bool pubblicato = true, bool nascosto = false,
        string? programmataA = null, string? inVigoreA = null) =>
        new(ReleaseTargetType.Airport, titolo, chiave, "LIRR",
            IsPublished: pubblicato, HasDraft: false, IsHidden: nascosto,
            ReleaseTargetType.Airport, chiave, DocumentId: 1,
            EffectiveCycle: inVigoreA,
            NextScheduledCycle: programmataA);

    private static ProssimoAiracService Sut(FakeAdmin admin, FakeReleases rel) =>
        new(admin, rel, new AiracService());

    [Fact]
    public async Task Distingue_chi_ha_gia_una_release_al_ciclo_entrante()
    {
        var admin = new FakeAdmin(
            Doc("LIRF", "LIRF", programmataA: Entrante),
            Doc("LIMC", "LIMC"),
            Doc("LIPZ", "LIPZ", programmataA: "2610"));   // programmata SÌ, ma a un altro ciclo

        var q = await Sut(admin, new FakeReleases()).LeggiAsync();

        Assert.Equal(Entrante, q.CicloEntrante);
        Assert.Equal(1, q.Programmati);
        Assert.Equal(2, q.DaProgrammare);
    }

    /// <summary>
    /// ⚠️ Una release schedulata a un ciclo <b>più in là</b> non copre quello entrante: al rollover del 2609
    /// resterebbe in vigore la copia vecchia, e la riga direbbe «a posto». È il caso di <c>LIPZ</c> sopra.
    /// </summary>
    [Fact]
    public async Task Una_release_a_un_ciclo_piu_lontano_non_copre_lentrante()
    {
        var admin = new FakeAdmin(Doc("LIPZ", "LIPZ", programmataA: "2612"));

        var q = await Sut(admin, new FakeReleases()).LeggiAsync();

        Assert.Equal(1, q.DaProgrammare);
        Assert.False(Assert.Single(q.Documenti).GiaProgrammato);
    }

    /// <summary>Bozze e documenti nascosti restano fuori: su una bozza «programmare una release» non vuol
    /// dire niente, e un documento nascosto non lo legge nessuno.</summary>
    [Fact]
    public async Task Bozze_e_nascosti_restano_fuori()
    {
        var admin = new FakeAdmin(
            Doc("bozza", "A", pubblicato: false),
            Doc("nascosto", "B", nascosto: true),
            Doc("buono", "C"));

        var q = await Sut(admin, new FakeReleases()).LeggiAsync();

        Assert.Equal("buono", Assert.Single(q.Documenti).Titolo);
    }

    /// <summary>
    /// ⚠️ <b>Il difetto misurato dal vivo il 2 settembre 2026.</b> Una release <b>programmata</b> non promuove
    /// la bozza a versione pubblicata — è voluto — quindi un documento pubblicato <i>solo</i> per
    /// schedulazione resta <c>Status = Draft</c> pur essendo <b>in vigore e letto dal pubblico</b>. Col
    /// cancello su <c>IsPublished</c> restava fuori da qui e dal giro della deriva: sul database di sviluppo
    /// erano due su diciassette (vIPI Milano al 2608, Catania Radar al 2607).
    /// <para>E si alimentava da sé: programmare al ciclo entrante è proprio il gesto che questa carta
    /// insegna, quindi più lo si usava, più documenti uscivano dal controllo.</para>
    /// </summary>
    [Fact]
    public async Task Un_documento_in_vigore_ma_non_promosso_resta_dentro()
    {
        var admin = new FakeAdmin(Doc("vIPI Milano", "LIMM_WS2_CTR", pubblicato: false, inVigoreA: "2608"));

        var q = await Sut(admin, new FakeReleases()).LeggiAsync();

        Assert.Equal("vIPI Milano", Assert.Single(q.Documenti).Titolo);
        Assert.Equal(1, q.DaProgrammare);
    }

    /// <summary>⚠️ Ma nascosto resta fuori comunque: il cancello è «non nascosto E (pubblicato O in vigore)».</summary>
    [Fact]
    public async Task Un_documento_in_vigore_ma_nascosto_resta_fuori()
    {
        var admin = new FakeAdmin(Doc("vLOA nascosta", "X", pubblicato: false, nascosto: true, inVigoreA: "2607"));

        Assert.Empty((await Sut(admin, new FakeReleases()).LeggiAsync()).Documenti);
    }

    /// <summary>I mancanti stanno in cima: sono quelli da guardare.</summary>
    [Fact]
    public async Task I_mancanti_vengono_prima()
    {
        var admin = new FakeAdmin(Doc("AAA", "A", programmataA: Entrante), Doc("ZZZ", "Z"));

        var q = await Sut(admin, new FakeReleases()).LeggiAsync();

        Assert.Equal(new[] { "ZZZ", "AAA" }, q.Documenti.Select(d => d.Titolo).ToArray());
    }

    [Fact]
    public async Task Programma_solo_i_mancanti_e_al_ciclo_entrante()
    {
        var admin = new FakeAdmin(Doc("LIRF", "LIRF", programmataA: Entrante), Doc("LIMC", "LIMC"));
        var rel = new FakeReleases();

        var esito = await Sut(admin, rel).ProgrammaMancantiAsync();

        Assert.Equal(1, esito.Programmati);
        Assert.Empty(esito.Saltati);
        Assert.Equal(new[] { ("LIMC", Entrante) }, rel.Pubblicate.ToArray());
    }

    /// <summary>
    /// ⚠️ <b>Un documento occupato non ferma gli altri.</b> Il permesso negato e il lock di un altro editor
    /// sono i casi normali di un giro su decine di documenti: si saltano, si <b>dicono</b>, e il giro
    /// prosegue. Un giro che riesce a metà in silenzio è peggio di uno che fallisce.
    /// </summary>
    [Fact]
    public async Task Chi_non_passa_viene_detto_e_gli_altri_proseguono()
    {
        var admin = new FakeAdmin(Doc("LIRF", "LIRF"), Doc("LIMC", "LIMC"), Doc("LIPZ", "LIPZ"));
        var rel = new FakeReleases { RifiutaChiave = "LIMC", Motivo = "Documento occupato da un altro editor." };

        var esito = await Sut(admin, rel).ProgrammaMancantiAsync();

        Assert.Equal(2, esito.Programmati);
        var (titolo, motivo) = Assert.Single(esito.Saltati);
        Assert.Equal("LIMC", titolo);
        Assert.Equal("Documento occupato da un altro editor.", motivo);
        // L'ordine e' quello del quadro: mancanti prima, poi ACC, poi titolo. LIMC salta.
        Assert.Equal(new[] { "LIPZ", "LIRF" }, rel.Pubblicate.Select(p => p.Chiave).ToArray());
    }

    // ---- doppi ----

    private sealed class FakeAdmin : IDocumentAdminRepository
    {
        private readonly ManagedDoc[] _docs;
        public FakeAdmin(params ManagedDoc[] docs) => _docs = docs;
        public Task<IReadOnlyList<ManagedDoc>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ManagedDoc>>(_docs);
        public Task<IReadOnlyDictionary<int, ManagedDoc>> DescribeAsync(IReadOnlyCollection<int> documentIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, ManagedDoc>>(
                _docs.Where(d => d.DocumentId is not null && documentIds.Contains(d.DocumentId.Value))
                     .ToDictionary(d => d.DocumentId!.Value));
        public Task<ManagedDocRef?> FindAsync(ReleaseTargetType kind, string key, CancellationToken ct = default) =>
            Task.FromResult<ManagedDocRef?>(null);
        public Task<IReadOnlyDictionary<int, string>> GetTitlesAsync(IReadOnlyCollection<int> documentIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string>());
        public Task<DocumentLanguageState?> GetLanguageAsync(ManagedDocRef doc, CancellationToken ct = default) =>
            Task.FromResult<DocumentLanguageState?>(null);
        public Task SetLanguageAsync(ManagedDocRef doc, Language language, bool locked, int actorUserId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<string?> GetAccCodeAsync(ManagedDocRef doc, CancellationToken ct = default) =>
            Task.FromResult<string?>("LIRR");
        public Task SetHiddenAsync(ManagedDocRef doc, bool hidden, int actorUserId, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(ManagedDocRef doc, int actorUserId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeReleases : IReleaseService
    {
        public List<(string Chiave, string Ciclo)> Pubblicate { get; } = new();
        public string? RifiutaChiave { get; set; }
        public string Motivo { get; set; } = "no";

        public AiracCycleInfo NextCycle() => new(Entrante, EfficaceEntrante);

        // Le porte dell'unione: questi doppi non pubblicano niente, e senza unione sono le stesse di sotto.
        public Task<IReadOnlyList<BersaglioUnito>> BersagliUnitiAsync(
            ReleaseTargetType type, string key, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BersaglioUnito>>(Array.Empty<BersaglioUnito>());
        public Task PublishUnionAsync(ReleaseTargetType type, string key, string releaseCycle, string? note, CancellationToken ct = default) =>
            PublishAsync(type, key, releaseCycle, note, ct);
        public Task PublishUnionNowAsync(ReleaseTargetType type, string key, string? note, CancellationToken ct = default) =>
            PublishNowAsync(type, key, note, ct);

        public Task PublishAsync(ReleaseTargetType type, string key, string releaseCycle, string? note, CancellationToken ct = default)
        {
            if (string.Equals(key, RifiutaChiave, StringComparison.Ordinal)) throw new InvalidOperationException(Motivo);
            Pubblicate.Add((key, releaseCycle));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ReleaseDiffRow>> DriftFromEffectiveAsync(ReleaseTargetType type, string key, string? alCiclo = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ReleaseDiffRow>>(Array.Empty<ReleaseDiffRow>());
        public Task<IReadOnlyList<ReleaseInfo>> ListAsync(ReleaseTargetType type, string key, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ReleaseInfo>>(Array.Empty<ReleaseInfo>());
        public Task PublishNowAsync(ReleaseTargetType type, string key, string? note, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> BackfillMissingReleasesAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task CancelReleaseAsync(int releaseId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<ReleaseDiff> DiffAsync(int releaseId, CancellationToken ct = default) => Task.FromResult(ReleaseDiff.Empty);
        public Task<ReleasePreview?> GetPreviewAsync(int releaseId, ReleaseTargetType expectedType, string expectedKey, CancellationToken ct = default) => Task.FromResult<ReleasePreview?>(null);
        public Task<ReleaseLocation?> GetLocationAsync(int releaseId, CancellationToken ct = default) => Task.FromResult<ReleaseLocation?>(null);
        public string CurrentCycle() => "2608";
        public IReadOnlyList<AiracCycleInfo> UpcomingCycles(int count) => new[] { NextCycle() };
        public Task<IReadOnlyDictionary<(ReleaseTargetType Type, string Key), ReleaseSummary>> SummariesAsync(
            IReadOnlyList<(ReleaseTargetType Type, string Key)> targets, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<(ReleaseTargetType, string), ReleaseSummary>>(
                new Dictionary<(ReleaseTargetType, string), ReleaseSummary>());
        public Task<int> PruneAllAsync(CancellationToken ct = default) => Task.FromResult(0);
    }
}
