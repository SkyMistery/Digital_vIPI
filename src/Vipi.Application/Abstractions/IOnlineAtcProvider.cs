namespace Vipi.Application.Abstractions;

/// <summary>Un ATC attualmente online (snapshot normalizzato dal polling IVAO). F3.</summary>
public sealed record OnlineAtc(string Callsign, int UserId, string Name, int Rating);

/// <summary>
/// Fotografia immutabile dell'ATC online in un istante. <see cref="Callsigns"/> alimenta
/// <c>IAorService.Resolve</c>; <see cref="Details"/> serve alle liste UI. ADR-0001 D6.
/// </summary>
public sealed class OnlineAtcSnapshot
{
    public required IReadOnlySet<string> Callsigns { get; init; }
    public required IReadOnlyList<OnlineAtc> Details { get; init; }
    public required DateTimeOffset AsOf { get; init; }

    /// <summary>Snapshot vuoto: usato prima del primo poll così le viste restano sicure (nessun online).</summary>
    public static readonly OnlineAtcSnapshot Empty = new()
    {
        Callsigns = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        Details = Array.Empty<OnlineAtc>(),
        AsOf = DateTimeOffset.MinValue,
    };
}

/// <summary>
/// Porta read-only verso l'ATC online corrente (cache aggiornata dal polling). Pura per l'Application:
/// non fa I/O, legge l'ultima fotografia in memoria. Impl. = <c>OnlineAtcCache</c> in Infrastructure.
/// </summary>
public interface IOnlineAtcProvider
{
    OnlineAtcSnapshot GetCurrent();
}
