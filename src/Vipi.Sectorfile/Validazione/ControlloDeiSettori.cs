using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.Validazione;

/// <summary>
/// I controlli dei settori dinamici contro i <c>.frq</c> (lotto «Subito» slice 13b, «file per file» D4): un settore
/// italiano si accende quando una sua posizione è collegata, e una posizione che nessun <c>.frq</c> conosce non si
/// collega mai.
/// </summary>
/// <remarks>
/// Misura sul fork del 4 ottobre 2026: 151 posizioni italiane diverse nelle teste dei <c>.tfl</c>, 147 definite in un
/// <c>.frq</c>, nessuna soltanto citata fra i trasferimenti, 4 assenti (<c>LIBC_TWR</c>: nei <c>.frq</c> è
/// <c>LIBC_I_TWR</c>; <c>LIMF_WW0_APP</c>, <c>LIQW_I_TWR</c>, <c>LIRE_APP</c>). Le posizioni estere (55, di cui 35 mai
/// citate) non stanno nei nostri <c>.frq</c> per forza: la regola non le guarda (committente, 24 settembre).
/// </remarks>
public static class ControlloDeiSettori
{
    /// <summary>
    /// I problemi fra settori e posizioni, nei due versi: il settore con una posizione italiana che i <c>.frq</c> non
    /// conoscono, la posizione che nessun settore nomina, il settore scritto due volte. <paramref name="settori"/> e
    /// <paramref name="frq"/>: per ogni file il percorso da mostrare e i record; <paramref name="testoDellaRiga"/>: la
    /// riga del disco (percorso da mostrare, da 1).
    /// </summary>
    public static IEnumerable<ProblemaDelSector> Di(IReadOnlyList<(string Relativo, IReadOnlyList<object> Record)> settori,
                                                    IReadOnlyList<(string Relativo, IReadOnlyList<object> Record)> frq,
                                                    Func<string, int, string> testoDellaRiga)
    {
        ArgumentNullException.ThrowIfNull(settori);
        ArgumentNullException.ThrowIfNull(frq);
        ArgumentNullException.ThrowIfNull(testoDellaRiga);

        var definite = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var conosciute = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var posizione in frq.SelectMany(f => f.Record.OfType<AtcPosition>()))
        {
            definite.Add(posizione.Code.Trim());
            conosciute.Add(posizione.Code.Trim());
            conosciute.UnionWith(posizione.TransferList.Select(t => t.PositionCode.Trim()));
        }

