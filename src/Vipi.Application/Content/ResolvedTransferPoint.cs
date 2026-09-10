namespace Vipi.Application.Content;

/// <summary>Esito della risoluzione live di un punto (vista live): chi prende davvero il traffico ora.</summary>
public sealed class ResolvedTransferPoint
{
    public required TransferPointRow Point { get; init; }
    /// <summary>Callsign del ricevente risolto (primo settore online risalendo la gerarchia), oppure «UNICOM».</summary>
    public required string ResolvedHandler { get; init; }
    public required bool IsOnline { get; init; }

    /// <summary>
    /// Che cosa ha detto il <b>rinvio</b>, se la catena ne ha incontrato uno. <c>null</c> = non c'era nessun
    /// rinvio da sciogliere, ed è il caso normale.
    ///
    /// <para>⚠️ Serve a <b>mostrarlo</b>: un rinvio che risolve in silenzio è indistinguibile da un guasto, e
    /// uno che tace perché il CoP non è un punto è una cosa diversa da uno che tace perché non lo copre
    /// nessuno. Carta <c>docs/feature/2026-09-10-rinvio-geometrico.md</c> §7.</para>
    /// </summary>
    public CoverageFallbackResult? Coverage { get; init; }
}
