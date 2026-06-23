namespace Vipi.Application.Content;

/// <summary>Informazioni di un ACC per la navigazione documentale (home a 4 ACC).</summary>
public sealed record AccInfo(string Code, string Name);

/// <summary>
/// Risolve la navigazione per ACC (RF-1 ridotto a F2: i 4 ACC italiani).
/// La ricerca full per callsign è rimandata alle fasi successive.
/// </summary>
public interface IStationResolver
{
    IReadOnlyList<AccInfo> Accs { get; }
    AccInfo? Resolve(string accCode);
}

/// <inheritdoc cref="IStationResolver"/>
public sealed class StationResolver : IStationResolver
{
    private static readonly AccInfo[] All =
    {
        new("LIRR", "Roma ACC"),
        new("LIMM", "Milano ACC"),
        new("LIPP", "Padova ACC"),
        new("LIBB", "Brindisi ACC"),
    };

    public IReadOnlyList<AccInfo> Accs => All;

    public AccInfo? Resolve(string accCode) =>
        All.FirstOrDefault(a => a.Code.Equals(accCode, StringComparison.OrdinalIgnoreCase));
}
