namespace Vipi.Infrastructure.Sectorfile;

/// <summary>
/// Cache di processo (singleton) dei file del sectorfile Aurora indipendenti dall'aeroporto: elenco navaid
/// (<c>itfix</c>+<c>itvor</c>) e poligoni TWR (<c>twrs.tfl</c>). Sono file grandi e stabili per ciclo di import,
/// richiesti da più percorsi (job periodico SID, bottone import nell'editor, fallback shape TWR).
/// <para>
/// La cache vive qui e NON dentro gli adapter perché questi sono registrati con
/// <c>AddHttpClient&lt;TInterface, TImplementation&gt;</c>, quindi con lifetime <b>transient</b>: un campo d'istanza
/// sarebbe una cache per-risoluzione (file ri-scaricato a ogni click) e un <see cref="SemaphoreSlim"/> d'istanza
/// non sincronizzerebbe nulla fra risoluzioni diverse. Qui invece il caricamento avviene una volta per processo e i
/// chiamanti concorrenti lo condividono.
/// </para>
/// </summary>
public sealed class SectorfileCache
{
    private readonly SemaphoreSlim _navGate = new(1, 1);
    private readonly SemaphoreSlim _twrGate = new(1, 1);

    private IReadOnlySet<string>? _navaids;
    private IReadOnlyDictionary<string, string>? _towerPolygons;

    /// <summary>Nomi dei navaid (fix+vor), caricati una volta sola per processo.</summary>
    public async Task<IReadOnlySet<string>> GetNavaidsAsync(
        Func<CancellationToken, Task<IReadOnlySet<string>>> load, CancellationToken ct = default)
    {
        if (Volatile.Read(ref _navaids) is { } hit) return hit;
        await _navGate.WaitAsync(ct);
        try
        {
            if (Volatile.Read(ref _navaids) is { } cached) return cached;   // caricato da un altro chiamante durante l'attesa
            var loaded = await load(ct);
            Volatile.Write(ref _navaids, loaded);
            return loaded;
        }
        finally { _navGate.Release(); }
    }

    /// <summary>Poligoni TWR per callsign, caricati una volta sola per processo.</summary>
    public async Task<IReadOnlyDictionary<string, string>> GetTowerPolygonsAsync(
        Func<CancellationToken, Task<IReadOnlyDictionary<string, string>>> load, CancellationToken ct = default)
    {
        if (Volatile.Read(ref _towerPolygons) is { } hit) return hit;
        await _twrGate.WaitAsync(ct);
        try
        {
            if (Volatile.Read(ref _towerPolygons) is { } cached) return cached;
            var loaded = await load(ct);
            Volatile.Write(ref _towerPolygons, loaded);
            return loaded;
        }
        finally { _twrGate.Release(); }
    }
}
