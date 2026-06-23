using Vipi.Application.Abstractions;
using Vipi.Domain.Services;

namespace Vipi.Application.Content;

/// <summary>Use-case "Cosa è cambiato": documenti pubblicati nel ciclo AIRAC corrente (lettura pubblica).</summary>
public interface IChangesService
{
    /// <summary>Ciclo AIRAC attualmente considerato (corrente).</summary>
    string CurrentCycle { get; }
    Task<IReadOnlyList<ChangeRow>> ListChangedAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IChangesService"/>
public sealed class ChangesService : IChangesService
{
    private readonly IChangesRepository _repo;
    private readonly IAiracService _airac;

    public ChangesService(IChangesRepository repo, IAiracService airac)
    {
        _repo = repo;
        _airac = airac;
    }

    public string CurrentCycle => _airac.GetCycle(DateTime.UtcNow);

    public Task<IReadOnlyList<ChangeRow>> ListChangedAsync(CancellationToken ct = default) =>
        _repo.ListChangedAsync(CurrentCycle, ct);
}
