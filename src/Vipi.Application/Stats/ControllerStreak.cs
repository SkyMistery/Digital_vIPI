using System;
using System.Collections.Generic;
using System.Linq;

namespace Vipi.Application.Stats;

/// <summary>
/// La costanza di un controllore: settimane consecutive con almeno un turno.
/// </summary>
/// <param name="CurrentWeeks">Striscia in corso. Zero se si è interrotta.</param>
/// <param name="BestWeeks">La striscia più lunga del periodo guardato.</param>
/// <param name="LastSessionUtc">Ultimo turno; <c>null</c> se non ce n'è nemmeno uno.</param>
public sealed record StatsStreak(int CurrentWeeks, int BestWeeks, DateTimeOffset? LastSessionUtc);

/// <summary>
/// Conta le settimane consecutive. Puro: l'istante «adesso» lo passa il chiamante.
/// </summary>
public static class ControllerStreak
{
    /// <summary>
    /// ⚠️ La striscia resta viva anche se in <b>questa</b> settimana non si è ancora controllato: conta
    /// l'ultima settimana chiusa. Senza questa regola ogni lunedì mattina la striscia di tutti tornerebbe a
    /// zero, che è falso e per giunta scoraggiante.
    /// </summary>
    public static StatsStreak Build(IEnumerable<DateTimeOffset> inizi, DateTimeOffset now)
    {
        var elenco = inizi.ToList();
        if (elenco.Count == 0) return new StatsStreak(0, 0, null);

        var settimane = elenco.Select(Settimana).Distinct().OrderBy(x => x).ToList();

        var migliore = 1;
        var corrente = 1;
        for (var i = 1; i < settimane.Count; i++)
        {
            corrente = settimane[i] - settimane[i - 1] == 1 ? corrente + 1 : 1;
            if (corrente > migliore) migliore = corrente;
        }

        var questa = Settimana(now);
        var ultima = settimane[^1];
        var viva = ultima == questa || ultima == questa - 1;

        return new StatsStreak(viva ? corrente : 0, migliore, elenco.Max());
    }

    /// <summary>
    /// Numero di settimane dal lunedì dell'epoca: due settimane consecutive differiscono di uno, e non c'è
    /// da inseguire il capodanno ISO (la settimana 1 dopo la 52 di un altro anno).
    /// </summary>
    private static int Settimana(DateTimeOffset quando)
    {
        var giorno = quando.UtcDateTime.Date;
        var offsetLunedi = ((int)giorno.DayOfWeek + 6) % 7;      // domenica = 0 in .NET, lunedì = 0 qui
        var lunedi = giorno.AddDays(-offsetLunedi);
        return (int)(lunedi - new DateTime(1970, 1, 5, 0, 0, 0, DateTimeKind.Utc)).TotalDays / 7;
    }
}
