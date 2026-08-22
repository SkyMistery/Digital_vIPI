using System.Text.Json;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Cattura Frozen delle sezioni derivate della vIPI ACC (doc 10 §3b). Chiave di release = "{accCode}|{rootCallsign}".
/// La vIPI ACC è a blocchi (Aerovia + gruppi APP): ogni blocco ha le proprie sotto-sezioni derivate (aor/frequenze/
/// coordinamenti, minime), con RenderMode indipendente. Assembla i blocchi dallo snapshot e, per ogni sotto-sezione Frozen,
/// deriva col contesto del blocco e serializza il view-model, keyed per Id della sotto-sezione.
/// </summary>
public sealed class AccFrozenSectionProvider : IFrozenSectionProvider
{
    private readonly IAccDerivationService _acc;
    public AccFrozenSectionProvider(IAccDerivationService acc) => _acc = acc;

    public ReleaseTargetType Type => ReleaseTargetType.AccVipi;

    public async Task<IReadOnlyDictionary<int, string>> CaptureFrozenAsync(string key, RawDocument doc, CancellationToken ct = default)
    {
        var result = new Dictionary<int, string>();
        var frozenIds = FrozenSectionScan.FrozenDerived(doc).Select(s => s.Id).ToHashSet();
        if (frozenIds.Count == 0) return result;

        var parts = key.Split('|', 2);
        var accCode = parts[0];
        var root = parts.Length > 1 ? parts[1] : null;

        foreach (var ab in AccDocumentAssembler.Assemble(doc))
        {
            foreach (var secKey in new[] { "aor", "frequencies", "coordination", "minima" })
            {
                if (!ab.ChildSectionIdsByKey.TryGetValue(secKey, out var sid) || !frozenIds.Contains(sid)) continue;
                object vm = secKey switch
                {
                    "aor" => await _acc.DeriveAorViewAsync(accCode, ab.Block, root, ct),
                    "frequencies" => await _acc.DeriveFrequenciesAsync(accCode, ab.Block, root, ct),
                    "minima" => await _acc.DeriveMinimaAsync(accCode, ab.Block, root, ct),
                    _ => await _acc.DeriveCoordinationAsync(accCode, ab.Block, root, ct),
                };
                result[sid] = JsonSerializer.Serialize(vm);
            }
        }
        return result;
    }
}
