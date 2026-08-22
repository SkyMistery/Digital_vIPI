namespace Vipi.Application.Content;

/// <summary>
/// Stato release riassuntivo di un bersaglio, per mostrarlo sulla riga collassata di /services/vsop/versions senza espanderla.
/// Caricato in blocco per tutte le righe. <see cref="EffectiveCycle"/> = ciclo della release in vigore ora (null =
/// il pubblico vede il fallback live); <see cref="NextScheduledCycle"/> = ciclo della prossima release futura, se c'è.
/// </summary>
public sealed record ReleaseSummary(string? EffectiveCycle, string? NextScheduledCycle, bool HasAnyRelease);
