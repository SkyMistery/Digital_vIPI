using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <inheritdoc cref="IAirportSectorImporter"/>
public sealed class AirportSectorImporter : IAirportSectorImporter
{
    private readonly IAirportDetailProvider _details;
    private readonly IAirportSectorRepository _repo;
    private readonly IImportPolicyStore _policy;

    public AirportSectorImporter(IAirportDetailProvider details, IAirportSectorRepository repo,
        IImportPolicyStore policy)
    {
        _details = details;
        _repo = repo;
        _policy = policy;
    }

    public async Task<(int Created, int Updated)> ImportAsync(string icao, CancellationToken ct = default)
    {
        icao = (icao ?? "").Trim().ToUpperInvariant();

        // Policy opt-out: categoria «Settori» esclusa → si esce PRIMA della fetch, così il catalogo resta
        // com'è e i settori aggiunti a mano in Struttura non vengono ripassati dall'import.
        // ⚠️ Il gate sta QUI e non negli hosted service perché questo è il corpo condiviso da quattro
        // chiamanti (job 24h, bottone dell'editor aeroporto, massivo di /vsop/admin/airports, «Genera
        // documenti»): messo in uno solo, gli altri tre lo scavalcherebbero. È la stessa lezione di
        // SpecialAreaImportUseCase, e fino al 22 agosto 2026 qui non c'era: escludere «Settori» vietava
        // l'aggiunta manuale (StructureEditingService.AddSectorAsync) ma non fermava un solo import.
        if (!(await _policy.GetAsync(ct)).IsImported(ImportCategory.Sectors)) return (0, 0);

        // 1) Lista postazioni ATC (callsign + freq + position/middle dalla lista).
        var positions = await _details.GetAtcPositionsAsync(icao, ct);
        if (positions.Count == 0) return (0, 0);

        // 2) Dettaglio per ogni postazione: freq + shape + limiti (best-effort; in errore tiene i dati di lista).
        var enriched = new List<SourceAtcPosition>(positions.Count);
        foreach (var p in positions)
        {
            var detail = await _details.GetAtcPositionDetailAsync(p.Callsign, ct);
            enriched.Add(detail is null ? p : p with
            {
                Frequency = detail.Frequency ?? p.Frequency,
                Position = detail.Position ?? p.Position,
                MiddleIdentifier = detail.MiddleIdentifier ?? p.MiddleIdentifier,
                RegionMapPolygon = detail.RegionMapPolygon,
                LowerLimit = detail.LowerLimit,
                UpperLimit = detail.UpperLimit,
                AirportLatitude = detail.AirportLatitude,
                AirportLongitude = detail.AirportLongitude,
            });
        }

        return await _repo.ImportForAirportAsync(icao, enriched, ct);
    }
}
