using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using static Vipi.Application.Messaggio;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <inheritdoc cref="IAirportSectorService"/>
public sealed class AirportSectorService : IAirportSectorService
{
    private readonly IAirportSectorRepository _repo;
    private readonly IAirportSectorImporter _importer;
    private readonly IEditAuthorizationService _authz;
    private readonly ISectorProjectionService _projection;
    private readonly IGithubTowerShapeService _githubShapes;
    private readonly IAirportLockGuard _lock;

    public AirportSectorService(IAirportSectorRepository repo, IAirportSectorImporter importer,
        IEditAuthorizationService authz, ISectorProjectionService projection,
        IGithubTowerShapeService githubShapes, IAirportLockGuard @lock)
    {
        _repo = repo;
        _importer = importer;
        _authz = authz;
        _projection = projection;
        _githubShapes = githubShapes;
        _lock = @lock;
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

        // NB: l'import popola SOLO il catalogo. La generazione del documento è scollegata (doc 03 §4.3):
        // avviene a parte via «Genera documenti» (GenerateAirportDocumentAsync).
        return new AirportSectorImportResult(created, updated);
    }

    public async Task<int> ApplyGithubTwrShapesAsync(string icao, CancellationToken ct = default)
    {
        icao = Norm(icao);
        await EnsureCanEditAsync(icao, ct);

        var applied = await _githubShapes.ApplyAsync(icao, ct);
        if (applied > 0) await _projection.SyncFromCatalogsAsync(ct);   // le nuove shape entrano nella proiezione
        return applied;
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

    /// <summary>Ruolo sulla ACC <b>e</b> lock del documento dello scalo (carta 2026-09-04-aeroporto-porta-sola).
    /// ⚠️ Il catalogo dei settori si scrive dal solo editor d'aeroporto, che quel lock ce l'ha: prima queste
    /// quattro scritture — limiti, nascondi, principale, «di ACC» — non lo chiedevano a nessuno.</summary>
    private async Task EnsureCanEditAsync(string icao, CancellationToken ct)
    {
        var acc = await _repo.GetAccCodeByIcaoAsync(Norm(icao), ct)
            ?? throw new ValidationException(Lingua($"Aeroporto {Norm(icao)} inesistente o senza ACC.", $"Airport {Norm(icao)} does not exist, or has no ACC."));
        _authz.EnsureAtLeast(VipiRole.Editor);
        await _lock.EnsureMineAsync(icao, ct);
    }

    /// <summary>La stessa guardia, per le scritture che nominano il SETTORE e non lo scalo: l'ICAO lo dice il
    /// settore stesso — è la sua chiave esterna, non una seconda verità.</summary>
    private async Task EnsureCanEditSectorAsync(int id, CancellationToken ct)
    {
        var icao = await _repo.GetIcaoBySectorIdAsync(id, ct)
            ?? throw new ValidationException(Lingua($"Settore d'aeroporto id {id} inesistente.", $"Airport sector id {id} does not exist."));
        await EnsureCanEditAsync(icao, ct);
    }

    private static string Norm(string icao) => (icao ?? "").Trim().ToUpperInvariant();
}
