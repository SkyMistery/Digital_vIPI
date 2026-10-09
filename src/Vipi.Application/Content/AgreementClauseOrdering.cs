using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// L'ordine in cui una sezione <b>mostra</b> le sue clausole, quando non è quello scritto a mano
/// (<see cref="AgreementClauseOrder"/>).
///
/// <para><b>Perché esiste</b> (committente, 7 ottobre 2026). Le clausole ospiti — condivise da un altro accordo —
/// non hanno un posto loro nella tabella che le ospita: stanno in coda. Con l'ordine a mano non c'è modo di
/// metterle fra due clausole della sezione; con un ordine <b>dichiarato</b> — alfabetico per punto, per quota — il
/// posto di ognuna lo decide la regola, uguale per le clausole di casa e per le ospiti. Vale qui, nei documenti e
/// nella vista live: tutti leggono le righe nell'ordine in cui la sezione le dà.</para>
///
/// <para>⚠️ <b>Un gruppo di varianti si muove intero</b>, e dentro resta com'era: l'ordine di varianti ed
/// eccezioni <i>è</i> la struttura («appartiene all'ultima di profondità inferiore che la precede»). Si ordinano i
/// blocchi, per la loro prima riga — che in un gruppo porta gli stessi punti di tutte le altre.</para>
///
/// <para>⚠️ L'ordine <b>salvato</b> non si tocca: tornando a «a mano» le clausole di casa riprendono il posto che
/// avevano. Per questo la riga porta anche <see cref="AgreementClauseRow.StoredOrder"/>.</para>
///
/// <para>Ogni ordine dichiarato si legge anche <b>al contrario</b> (committente, 9 ottobre 2026): dalla Z alla A,
/// dalla quota più alta.</para>
///
/// <para>Puro e deterministico.</para>
/// </summary>
public static class AgreementClauseOrdering
{
    public static IReadOnlyList<AgreementClauseRow> Sort(IReadOnlyList<AgreementClauseRow> rows, AgreementClauseOrder order)
    {
        if (order == AgreementClauseOrder.Manual || rows.Count < 2) return rows;

        var blocchi = new List<List<AgreementClauseRow>>();
        var perGruppo = new Dictionary<int, List<AgreementClauseRow>>();
        foreach (var r in rows)
        {
            if (r.VariantGroup is not int g) { blocchi.Add(new List<AgreementClauseRow> { r }); continue; }
            if (!perGruppo.TryGetValue(g, out var blocco)) { perGruppo[g] = blocco = new List<AgreementClauseRow>(); blocchi.Add(blocco); }
            blocco.Add(r);
        }

        // OrderBy è stabile, in tutti e due i versi: a parità di chiave resta l'ordine di prima, cioè quello
        // scritto a mano.
        // ⚠️ Chi NON ha la chiave — niente punti, niente quota — sta in fondo in tutti e due i versi: il verso
        // opposto capovolge l'ordine di chi un valore ce l'ha, non porta in testa le righe ancora da scrivere.
        var alContrario = IsDescending(order);
        IOrderedEnumerable<List<AgreementClauseRow>> ordinati;
        if (KeyOf(order) == AgreementClauseOrder.Level)
        {
            var conQuota = blocchi.OrderBy(b => Piedi(b) is null);
            ordinati = (alContrario ? conQuota.ThenByDescending(b => Piedi(b) ?? 0) : conQuota.ThenBy(b => Piedi(b) ?? 0))
                // A pari quota decide il punto, e sempre dalla A: è lo spareggio, non la chiave.
                .ThenBy(b => b[0].Cops, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            var conPunti = blocchi.OrderBy(b => string.IsNullOrWhiteSpace(b[0].Cops));
            ordinati = alContrario
                ? conPunti.ThenByDescending(b => b[0].Cops, StringComparer.OrdinalIgnoreCase)
                : conPunti.ThenBy(b => b[0].Cops, StringComparer.OrdinalIgnoreCase);
        }

        // ⚠️ Il posto si riscrive: chi legge (AgreementExpansion, la pagina) riordina per Order.
        var posto = 0;
        return ordinati.SelectMany(b => b).ToList().Select(r => r with { Order = ++posto }).ToList();
    }

    private static int? Piedi(List<AgreementClauseRow> blocco) => FallbackChain.FeetOf(blocco[0].LevelValue, blocco[0].LevelUnit);

    /// <summary>La <b>chiave</b> di un ordine, senza il verso: a mano, per punto o per quota.</summary>
    public static AgreementClauseOrder KeyOf(AgreementClauseOrder order) => order switch
    {
        AgreementClauseOrder.PointsDescending => AgreementClauseOrder.Points,
        AgreementClauseOrder.LevelDescending => AgreementClauseOrder.Level,
        _ => order,
    };

    /// <summary>L'ordine va al contrario: dalla Z alla A, dalla quota più alta.</summary>
    public static bool IsDescending(AgreementClauseOrder order) =>
        order is AgreementClauseOrder.PointsDescending or AgreementClauseOrder.LevelDescending;

    /// <summary>Lo stesso ordine nel verso opposto. «A mano» non ne ha uno: resta com'è.</summary>
    public static AgreementClauseOrder Reverse(AgreementClauseOrder order) => order switch
    {
        AgreementClauseOrder.Points => AgreementClauseOrder.PointsDescending,
        AgreementClauseOrder.PointsDescending => AgreementClauseOrder.Points,
        AgreementClauseOrder.Level => AgreementClauseOrder.LevelDescending,
        AgreementClauseOrder.LevelDescending => AgreementClauseOrder.Level,
        _ => order,
    };
}
