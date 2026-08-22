using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Sectorfile;

/// <summary>
/// Cache di processo (singleton) dei file del sectorfile Aurora indipendenti dall'aeroporto: catalogo dei punti
/// (<c>itvor</c>+<c>itndb</c>+<c>itfix</c>) e poligoni TWR (<c>twrs.tfl</c>). Sono file grandi e stabili per ciclo
/// di import, richiesti da più percorsi (job periodico SID, bottone import nell'editor, fallback shape TWR,
/// suggerimenti dei campi punto negli editor).
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

    private NavaidCatalog? _navaids;
    private IReadOnlyDictionary<string, string>? _towerPolygons;

    /// <summary>Il catalogo dei punti, caricato una volta sola per processo.</summary>
    public async Task<NavaidCatalog> GetNavaidsAsync(
        Func<CancellationToken, Task<NavaidCatalog>> load, CancellationToken ct = default)
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

    /// <summary>
    /// Butta via le due fette: il prossimo chiamante riscarica.
    ///
    /// <para>Serve perché questa cache non scade mai. Finché conteneva solo dati d'import andava bene — il ciclo
    /// delle 24h li rileggeva comunque — ma il catalogo dei punti lo legge anche chi <b>scrive</b>: senza questo,
    /// un fix pubblicato oggi su GitHub resta invisibile ai suggerimenti fino al riavvio dell'applicazione, e
    /// l'editor segnerebbe come typo un nome che è corretto.</para>
    ///
    /// <para>Svuota entrambe le fette e non solo i navaid: i poligoni TWR vengono dallo stesso repository e allo
    /// stesso ritmo, e ricaricarli è un GET che nessuno aspetta (avviene alla prima richiesta, non qui).</para>
    /// </summary>
    public void Invalidate()
    {
        Volatile.Write(ref _navaids, null);
        Volatile.Write(ref _towerPolygons, null);
    }
}
