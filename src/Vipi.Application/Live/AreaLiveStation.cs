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
    public AreaLiveStation(LiveStationParts parts, IAccDocumentService accDoc, IAccDerivationService deriv)
    {
        _parts = parts;
        _accDoc = accDoc;
        _deriv = deriv;
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
            AirportChips = await _parts.AirportChipsAsync(ctx, ct),
            Frequencies = Dedup(rows),
            Groups = groups,
            Transfers = await _parts.TransfersAsync(ctx.Acc.Code, ctx.Callsign, ctx.Online, ctx.Topology, ct),
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
}
