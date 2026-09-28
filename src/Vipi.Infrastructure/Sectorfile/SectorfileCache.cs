using System.Collections.Concurrent;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Sectorfile;

/// <summary>
/// Cache di processo (singleton) dei file del sectorfile Aurora indipendenti dall'aeroporto: catalogo dei punti
/// (<c>itvor</c>+<c>itndb</c>+<c>itfix</c>), poligoni TWR (<c>twrs.tfl</c>) e poligoni di settore
/// (<c>DYNAMIC_SEC/*.tfl</c>). Sono file grandi e stabili per ciclo
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
    private readonly TimeProvider _orologio;

    public SectorfileCache(TimeProvider? orologio = null) => _orologio = orologio ?? TimeProvider.System;

    private readonly SemaphoreSlim _navGate = new(1, 1);
    private readonly SemaphoreSlim _twrGate = new(1, 1);
    private readonly SemaphoreSlim _secGate = new(1, 1);

    private NavaidCatalog? _navaids;
    private IReadOnlyDictionary<string, string>? _towerPolygons;
    private SectorShapes? _sectorShapes;

    // Le carte MRVA sono UNA PER ENTE (ENRMVA/{acc}.mva, {icao}.mva): a differenza delle altre due fette non c'è
    // un file solo da tenere, ma fino a una trentina. ConcurrentDictionary e non Dictionary+lock perché
    // Invalidate() è sincrona e non può prendere il gate asincrono che protegge i caricamenti.
    private readonly ConcurrentDictionary<string, MvaFile> _mvaCharts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Il catalogo dei punti, caricato una volta sola per processo.</summary>
    public Task<NavaidCatalog> GetNavaidsAsync(
        Func<CancellationToken, Task<NavaidCatalog>> load, CancellationToken ct = default) =>
        GetNavaidsAsync(async t => (await load(t), true), ct);

    /// <summary>
    /// Il catalogo dei punti, tenuto <b>solo se chi lo carica dice che è completo</b>.
    ///
    /// <para>🔴 U-033 (revisione totale 3): un catalogo ridotto — l'indice che risponde 503 e il ripiego sui tre
    /// file di configurazione, 1387 nomi invece di 3745 — restava qui per tutto il giro e per ogni «Reimporta»,
    /// senza che nessuno sapesse che era ridotto. Ora si consegna a chi l'ha chiesto e non si tiene: il chiamante
    /// dopo riprova la strada intera.</para>
    /// </summary>
    public async Task<NavaidCatalog> GetNavaidsAsync(
        Func<CancellationToken, Task<(NavaidCatalog Catalogo, bool DaTenere)>> load, CancellationToken ct = default)
    {
        if (Volatile.Read(ref _navaids) is { } hit) return hit;
        await _navGate.WaitAsync(ct);
        try
        {
            if (Volatile.Read(ref _navaids) is { } cached) return cached;   // caricato da un altro chiamante durante l'attesa
            var (loaded, daTenere) = await load(ct);
            if (daTenere) Volatile.Write(ref _navaids, loaded);
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
    /// Poligoni di SETTORE (CTR/APP/MIL/FSS), caricati una volta sola per processo. Costano di piu' delle
    /// altre fette — l'indice piu' una ventina di file — ed e' il motivo per cui stanno qui e non nel provider,
    /// che e' transient.
    /// </summary>
    public async Task<SectorShapes> GetSectorPolygonsAsync(
        Func<CancellationToken, Task<SectorShapes>> load, CancellationToken ct = default)
    {
        if (Volatile.Read(ref _sectorShapes) is { } hit) return hit;
        await _secGate.WaitAsync(ct);
        try
        {
            if (Volatile.Read(ref _sectorShapes) is { } cached) return cached;
            var loaded = await load(ct);
            Volatile.Write(ref _sectorShapes, loaded);
            return loaded;
        }
        finally { _secGate.Release(); }
    }

    /// <summary>
    /// La carta MRVA di un ente (chiave = percorso del file), caricata una volta sola per processo. Un esito
    /// vuoto viene messo in cache come gli altri: i 25 APP su 49 che non hanno il file darebbero altrimenti un
    /// GET a ogni apertura del documento, per un 404 che non cambia fino al prossimo ciclo AIRAC.
    ///
    /// <para>🔴 U-039 (revisione totale 3): <b>un semaforo per carta, e il guasto si ricorda</b>. Il semaforo era
    /// uno per tutte, e un caricamento fallito non lasciava traccia: con GitHub giù ogni richiesta riprovava, in
    /// fila — tre editor su tre vIPI ACC aspettavano 15, 30 e 45 secondi prima di vedere l'errore. Ora ogni carta
    /// ha la sua fila, e per <see cref="DurataDelGuasto"/> chi la chiede riceve subito lo stesso guasto.</para>
    /// </summary>
    public async Task<MvaChart> GetMvaChartAsync(
        string key, Func<CancellationToken, Task<MvaChart>> load, CancellationToken ct = default) =>
        (await GetMvaFileAsync(key, async t => new MvaFile(null, await load(t)), ct)).Carta;

    /// <summary>Come <see cref="GetMvaChartAsync"/>, col <b>testo</b> del file accanto alla carta: serve al
    /// cancello del ciclo AIRAC (U-037), che ricorda il testo e non la carta letta.</summary>
    public async Task<MvaFile> GetMvaFileAsync(
        string key, Func<CancellationToken, Task<MvaFile>> load, CancellationToken ct = default)
    {
        if (_mvaCharts.TryGetValue(key, out var hit)) return hit;
        var gate = _mvaGates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (_mvaCharts.TryGetValue(key, out var cached)) return cached;   // caricata durante l'attesa
            if (_mvaGuasti.TryGetValue(key, out var guasto) && _orologio.GetUtcNow() - guasto.Quando < DurataDelGuasto)
                throw new HttpRequestException(
                    $"Carta MRVA {key} non disponibile: la sorgente non ha risposto alle {guasto.Quando:HH:mm:ss} UTC ({guasto.Perche}).");
            try
            {
                var loaded = await load(ct);
                _mvaCharts[key] = loaded;
                _mvaGuasti.TryRemove(key, out _);
                return loaded;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _mvaGuasti[key] = (_orologio.GetUtcNow(), ex.Message);
                throw;
            }
        }
        finally { gate.Release(); }
    }

    /// <summary>Quanto si ricorda un caricamento MRVA fallito. Vedi <see cref="GetMvaChartAsync"/>.</summary>
    public static readonly TimeSpan DurataDelGuasto = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _mvaGates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (DateTimeOffset Quando, string Perche)> _mvaGuasti = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// <b>Che cosa dice di sé la sorgente delle SID</b> — ciclo dichiarato e ultimo cambiamento — chiesto una
    /// volta sola per processo (carta 2026-09-02 §AW2). Il giro d'import chiama <c>ImportAsync</c> <b>una
    /// volta per aeroporto</b> — decine — e questa risposta non dipende dall'ICAO: senza cache sarebbe una
    /// chiamata alla API di GitHub per ogni scalo, cioè la quota anonima esaurita a metà giro.
    ///
    /// <para>⚠️ <b>Anche il «non lo so» si mette in cache</b>: <see cref="SidSourceRelease.Muta"/> è un valore
    /// come gli altri e si distingue dal «non ancora chiesto», che è il <c>null</c> del campo. Se la sorgente
    /// ha risposto 403, richiederglielo altre trentanove volte nello stesso giro dà trentanove 403.</para>
    /// </summary>
    ///
    /// <para>🔴 <b>Ma vale pochi minuti, non per sempre</b> (U-032, revisione totale 3). Solo il giro automatico
    /// svuota questa cache, e il tasto «Reimporta» dell'editor non passa di lì: la divisione pubblicava il
    /// changelog del ciclo nuovo con le SID riviste, un editor premeva il tasto prima del giro, e le SID nuove
    /// prendevano il ciclo VECCHIO — pubbliche subito, e per sempre, perché a contenuto invariato si conserva il
    /// primo timbro. Cinque minuti bastano a un giro intero (una chiamata per giro, non una per scalo) e sono
    /// pochi per un tasto premuto dopo.</para>
    /// </summary>
    public async Task<SidSourceRelease> GetSidSourceReleaseAsync(
        Func<CancellationToken, Task<SidSourceRelease>> load, CancellationToken ct = default)
    {
        if (Fresca(Volatile.Read(ref _sidStamp)) is { } hit) return hit;
        await _stampGate.WaitAsync(ct);
        try
        {
            if (Fresca(Volatile.Read(ref _sidStamp)) is { } cached) return cached;
            var loaded = await load(ct);
            Volatile.Write(ref _sidStamp, new Timbro(loaded, _orologio.GetUtcNow()));
            return loaded;
        }
        finally { _stampGate.Release(); }
    }

    /// <summary>Quanto vale la risposta della sorgente delle SID. Vedi <see cref="GetSidSourceReleaseAsync"/>.</summary>
    public static readonly TimeSpan DurataDelTimbro = TimeSpan.FromMinutes(5);

    private SidSourceRelease? Fresca(Timbro? t) =>
        t is not null && _orologio.GetUtcNow() - t.Quando < DurataDelTimbro ? t.Valore : null;

    private sealed record Timbro(SidSourceRelease Valore, DateTimeOffset Quando);

    private readonly SemaphoreSlim _stampGate = new(1, 1);
    private Timbro? _sidStamp;

    /// <summary>
    /// Butta via le fette: il prossimo chiamante riscarica.
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
        Volatile.Write(ref _sectorShapes, null);
        Volatile.Write(ref _sidStamp, null);
        _mvaCharts.Clear();
        _mvaGuasti.Clear();
    }

    /// <summary>Butta via il solo catalogo dei punti. È la fetta che serve a chi SCRIVE, ed è l'unica che
    /// qualcuno possa voler rileggere subito senza aspettare il giro delle 24 ore.</summary>
    public void InvalidateNavaids() => Volatile.Write(ref _navaids, null);
}
