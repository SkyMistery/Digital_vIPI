using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;

namespace Vipi.Hosting;

/// <summary>
/// Limita la frequenza di registrazione del login per UserId (una scrittura al massimo ogni finestra),
/// così il middleware non tocca il DB a ogni richiesta. Singleton, thread-safe.
/// </summary>
public sealed class StaffLoginThrottle
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<int, DateTime> _last = new();

    /// <summary>
    /// True al massimo una volta per finestra e per UserId. La decisione è atomica: un semplice
    /// leggi-poi-scrivi lascerebbe passare due richieste concorrenti dello stesso utente (entrambe vedono il
    /// valore vecchio e ritornano true), che è esattamente la doppia scrittura che questa classe deve evitare —
    /// e Blazor Server apre più richieste in parallelo per ogni caricamento di pagina.
    /// </summary>
    public bool ShouldRecord(int userId)
    {
        var now = DateTime.UtcNow;
        while (true)
        {
            if (_last.TryGetValue(userId, out var prev))
            {
                if (now - prev < Window) return false;
                // Vince solo chi sostituisce il valore che ha effettivamente osservato.
                if (_last.TryUpdate(userId, now, prev)) return true;
            }
            else if (_last.TryAdd(userId, now))
            {
                return true;   // primo login di questo utente in questo processo
            }
            // Perso il confronto: qualcun altro ha aggiornato tra la lettura e la scrittura. Rileggi e ridecidi
            // (il giro successivo trova la finestra fresca e ritorna false).
        }
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
        // ⚠️ TUTTO il corpo è protetto, non solo la scrittura. Questo middleware annota una statistica: se
        // fallisce, la richiesta deve proseguire come se non ci fosse. Il `try` copriva solo
        // `RecordLoginAsync`, e `users.Get()` restava scoperto — misurato il 24 agosto 2026 provocando un
        // guasto lì: l'eccezione usciva, il gestore d'errore rieseguiva «/Error», questo middleware girava
        // di nuovo sulla richiesta rieseguita e lanciava una seconda volta. Risultato: nemmeno la pagina
        // d'errore riusciva a uscire. Un pezzo che gira PRIMA del routing gira anche sulla via di fuga.
        try
        {
            var user = users.Get();
            if (user is not null && throttle.ShouldRecord(user.UserId))
                await roster.RecordLoginAsync(user, ctx.RequestAborted);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Registrazione login staff non riuscita: la richiesta prosegue lo stesso.");
        }

        await _next(ctx);
    }
}
