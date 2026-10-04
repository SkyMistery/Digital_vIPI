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
    /// I settori con una posizione italiana che i <c>.frq</c> non conoscono. <paramref name="settori"/>: per ogni
    /// <c>.tfl</c> il percorso da mostrare e i record; <paramref name="posizioni"/>: i record di tutti i <c>.frq</c>;
    /// <paramref name="testoDellaRiga"/>: la riga del disco (percorso da mostrare, da 1).
    /// </summary>
    public static IEnumerable<ProblemaDelSector> Di(IReadOnlyList<(string Relativo, IReadOnlyList<object> Record)> settori,
                                                    IEnumerable<AtcPosition> posizioni, Func<string, int, string> testoDellaRiga)
    {
        ArgumentNullException.ThrowIfNull(settori);
        ArgumentNullException.ThrowIfNull(posizioni);
        ArgumentNullException.ThrowIfNull(testoDellaRiga);

        var definite = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var conosciute = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var posizione in posizioni)
        {
            definite.Add(posizione.Code.Trim());
            conosciute.Add(posizione.Code.Trim());
            conosciute.UnionWith(posizione.TransferList.Select(t => t.PositionCode.Trim()));
        }

        foreach (var (relativo, record) in settori)
        {
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

    // Come nei trasferimenti dei .frq (ControlloDellePosizioni): una posizione dei nostri scali, col suo tipo.
    private static bool Italiana(string posizione)
        => posizione.Length > 5 && posizione.StartsWith("LI", StringComparison.OrdinalIgnoreCase) && posizione[4] == '_';
}
