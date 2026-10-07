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

        // OrderBy è stabile: a parità di chiave resta l'ordine di prima, cioè quello scritto a mano.
        var ordinati = order == AgreementClauseOrder.Level
            ? blocchi.OrderBy(b => FallbackChain.FeetOf(b[0].LevelValue, b[0].LevelUnit) ?? int.MaxValue)
                .ThenBy(b => b[0].Cops, StringComparer.OrdinalIgnoreCase)
            : blocchi.OrderBy(b => string.IsNullOrWhiteSpace(b[0].Cops))
                .ThenBy(b => b[0].Cops, StringComparer.OrdinalIgnoreCase);

        // ⚠️ Il posto si riscrive: chi legge (AgreementExpansion, la pagina) riordina per Order.
        var posto = 0;
        return ordinati.SelectMany(b => b).ToList().Select(r => r with { Order = ++posto }).ToList();
    }
}
