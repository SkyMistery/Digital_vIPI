using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <summary>
/// Core import ACC + settori ATC (subcenter) dalla sorgente: corpo unico condiviso da manual (con authz,
/// <see cref="AccAdminService.ImportFromSourceAsync"/>) e auto (hosted service). Nessuno lo ri-scrive.
/// Deriva agnostica dalla sorgente via porta <see cref="IAccDirectory"/>; riproietta i Sector al termine.
/// Doc refactor 01 §4.4. Nessun controllo di autorizzazione qui: lo applica solo il chiamante manual.
/// </summary>
public interface IAccImportUseCase
{
    /// <summary>Esegue fetch → upsert → proiezione. Ritorna il conteggio di creati/aggiornati.</summary>
    Task<AccImportResult> RunAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IAccImportUseCase"/>
public sealed class AccImportUseCase : IAccImportUseCase
{
    private readonly IAccAdminRepository _repo;
    private readonly IAccDirectory _directory;
    private readonly ISectorProjectionService _projection;

    public AccImportUseCase(IAccAdminRepository repo, IAccDirectory directory, ISectorProjectionService projection)
    {
        _repo = repo;
        _directory = directory;
        _projection = projection;
    }

    public async Task<AccImportResult> RunAsync(CancellationToken ct = default)
    {
        // 1) ACC (center area).
        var centers = await _directory.GetCentersAsync(ct);
        var (accsCreated, accsUpdated) = await _repo.ImportAsync(centers, ct);

        // 2) Settori ATC (subcenter) per ogni ACC importato.
        var accs = await _repo.ListAccsAsync(ct);
        var subs = new List<SourceSubcenter>();
        foreach (var a in accs)
            subs.AddRange(await _directory.GetSubcentersAsync(a.Code, ct));

        var (subCreated, subUpdated) = await _repo.ImportSubcentersAsync(subs, ct);

        // Riproietta i Sector operativi dai cataloghi aggiornati (fonte autoritativa unica, Round 20).
        await _projection.SyncFromCatalogsAsync(ct);
        return new AccImportResult(accsCreated, accsUpdated, subCreated, subUpdated);
    }
}
