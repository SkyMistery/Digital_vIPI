using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <inheritdoc cref="ICopPositions"/>
/// <remarks>
/// Non ha I/O suo: mette in fila due porte che esistono già, e l'<b>ordine</b> è la sola decisione che prende.
///
/// <para><b>Prima l'anagrafica, poi il catalogo punti.</b> In anagrafica le coordinate si possono scrivere
/// <b>a mano</b> (<c>NavaidFieldOrigin</c>), ed è l'unica valvola per un punto che il sectorfile colloca male
/// o non colloca affatto. Se il catalogo venisse per primo, quella valvola non si aprirebbe mai — una
/// correzione scritta e mai applicata è peggio di nessuna valvola.</para>
///
/// <para>⚠️ Il catalogo punti è dietro una cache <b>singleton</b> (<c>SectorfileCache</c>), quindi chiamarlo
/// una volta per richiesta non è una scaricata dalla rete per richiesta. E se la sorgente è muta torna
/// <c>NavaidCatalog.Empty</c>: restano i soli VOR/NDB dell'anagrafica, che è una degradazione, non un guasto.</para>
/// </remarks>
public sealed class CopPositionsProvider : ICopPositions
{
    private readonly INavaidCatalog _anagrafica;
    private readonly INavaidSource _sorgente;

    public CopPositionsProvider(INavaidCatalog anagrafica, INavaidSource sorgente)
    {
        _anagrafica = anagrafica;
        _sorgente = sorgente;
    }

    public async Task<CopPositions> GetAsync(CancellationToken ct = default)
    {
        var punti = new List<(string, double, double)>();

        foreach (var r in await _anagrafica.ListAsync(ct))
            if (r.Latitude is double lat && r.Longitude is double lon)
                punti.Add((r.Code, lat, lon));

        var catalogo = await _sorgente.GetAsync(ct);
        foreach (var e in catalogo.Entries)
            if (e.Lat is double lat && e.Lon is double lon)
                punti.Add((e.Name, lat, lon));

        return new CopPositions(punti);
    }
}
