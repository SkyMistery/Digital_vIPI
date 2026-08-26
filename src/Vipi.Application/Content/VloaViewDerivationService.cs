using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Sezioni derivate della vLOA risolte per la vista (frozen o live) — doc 10 §3d.</summary>
public sealed record VloaViewDerived(VloaAorData Aor, VloaFreqData Freq, VloaCoordination Coord);

/// <summary>
/// Risolve le sezioni derivate (aor/freq/coord) della vLOA per la VISTA (doc 10 §3d): se <paramref name="useFrozen"/>
/// e c'è una release effettiva, legge l'output CONGELATO dallo snapshot per chiave di sezione; altrimenti deriva live
/// via <see cref="IVloaDerivationService"/>. Chiave di release = Id del Document (string). La cattura salva SOLO le
/// sezioni Frozen → per una Live il reader ritorna null e si ricade su live (nessun check di RenderMode qui).
/// </summary>
public interface IVloaViewDerivationService
{
    Task<VloaViewDerived> ResolveForViewAsync(int docId, bool useFrozen, CancellationToken ct = default);
}

/// <inheritdoc cref="IVloaViewDerivationService"/>
public sealed class VloaViewDerivationService : IVloaViewDerivationService
{
    private readonly IVloaDerivationService _vloa;
    private readonly IFrozenSectionReader _frozen;

    public VloaViewDerivationService(IVloaDerivationService vloa, IFrozenSectionReader frozen)
    {
        _vloa = vloa;
        _frozen = frozen;
    }

    public async Task<VloaViewDerived> ResolveForViewAsync(int docId, bool useFrozen, CancellationToken ct = default)
    {
        var key = docId.ToString();

        // Lo snapshot una volta sola (doc 14 §3c): la vLOA piu' grande dell'archivio pesa 221 KB e questo
        // metodo lo rileggeva tre volte — piu' di mezzo megabyte per apertura di pagina.
        var frozen = useFrozen ? await _frozen.LoadAsync(ReleaseTargetType.Vloa, key, ct) : FrozenSections.Empty;

        var aor = frozen.Get<VloaAorData>("aor") ?? await _vloa.DeriveAorAsync(docId, ct);
        var freq = frozen.Get<VloaFreqData>("frequencies") ?? await _vloa.DeriveFrequenciesAsync(docId, ct);
        var coord = frozen.Get<VloaCoordination>("coordination") ?? await _vloa.DeriveCoordinationAsync(docId, ct);
        return new VloaViewDerived(aor, freq, coord);
    }
}
