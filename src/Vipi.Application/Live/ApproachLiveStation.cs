using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Live;

/// <summary>
/// Postazioni di avvicinamento. Due nature, stessa resa: <b>standalone</b> (documento proprio) e
/// <b>remotizzato</b> (contenuto in un gruppo-APP della vIPI di ACC). Cambia solo da dove escono frequenze e
/// titolo, e quale documento esteso si linka — per questo è UN descrittore, non due.
/// </summary>
public sealed class ApproachLiveStation : ILiveStationKind
{
    private readonly LiveStationParts _parts;
    private readonly IAppDocumentService _appDoc;
    private readonly IAccDocumentService _accDoc;
    private readonly IAccDerivationService _deriv;

    public ApproachLiveStation(LiveStationParts parts, IAppDocumentService appDoc,
        IAccDocumentService accDoc, IAccDerivationService deriv)
    {
        _parts = parts;
        _appDoc = appDoc;
        _accDoc = accDoc;
        _deriv = deriv;
    }

    public int Priority => 20;

    public bool Matches(LiveStationContext ctx) => ctx.Sector.Type == SectorType.App;

    public async Task<LiveView> BuildAsync(LiveStationContext ctx, CancellationToken ct = default)
    {
        var standalone = ctx.Sector.ApproachKind == ApproachKind.Standalone;

        var view = new LiveView
        {
            Callsign = ctx.Callsign,
            Title = ctx.Callsign,
            AccCode = ctx.Acc.Code,
            Type = LiveStationType.Approach,
            AirportIcao = ctx.Sector.AirportIcao ?? IcaoFromCallsign(ctx.Callsign),
            Transfers = await _parts.TransfersAsync(ctx.Acc.Code, ctx.Callsign, ctx.Online, ct),
            Aor = _parts.Aor(ctx.Topology, ctx.Callsign, ctx.Online),
            CoverageChain = LiveStationParts.CoverageChain(ctx.Topology, ctx.Callsign),
        };

        if (standalone)
        {
            var identity = await _appDoc.GetIdentityAsync(ctx.Callsign, ct);
            return view with
            {
                Title = identity is null || string.IsNullOrWhiteSpace(identity.Title) ? ctx.Callsign : identity.Title,
                Frequencies = await _appDoc.DeriveFrequenciesAsync(ctx.Callsign, ct),
                ExtendedDoc = new LiveDocRef(ManagedDocKind.AppVipi, ctx.Acc.Code, ctx.Callsign),
                NoDocument = identity is null,
            };
        }

        // Remotizzato: il gruppo-APP della vIPI di ACC che lo contiene.
        var roots = await _deriv.ListTreeRootsAsync(ctx.Acc.Code, ct);
        var root = roots.Count > 0 ? roots[0].Callsign : null;
        var model = await _accDoc.LoadForViewAsync(ctx.Acc.Code, ct);
        var block = model?.Data.Blocks.FirstOrDefault(b => b.Kind == AccBlockKind.AppGroup
            && b.MemberCallsigns.Contains(ctx.Callsign, StringComparer.OrdinalIgnoreCase));

        // Senza blocco (APP non ancora messo in nessun gruppo) resta la derivazione sul solo callsign: il
        // catalogo del suo aeroporto c'è comunque, manca solo il raggruppamento editoriale.
        var freqs = block is not null
            ? await _deriv.DeriveFrequenciesAsync(ctx.Acc.Code, block, root, ct)
            : await _parts.FrequenciesAsync(ctx.Acc.Code, new[] { ctx.Callsign }, root, ct);

        return view with
        {
            Title = block?.Title ?? ctx.Callsign,
            Frequencies = freqs,
            Groups = block is null ? Array.Empty<LiveGroup>() : new[] { new LiveGroup(block, freqs.ToList(), false) },
            ExtendedDoc = new LiveDocRef(ManagedDocKind.AccVipi, ctx.Acc.Code, null),
            NoDocument = block is null,
        };
    }

    private static string? IcaoFromCallsign(string callsign) =>
        callsign.Contains('_') ? callsign.Split('_', 2)[0] : null;
}
