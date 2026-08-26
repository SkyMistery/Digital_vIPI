namespace Vipi.Application.Abstractions;

/// <summary>
/// Le shape dei settori lette dal sectorfile, e i punti che non si sono risolti.
/// </summary>
/// <param name="PolygonsByCallsign">Poligono grezzo (<c>[[lng,lat],…]</c>, la forma di <c>RegionMapPolygon</c>)
/// per callsign. Una stessa shape può comparire su più callsign: nel sectorfile un'intestazione ne porta
/// fino a cinque.</param>
/// <param name="UnresolvedPoints">
/// I nomi di punto che il catalogo navaid non conosce, con i callsign che li citavano. <b>Non è un dettaglio
/// da log</b>: ognuno di questi è un settore rimasto senza area, e senza questo elenco la cosa si vedrebbe
/// solo aprendo il documento.
/// </param>
public sealed record SectorShapes(
    IReadOnlyDictionary<string, string> PolygonsByCallsign,
    IReadOnlyList<(string Point, string Callsigns)> UnresolvedPoints)
{
    public static readonly SectorShapes Empty =
        new(new Dictionary<string, string>(), Array.Empty<(string, string)>());
}

/// <summary>
/// Porta verso i poligoni di <b>settore</b> (CTR/APP/MIL/FSS) del sectorfile Aurora. Gemella di
/// <see cref="ITowerShapeSource"/>, che fa lo stesso per le sole TWR.
///
/// <para>⚠️ È un <b>ripiego</b>, non una sorgente: si usa quando l'anagrafica IVAO non dà la shape — cosa
/// che dal 26 agosto 2026 succede sempre, su tutta l'API. Se l'anagrafica ricomincia a rispondere torna a
/// comandare lei, senza che si tocchi niente.</para>
///
/// <para>⚠️ E il sectorfile lo scriviamo <b>noi, in anticipo sul ciclo AIRAC</b>: quel che ne esce può non
/// essere ancora in vigore. Chi lo scrive in archivio deve passare dal gate (carta
/// <c>2026-08-26-shape-dal-sectorfile.md</c> §3), non scriverlo dritto nel documento pubblico.</para>
/// </summary>
public interface ISectorShapeSource
{
    /// <summary>Tutti i poligoni di settore pubblicati nel sectorfile. Vuoto se la sorgente non è
    /// configurata o non risponde: è un caso normale, si tiene quel che si ha.</summary>
    Task<SectorShapes> GetSectorPolygonsAsync(CancellationToken ct = default);
}