        // Slice 13h (il verso opposto di D4; committente, 6 ottobre 2026): una posizione italiana che controlla uno
        // spazio — torre, avvicinamento, partenze, ACC, FIC — e che nessun settore dinamico nomina. Terra e delivery non
        // hanno un settore. Una volta per posizione: lo stesso nominativo nel .frq di una FIR è una copia. Sul fork 27
        // (17 torri; LICD_APP, LIMC_ANW_APP, LIMC_MAR_APP, LIRF_AET_APP, LIRF_AWL_APP, LIRF_PS1_APP; 4 CTR militari).
        var accese = settori.SelectMany(s => s.Record.OfType<TflSector>()).SelectMany(s => s.Posizioni())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dette = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (relativo, record) in frq)
        {
            foreach (var posizione in record.OfType<AtcPosition>())
            {
                string codice = posizione.Code.Trim();
                if (!Italiana(codice) || !ConUnoSpazio(codice) || accese.Contains(codice) || !dette.Add(codice))
                    continue;

                // I settori che lo stesso scalo ha davvero: spesso è lui, col nome di prima (LIBC_I_TWR ↔ LIBC_TWR).
                string scalo = codice[..4];
                var vicini = accese.Where(a => a.StartsWith(scalo + "_", StringComparison.OrdinalIgnoreCase))
                    .Order(StringComparer.OrdinalIgnoreCase).ToList();
                int riga = posizione.Sources.Count > 0 ? posizione.Sources[0].LineNumber : 0;
                yield return new(Regola.PosizioneSenzaSettore, relativo, riga, testoDellaRiga(relativo, riga),
                    $"«{codice}» non ha un settore dinamico: collegata, in Aurora non accende niente"
                    + (vicini.Count > 0 ? $" — nei .tfl {scalo} ha {string.Join(", ", vicini)}" : string.Empty));
            }
        }

        foreach (var (relativo, record) in settori)
        {
            // Slice 13d (R-4): lo stesso settore due volte nel file — stesse posizioni E stessa forma. Sul fork la stessa
            // testa torna in tre file, e solo LIBB_FSS in libb_es_ctr.tfl è una copia: i cinque LIMM_FSS di limmfic.tfl
            // sono la FIC e i quattro laghi, i tre LIMMLIM di limmctr.tfl i pezzi di un confine.
            var visti = new Dictionary<string, List<(int Riga, string[] Anello)>>(StringComparer.OrdinalIgnoreCase);
            foreach (var settore in record.OfType<TflSector>())
            {
                string testa = settore.Statico ? "STATIC" : string.Join(' ', settore.Posizioni().Order(StringComparer.OrdinalIgnoreCase));
                string[] anello = Anello(settore);
                int suaRiga = settore.Source.LineNumber;
                if (!visti.TryGetValue(testa, out var prima))
                    visti[testa] = prima = [];
                if (anello.Length >= 3 && prima.FirstOrDefault(p => StessoAnello(p.Anello, anello)) is { Riga: > 0 } copia)
                {
                    yield return new(Regola.SettoreRipetuto, relativo, suaRiga, testoDellaRiga(relativo, suaRiga),
                        $"«{settore.SectorCode.Trim()}» c'è già alla riga {copia.Riga} con la stessa forma ({anello.Length} vertici): è una copia");
                }
                else
                {
                    prima.Add((suaRiga, anello));
                }
            }

            foreach (var settore in record.OfType<TflSector>())
            {
                foreach (string posizione in settore.Posizioni().Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!Italiana(posizione) || conosciute.Contains(posizione))
                        continue;

                    // Le posizioni che lo stesso scalo ha davvero: spesso il nome è cambiato (LIBC_TWR → LIBC_I_TWR).
                    string scalo = posizione[..4];
                    var vicine = definite.Where(d => d.StartsWith(scalo + "_", StringComparison.OrdinalIgnoreCase))
                        .Order(StringComparer.OrdinalIgnoreCase).ToList();
                    int riga = settore.Source.LineNumber;
                    yield return new(Regola.SettoreSenzaPosizione, relativo, riga, testoDellaRiga(relativo, riga),
                        $"«{posizione}» non è in nessun .frq, né definita né fra i trasferimenti: il settore non si accende mai"
                        + (vicine.Count > 0 ? $" — {scalo} nei .frq ha {string.Join(", ", vicine)}" : $" — nei .frq {scalo} non ha posizioni"));
                }
            }
        }
    }

    // I vertici come chiavi (al decimo di metro, o il nome), senza le ripetizioni di seguito e senza quello che chiude.
    private static string[] Anello(TflSector settore)
    {
        var chiavi = new List<string>(settore.Vertices.Count);
        foreach (var vertice in settore.Vertices)
        {
            string chiave = vertice.Posizione is { } c
                ? FormattableString.Invariant($"{c.LatitudeDeg:F6};{c.LongitudeDeg:F6}")
                : (vertice.Nome + ";" + vertice.NomeLongitudine).ToUpperInvariant();
            if (chiavi.Count == 0 || chiavi[^1] != chiave)
                chiavi.Add(chiave);
        }

        if (chiavi.Count > 1 && chiavi[0] == chiavi[^1])
            chiavi.RemoveAt(chiavi.Count - 1);
        return [.. chiavi];
    }

    // Lo stesso anello: stessi vertici nello stesso giro, da qualunque vertice e in qualunque verso (come D5).
    private static bool StessoAnello(string[] a, string[] b)
    {
        if (a.Length != b.Length)
            return false;
        int n = a.Length;
        for (int inizio = 0; inizio < n; inizio++)
        {
            if (b[inizio] != a[0])
                continue;
            bool avanti = true, indietro = true;
            for (int k = 0; k < n && (avanti || indietro); k++)
            {
                avanti &= b[(inizio + k) % n] == a[k];
                indietro &= b[((inizio - k) % n + n) % n] == a[k];
            }

            if (avanti || indietro)
                return true;
        }

        return false;
    }

    // I tipi di posizione che controllano uno spazio: gli altri (GND, DEL) non hanno un settore da accendere.
    private static bool ConUnoSpazio(string posizione)
        => posizione[(posizione.LastIndexOf('_') + 1)..].ToUpperInvariant() is "TWR" or "APP" or "DEP" or "CTR" or "FSS";

    // Come nei trasferimenti dei .frq (ControlloDellePosizioni): una posizione dei nostri scali, col suo tipo.
    private static bool Italiana(string posizione)
        => posizione.Length > 5 && posizione.StartsWith("LI", StringComparison.OrdinalIgnoreCase) && posizione[4] == '_';
}
