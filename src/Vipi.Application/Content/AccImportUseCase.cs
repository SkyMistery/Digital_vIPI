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
    private readonly IImportStateStore? _stati;

    /// <param name="stati">Il registro dei giri riusciti: lo timbra il <b>corpo</b>, così il bottone della
    /// pagina ACC conta quanto il giro notturno. Vedi <see cref="SogliaEliminazione"/>.</param>
    public AccImportUseCase(IAccAdminRepository repo, IAccDirectory directory,
        ISectorProjectionService projection, IImportStateStore? stati = null)
    {
        _repo = repo;
        _directory = directory;
        _projection = projection;
        _stati = stati;
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

        // Il giro è arrivato in fondo: timbralo (manuale o automatico che sia).
        if (_stati is not null)
            await _stati.MarkSuccessAsync(ImportCategories.Acc, DateTime.UtcNow, ct);

        return new AccImportResult(accsCreated, accsUpdated, subCreated, subUpdated);
    }
}
