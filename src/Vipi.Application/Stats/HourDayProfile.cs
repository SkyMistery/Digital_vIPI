using System;
using System.Collections.Generic;
using System.Linq;

namespace Vipi.Application.Stats;

/// <summary>
/// Le abitudini di una persona lette dalle 168 caselle: quanto tempo in ogni ora del giorno, quanto in ogni
/// giorno della settimana, e la fascia in cui ne sta la metà.
/// </summary>
/// <param name="PerHour">24 valori in minuti, indice = ora UTC.</param>
/// <param name="PerDay">7 valori in minuti, indice 0 = lunedì.</param>
/// <param name="PeakFromHour">Prima ora della fascia tipica; <c>-1</c> se non c'è niente da riassumere.</param>
/// <param name="PeakToHour">Ora in cui la fascia <b>finisce</b> (esclusa): 19→23 vuol dire «dalle 19 alle 23».</param>
/// <param name="BusiestDay">1 = lunedì … 7 = domenica; <c>0</c> se non c'è niente.</param>
public sealed record HourDayProfile(
    IReadOnlyList<int> PerHour,
    IReadOnlyList<int> PerDay,
    int PeakFromHour,
    int PeakToHour,
    int BusiestDay,
    int TotalMinutes)
{
    /// <summary>Vero quando c'è abbastanza da scrivere la frase dell'orario tipico.</summary>
    public bool HasPeak => PeakFromHour >= 0 && BusiestDay > 0;

    /// <summary>
    /// Quanto della settimana occupa la fascia tipica: serve a non scrivere «di solito fra le 00 e le 23»
    /// come se fosse un'abitudine.
    /// </summary>
    public int PeakHours => PeakFromHour < 0 ? 0 : (PeakToHour - PeakFromHour + 24) % 24 is var h && h == 0 ? 24 : h;
}

/// <summary>
/// Da griglia a profilo: le due domande separate («a che ora?» e «che giorno?») più la fascia tipica.
///
/// <para><b>Perché due elenchi e non la griglia.</b> La griglia 7×24 risponde alla domanda incrociata — che
/// è quella giusta per <i>pianificare la copertura di una divisione</i> — ma a una persona che guarda le
/// proprie ore serve sapere «di solito la sera» e «di solito nel fine settimana», e in 168 caselle da
/// undici pixel quelle due risposte non si leggono. Il committente l'ha chiesto il 25 agosto 2026; la
/// griglia <b>resta</b> sulla pagina della divisione, dove l'incrocio serve davvero.</para>
///
/// <para>⚠️ Si contano i <b>minuti</b>, non le caselle accese: un'ora piena e cinque minuti non sono la
/// stessa cosa, e contare le caselle farebbe sembrare abitudine un collegamento lampo.</para>
///
/// <para>Puro e deterministico. Nessun I/O, nessun orologio.</para>
/// </summary>
public static class HourDayProfileBuilder
{
    /// <summary>Quanta parte del tempo deve stare nella fascia perché valga la pena chiamarla «tipica».</summary>
    private const double QuotaFascia = 0.5;

    public static HourDayProfile Build(IReadOnlyList<CoverageCell> celle)
    {
        var perOra = new int[24];
        var perGiorno = new int[7];

        foreach (var c in celle)
        {
            // Una casella fuori scala non deve poter falsare tutto: la griglia dà 1..7 e 0..23, ma questo
            // metodo è pubblico e un giorno lo chiamerà qualcun altro.
            if (c.Hour is < 0 or > 23 || c.DayOfWeek is < 1 or > 7) continue;

            perOra[c.Hour] += c.CoveredMinutes;
            perGiorno[c.DayOfWeek - 1] += c.CoveredMinutes;
        }

        var totale = perOra.Sum();
        if (totale <= 0)
            return new HourDayProfile(perOra, perGiorno, -1, -1, 0, 0);

        var (da, a) = FasciaTipica(perOra, totale);

        // Il giorno più frequentato: a parità vince il primo della settimana, così la frase è stabile fra
        // due caricamenti della stessa pagina.
        var giorno = 0;
        var meglio = -1;
        for (var g = 0; g < 7; g++)
            if (perGiorno[g] > meglio) { meglio = perGiorno[g]; giorno = g + 1; }

        return new HourDayProfile(perOra, perGiorno, da, a, giorno, totale);
    }

    /// <summary>
    /// La più CORTA fascia oraria continua che tenga almeno metà del tempo.
    ///
    /// <para>⚠️ Circolare, cioè può scavalcare la mezzanotte: chi controlla dalle 21 alle 01 ha
    /// un'abitudine sola, non due tronconi ai due bordi del giorno. Una finestra non circolare gliela
    /// spezzerebbe in due e la frase direbbe «di solito fra le 00 e le 23».</para>
    /// </summary>
    private static (int Da, int A) FasciaTipica(int[] perOra, int totale)
    {
        var soglia = totale * QuotaFascia;
        var migliorLunghezza = 25;
        var migliorSomma = -1;
        var migliorInizio = -1;

        for (var inizio = 0; inizio < 24; inizio++)
        {
            var somma = 0;
            for (var lung = 1; lung <= 24; lung++)
            {
                somma += perOra[(inizio + lung - 1) % 24];
                if (somma < soglia) continue;

                // A parità di lunghezza vince la fascia più piena: due finestre da quattro ore che tengono
                // entrambe la metà non sono la stessa risposta.
                if (lung < migliorLunghezza || (lung == migliorLunghezza && somma > migliorSomma))
                {
                    migliorLunghezza = lung;
                    migliorSomma = somma;
                    migliorInizio = inizio;
                }
                break;
            }
        }

        return migliorInizio < 0 ? (-1, -1) : (migliorInizio, (migliorInizio + migliorLunghezza) % 24);
    }
}
