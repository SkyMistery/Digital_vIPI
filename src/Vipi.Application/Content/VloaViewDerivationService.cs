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

        var aor = (useFrozen ? await FrozenAsync<VloaAorData>("aor") : null)
            ?? await _vloa.DeriveAorAsync(docId, ct);
        var freq = (useFrozen ? await FrozenAsync<VloaFreqData>("frequencies") : null)
            ?? await _vloa.DeriveFrequenciesAsync(docId, ct);
        var coord = (useFrozen ? await FrozenAsync<VloaCoordination>("coordination") : null)
            ?? await _vloa.DeriveCoordinationAsync(docId, ct);
        return new VloaViewDerived(aor, freq, coord);

        Task<T?> FrozenAsync<T>(string sectionKey) where T : class =>
            _frozen.GetFrozenByKeyAsync<T>(ReleaseTargetType.Vloa, key, sectionKey, ct);
    }
}
