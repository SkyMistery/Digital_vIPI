using System.Text.Json;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Cattura Frozen della sezione derivata dell'aeroporto (doc 10 §3e). Chiave di release = ICAO. L'unica sezione
/// derivabile è <c>sids</c> (default <see cref="RenderMode.Live"/> → di norma NON catturata); se l'admin la mette
/// Frozen, se ne congela l'output derivato via <see cref="IAirportSidDerivationService"/>. Il resto del documento
/// d'aeroporto è statico (già nei blocchi del <c>Doc</c>).
/// </summary>
public sealed class AirportFrozenSectionProvider : IFrozenSectionProvider
{
    private readonly IAirportSidDerivationService _sids;
    public AirportFrozenSectionProvider(IAirportSidDerivationService sids) => _sids = sids;

    public ReleaseTargetType Type => ReleaseTargetType.Airport;

    public async Task<IReadOnlyDictionary<int, string>> CaptureFrozenAsync(string key, RawDocument doc, CancellationToken ct = default)
    {
        var result = new Dictionary<int, string>();
        foreach (var s in FrozenSectionScan.FrozenDerived(doc))
        {
            object? vm = s.SectionKey.ToLowerInvariant() switch
            {
                "sids" => await _sids.DeriveAsync(key, ct),
                _ => null,
            };
            if (vm is not null) result[s.Id] = JsonSerializer.Serialize(vm);
        }
        return result;
    }
}
