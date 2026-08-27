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
    Task<DocumentView?> BuildAirportVipiAsync(string icao, BlockTier tier, bool live, bool ignoreRelease = false, bool preferWorking = false, CancellationToken ct = default);

    /// <summary>Vista documentale di un APP non remotizzato (storage su Document, doc 08e). Le sezioni derivate
    /// (aor/freq/coord/minima) restano vuote nel view: la pagina le rende live per <c>SectionKey</c>.</summary>
    Task<DocumentView?> BuildAppVipiAsync(string appCallsign, BlockTier tier, bool live, bool ignoreRelease = false, bool preferWorking = false, CancellationToken ct = default);
    Task<DocumentView?> BuildVloaByIdAsync(int docId, BlockTier tier, bool live, bool ignoreRelease = false, bool preferWorking = false, CancellationToken ct = default);

    /// <summary>Compone la vista da un <see cref="RawDocument"/> già in mano (es. snapshot di una release), senza I/O.</summary>
    Task<DocumentView?> BuildFromRawAsync(RawDocument raw, BlockTier tier, CancellationToken ct = default);
}

/// <inheritdoc cref="IVipiViewService"/>
public sealed class VipiViewService : IVipiViewService
{
    private readonly IContentRepository _repo;
    private readonly IContentService _content;

    public VipiViewService(IContentRepository repo, IContentService content)
    {
        _repo = repo;
        _content = content;
    }

    public Task<DocumentView?> BuildAirportVipiAsync(string icao, BlockTier tier, bool live, bool ignoreRelease = false, bool preferWorking = false, CancellationToken ct = default) =>
        BuildAsync(_repo.LoadAirportVipiAsync(icao, ignoreRelease, preferWorking, ct), tier, live, null);

    public Task<DocumentView?> BuildAppVipiAsync(string appCallsign, BlockTier tier, bool live, bool ignoreRelease = false, bool preferWorking = false, CancellationToken ct = default) =>
        BuildAsync(_repo.LoadAppVipiAsync(appCallsign, ignoreRelease, preferWorking, ct), tier, live, null);

    public Task<DocumentView?> BuildVloaByIdAsync(int docId, BlockTier tier, bool live, bool ignoreRelease = false, bool preferWorking = false, CancellationToken ct = default) =>
        BuildAsync(_repo.LoadVloaByIdAsync(docId, ignoreRelease, preferWorking, ct), tier, live, null);

    public Task<DocumentView?> BuildFromRawAsync(RawDocument raw, BlockTier tier, CancellationToken ct = default) =>
        BuildAsync(Task.FromResult<RawDocument?>(raw), tier, live: false, null);

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
            .ToList();

        // Lingua e traduzioni congelate passano DI PESO dallo snapshot alla vista: chi rende il documento
        // deve poter sapere da che lingua si parte e che cosa era gia' tradotto quando si e' pubblicato.
        return new DocumentView
        {
            Title = raw.Title,
            AiracCycle = raw.AiracCycle,
            Sections = sections,
            Language = raw.Language,
            Translations = raw.Translations,
        };
    }

    private static IEnumerable<RawSection> Flatten(RawSection s)
    {
        yield return s;
        foreach (var c in s.Children)
            foreach (var d in Flatten(c))
                yield return d;
    }

    /// <summary>Mappa una sezione grezza in SectionView. Nessuna sezione viene scartata: una sezione esiste perché
    /// l'editore l'ha creata, quindi deve comparire anche vuota (doc 11 §3b) — prima le sezioni senza blocchi né figli
    /// sparivano dalla bozza subito dopo essere state create, e le DERIVATE (es. <c>sids</c>) erano l'unica eccezione
    /// esplicita. Restano filtrati solo i blocchi fuori Tier.</summary>
    private static SectionView Map(RawSection s, IReadOnlyDictionary<int, BlockRender> renders)
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
            .ToList();

        return new SectionView
        {
            Id = $"s-{s.Id}",
            Title = s.Title,
            Depth = s.Depth,
            SectionKey = s.SectionKey,
            IsHidden = s.IsHidden,
            BeforeParentBody = s.BeforeParentBody,
            LeadSentence = s.LeadSentence,
            Blocks = blocks,
            Children = children,
        };
    }
}
