using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;

namespace Vipi.Application.Content;

/// <inheritdoc cref="IAirportSectorService"/>
public sealed class AirportSectorService : IAirportSectorService
{
    private readonly IAirportSectorRepository _repo;
    private readonly IAirportSectorImporter _importer;
    private readonly IEditAuthorizationService _authz;
    private readonly IStructureEditingService _structure;
    private readonly ISectorProjectionService _projection;

    public AirportSectorService(IAirportSectorRepository repo, IAirportSectorImporter importer,
        IEditAuthorizationService authz, IStructureEditingService structure, ISectorProjectionService projection)
    {
        _repo = repo;
        _importer = importer;
        _authz = authz;
        _structure = structure;
        _projection = projection;
    }

    public Task<IReadOnlyList<AirportSectorRow>> ListByAirportAsync(string icao, CancellationToken ct = default) =>
        _repo.ListByAirportAsync(Norm(icao), ct);

    public async Task<AirportSectorImportResult> ImportFromSourceAsync(string icao, CancellationToken ct = default)
    {
        icao = Norm(icao);
        await EnsureCanEditAsync(icao, ct);

        var (created, updated) = await _importer.ImportAsync(icao, ct);

        // Riproietta i Sector operativi dai cataloghi aggiornati (fonte autoritativa unica, Round 20).
        await _projection.SyncFromCatalogsAsync(ct);

        // Documento aeroporto creato/aggiornato in automatico (l'utente ha già passato la guardia ACC sopra).
        try { await _structure.EnsureAirportDocumentSystemAsync(icao, ct); } catch { /* best-effort */ }

        return new AirportSectorImportResult(created, updated);
    }

    public async Task SetHiddenAsync(int id, bool hidden, CancellationToken ct = default)
    {
        await EnsureCanEditSectorAsync(id, ct);
        await _repo.SetHiddenAsync(id, hidden, ct);
        await _projection.SyncFromCatalogsAsync(ct);   // nascondere un settore lo disattiva nella proiezione
    }

    public async Task SetLimitsAsync(int id, int? lower, int? upper, CancellationToken ct = default)
    {
        await EnsureCanEditSectorAsync(id, ct);
        await _repo.SetLimitsAsync(id, lower, upper, ct);
    }

    public async Task SetPrimaryAsync(int id, CancellationToken ct = default)
    {
        await EnsureCanEditSectorAsync(id, ct);
        await _repo.SetPrimaryAsync(id, ct);
    }

    public async Task SetAccAppAsync(int id, bool isAccApp, CancellationToken ct = default)
    {
        await EnsureCanEditSectorAsync(id, ct);
        await _repo.SetIsAccAppAsync(id, isAccApp, ct);
        await _projection.SyncFromCatalogsAsync(ct);   // cambia l'ApproachKind del Sector proiettato
    }

    private async Task EnsureCanEditAsync(string icao, CancellationToken ct)
    {
        var acc = await _repo.GetAccCodeByIcaoAsync(Norm(icao), ct)
            ?? throw new ValidationException($"Aeroporto {Norm(icao)} inesistente o senza ACC.");
        await _authz.EnsureCanEditAccAsync(acc, ct);
    }

    private async Task EnsureCanEditSectorAsync(int id, CancellationToken ct)
    {
        var acc = await _repo.GetAccCodeBySectorIdAsync(id, ct)
            ?? throw new ValidationException($"Settore d'aeroporto id {id} inesistente.");
        await _authz.EnsureCanEditAccAsync(acc, ct);
    }

    private static string Norm(string icao) => (icao ?? "").Trim().ToUpperInvariant();
}
