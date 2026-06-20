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

/// <summary>Porta per il calcolo del ciclo AIRAC.</summary>
public interface IAiracService
{
    /// <summary>Ciclo AIRAC "YYNN" valido alla data UTC indicata.</summary>
    string GetCycle(DateTime utc);
}
