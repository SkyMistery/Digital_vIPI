using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Live;

/// <summary>
/// Postazioni di avvicinamento. Due nature, stessa resa: <b>standalone</b> (documento proprio) e
/// <b>remotizzato</b> (contenuto in un gruppo-APP della vIPI di ACC). Cambia solo da dove escono frequenze e
/// titolo, e quale documento esteso si linka — per questo è UN descrittore, non due.
///
/// Resa uguale a quella d'area: <b>chip</b> degli aeroporti (un avvicinamento ne copre spesso più d'uno —
/// LIBD_CS0_APP tiene LIBD e LIBR), frequenze utili, trasferimenti. Prima mostrava un pannello fisso sul solo
/// aeroporto dedotto dal callsign, quindi gli altri scali del suo dominio non erano raggiungibili.
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

    /// <summary>Un APP, oppure una posizione di qualunque tipo che appartiene a un ENTE con documento proprio (S49):
    /// a Pratica di Mare la torre fa l'avvicinamento, e chi è su LIRE_TWR vede la vIPI dell'ente.</summary>
    public bool Matches(LiveStationContext ctx) =>
        ctx.Sector.Type == SectorType.App || ctx.UnitCode is not null || ctx.UnitInAccVipi;

    public async Task<LiveView> BuildAsync(LiveStationContext ctx, CancellationToken ct = default)
    {
        // Documento proprio: quello dell'ENTE se la posizione ne ha uno (qualunque cosa dica IVAO dell'APP — un
        // APP spuntato «remotizzato» che ha ancora il suo documento lo tiene), altrimenti un APP non remotizzato
        // che il documento non l'ha ancora.
        // Un ente remotizzato (S50) vive nel gruppo APP della vIPI ACC, qualunque cosa dica IVAO dell'APP.
        var standalone = ctx.UnitCode is not null
                         || (!ctx.UnitInAccVipi && ctx.Sector.ApproachKind == ApproachKind.Standalone);
        var chiave = ctx.UnitCode ?? ctx.Callsign;

        var view = new LiveView
        {
            Callsign = ctx.Callsign,
            Title = ctx.Callsign,
            AccCode = ctx.Acc.Code,
            Type = LiveStationType.Approach,
            AirportChips = await _parts.AirportChipsAsync(ctx, ct),
            Transfers = await _parts.TransfersAsync(ctx.Acc.Code, ctx.Callsign, ctx.Online, ctx.Topology, ct),
            Aor = _parts.Aor(ctx.Topology, ctx.Callsign, ctx.Online),
            CoverageChain = LiveStationParts.CoverageChain(ctx.Topology, ctx.Callsign),
        };

        if (standalone)
        {
            var identity = await _appDoc.GetIdentityAsync(chiave, ct);
            return view with
            {
                Title = identity is null || string.IsNullOrWhiteSpace(identity.Title) ? ctx.Callsign : identity.Title,
                Frequencies = await _appDoc.DeriveFrequenciesAsync(chiave, ct),
                ExtendedDoc = new LiveDocRef(ReleaseTargetType.App, ctx.Acc.Code, identity?.Code ?? chiave),
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
            TreeRoot = root,
            ExtendedDoc = new LiveDocRef(ReleaseTargetType.AccVipi, ctx.Acc.Code, null),
            NoDocument = block is null,
        };
    }
}
