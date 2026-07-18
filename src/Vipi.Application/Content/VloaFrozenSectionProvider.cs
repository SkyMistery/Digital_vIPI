using System.Text.Json;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Cattura Frozen delle sezioni derivate della vLOA (doc 10 §3b). Chiave di release = Id del Document (string).
/// Deriva AoR/Frequenze/Coordinamenti via <see cref="IVloaDerivationService"/> e serializza il view-model, così il
/// viewer (S4) può renderizzarlo congelato senza ri-derivare dai cataloghi.
/// </summary>
public sealed class VloaFrozenSectionProvider : IFrozenSectionProvider
{
    private readonly IVloaDerivationService _vloa;
    public VloaFrozenSectionProvider(IVloaDerivationService vloa) => _vloa = vloa;

    public ReleaseTargetType Type => ReleaseTargetType.Vloa;

    public async Task<IReadOnlyDictionary<int, string>> CaptureFrozenAsync(string key, RawDocument doc, CancellationToken ct = default)
    {
        var result = new Dictionary<int, string>();
        if (!int.TryParse(key, out var docId)) return result;

        foreach (var s in FrozenSectionScan.FrozenDerived(doc))
        {
            object? vm = s.SectionKey.ToLowerInvariant() switch
            {
                "aor" => await _vloa.DeriveAorAsync(docId, ct),
                "frequencies" => await _vloa.DeriveFrequenciesAsync(docId, ct),
                "coordination" => await _vloa.DeriveCoordinationAsync(docId, ct),
                _ => null,   // altre chiavi derivate (es. minima) non previste per la vLOA
            };
            if (vm is not null) result[s.Id] = JsonSerializer.Serialize(vm);
        }
        return result;
    }
}
