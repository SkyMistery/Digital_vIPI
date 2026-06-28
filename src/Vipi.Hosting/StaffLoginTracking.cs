using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;

namespace Vipi.Hosting;

/// <summary>
/// Limita la frequenza di registrazione del login per UserId (una scrittura al massimo ogni finestra),
/// così il middleware non tocca il DB a ogni richiesta. Singleton.
/// </summary>
public sealed class StaffLoginThrottle
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<int, DateTime> _last = new();

    public bool ShouldRecord(int UserId)
    {
        var now = DateTime.UtcNow;
        var prev = _last.GetOrAdd(UserId, DateTime.MinValue);
        if (now - prev < Window) return false;
        _last[UserId] = now;
        return true;
    }
}

/// <summary>
/// A ogni richiesta con un utente corrente, registra il login nel roster staff (throttled). È qui che
/// uno staffista IT entra nel roster "almeno una volta loggato". Funziona con qualunque
/// <see cref="ICurrentUserProvider"/> (dev oggi, HostIdentity/OIDC domani).
/// </summary>
public sealed class StaffLoginTrackingMiddleware
{
    private readonly RequestDelegate _next;
    public StaffLoginTrackingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext ctx,
        ICurrentUserProvider users,
        IStaffRosterService roster,
        StaffLoginThrottle throttle,
        ILogger<StaffLoginTrackingMiddleware> log)
    {
        var user = users.Get();
        if (user is not null && throttle.ShouldRecord(user.UserId))
        {
            try { await roster.RecordLoginAsync(user, ctx.RequestAborted); }
            catch (Exception ex) { log.LogWarning(ex, "Registrazione login staff fallita per UserId {UserId}.", user.UserId); }
        }

        await _next(ctx);
    }
}
