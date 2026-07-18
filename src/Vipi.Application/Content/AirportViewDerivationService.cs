using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Risolve la sezione SID dell'aeroporto per la VISTA (doc 10 §3d/§3e): se <paramref name="useFrozen"/> e c'è una
/// release effettiva con la sezione congelata, legge l'output frozen (by-key, chiave = ICAO); altrimenti deriva live
/// via <see cref="IAirportSidDerivationService"/>. La cattura salva solo le sezioni Frozen → per una Live (default) il
/// reader ritorna null e si ricade su live.
/// </summary>
public interface IAirportViewDerivationService
{
    Task<AirportSidView> ResolveSidsForViewAsync(string icao, bool useFrozen, CancellationToken ct = default);
}

/// <inheritdoc cref="IAirportViewDerivationService"/>
public sealed class AirportViewDerivationService : IAirportViewDerivationService
{
    private readonly IAirportSidDerivationService _sids;
    private readonly IFrozenSectionReader _frozen;

    public AirportViewDerivationService(IAirportSidDerivationService sids, IFrozenSectionReader frozen)
    {
        _sids = sids;
        _frozen = frozen;
    }

    public async Task<AirportSidView> ResolveSidsForViewAsync(string icao, bool useFrozen, CancellationToken ct = default)
    {
        icao = (icao ?? "").Trim().ToUpperInvariant();
        return (useFrozen ? await _frozen.GetFrozenByKeyAsync<AirportSidView>(ReleaseTargetType.Airport, icao, "sids", ct) : null)
            ?? await _sids.DeriveAsync(icao, ct);
    }
}
