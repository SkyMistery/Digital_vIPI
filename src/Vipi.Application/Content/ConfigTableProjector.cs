using System.Text.Json;
using Vipi.Application.Aor;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Proiettore PURO dell'accorpamento di una configurazione: dato l'insieme dei settori APERTI, risolve chi copre chi
/// (<see cref="IAorService.Resolve"/> per ogni radice del dominio) e produce la tabella «settore unificato → assorbiti».
/// Fonte unica condivisa da vIPI ACC (Aerovia CTR + gruppi APP) e vIPI APP standalone (Regola del 2: la derivazione
/// config compariva in più punti). Nessun I/O: gli input (topologia, radici, pool, nomi) li risolve il chiamante.
/// </summary>
public static class ConfigTableProjector
{
    private static readonly StringComparer OIC = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Tabella accorpamento per ogni configurazione. <paramref name="roots"/> = radici su cui risolvere l'ownership
    /// (alberi CTR dell'ACC, o i callsign APP membri/primario); <paramref name="pool"/> = settori ammessi come righe
    /// (solo i settori del tipo pertinente al blocco). Settore unificato e assorbiti sono resi come <b>callsign</b>.
    /// </summary>
    public static IReadOnlyList<AccConfigTableView> Build(
        IAorService aor, Topology topology, IReadOnlyList<string> roots,
        IReadOnlySet<string> pool, IReadOnlyList<AccConfiguration> configs)
    {
        if (roots.Count == 0 || configs.Count == 0) return Array.Empty<AccConfigTableView>();

        var result = new List<AccConfigTableView>();
        foreach (var cfg in configs)
        {
            var open = new HashSet<string>(cfg.OpenCallsigns, OIC);

            // Union dell'ownership su tutte le radici (i domini delle radici sono disgiunti).
            var ownership = new Dictionary<string, string>(OIC);
            foreach (var root in roots)
                foreach (var kv in aor.Resolve(topology, root, open).Ownership)
                    ownership[kv.Key] = kv.Value;

            // Ordine di apertura per callsign, precomputato (lookup O(1) nell'OrderBy invece di FindIndex O(n) per confronto).
            var openOrder = new Dictionary<string, int>(OIC);
            var openIdx = 0;
            foreach (var cs in cfg.OpenCallsigns)
                if (!openOrder.ContainsKey(cs)) openOrder[cs] = openIdx++;
            // Il "settore unificato" è per definizione un settore APERTO: si tengono solo le righe del pool il cui
            // proprietario è nell'insieme aperto (i rami senza aperti non compaiono come unificati).
            var rows = ownership
                .Where(kv => pool.Contains(kv.Key) && open.Contains(kv.Value))
                .GroupBy(kv => kv.Value, OIC)
                .Select(g =>
                {
                    var cp = cfg.Open.FirstOrDefault(o => string.Equals(o.Callsign, g.Key, StringComparison.OrdinalIgnoreCase));
                    var absorbed = g.Select(kv => kv.Key).OrderBy(c => c, OIC).ToList();   // callsign, non nomi
                    return new AccConfigTableRow(g.Key, absorbed, cp?.CenterPoint, cp?.Range);
                })
                .OrderBy(r => openOrder.TryGetValue(r.UnifiedCallsign, out var i) ? i : int.MaxValue)
                .ThenBy(r => r.UnifiedCallsign, OIC)
                .ToList();

            result.Add(new AccConfigTableView(cfg.Key, cfg.Name, rows));
        }
        return result;
    }

    /// <summary>Deserializza una lista di configurazioni dal BodyJson d'una sezione «configurations» (vuoto/malformato = nessuna).</summary>
    public static List<AccConfiguration> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<AccConfiguration>();
        try { return JsonSerializer.Deserialize<List<AccConfiguration>>(json) ?? new List<AccConfiguration>(); }
        catch (JsonException) { return new List<AccConfiguration>(); }
    }
}
