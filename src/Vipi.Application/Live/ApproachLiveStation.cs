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
            Transfers = await _parts.TransfersAsync(ctx.Acc.Code, ctx.Callsign, ctx.Online, ct),
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
                // L'ACC dell'ENTE, non del settore online (revisione, S52): una posizione può stare in un altro ACC.
                ExtendedDoc = new LiveDocRef(ReleaseTargetType.App, identity?.AccCode ?? ctx.Acc.Code, identity?.Code ?? chiave),
                NoDocument = identity is null,
            };
        }

        // Remotizzato: il gruppo-APP della vIPI di ACC che lo contiene. Per un ente spostato (S50) è la vIPI del SUO
        // ACC, e il gruppo è il suo anche quando online c'è il codice e non una posizione (revisione, S52).
        var accCode = ctx.UnitAccCode ?? ctx.Acc.Code;
        var roots = await _deriv.ListTreeRootsAsync(accCode, ct);
        var root = roots.Count > 0 ? roots[0].Callsign : null;
        var model = await _accDoc.LoadForViewAsync(accCode, ct);
        var block = model is null ? null : GruppoDi(model.Data.Blocks, ctx.Callsign, ctx.UnitId, ctx.UnitGroupKey);

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
            ExtendedDoc = new LiveDocRef(ReleaseTargetType.AccVipi, accCode, null),
            NoDocument = block is null,
        };
    }

    /// <summary>Il gruppo APP della postazione: quello dell'ente se ce n'è uno — per chiave del gruppo (S55) o per
    /// l'ente da cui è nato (S50) — altrimenti quello che la elenca.</summary>
    internal static AccBlock? GruppoDi(IEnumerable<AccBlock> blocchi, string callsign, int? unitId, string? groupKey = null)
    {
        var gruppi = blocchi.Where(b => b.Kind == AccBlockKind.AppGroup).ToList();
        return (groupKey is { Length: > 0 } k ? gruppi.FirstOrDefault(b => string.Equals(b.Key, k, StringComparison.OrdinalIgnoreCase)) : null)
               ?? (unitId is int id ? gruppi.FirstOrDefault(b => b.UnitId == id) : null)
               ?? gruppi.FirstOrDefault(b => b.MemberCallsigns.Contains(callsign, StringComparer.OrdinalIgnoreCase));
    }
}
