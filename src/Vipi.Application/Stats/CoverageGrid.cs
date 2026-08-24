using System;
using System.Collections.Generic;
using System.Linq;

namespace Vipi.Application.Stats;

/// <summary>Un tratto di tempo in cui qualcuno era in frequenza.</summary>
public readonly record struct OnlineSpan(DateTimeOffset StartUtc, DateTimeOffset EndUtc);

/// <summary>
/// Una casella della griglia: quanti minuti di quella fascia oraria, in quel giorno della settimana, sono
/// stati coperti — e quanti ce n'erano da coprire.
/// </summary>
/// <param name="DayOfWeek">1 = lunedì … 7 = domenica (l'ordine con cui si legge una settimana).</param>
/// <param name="Hour">Ora UTC, 0-23.</param>
public sealed record CoverageCell(int DayOfWeek, int Hour, int CoveredMinutes, int PossibleMinutes)
{
    /// <summary>Frazione coperta, 0-1. Zero possibili (finestra che non tocca quella casella) → 0.</summary>
    public double Ratio => PossibleMinutes <= 0 ? 0 : (double)CoveredMinutes / PossibleMinutes;
}

/// <summary>
/// La griglia ora × giorno: <b>quando c'è copertura e quando resta il buco</b>.
///
/// <para><b>Due cose che sembrano dettagli e non lo sono.</b></para>
/// <list type="number">
///   <item><b>Gli intervalli si uniscono prima di contare.</b> Tre controllori online insieme non fanno tre
///     ore di copertura: fanno un'ora coperta. Sommando le durate, una divisione affollata sembrerebbe
///     coprire il 300% di una fascia — e la domanda «dove manca qualcuno?» avrebbe risposte assurde.</item>
///   <item><b>Una sessione occupa più caselle.</b> Un turno dalle 20:40 alle 23:10 non è «alle 20»: sono 20
///     minuti alle 20, 60 alle 21, 60 alle 22, 10 alle 23. Contarlo sull'ora d'inizio sposterebbe la
///     copertura verso l'orario in cui la gente si collega, che è proprio il contrario di quel che si cerca.</item>
/// </list>
///
/// <para>I «minuti possibili» dicono quante volte quella casella <i>esiste</i> nella finestra (un anno ha
/// ~52 lunedì alle 21): senza, una finestra corta farebbe sembrare scoperte le fasce che semplicemente
/// capitano poche volte.</para>
///
/// <para>Puro e deterministico, nessun I/O. Tutto in UTC, come il resto dell'applicazione.</para>
/// </summary>
public static class CoverageGrid
{
    /// <summary>Le 168 caselle della settimana, sempre tutte: una casella assente non si distinguerebbe da una vuota.</summary>
    public static IReadOnlyList<CoverageCell> Build(
        IReadOnlyList<OnlineSpan> spans, DateTimeOffset from, DateTimeOffset to)
    {
        var coperti = new int[7, 24];
        var possibili = new int[7, 24];

        foreach (var (giorno, ora, minuti) in Fette(from, to))
            possibili[giorno, ora] += minuti;

        foreach (var s in Unione(spans, from, to))
            foreach (var (giorno, ora, minuti) in Fette(s.StartUtc, s.EndUtc))
                coperti[giorno, ora] += minuti;

        var celle = new List<CoverageCell>(168);
        for (var g = 0; g < 7; g++)
            for (var h = 0; h < 24; h++)
                // I minuti coperti non possono superare i possibili: se la finestra taglia a metà un'ora, il
                // tetto è quel che resta. Senza il clamp una sessione a cavallo del bordo darebbe il 120%.
                celle.Add(new CoverageCell(g + 1, h, Math.Min(coperti[g, h], possibili[g, h]), possibili[g, h]));

        return celle;
    }

    /// <summary>
    /// Fonde gli intervalli che si sovrappongono o si toccano, dopo averli ritagliati alla finestra.
    /// È il passo che trasforma «quante ore ha fatto la gente» in «quanto tempo c'era qualcuno».
    /// </summary>
    public static IReadOnlyList<OnlineSpan> Unione(
        IReadOnlyList<OnlineSpan> spans, DateTimeOffset from, DateTimeOffset to)
    {
        var puliti = spans
            .Select(s => new OnlineSpan(
                s.StartUtc < from ? from : s.StartUtc,
                s.EndUtc > to ? to : s.EndUtc))
            .Where(s => s.EndUtc > s.StartUtc)
            .OrderBy(s => s.StartUtc)
            .ToList();

        var uniti = new List<OnlineSpan>();
        foreach (var s in puliti)
        {
            if (uniti.Count > 0 && s.StartUtc <= uniti[^1].EndUtc)
            {
                if (s.EndUtc > uniti[^1].EndUtc) uniti[^1] = uniti[^1] with { EndUtc = s.EndUtc };
                continue;
            }
            uniti.Add(s);
        }
        return uniti;
    }

    /// <summary>Spezza un intervallo nelle sue fette orarie: (giorno 0-6, ora 0-23, minuti in quella fetta).</summary>
    private static IEnumerable<(int Giorno, int Ora, int Minuti)> Fette(DateTimeOffset da, DateTimeOffset a)
    {
        var cursore = da;
        while (cursore < a)
        {
            var fineOra = new DateTimeOffset(cursore.UtcDateTime.Date, TimeSpan.Zero)
                .AddHours(cursore.UtcDateTime.Hour + 1);
            var fine = fineOra < a ? fineOra : a;

            var minuti = (int)Math.Round((fine - cursore).TotalMinutes);
            if (minuti > 0)
            {
                // .NET conta la domenica come 0: qui la settimana comincia di lunedì, come si legge.
                var giorno = ((int)cursore.UtcDateTime.DayOfWeek + 6) % 7;
                yield return (giorno, cursore.UtcDateTime.Hour, minuti);
            }
            cursore = fine;
        }
    }
}
