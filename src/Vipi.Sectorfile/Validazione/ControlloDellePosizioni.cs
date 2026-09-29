using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.Validazione;

/// <summary>
/// I controlli delle posizioni ATC dei <c>.frq</c> (lotto «Subito» slice 11a, «file per file» M2): include dopo un escluso,
/// posizione italiana citata e mai definita, la stessa posizione due volte nello stesso file.
/// </summary>
/// <remarks>
/// Misure sul fork del 29 settembre: 102 posizioni con un include dopo un escluso (51 in <c>itfreq.frq</c>, per esempio
/// <c>LIML_TWR</c> con <c>LIRO LIVK LIZZ</c> dopo <c>-LIMC_MAR_APP</c>): il manuale dice che «non funziona», e il committente
/// lo vuole un errore. Citate e mai definite: <c>LIMJ_APP</c>, <c>LIBB_APP</c>, <c>LIMM_WN4_CTR</c>, <c>LIMM_EN4_CTR</c>; le
/// posizioni straniere (<c>LFMM_S_CTR</c>) non stanno nei nostri <c>.frq</c> per forza, e non si guardano. Un ICAO da solo
/// (<c>LIRO</c>) vale per tutte le sue posizioni. <c>LIMF_WN0_APP</c> due volte in <c>itfreq.frq</c> (righe 17 e 31).
/// </remarks>
public static class ControlloDellePosizioni
{
    /// <summary>
    /// I problemi delle posizioni dei <c>.frq</c> dell'albero. <paramref name="file"/>: per ogni <c>.frq</c> il percorso da
    /// mostrare e i record; <paramref name="testoDellaRiga"/>: la riga del disco (percorso da mostrare, da 1).
    /// </summary>
    public static IEnumerable<ProblemaDelSector> Di(IReadOnlyList<(string Relativo, IReadOnlyList<object> Record)> file,
                                                    Func<string, int, string> testoDellaRiga)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(testoDellaRiga);

        var definite = file.SelectMany(f => f.Record.OfType<AtcPosition>()).Select(p => p.Code.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (relativo, record) in file)
        {
            var prime = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var posizione in record.OfType<AtcPosition>())
            {
                int riga = posizione.Sources.Count > 0 ? posizione.Sources[0].LineNumber : 0;
                string testo = testoDellaRiga(relativo, riga);

                if (prime.TryGetValue(posizione.Code.Trim(), out int prima))
                {
                    yield return new(Regola.PosizioneRipetuta, relativo, riga, testo,
                        $"{posizione.Code.Trim()} è già alla riga {prima}: Aurora ne legge una sola");
                }
                else
                {
                    prime[posizione.Code.Trim()] = riga;
                }

                var dopo = posizione.TransferList.SkipWhile(t => !t.IsNegative).Where(t => !t.IsNegative).Select(t => t.PositionCode).ToList();
                if (dopo.Count > 0)
                {
                    yield return new(Regola.IncludeDopoEscluso, relativo, riga, testo,
                        $"{string.Join(" ", dopo)} dopo un escluso: Aurora non li legge — prima gli inclusi, poi gli esclusi (-POS)",
                        Riordinata(testo));
                }

                foreach (string citata in posizione.TransferList.Select(t => t.PositionCode.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (citata.StartsWith("LI", StringComparison.OrdinalIgnoreCase) && citata.Contains('_', StringComparison.Ordinal)
                        && !definite.Contains(citata))
                    {
                        yield return new(Regola.PosizioneNonDefinita, relativo, riga, testo,
                            $"«{citata}» non è definita in nessun .frq: il trasferimento non va da nessuna parte");
                    }
                }
            }
        }
    }

    // La riga coi trasferimenti in ordine: prima gli inclusi, poi gli esclusi, nell'ordine in cui erano; il resto com'è.
    private static string? Riordinata(string riga)
    {
        string[] campi = riga.Split(';');
        if (campi.Length < 3)
            return null;
        var voci = campi[2].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        campi[2] = string.Join(" ", voci.Where(v => !v.StartsWith('-')).Concat(voci.Where(v => v.StartsWith('-'))));
        return string.Join(";", campi);
    }
}
