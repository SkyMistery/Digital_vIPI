using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Live;

/// <summary>
/// Postazioni d'aeroporto: torre, torre informativa, ground, delivery. Il soggetto della pagina è l'aeroporto —
/// piste in uso, vento, TA/TL, SID — più il catalogo delle sue frequenze.
///
/// Per ground e delivery i trasferimenti propri sono di norma **vuoti per costruzione** (i flussi si modellano
/// sui settori che consegnano traffico, non su chi lo mette in moto): per loro l'informazione utile è la
/// <see cref="LiveView.CoverageChain"/>, cioè chi li copre e a chi passano salendo. La pagina lo tiene presente
/// nell'ordine con cui rende le sezioni.
/// </summary>
public sealed class AirportLiveStation : ILiveStationKind
{
    private static readonly SectorType[] Handled = { SectorType.Twr, SectorType.ITwr, SectorType.Gnd, SectorType.Del };

    private readonly LiveStationParts _parts;
    private readonly IDocumentAdminService _docs;

    public AirportLiveStation(LiveStationParts parts, IDocumentAdminService docs)
    {
        _parts = parts;
        _docs = docs;
    }

    public int Priority => 30;

    public bool Matches(LiveStationContext ctx) => Handled.Contains(ctx.Sector.Type);

    public async Task<LiveView> BuildAsync(LiveStationContext ctx, CancellationToken ct = default)
    {
        var icao = ctx.Sector.AirportIcao ?? IcaoFromCallsign(ctx.Callsign);

        // Un membro d'aeroporto espande l'intero catalogo del suo aeroporto (ATIS·DEL·GND·TWR·APP): per una
        // torre è esattamente l'elenco che serve, dalla delivery all'avvicinamento.
        var freqs = await _parts.FrequenciesAsync(ctx.Acc.Code, new[] { ctx.Callsign }, null, ct);

        // La vIPI civile se c'è, altrimenti il vSOP militare: una torre di un campo solo militare ha il suo
        // documento, e fino al 30 settembre 2026 la vista diceva «nessun documento» e linkava una vIPI inesistente.
        var edizione = icao is null ? null : Awos.AwosGate.Edizione(await _docs.ListAsync(ct), icao);

        return new LiveView
        {
            Callsign = ctx.Callsign,
            Title = string.IsNullOrWhiteSpace(ctx.Sector.Name) ? ctx.Callsign : ctx.Sector.Name,
            AccCode = ctx.Acc.Code,
            Type = ctx.Sector.Type is SectorType.Twr or SectorType.ITwr ? LiveStationType.Tower : LiveStationType.Ground,
            AirportIcao = icao,
            Frequencies = freqs,
            Transfers = await _parts.TransfersAsync(ctx.Acc.Code, ctx.Callsign, ctx.Online, ct),
            Aor = _parts.Aor(ctx.Topology, ctx.Callsign, ctx.Online),
            CoverageChain = LiveStationParts.CoverageChain(ctx.Topology, ctx.Callsign),
            ExtendedDoc = icao is null ? null : new LiveDocRef(edizione ?? ReleaseTargetType.Airport, ctx.Acc.Code, icao),
            NoDocument = edizione is null,
        };
    }

    private static string? IcaoFromCallsign(string callsign) =>
        callsign.Contains('_') ? callsign.Split('_', 2)[0] : null;
}
