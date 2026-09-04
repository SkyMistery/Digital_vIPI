using Vipi.Application.Content;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il lock del documento sempre concesso: il doppio di <see cref="IAirportLockGuard"/> per le prove che
/// <b>non parlano del lock</b> — policy d'import, edizione giusta per campo, merge delle piste.
///
/// <para>⚠️ Sta in un file suo per la stessa ragione di <see cref="SenzaPromozioni"/>: più classi di prova
/// costruiscono <c>AirportEditingService</c>, e altrettante copie di questa classe vuota sarebbero altrettanti
/// posti in cui un domani scriverla diversa.</para>
///
/// <para>Il lock vero lo prova <c>AirportLockGuardTests</c>, che è il posto dove deve stare.</para>
/// </summary>
internal sealed class LockAperto : IAirportLockGuard
{
    public static readonly LockAperto Instance = new();

    public Task EnsureMineAsync(string icao, CancellationToken ct = default) => Task.CompletedTask;
    public Task EnsureNotOtherAsync(string icao, CancellationToken ct = default) => Task.CompletedTask;
}
