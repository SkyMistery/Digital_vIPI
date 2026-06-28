using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Compone la vista documentale per la UI: carica il grezzo dal repository e applica il filtro Tier /
/// la logica di visibilità tramite <see cref="IContentService"/>. In F2 live=false (tutto espanso).
/// </summary>
public interface IVipiViewService
{
    Task<DocumentView?> BuildAccVipiAsync(string accCode, BlockTier tier, bool live, string? viewerPosition = null, CancellationToken ct = default);
    Task<DocumentView?> BuildAirportVipiAsync(string icao, BlockTier tier, bool live, CancellationToken ct = default);
    Task<DocumentView?> BuildVloaAsync(string accCode, BlockTier tier, bool live, CancellationToken ct = default);
    Task<DocumentView?> BuildVloaByIdAsync(int docId, BlockTier tier, bool live, CancellationToken ct = default);
}

/// <inheritdoc cref="IVipiViewService"/>
public sealed class VipiViewService : IVipiViewService
{
    private readonly IContentRepository _repo;
    private readonly IContentService _content;
    private readonly IAorService _aor;
    private readonly ITopologyProvider _topology;
    private readonly IOnlineAtcProvider _online;

    public VipiViewService(
        IContentRepository repo,
        IContentService content,
        IAorService aor,
        ITopologyProvider topology,
        IOnlineAtcProvider online)
    {
        _repo = repo;
        _content = content;
        _aor = aor;
        _topology = topology;
        _online = online;
    }

    public async Task<DocumentView?> BuildAccVipiAsync(
        string accCode, BlockTier tier, bool live, string? viewerPosition = null, CancellationToken ct = default)
    {
        // F3: in live calcolo l'AoR reale dalla topologia della ACC + chi è online ora.
        var aor = live ? await ResolveLiveAorAsync(accCode, viewerPosition, ct) : null;
        return await BuildAsync(_repo.LoadAccVipiAsync(accCode, ct), tier, live, aor);
    }

    public Task<DocumentView?> BuildAirportVipiAsync(string icao, BlockTier tier, bool live, CancellationToken ct = default) =>
        BuildAsync(_repo.LoadAirportVipiAsync(icao, ct), tier, live, null);

    public Task<DocumentView?> BuildVloaAsync(string accCode, BlockTier tier, bool live, CancellationToken ct = default) =>
        BuildAsync(_repo.LoadVloaAsync(accCode, ct), tier, live, null);

    public Task<DocumentView?> BuildVloaByIdAsync(int docId, BlockTier tier, bool live, CancellationToken ct = default) =>
        BuildAsync(_repo.LoadVloaByIdAsync(docId, ct), tier, live, null);

    /// <summary>AoR reale per la vista di <paramref name="viewerPosition"/> (default = radice topologia).</summary>
    private async Task<AorResult?> ResolveLiveAorAsync(string accCode, string? viewerPosition, CancellationToken ct)
    {
        var topo = await _topology.BuildByAccCodeAsync(accCode, ct);
        if (topo is null) return null;

        var p = viewerPosition ?? DefaultViewer(topo);
        if (p is null) return null;

        return _aor.Resolve(topo, p, _online.GetCurrent().Callsigns);
    }

    /// <summary>Settore di default quando non specificato: prima radice (callsign senza genitore).</summary>
    private static string? DefaultViewer(Topology topo) =>
        topo.Sectors
            .Where(cs => !topo.Parent.ContainsKey(cs))
            .OrderBy(cs => cs, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
        ?? topo.Sectors.FirstOrDefault();

    private async Task<DocumentView?> BuildAsync(Task<RawDocument?> load, BlockTier tier, bool live, AorResult? liveAor)
    {
        var raw = await load;
        if (raw is null) return null;

        // In live uso l'AoR calcolato; altrimenti AoR neutro (tutto espanso, F2).
        var aor = liveAor ?? new AorResult
        {
            Ownership = new Dictionary<string, string>(),
            State = new Dictionary<string, SectorState>(),
        };

        // Filtro/resa di tutti i blocchi in un colpo solo, poi rimappo sull'albero.
        var flat = raw.Roots.SelectMany(Flatten).SelectMany(s => s.Blocks).ToList();
        var inputs = flat.Select(bl => new BlockInput(bl.Id, bl.Visibility, bl.ScopeSectorKey, bl.Tier));
        var renders = _content.BuildView(inputs, aor, tier, live).ToDictionary(r => r.BlockId);

        var sections = raw.Roots
            .Select(s => Map(s, renders))
            .OfType<SectionView>()
            .ToList();

        return new DocumentView { Title = raw.Title, AiracCycle = raw.AiracCycle, Sections = sections };
    }

    private static IEnumerable<RawSection> Flatten(RawSection s)
    {
        yield return s;
        foreach (var c in s.Children)
            foreach (var d in Flatten(c))
                yield return d;
    }

    /// <summary>Mappa una sezione grezza in SectionView; ritorna null se vuota (nessun blocco tenuto, nessun figlio).</summary>
    private static SectionView? Map(RawSection s, IReadOnlyDictionary<int, BlockRender> renders)
    {
        var blocks = s.Blocks
            .Where(b => renders.ContainsKey(b.Id))         // filtrati per Tier
            .OrderBy(b => b.Order)
            .Select(b =>
            {
                var r = renders[b.Id];
                return new BlockView
                {
                    Id = b.Id,
                    Format = b.Format,
                    State = r.State,
                    CollapseLabel = r.CollapseLabel,
                    Body = b.Body,
                    BodyJson = b.BodyJson,
                    CalloutKind = b.CalloutKind,
                };
            })
            .ToList();

        var children = s.Children
            .OrderBy(c => c.Order)
            .Select(c => Map(c, renders))
            .OfType<SectionView>()
            .ToList();

        if (blocks.Count == 0 && children.Count == 0)
            return null;

        return new SectionView
        {
            Id = $"s-{s.Id}",
            Title = s.Title,
            Depth = s.Depth,
            Kind = s.Kind,
            Blocks = blocks,
            Children = children,
        };
    }
}
