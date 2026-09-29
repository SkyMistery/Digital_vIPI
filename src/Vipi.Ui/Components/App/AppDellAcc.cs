using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Ui.Components.App;

/// <summary>
/// Le vIPI APP pubblicate di una ACC, per la pagina dell'ACC: ognuna sul settore di una posizione del suo ente,
/// col collasso d'albero (un APP sotto un altro APP mostrato non si ripete). Stessa regola di <c>/apps</c>.
/// </summary>
public static class AppDellAcc
{
    /// <param name="enti">Gli enti dell'ACC: la chiave pubblicata è il CODICE dell'ente, che non è per forza il
    /// nominativo di un settore che esiste ancora.</param>
    public static List<SectorRow> Elenco(IReadOnlyList<SectorRow> all, IReadOnlyList<ManagedDoc> managed,
        IReadOnlyList<AtcUnitRow> enti, string accCode)
    {
        var published = managed
            .Where(m => m.Kind == ReleaseTargetType.App && m.HasEffectiveRelease && !m.IsHidden
                        && string.Equals(m.AccCode, accCode, StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Scope)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var perCallsign = all.GroupBy(s => s.Callsign, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // ⚠️ Dal codice al settore passando dall'ENTE (revisione degli enti ATC, S52): prima si cercava un settore
        // col nominativo uguale al codice, e a Pratica — dove il codice LIRE_APP resta anche quando la posizione
        // LIRE_APP sparisce da IVAO — la vIPI pubblicata spariva dalla pagina dell'ACC, mentre /apps la mostrava
        // sotto LIRE_TWR. Il settore è quello della principale, poi di un'altra posizione, poi del codice.
        var enteDi = enti.ToDictionary(u => u.Code, StringComparer.OrdinalIgnoreCase);
        var standalone = new List<SectorRow>();
        foreach (var scope in published)
        {
            SectorRow? settore = null;
            if (enteDi.TryGetValue(scope, out var ente))
                foreach (var cs in ente.Positions.Append(ente.Code))
                    if (perCallsign.TryGetValue(cs, out settore)) break;
            settore ??= perCallsign.GetValueOrDefault(scope);
            if (settore is not null) standalone.Add(settore);
        }
        standalone = standalone.DistinctBy(s => s.Id).ToList();

        var standaloneIds = standalone.Select(s => s.Id).ToHashSet();
        var byId = all.ToDictionary(s => s.Id);
        bool HasStandaloneAncestor(SectorRow s)
        {
            var pid = s.ParentSectorId;
            while (pid is int id && byId.TryGetValue(id, out var p))
            {
                if (standaloneIds.Contains(p.Id)) return true;
                pid = p.ParentSectorId;
            }
            return false;
        }
        return standalone.Where(s => !HasStandaloneAncestor(s)).ToList();
    }
}
