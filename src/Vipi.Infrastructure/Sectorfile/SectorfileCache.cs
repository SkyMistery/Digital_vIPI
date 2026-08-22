using System.Collections.Concurrent;
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
    private readonly SemaphoreSlim _mvaGate = new(1, 1);

    private NavaidCatalog? _navaids;
    private IReadOnlyDictionary<string, string>? _towerPolygons;

    // Le carte MRVA sono UNA PER ENTE (ENRMVA/{acc}.mva, {icao}.mva): a differenza delle altre due fette non c'è
    // un file solo da tenere, ma fino a una trentina. ConcurrentDictionary e non Dictionary+lock perché
    // Invalidate() è sincrona e non può prendere il gate asincrono che protegge i caricamenti.
    private readonly ConcurrentDictionary<string, MvaChart> _mvaCharts = new(StringComparer.OrdinalIgnoreCase);

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
    /// La carta MRVA di un ente (chiave = percorso del file), caricata una volta sola per processo. Un esito
    /// vuoto viene messo in cache come gli altri: i 25 APP su 49 che non hanno il file darebbero altrimenti un
    /// GET a ogni apertura del documento, per un 404 che non cambia fino al prossimo ciclo AIRAC.
    /// </summary>
    public async Task<MvaChart> GetMvaChartAsync(
        string key, Func<CancellationToken, Task<MvaChart>> load, CancellationToken ct = default)
    {
        if (_mvaCharts.TryGetValue(key, out var hit)) return hit;
        await _mvaGate.WaitAsync(ct);
        try
        {
            if (_mvaCharts.TryGetValue(key, out var cached)) return cached;   // caricata durante l'attesa
            var loaded = await load(ct);
            _mvaCharts[key] = loaded;
            return loaded;
        }
        finally { _mvaGate.Release(); }
    }

    /// <summary>
    /// Butta via le tre fette: il prossimo chiamante riscarica.
    ///
    /// <para>Serve perché questa cache non scade mai. Finché conteneva solo dati d'import andava bene — il ciclo
    /// delle 24h li rileggeva comunque — ma il catalogo dei punti lo legge anche chi <b>scrive</b>: senza questo,
    /// un fix pubblicato oggi su GitHub resta invisibile ai suggerimenti fino al riavvio dell'applicazione, e
    /// l'editor segnerebbe come typo un nome che è corretto.</para>
    ///
    /// <para>Svuota tutte le fette e non solo i navaid: poligoni TWR e carte MRVA vengono dallo stesso repository
    /// e allo stesso ritmo, e ricaricarli è un GET che nessuno aspetta (avviene alla prima richiesta, non qui).</para>
    /// </summary>
    public void Invalidate()
    {
        InvalidateNavaids();
        Volatile.Write(ref _towerPolygons, null);
        _mvaCharts.Clear();
    }

    /// <summary>Butta via il solo catalogo dei punti. È la fetta che serve a chi SCRIVE, ed è l'unica che
    /// qualcuno possa voler rileggere subito senza aspettare il giro delle 24 ore.</summary>
    public void InvalidateNavaids() => Volatile.Write(ref _navaids, null);
}
