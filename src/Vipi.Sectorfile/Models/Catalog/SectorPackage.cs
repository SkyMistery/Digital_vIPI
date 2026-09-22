using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// Root object created when the Entry ISC is opened. Holds the global shared data, the catalog,
/// and all independent layer collections (flat, no monolithic Fir geometry container).
///
/// Each *Ready Task completes when the corresponding Phase 1 sub-load finishes; they are
/// completed/cancelled by the GlobalLayerLoader. AirportLoader awaits <see cref="AtzShapesReady"/>
/// before extracting per-ICAO ATZ shapes; render-time fix resolution awaits <see cref="NavaidsReady"/>.
/// </summary>
public sealed class SectorPackage
{
    // Every signal is created through Track(), which adds it to _signals so CancelPendingLayers
    // can cancel them all by iteration — no hand-maintained cancel list to fall out of sync.
    private readonly List<ILayerSignal> _signals = new();

    private readonly LayerSignal<NavaidSet> _navaids;
    private readonly LayerSignal<GlobalGeoData> _globalGeo;
    private readonly LayerSignal<TflLayer> _dynamicSectors;
    private readonly LayerSignal<TflLayer> _gcaAirspace;
    private readonly LayerSignal<TflLayer> _atzShapes;
    private readonly LayerSignal<HartccLayer> _hiAirspace;
    private readonly LayerSignal<LartccLayer> _loAirspace;
    private readonly LayerSignal<AtcFrequencyLayer> _frequencies;
    private readonly LayerSignal<EnrMvaLayer> _enrMva;
    private readonly LayerSignal<EnrVfrLayer> _enrVfr;
    private readonly LayerSignal<FicLayer> _ficAirspace;

    public SectorPackage(string entryIscPath)
    {
        EntryIscPath = entryIscPath;

        _navaids = Track(new LayerSignal<NavaidSet>());
        _globalGeo = Track(new LayerSignal<GlobalGeoData>());
        _dynamicSectors = Track(new LayerSignal<TflLayer>());
        _gcaAirspace = Track(new LayerSignal<TflLayer>());
        _atzShapes = Track(new LayerSignal<TflLayer>());
        _hiAirspace = Track(new LayerSignal<HartccLayer>());
        _loAirspace = Track(new LayerSignal<LartccLayer>());
        _frequencies = Track(new LayerSignal<AtcFrequencyLayer>());
        _enrMva = Track(new LayerSignal<EnrMvaLayer>());
        _enrVfr = Track(new LayerSignal<EnrVfrLayer>());
        _ficAirspace = Track(new LayerSignal<FicLayer>());
    }

    private LayerSignal<T> Track<T>(LayerSignal<T> signal)
    {
        _signals.Add(signal);
        return signal;
    }

    public string EntryIscPath { get; }

    // ── Phase 0 data ───────────────────────────────────────────────────────────────
    public ColorPalette Palette { get; set; } = new();   // loaded during ISC scan
    public SymbolSet Symbols { get; set; } = new();       // from .sym
    public Catalog Catalog { get; } = new();              // built during Phase 0

    // ── Global geographic layers (loaded lazily per layer on demand) ────────────────
    public GlobalGeoData GlobalGeo { get; } = new();
    public NavaidSet Navaids { get; } = new();
    public AtcFrequencyLayer Frequencies { get; } = new();
    public TflLayer DynamicSectors { get; } = new();
    public TflLayer GcaAirspace { get; } = new();
    public TflLayer AtzShapes { get; } = new();
    public HartccLayer HiAirspaceBounds { get; } = new();
    public LartccLayer LoAirspaceBounds { get; } = new();
    public EnrMvaLayer EnrMva { get; } = new();
    public EnrVfrLayer EnrVfr { get; } = new();
    public FicLayer FicAirspace { get; } = new();

    // ── Layer-ready signals (awaited by AirportLoader and other consumers) ──────────
    public Task<NavaidSet> NavaidsReady => _navaids.Task;
    public Task<GlobalGeoData> GlobalGeoReady => _globalGeo.Task;
    public Task<TflLayer> DynamicSectorsReady => _dynamicSectors.Task;
    public Task<TflLayer> GcaAirspaceReady => _gcaAirspace.Task;
    public Task<TflLayer> AtzShapesReady => _atzShapes.Task;
    public Task<HartccLayer> HiAirspaceReady => _hiAirspace.Task;
    public Task<LartccLayer> LoAirspaceReady => _loAirspace.Task;
    public Task<AtcFrequencyLayer> FrequenciesReady => _frequencies.Task;
    public Task<EnrMvaLayer> EnrMvaReady => _enrMva.Task;
    public Task<EnrVfrLayer> EnrVfrReady => _enrVfr.Task;
    public Task<FicLayer> FicAirspaceReady => _ficAirspace.Task;

    // ── Completion (called by GlobalLayerLoader as each sub-load finishes) ───────────
    public void CompleteNavaids() => _navaids.Complete(Navaids);
    public void CompleteGlobalGeo() => _globalGeo.Complete(GlobalGeo);
    public void CompleteDynamicSectors() => _dynamicSectors.Complete(DynamicSectors);
    public void CompleteGcaAirspace() => _gcaAirspace.Complete(GcaAirspace);
    public void CompleteAtzShapes() => _atzShapes.Complete(AtzShapes);
    public void CompleteHiAirspace() => _hiAirspace.Complete(HiAirspaceBounds);
    public void CompleteLoAirspace() => _loAirspace.Complete(LoAirspaceBounds);
    public void CompleteFrequencies() => _frequencies.Complete(Frequencies);
    public void CompleteEnrMva() => _enrMva.Complete(EnrMva);
    public void CompleteEnrVfr() => _enrVfr.Complete(EnrVfr);
    public void CompleteFicAirspace() => _ficAirspace.Complete(FicAirspace);

    /// <summary>Cancels every layer signal that has not yet completed (Phase 1 cancellation).</summary>
    public void CancelPendingLayers()
    {
        foreach (var signal in _signals)
        {
            signal.Cancel();
        }
    }
}
