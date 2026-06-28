using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <summary>Informazioni di un ACC per la navigazione documentale (derivato dalle ACC nel DB).</summary>
public sealed record AccInfo(string Code, string Name);

/// <summary>
/// Risolve la navigazione per ACC dalle ACC esistenti nel DB (via <see cref="IStationDirectory"/>).
/// </summary>
public interface IStationResolver
{
    IReadOnlyList<AccInfo> Accs { get; }
    AccInfo? Resolve(string accCode);
}

/// <inheritdoc cref="IStationResolver"/>
public sealed class StationResolver : IStationResolver
{
    private readonly IStationDirectory _dir;
    private IReadOnlyList<AccInfo>? _cache;

    public StationResolver(IStationDirectory dir) => _dir = dir;

    // Scoped: una sola lettura per richiesta, poi cache.
    public IReadOnlyList<AccInfo> Accs => _cache ??= _dir.ListAccs();

    public AccInfo? Resolve(string accCode) =>
        Accs.FirstOrDefault(a => a.Code.Equals(accCode, StringComparison.OrdinalIgnoreCase));
}
