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

            // Union di chi tiene cosa su tutte le radici (dopo la riduzione i domini sono disgiunti). Si leggono le
            // FASCE e non l'ownership a una voce: un settore diviso per quota fra due aperti va scritto sotto
            // tutti e due, ognuno con la sua fascia — con una voce sola una metà sparirebbe dalla tabella.
            var tenuti = new List<(string Settore, string Chi, string Voce)>();
            var visti = new HashSet<string>(OIC);
            foreach (var root in roots)
                foreach (var (settore, fasce) in aor.Resolve(topology, root, open).Holdings)
                {
                    if (!visti.Add(settore)) continue;
                    foreach (var f in fasce)
                        tenuti.Add((settore, f.Owner,
                            fasce.Count == 1 ? settore : $"{settore} ({Fascia(f.BaseFeet, f.TopFeet)})"));
                }

            // Ordine di apertura per callsign, precomputato (lookup O(1) nell'OrderBy invece di FindIndex O(n) per confronto).
            var openOrder = new Dictionary<string, int>(OIC);
            var openIdx = 0;
            foreach (var cs in cfg.OpenCallsigns)
                if (!openOrder.ContainsKey(cs)) openOrder[cs] = openIdx++;
            // Il "settore unificato" è per definizione un settore APERTO: si tengono solo le righe del pool il cui
            // proprietario è nell'insieme aperto (i rami senza aperti non compaiono come unificati).
            var rows = tenuti
                .Where(t => pool.Contains(t.Settore) && open.Contains(t.Chi))
                .GroupBy(t => t.Chi, OIC)
                .Select(g =>
                {
                    var cp = cfg.Open.FirstOrDefault(o => string.Equals(o.Callsign, g.Key, StringComparison.OrdinalIgnoreCase));
                    var absorbed = g.Select(t => t.Voce).OrderBy(c => c, OIC).ToList();   // callsign, non nomi
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

    /// <summary>
    /// La fascia di un settore diviso, come sta accanto al suo callsign: <c>FL325–UNL</c>, <c>SFC–2500 ft</c>.
    ///
    /// <para>⚠️ Senza parole di una lingua e senza separatori di cultura: la tabella finisce <b>scritta</b> nelle
    /// release pubblicate, che si leggono in italiano e in inglese. Sotto i 10 000 piedi si scrive in piedi, sopra
    /// in livelli di volo — la convenzione che il sito usa già per le fasce dei ripieghi.</para>
    /// </summary>
    internal static string Fascia(int? baseFeet, int? topFeet)
    {
        static string Quota(int piedi) => piedi >= 10_000
            ? "FL" + (piedi / 100).ToString("000", System.Globalization.CultureInfo.InvariantCulture)
            : piedi.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ft";

        return $"{(baseFeet is int b && b > 0 ? Quota(b) : "SFC")}–{(topFeet is int t ? Quota(t) : "UNL")}";
    }

    /// <summary>Deserializza una lista di configurazioni dal BodyJson d'una sezione «configurations» (vuoto/malformato = nessuna).</summary>
    public static List<AccConfiguration> Deserialize(string? json) => ConfigurazioniJson.Leggi(json);
}
