using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>Elenco documenti la cui versione pubblicata corrente è del ciclo AIRAC indicato. Impl. EF.</summary>
public interface IChangesRepository
{
    Task<IReadOnlyList<ChangeRow>> ListChangedAsync(string airacCycle, CancellationToken ct = default);
}
