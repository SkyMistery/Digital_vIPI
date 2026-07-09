namespace Vipi.Domain.Services;

/// <summary>
/// Calcola il ciclo AIRAC (28 giorni) corrispondente a una data UTC. PIANO §16.
/// Servizio di dominio puro e deterministico: nessuna dipendenza I/O.
/// </summary>
public sealed class AiracService : IAiracService
{
    // Ancora nota: AIRAC 2001 efficace il 2020-01-02 (giovedì). Cicli ogni 28 giorni.
    private static readonly DateOnly Epoch = new(2020, 1, 2);
    private const int CycleDays = 28;

    /// <summary>Ciclo AIRAC in formato "YYNN" (es. "2606") valido alla data indicata.</summary>
    public string GetCycle(DateTime utc)
    {
        var date = DateOnly.FromDateTime(utc);
        var effective = EffectiveDateFor(date);

        // Numero del ciclo dentro l'anno della sua data efficace.
        int year = effective.Year;
        var firstOfYear = FirstCycleOfYear(year);
        int cycleNo = (effective.DayNumber - firstOfYear.DayNumber) / CycleDays + 1;

        return $"{year % 100:00}{cycleNo:00}";
    }

    /// <summary>Data efficace (inizio) del ciclo AIRAC che contiene <paramref name="date"/>.</summary>
    public DateOnly EffectiveDateFor(DateOnly date)
    {
        int periods = (int)Math.Floor((date.DayNumber - Epoch.DayNumber) / (double)CycleDays);
        return Epoch.AddDays(periods * CycleDays);
    }

    /// <summary>Data efficace (inizio, UTC) del ciclo AIRAC indicato in formato "YYNN". Throws se malformato.</summary>
    public DateTime EffectiveUtcForCycle(string cycle)
    {
        if (string.IsNullOrWhiteSpace(cycle) || cycle.Trim().Length != 4 || !int.TryParse(cycle.Trim(), out _))
            throw new ArgumentException($"Ciclo AIRAC non valido: '{cycle}' (atteso YYNN).", nameof(cycle));
        var c = cycle.Trim();
        int year = 2000 + int.Parse(c[..2]);
        int n = int.Parse(c[2..]);
        var d = FirstCycleOfYear(year).AddDays((n - 1) * CycleDays);
        return new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>I prossimi <paramref name="count"/> cicli AIRAC a partire da quello valido a <paramref name="fromUtc"/>
    /// (incluso), con la loro data efficace UTC. Per popolare il selettore del ciclo di rilascio.</summary>
    public IReadOnlyList<AiracCycleInfo> NextCycles(DateTime fromUtc, int count)
    {
        var start = EffectiveDateFor(DateOnly.FromDateTime(fromUtc));
        var list = new List<AiracCycleInfo>(Math.Max(0, count));
        for (int i = 0; i < count; i++)
        {
            var eff = start.AddDays(i * CycleDays);
            var effUtc = new DateTime(eff.Year, eff.Month, eff.Day, 0, 0, 0, DateTimeKind.Utc);
            list.Add(new AiracCycleInfo(GetCycle(effUtc), effUtc));
        }
        return list;
    }

    private static DateOnly FirstCycleOfYear(int year)
    {
        // Primo ciclo la cui data efficace cade nell'anno richiesto.
        var jan1 = new DateOnly(year, 1, 1);
        int periods = (int)Math.Ceiling((jan1.DayNumber - Epoch.DayNumber) / (double)CycleDays);
        var candidate = Epoch.AddDays(periods * CycleDays);
        if (candidate.Year < year) candidate = candidate.AddDays(CycleDays);
        return candidate;
    }
}

/// <summary>Un ciclo AIRAC ("YYNN") con la sua data efficace di inizio (UTC).</summary>
public sealed record AiracCycleInfo(string Cycle, DateTime EffectiveUtc);

/// <summary>Porta per il calcolo del ciclo AIRAC.</summary>
public interface IAiracService
{
    /// <summary>Ciclo AIRAC "YYNN" valido alla data UTC indicata.</summary>
    string GetCycle(DateTime utc);

    /// <summary>Data efficace (inizio, UTC) del ciclo AIRAC "YYNN" indicato.</summary>
    DateTime EffectiveUtcForCycle(string cycle);

    /// <summary>I prossimi <paramref name="count"/> cicli AIRAC da quello valido a <paramref name="fromUtc"/> (incluso).</summary>
    IReadOnlyList<AiracCycleInfo> NextCycles(DateTime fromUtc, int count);
}
