using System.Text.Json;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Cattura Frozen delle sezioni derivate della vIPI ACC (doc 10 §3b). Chiave di release = "{accCode}|{rootCallsign}".
/// La vIPI ACC è a blocchi (Aerovia + gruppi APP): ogni blocco ha le proprie sotto-sezioni derivate (aor/frequenze/
/// coordinamenti, minime), con RenderMode indipendente. Assembla i blocchi dallo snapshot e, per ogni sotto-sezione Frozen,
/// deriva col contesto del blocco e serializza il view-model, keyed per Id della sotto-sezione.
/// </summary>
internal sealed class AccFrozenSectionProvider : IFrozenSectionProvider
{
    private readonly IAccDerivationService _acc;
    private readonly ISectorConfigurationService _configurazioni;
    private readonly Abstractions.IAtcUnitRepository _enti;

    public AccFrozenSectionProvider(IAccDerivationService acc, ISectorConfigurationService configurazioni,
        Abstractions.IAtcUnitRepository enti)
    {
        _acc = acc;
        _configurazioni = configurazioni;
        _enti = enti;
    }

    public ReleaseTargetType Type => ReleaseTargetType.AccVipi;

    public async Task<IReadOnlyDictionary<int, string>> CaptureFrozenAsync(string key, RawDocument doc, CancellationToken ct = default)
    {
        var result = new Dictionary<int, string>();
        var frozenIds = FrozenSectionScan.FrozenDerived(doc).Select(s => s.Id).ToHashSet();
        if (frozenIds.Count == 0) return result;

        var parts = key.Split('|', 2);
        var accCode = parts[0];
        var root = parts.Length > 1 ? parts[1] : null;

        var blocchi = AccDocumentAssembler.Assemble(doc);
        // ⚠️ PRIMA di derivare qualunque cosa: la mappa AoR porta le chip delle configurazioni, e quelle sono le
        // configurazioni della Struttura — non il BodyJson che può essere rimasto nella sezione del documento
        // (carta 2026-10-08-configurazioni-possibili §5).
        await ConfigurazioniDelDocumento.DallaStrutturaAsync(_configurazioni, _enti, accCode, blocchi.Select(b => b.Block), ct);

        foreach (var ab in blocchi)
        {
            // Le configurazioni si congelano come le altre derivate: la release dice quelle di ALLORA, anche se
            // in Struttura poi cambiano. Il valore è la lista com'è; la tabella d'accorpamento si ricalcola al view.
            if (ab.ChildSectionIdsByKey.TryGetValue(ConfigurazioniDelDocumento.Chiave, out var cfgId) && frozenIds.Contains(cfgId))
                result[cfgId] = JsonSerializer.Serialize(ab.Block.Configurations);
            // ⚠️ Le AoR dei settori MIL e FSS (21 settembre 2026) si congelano come quella principale: sono geometrie,
            // e un documento pubblicato che le mostrasse vive direbbe «oggi» accanto a una AoR che dice «allora».
            foreach (var secKey in new[] { "aor", SectionKeys.AorMil, SectionKeys.AorFss, "frequencies", "coordination", "minima" })
            {
                if (!ab.ChildSectionIdsByKey.TryGetValue(secKey, out var sid) || !frozenIds.Contains(sid)) continue;
                object vm = secKey switch
                {
                    "aor" => await _acc.DeriveAorViewAsync(accCode, ab.Block, root, ct),
                    SectionKeys.AorMil => await _acc.DeriveAorViewAsync(accCode, ab.Block, FamigliaAor.Mil, root, ct),
                    SectionKeys.AorFss => await _acc.DeriveAorViewAsync(accCode, ab.Block, FamigliaAor.Fss, root, ct),
                    "frequencies" => await _acc.DeriveFrequenciesAsync(accCode, ab.Block, root, ct),
                    "minima" => MinimaCharts.DaCongelare(await _acc.DeriveMinimaAsync(accCode, ab.Block, root, ct)),
                    _ => await _acc.DeriveCoordinationAsync(accCode, ab.Block, root, ct),
                };
                result[sid] = JsonSerializer.Serialize(vm);
            }
        }
        return result;
    }
}
