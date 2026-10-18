using System.Globalization;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// I cicli AIRAC: uno ogni 28 giorni, contati dall'AIRAC 2001 (in vigore il 2 gennaio 2020). Il nome è l'anno a due
/// cifre e il numero del ciclo nell'anno (<c>2609</c> = il nono del 2026, dal 3 settembre).
/// </summary>
public static class CicloAirac
{
    private static readonly DateOnly Riferimento = new(2020, 1, 2);

    /// <summary>Il ciclo in vigore il giorno <paramref name="giorno"/>, con la sua data di entrata in vigore.</summary>
    public static (string Nome, DateOnly InVigore) Di(DateOnly giorno)
    {
        var giorni = giorno.DayNumber - Riferimento.DayNumber;
        var n = (int)Math.Floor(giorni / 28.0);
        var inVigore = Riferimento.AddDays(28 * n);
        var primo = inVigore;
        while (primo.AddDays(-28).Year == inVigore.Year)
            primo = primo.AddDays(-28);
        var numero = (inVigore.DayNumber - primo.DayNumber) / 28 + 1;
        return (string.Create(CultureInfo.InvariantCulture, $"{inVigore.Year % 100:00}{numero:00}"), inVigore);
    }

    /// <summary>Vero se <paramref name="nome"/> ha la forma di un ciclo (<c>AACC</c>, il numero da 01 a 14).</summary>
    public static bool Valido(string? nome) =>
        nome is { Length: 4 } && nome.All(char.IsAsciiDigit) && int.Parse(nome.AsSpan(2), CultureInfo.InvariantCulture) is >= 1 and <= 14;
}
