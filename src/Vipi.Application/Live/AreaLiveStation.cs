using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Live;

/// <summary>
/// Postazioni d'area: i CTR (e gli FSS, che nel catalogo sono tipizzati CTR). Vedono le frequenze dell'ACC,
/// i gruppi-APP del documento col collasso morbido, e i chip di vista rapida degli aeroporti nel proprio dominio.
/// </summary>
public sealed class AreaLiveStation : ILiveStationKind
{
    private readonly LiveStationParts _parts;
    private readonly IAccDocumentService _accDoc;
    private readonly IAccDerivationService _deriv;
    private readonly IDocumentAdminService _docs;

    public AreaLiveStation(LiveStationParts parts, IAccDocumentService accDoc,
        IAccDerivationService deriv, IDocumentAdminService docs)
    {
        _parts = parts;
        _accDoc = accDoc;
        _deriv = deriv;
        _docs = docs;
    }

    public int Priority => 10;

    public bool Matches(LiveStationContext ctx) => ctx.Sector.Type == SectorType.Ctr;

    public async Task<LiveView> BuildAsync(LiveStationContext ctx, CancellationToken ct = default)
    {
        var roots = await _deriv.ListTreeRootsAsync(ctx.Acc.Code, ct);
        var root = roots.Count > 0 ? roots[0].Callsign : null;

        var model = await _accDoc.LoadForViewAsync(ctx.Acc.Code, ct);
        var aor = _parts.Aor(ctx.Topology, ctx.Callsign, ctx.Online);

        // Frequenze: area (tutti i CTR) più i gruppi-APP del documento. Senza documento resta l'area più
        // tutti gli APP dell'ACC — il blocco porta raggruppamento e ordine, non il dato (vedi live-view-design).
        var rows = new List<AppFreqRow>(await _parts.AreaFrequenciesAsync(ctx.Acc.Code, root, ct));
        var groups = new List<LiveGroup>();

        var blocks = model?.Data.Blocks.Where(b => b.Kind == AccBlockKind.AppGroup).ToList();
        if (blocks is { Count: > 0 })
        {
            foreach (var block in blocks)
            {
                var freqs = (await _deriv.DeriveFrequenciesAsync(ctx.Acc.Code, block, root, ct)).ToList();
                rows.AddRange(freqs);
                var delegated = block.MemberCallsigns.Any(cs =>
                    aor.State.TryGetValue(cs, out var st) && st == SectorState.Online);
                groups.Add(new LiveGroup(block, freqs, delegated));
            }
        }
        else
        {
            var apps = await _deriv.ListAppSectorsAsync(ctx.Acc.Code, ct);
            rows.AddRange(await _parts.FrequenciesAsync(ctx.Acc.Code, apps.Select(a => a.Callsign).ToList(), root, ct));
        }

        return new LiveView
        {
            Callsign = ctx.Callsign,
            Title = Title(ctx),
            AccCode = ctx.Acc.Code,
            Type = LiveStationType.Area,
            AirportChips = await ChipsAsync(ctx, ct),
            Frequencies = Dedup(rows),
            Groups = groups,
            Transfers = await _parts.TransfersAsync(ctx.Acc.Code, ctx.Callsign, ctx.Online, ct),
            Aor = aor,
            CoverageChain = LiveStationParts.CoverageChain(ctx.Topology, ctx.Callsign),
            TreeRoot = root,
            ExtendedDoc = new LiveDocRef(ManagedDocKind.AccVipi, ctx.Acc.Code, null),
            NoDocument = model is null,
        };
    }

    private static string Title(LiveStationContext ctx) =>
        string.IsNullOrWhiteSpace(ctx.Sector.Name) ? ctx.Callsign : $"{ctx.Acc.Name} — {ctx.Sector.Name}";

    /// <summary>Una frequenza per callsign: i gruppi si sovrappongono all'area (un APP compare in entrambi).</summary>
    private static IReadOnlyList<AppFreqRow> Dedup(IEnumerable<AppFreqRow> rows) => rows
        .GroupBy(r => r.Callsign, StringComparer.OrdinalIgnoreCase)
        .Select(g => g.First()).ToList();

    /// <summary>
    /// Chip: SOLO gli aeroporti pubblicati appesi a un settore del dominio della postazione. In coda quelli
    /// «delegati» (una posizione del loro ICAO è online): li controlla qualcun altro, non tu.
    /// </summary>
    private async Task<IReadOnlyList<LiveAirportChip>> ChipsAsync(LiveStationContext ctx, CancellationToken ct)
    {
        var published = (await _docs.ListAsync(ct))
            .Where(m => m.Kind == ManagedDocKind.AirportVipi && m.HasEffectiveRelease && !m.IsHidden
                        && string.Equals(m.AccCode, ctx.Acc.Code, StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Scope).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var domain = ctx.Topology.DomainOf(ctx.Callsign);

        return ctx.Structure.Airports
            .Where(a => a.IsPublic && published.Contains(a.Icao))
            .Where(a => a.ParentCallsign is { } pc && domain.Contains(pc))
            .Select(a => new LiveAirportChip(a.Icao, IsDelegated(ctx, a.Icao)))
            .OrderByDescending(c => !c.Delegated)
            .ThenBy(c => c.Icao, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsDelegated(LiveStationContext ctx, string icao) =>
        ctx.Online.Any(cs => cs.Split('_', 2)[0].Equals(icao, StringComparison.OrdinalIgnoreCase));
}
