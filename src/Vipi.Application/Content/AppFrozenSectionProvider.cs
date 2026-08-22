using System.Text.Json;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Cattura Frozen delle sezioni derivate dell'APP standalone (doc 10 §3b). Chiave di release = callsign del settore APP
/// primario. Deriva AoR/Frequenze/Coordinamenti via <see cref="IAppDocumentService"/> e serializza il view-model.
/// Le sezioni editoriali (separazioni/configurazioni/vfr) vivono già nei blocchi statici del <c>Doc</c>; il config-table
/// si deriva dalla config congelata → non va catturato qui.
/// </summary>
public sealed class AppFrozenSectionProvider : IFrozenSectionProvider
{
    private readonly IAppDocumentService _app;
    public AppFrozenSectionProvider(IAppDocumentService app) => _app = app;

    public ReleaseTargetType Type => ReleaseTargetType.App;

    public async Task<IReadOnlyDictionary<int, string>> CaptureFrozenAsync(string key, RawDocument doc, CancellationToken ct = default)
    {
        var result = new Dictionary<int, string>();
        foreach (var s in FrozenSectionScan.FrozenDerived(doc))
        {
            object? vm = s.SectionKey.ToLowerInvariant() switch
            {
                "aor" => await _app.GetAorViewAsync(key, ct),
                "frequencies" => await _app.DeriveFrequenciesAsync(key, ct),
                "coordination" => await _app.DeriveCoordinationAsync(key, ct),
                "minima" => await _app.DeriveMinimaAsync(key, ct),
                _ => null,
            };
            if (vm is not null) result[s.Id] = JsonSerializer.Serialize(vm);
        }
        return result;
    }
}
