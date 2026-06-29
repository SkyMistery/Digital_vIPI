using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;

namespace Vipi.Application.Content;

/// <summary>
/// Riga settore ATC d'aeroporto per l'editor: chiave naturale + dati sorgente + limiti admin.
/// <paramref name="IsHidden"/> = flag proprio (l'aeroporto non si nasconde, quindi niente flag derivato).
/// </summary>
public sealed record AirportSectorRow(
    int Id, string ComposePosition, string AirportIcao, string AccCode, string? Position,
    string? MiddleIdentifier, string? Frequency, int? LowerLimit, int? UpperLimit, bool IsHidden, bool HasPolygon, bool IsPrimary);

/// <summary>Esito dell'import dei settori ATC di un aeroporto dalla sorgente.</summary>
public sealed record AirportSectorImportResult(int Created, int Updated);

/// <summary>
/// Use-case di gestione dei settori ATC d'aeroporto importati dalla sorgente. L'import scarica le
/// postazioni ATC (porta neutra <see cref="IAirportDetailProvider"/>) — TUTTE, inclusi gli APP — e fa
/// upsert: il sito resta agnostico dalla sorgente e contiene SOLO ciò che la sorgente fornisce.
/// Letture libere (servono all'editor in sola lettura); scritture ACC-gated via <see cref="IEditAuthorizationService"/>.
/// </summary>
public interface IAirportSectorService
{
    /// <summary>Settori ATC di un aeroporto (anche nascosti). Lettura libera.</summary>
    Task<IReadOnlyList<AirportSectorRow>> ListByAirportAsync(string icao, CancellationToken ct = default);

    /// <summary>Importa/aggiorna dalla sorgente i settori ATC dell'aeroporto (incl. APP). ACC-gated.</summary>
    Task<AirportSectorImportResult> ImportFromSourceAsync(string icao, CancellationToken ct = default);

    /// <summary>Mostra/nasconde un settore ATC d'aeroporto. ACC-gated.</summary>
    Task SetHiddenAsync(int id, bool hidden, CancellationToken ct = default);

    /// <summary>Imposta i limiti di quota di un settore ATC d'aeroporto. ACC-gated.</summary>
    Task SetLimitsAsync(int id, int? lower, int? upper, CancellationToken ct = default);

    /// <summary>Imposta il settore come frequenza principale dell'aeroporto (esclusiva). ACC-gated.</summary>
    Task SetPrimaryAsync(int id, CancellationToken ct = default);
}

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
