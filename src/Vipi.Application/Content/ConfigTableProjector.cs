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
internal static class ConfigTableProjector
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

        // ⚠️ Le radici arrivano dal DOCUMENTO (i settori scelti nel gruppo-APP) e possono ANNIDARSI: a Milano
        // il gruppo elenca LIMF_WW0 insieme ai suoi figli LIMF_WN0 e LIMJ_WS0. Risolvere anche dai figli non
        // aggiunge niente — il loro dominio è dentro quello del padre — ma l'unione qui sotto scrive per
        // ULTIMO chi arriva per ultimo, e un figlio risolto da sé stesso torna sempre padrone di sé (il suo
        // dominio è un solo settore, e la risalita si ferma al bordo del dominio). Così il figlio elencato
        // DOPO il padre cancellava la riga giusta: con la sola WW0 aperta, WS0 tornava «WS0 possiede WS0»,
        // non aperto, e spariva dagli assorbiti — la configurazione unica usciva senza i settori inclusi.
        roots = RadiciMassime(topology, roots);

        var result = new List<AccConfigTableView>();
        foreach (var cfg in configs)
        {
            var open = new HashSet<string>(cfg.OpenCallsigns, OIC);

            // Union dell'ownership su tutte le radici (dopo la riduzione i domini sono disgiunti).
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

    /// <summary>
    /// Le sole radici MASSIME: si scarta chi ha, fra i propri antenati, un'altra radice dell'elenco. Il dominio di
    /// un discendente è già dentro quello del suo antenato, quindi non si perde niente e i domini tornano disgiunti
    /// — che è la premessa dell'unione qui sopra.
    ///
    /// <para>⚠️ Se la riduzione svuotasse l'elenco si tengono le radici di partenza: succede solo con una gerarchia
    /// ad ANELLO (dove ognuno è antenato dell'altro), e una tabella storta si legge, una vuota no. L'anello lo
    /// nomina il rilievo «Gerarchia ciclica» del report di consistenza, che è il posto dove si aggiusta.</para>
    /// </summary>
    private static IReadOnlyList<string> RadiciMassime(Topology topology, IReadOnlyList<string> roots)
    {
        var insieme = new HashSet<string>(roots, OIC);
        var viste = new HashSet<string>(OIC);
        var massime = new List<string>();
        foreach (var r in roots)
        {
            if (!viste.Add(r)) continue;                                          // stesso callsign elencato due volte
            if (topology.Ancestors(r).Any(a => insieme.Contains(a) && !OIC.Equals(a, r))) continue;
            massime.Add(r);
        }
        return massime.Count > 0 ? massime : roots;
    }

    /// <summary>Deserializza una lista di configurazioni dal BodyJson d'una sezione «configurations» (vuoto/malformato = nessuna).</summary>
    public static List<AccConfiguration> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<AccConfiguration>();
        try { return JsonSerializer.Deserialize<List<AccConfiguration>>(json) ?? new List<AccConfiguration>(); }
        catch (JsonException) { return new List<AccConfiguration>(); }
    }
}
