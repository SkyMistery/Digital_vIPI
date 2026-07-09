using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <inheritdoc cref="IAirportImportUseCase"/>
public sealed class AirportImportUseCase : IAirportImportUseCase
{
    private readonly IAirportDirectory _directory;
    private readonly IStructureEditingRepository _repo;
    private readonly IAirportSectorImporter _sectorImporter;
    private readonly ISectorProjectionService _projection;

    public AirportImportUseCase(IAirportDirectory directory, IStructureEditingRepository repo,
        IAirportSectorImporter sectorImporter, ISectorProjectionService projection)
    {
        _directory = directory;
        _repo = repo;
        _sectorImporter = sectorImporter;
        _projection = projection;
    }

    public async Task<AirportImportResult> RunAsync(CancellationToken ct = default)
    {
        var ivao = await _directory.GetAirportsAsync(ct);
        var candidates = ivao
            .Where(a => !string.IsNullOrWhiteSpace(a.AccCode))
            .Select(a => (AccCode: a.AccCode!, a.Icao, a.Name))
            .ToList();
        var assigned = await _repo.AutoAssignAirportsAsync(candidates, ct);

        // Per ogni aeroporto appena assegnato: importa il catalogo settori (DEL/GND/TWR/APP) dalla sorgente, così
        // compaiono subito i settori. La documentazione vIPI resta un passo a parte («Genera documenti»).
        var failures = new List<AirportImportFailure>();
        foreach (var icao in assigned)
        {
            // Aeroporto senza settori nella sorgente o sorgente non disponibile: salta e riporta (il chiamante logga).
            try { await _sectorImporter.ImportAsync(icao, ct); }
            catch (Exception ex) { failures.Add(new AirportImportFailure(icao, ex)); }
        }
        // Proietta i cataloghi aggiornati nei Sector operativi (fonte autoritativa unica, Round 20).
        if (assigned.Count > 0) await _projection.SyncFromCatalogsAsync(ct);

        return new AirportImportResult(assigned.Count, failures);
    }
}
