using Microsoft.Extensions.Logging;
using Vipi.Application.Abstractions;

namespace Vipi.Hosting;

/// <summary>
/// Adapter di sviluppo per <see cref="ICurrentUserProvider"/>: simula "il login" con un UserId reale e ne
/// legge le posizioni staff **dal vivo** dall'API IVAO (così si verifica l'intera pipeline
/// identità → CurrentUser → UI). Memoizzato (una fetch sola). Con fallback statico se l'API non risponde.
/// In produzione è sostituito da <see cref="HostIdentityCurrentUserProvider"/> (claim host). ADR-0002.
/// </summary>
public sealed class DevCurrentUserProvider : ICurrentUserProvider
{
    // UserId di sviluppo da impersonare. Cambiare per testare con un altro utente.
    private const int DevUserId = 704798;

    private static CurrentUser? _cached;
    private static readonly object Lock = new();

    private readonly IUserDirectory _ivao;
    private readonly ILogger<DevCurrentUserProvider> _log;
    public DevCurrentUserProvider(IUserDirectory ivao, ILogger<DevCurrentUserProvider> log)
    {
        _ivao = ivao;
        _log = log;
    }

    public CurrentUser? Get()
    {
        if (_cached is not null) return _cached;
        lock (Lock)
        {
            _cached ??= Build();
            return _cached;
        }
    }

    private CurrentUser Build()
    {
        try
        {
            // Niente SynchronizationContext in ASP.NET Core: il blocking qui è sicuro e avviene una volta sola.
            var info = _ivao.GetUserAsync(DevUserId).GetAwaiter().GetResult();
            if (info is not null)
                return new CurrentUser(
                    UserId: info.UserId,
                    Name: info.Nickname ?? $"UserId {info.UserId}",
                    Acc: null,
                    StaffPositions: info.StaffPositionCodes)
                {
                    CanEdit = info.StaffPositionCodes.Count > 0,
                };
        }
        catch (Exception ex)
        {
            // Offline / credenziali assenti: si usa il fallback statico. NON ingoiare in silenzio (nascondeva anche
            // errori di programmazione, es. NRE nel fetcher): logga così la degradazione è diagnosticabile.
            _log.LogWarning(ex, "DevCurrentUserProvider: fetch utente {UserId} fallita, uso il fallback statico.", DevUserId);
        }

        return new CurrentUser(DevUserId, $"VID {DevUserId}", "LIRR",
            new[] { "IT-AOA1", "IT-T03" }) { CanEdit = true };
    }
}
