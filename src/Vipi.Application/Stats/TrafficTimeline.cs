using System;
using System.Collections.Generic;
using System.Linq;

namespace Vipi.Application.Stats;

/// <summary>Un volo sulla striscia del turno: dove comincia, dove finisce e su che corsia sta.</summary>
/// <param name="Index">Posizione nella lista d'ingresso: serve a ritrovare la riga da cui viene.</param>
/// <param name="Lane">Corsia (0, 1, 2…): due voli sulla stessa corsia non si sovrappongono mai.</param>
public sealed record TimelineBar(int Index, DateTimeOffset From, DateTimeOffset To, int Lane);

/// <summary>
/// La striscia di un turno: le barre già assegnate alle corsie, e la punta di traffico simultaneo.
/// </summary>
/// <param name="PeakConcurrent">Quanti voli c'erano insieme nel momento più pieno.</param>
/// <param name="PeakAtUtc">Quando: l'istante in cui il conteggio raggiunge quella punta.</param>
public sealed record TrafficTimelineResult(
    IReadOnlyList<TimelineBar> Bars,
    int Lanes,
    int PeakConcurrent,
    DateTimeOffset? PeakAtUtc);

/// <summary>
/// Dispone i voli di una sessione su una striscia temporale. Puro: nessun I/O, nessun orologio interno.
///
/// <para>⚠️ <b>La barra è la finestra, non la presenza.</b> Un volo che esce dal settore e rientra ha una
/// barra continua ma minuti contati per giro (<c>SeenMinutes</c>): la punta di traffico che ne esce è
/// <b>stimata dalla finestra</b>, e va scritto dove si mostra. Contare la presenza vera vorrebbe dire
/// conservare un campione al minuto per volo — mezzo milione di righe l'anno che diventerebbero trenta.</para>
/// </summary>
public static class TrafficTimeline
{
    /// <param name="voli">Finestre dei voli, in qualsiasi ordine.</param>
    public static TrafficTimelineResult Build(IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> voli)
    {
        if (voli.Count == 0) return new TrafficTimelineResult(Array.Empty<TimelineBar>(), 0, 0, null);

        // Corsie: si assegna in ordine di inizio la prima corsia libera. È l'algoritmo dei binari di stazione,
        // e sul dato vero (una TWR fa ~40 voli in tre ore) tiene le corsie a una manciata.
        var ordinati = voli
            .Select((v, i) => (Index: i, v.From, To: v.To < v.From ? v.From : v.To))
            .OrderBy(v => v.From).ThenBy(v => v.Index)
            .ToList();

        var fineCorsia = new List<DateTimeOffset>();
        var barre = new List<TimelineBar>(ordinati.Count);

        foreach (var v in ordinati)
        {
            var corsia = fineCorsia.FindIndex(fine => fine <= v.From);
            if (corsia < 0)
            {
                corsia = fineCorsia.Count;
                fineCorsia.Add(v.To);
            }
            else
            {
                fineCorsia[corsia] = v.To;
            }

            barre.Add(new TimelineBar(v.Index, v.From, v.To, corsia));
        }

        var (punta, quando) = Punta(ordinati.Select(v => (v.From, v.To)).ToList());

        return new TrafficTimelineResult(
            barre.OrderBy(b => b.Lane).ThenBy(b => b.From).ToList(), fineCorsia.Count, punta, quando);
    }

    /// <summary>
    /// La punta di sovrapposizione, con una spazzata degli estremi. ⚠️ Le chiusure si contano <b>prima</b>
    /// delle aperture a parità d'istante: un volo che finisce nel minuto in cui un altro comincia non fa due.
    /// </summary>
    private static (int Punta, DateTimeOffset? Quando) Punta(IReadOnlyList<(DateTimeOffset From, DateTimeOffset To)> voli)
    {
        var eventi = new List<(DateTimeOffset When, int Delta)>(voli.Count * 2);
        foreach (var v in voli)
        {
            eventi.Add((v.From, +1));

            // ⚠️ Un volo visto UNA volta sola ha finestra di lunghezza zero: con la chiusura contata prima
            // dell'apertura sparirebbe dal conteggio, e una sessione di un giro direbbe «punta 0 aerei».
            eventi.Add((v.To > v.From ? v.To : v.From.AddTicks(1), -1));
        }

        var attuale = 0;
        var punta = 0;
        DateTimeOffset? quando = null;

        foreach (var e in eventi.OrderBy(e => e.When).ThenBy(e => e.Delta))
        {
            attuale += e.Delta;
            if (attuale <= punta) continue;
            punta = attuale;
            quando = e.When;
        }

        return (punta, quando);
    }
}
