using System.Text.RegularExpressions;
using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.Validazione;

/// <summary>Un legame fra due voci dello stesso <c>.str</c>: <see cref="Da"/> porta ad <see cref="A"/> sulla <see cref="Pista"/>.</summary>
/// <param name="Da">L'indice della voce da cui si arriva.</param>
/// <param name="A">L'indice della voce a cui si arriva.</param>
/// <param name="Pista">La pista che hanno in comune (il primo verso comune, nell'ordine della voce di partenza).</param>
/// <param name="Punto">Il punto dove si passa dall'una all'altra.</param>
public sealed record LegameFraProcedure(int Da, int A, string Pista, string Punto);

/// <summary>
/// I legami fra le procedure della stessa pista (lotto «Subito» slice 9e, «file per file» Q2c): la STAR porta all'attesa
/// di scalo e all'avvicinamento che passano dal suo ultimo punto, l'attesa all'avvicinamento che passa dal suo punto,
/// l'avvicinamento al mancato avvicinamento che comincia dove lui finisce (o da un suo punto). Solo dentro lo stesso
/// file, solo per nome: ❌ niente legame con <c>HOLDENR.hold</c>, che sono attese in rotta.
/// </summary>
/// <remarks>
/// Misura sul fork (29 settembre): 645 STAR su una pista coi punti per nome; 602 su piste che hanno un avvicinamento;
/// in 533 un avvicinamento della pista COMINCIA dal loro ultimo punto, in 552 ci PASSA. L'avviso
/// <see cref="Regola.StarSenzaAvvicinamento"/> usa «ci passa» (scelta dell'agente: una STAR può finire all'IF, dentro
/// l'avvicinamento): 50 STAR invece di 69. Le GA su una pista, nel fork, sono zero.
/// </remarks>
public static class LegamiDelleProcedure
{
    private static readonly Regex UnVerso = new(@"^\d{2}[LRC]?$", RegexOptions.CultureInvariant);

    /// <summary>I legami fra le voci di un <c>.str</c>, in ordine di voce di partenza e d'arrivo.</summary>
    public static IReadOnlyList<LegameFraProcedure> Di(IReadOnlyList<object> record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var voci = Voci(record);
        var legami = new Dictionary<(int, int), LegameFraProcedure>();

        void Lega(Voce da, Voce a, string punto)
        {
            if (da.Indice != a.Indice && da.Piste.FirstOrDefault(a.Piste.Contains) is { } pista)
                legami.TryAdd((da.Indice, a.Indice), new LegameFraProcedure(da.Indice, a.Indice, pista, punto));
        }

        foreach (var da in voci)
        {
            foreach (var a in voci.Where(v => v.Piste.Overlaps(da.Piste)))
            {
                switch (da.Tipo, a.Tipo)
                {
                    case (StrRecordType.Star, StrRecordType.Holding or StrRecordType.Iap) when a.Punti.Contains(da.Punti[^1]):
                        Lega(da, a, da.Punti[^1]);
                        break;
                    case (StrRecordType.Holding, StrRecordType.Iap) when a.Punti.Contains(da.Punti[0]):
                        Lega(da, a, da.Punti[0]);
                        break;
                    case (StrRecordType.Iap, StrRecordType.GoAround) when da.Punti[^1] == a.Punti[0] || da.Punti.Contains(a.Punti[0]):
                        Lega(da, a, a.Punti[0]);
                        break;
                }
            }
        }

        return [.. legami.Values.OrderBy(l => l.Da).ThenBy(l => l.A)];
    }

    /// <summary>
    /// Le STAR che finiscono dove nessun avvicinamento della loro pista passa, se la pista ne ha (Q2c): l'indice, il
    /// punto, e gli avvicinamenti che ci sono. Una pista senza avvicinamenti nel file non si controlla.
    /// </summary>
    public static IEnumerable<(int Indice, string Punto, IReadOnlyList<string> Avvicinamenti)> StarSenzaAvvicinamento(IReadOnlyList<object> record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var voci = Voci(record);
        foreach (var star in voci.Where(v => v.Tipo == StrRecordType.Star))
        {
            var avvicinamenti = voci.Where(v => v.Tipo == StrRecordType.Iap && v.Piste.Overlaps(star.Piste)).ToList();
            if (avvicinamenti.Count > 0 && !avvicinamenti.Any(v => v.Punti.Contains(star.Punti[^1])))
                yield return (star.Indice, star.Punti[^1], [.. avvicinamenti.Select(v => v.Nome).Distinct()]);
        }
    }

    private sealed record Voce(int Indice, string Nome, StrRecordType Tipo, HashSet<string> Piste, IReadOnlyList<string> Punti);

    /// <summary>Le voci su una pista vera, coi loro punti per nome (le coordinate non legano niente).</summary>
    private static List<Voce> Voci(IReadOnlyList<object> record)
    {
        var voci = new List<Voce>();
        for (int i = 0; i < record.Count; i++)
        {
            if (record[i] is not StrRecord voce)
                continue;
            var piste = voce.RunwaySpec.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(p => UnVerso.IsMatch(p)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            IEnumerable<string> nomi = voce switch
            {
                ProcedureStrRecord p => p.Waypoints.Select(w => w.FixName),
                HoldingStrRecord h => h.Points.OfType<HoldingFixPoint>().Select(f => f.FixName),
                _ => [],
            };
            var punti = nomi.Select(n => n.Trim().ToUpperInvariant()).Where(n => n.Length > 0).ToList();
            if (piste.Count > 0 && punti.Count > 0)
                voci.Add(new Voce(i, voce.ProcedureId.Trim(), voce.RecordType, piste, punti));
        }

        return voci;
    }
}
