using Vipi.Application.Abstractions;
using Vipi.Application.Aor;

namespace Vipi.Application.Content;

/// <summary>
/// Applica i poligoni TWR REALI presi dalla sorgente GitHub Aurora (<c>twrs.tfl</c>, via <see cref="ITowerShapeSource"/>)
/// alle TWR prive di shape dalla sorgente IVAO. È il ripiego "buono": va eseguito PRIMA del cerchio sintetico
/// (<see cref="ITowerShapeFallbackService"/>), così il cerchio copre solo le TWR che nemmeno GitHub ha. Match per
/// callsign (es. <c>LIBA_TWR</c>). Idempotente: bersaglia solo le TWR il cui poligono attuale non si proietta.
/// Job di sistema (no authz): invocato dopo l'import automatico dei settori d'aeroporto.
/// </summary>
public interface IGithubTowerShapeService
{
    /// <summary>Applica le shape GitHub alle TWR senza poligono. Se <paramref name="icao"/> è dato, si limita a
    /// quell'aeroporto (bottone manuale nell'editor); null = tutte (job automatico). Ritorna il numero applicate.</summary>
    Task<int> ApplyAsync(string? icao = null, CancellationToken ct = default);
}

/// <inheritdoc cref="IGithubTowerShapeService"/>
///
/// <para>🔴 <b>U-037 (revisione totale 3): una regola sola per il sectorfile.</b> L'area presa da <c>twrs.tfl</c>
/// restava marcata come dell'anagrafica IVAO, e da lì non era più un bersaglio: il file era una sorgente
/// <i>write-once</i>, e una torre ridisegnata dalla divisione restava com'era per sempre (66 torri su 70 avevano
/// l'anello di <c>twrs.tfl</c>). Ora vale quel che vale per le aree di settore
/// (<see cref="SectorShapeFallbackService"/>): l'area porta la provenienza del sectorfile, si aggiorna quando il file
/// cambia, e il cambio entra <b>dal ciclo successivo</b> tenendo in vigore quella di prima
/// (<see cref="ShapeAiracGate"/>). Le torri scritte prima di questa regola si riconoscono: stessa geometria del file.</para>
public sealed class GithubTowerShapeService : IGithubTowerShapeService
{
    private readonly IAirportSectorRepository _repo;
    private readonly ITowerShapeSource _source;
    private readonly ShapeFallbackScope _scope;
    private readonly Vipi.Domain.Services.IAiracService _airac;
    private readonly TimeProvider _clock;

    public GithubTowerShapeService(
        IAirportSectorRepository repo, ITowerShapeSource source, ShapeFallbackScope? scope = null,
        Vipi.Domain.Services.IAiracService? airac = null, TimeProvider? clock = null)
    {
        _repo = repo;
        _source = source;
        _scope = scope ?? new ShapeFallbackScope();
        _airac = airac ?? new Vipi.Domain.Services.AiracService();
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<int> ApplyAsync(string? icao = null, CancellationToken ct = default)
    {
        var filter = string.IsNullOrWhiteSpace(icao) ? null : icao.Trim().ToUpperInvariant();

        // Bersaglio: TWR senza poligono valido ("[]"/null → non proiettabile), con un cerchio SINTETICO di ripiego,
        // presa dall'AIP (il sectorfile è la fonte primaria, decisione 2 del committente), o GIÀ del sectorfile
        // (U-037: si aggiorna). E le torri con la provenienza dell'anagrafica: fra loro ci sono quelle scritte da
        // qui prima che la provenienza esistesse, e si riconoscono sotto, per geometria. Una shape reale IVAO,
        // diversa dal file, resta la verità primaria.
        var targets = (await _repo.ListTwrShapesAsync(ct))
            .Where(t => filter is null || string.Equals(t.AirportIcao, filter, StringComparison.OrdinalIgnoreCase))
            // ⚠️ Solo aeroporti della divisione: la TWR di un campo estero prende l'area da IVAO o resta senza
            // (ShapeFallbackScope). Vale anche col bottone manuale: se qualcuno passa un ICAO estero, non succede nulla.
            .Where(t => _scope.IsDomestic(t.AirportIcao))
            .ToList();
        if (targets.Count == 0) return 0;

        var polygons = await _source.GetTowerPolygonsAsync(ct);
        if (polygons.Count == 0) return 0;

        var applied = 0;
        foreach (var t in targets)
        {
            if (!polygons.TryGetValue(t.ComposePosition, out var json)) continue;   // GitHub non ha questa TWR
            if (AorPolygonProjector.Project(json) is null) continue;                 // poligono GitHub degenere: salta

            var vuota = t.IsShapeSynthetic || t.ShapeSource == Vipi.Domain.ShapeSource.Aip
                        || AorPolygonProjector.Project(t.RawPolygon) is null;
            if (vuota)
            {
                // Primo riempimento: in vigore subito. Differirlo vorrebbe dire nessuna area fino a 28 giorni.
                await _repo.SetRealShapeAsync(t.SectorId, json, ct: ct);
                applied++;
            }
            else if (t.ShapeSource == Vipi.Domain.ShapeSource.Sectorfile)
            {
                if (string.Equals(t.RawPolygon, json, StringComparison.Ordinal)) continue;   // identica: niente
                // Cambiata nel file: entra dal ciclo successivo, e resta in vigore quella di prima — se un
                // differimento era già aperto, la precedente è ancora lei, non la corrente mai entrata in vigore.
                await _repo.SetRealShapeAsync(t.SectorId, json, t.RawPolygonInForce ?? t.RawPolygon, CicloSuccessivo(), ct);
                applied++;
            }
            else if (t.ShapeSource == Vipi.Domain.ShapeSource.Source && StessaGeometria(t.RawPolygon, json))
            {
                // Scritta da qui prima della provenienza: la si riconosce e la si segna, senza toccarla.
                await _repo.SetRealShapeAsync(t.SectorId, t.RawPolygon!, ct: ct);
            }
        }
        return applied;
    }

    /// <summary>Il ciclo successivo a quello corrente: è da lì che la geometria nuova entra in vigore.</summary>
    private string CicloSuccessivo()
    {
        var adesso = _clock.GetUtcNow().UtcDateTime;
        var prossimi = _airac.NextCycles(adesso, 2);
        return prossimi.Count > 1 ? prossimi[1].Cycle : _airac.GetCycle(adesso);
    }

    /// <summary>
    /// Due anelli con gli stessi vertici, a meno di un milionesimo di grado. Il testo non basta: la stessa area
    /// scritta da un'altra versione del serializzatore cambierebbe decimali e spazi, e la torre non si
    /// riconoscerebbe più.
    /// </summary>
    private static bool StessaGeometria(string? a, string b)
    {
        if (string.Equals(a, b, StringComparison.Ordinal)) return true;
        var pa = PolygonGeometry.PuntiGrezzi(a);
        var pb = PolygonGeometry.PuntiGrezzi(b);
        return pa.Count > 0 && pa.Count == pb.Count
            && pa.Zip(pb).All(x => Math.Abs(x.First.Lat - x.Second.Lat) < 1e-6 && Math.Abs(x.First.Lon - x.Second.Lon) < 1e-6);
    }
}
