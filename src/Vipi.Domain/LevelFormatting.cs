namespace Vipi.Domain;

/// <summary>Formattazione condivisa del livello di un punto di trasferimento (editor + resa documento).</summary>
public static class LevelFormatting
{
    /// <summary>Rende il livello come testo: «FL130↓», «FL280↑», «2500 ft», «per aerovia» o «—».</summary>
    public static string Format(int? value, LevelUnit unit, LevelConstraint constraint, string? special)
    {
        if (constraint == LevelConstraint.Special)
            return string.IsNullOrWhiteSpace(special) ? "—" : special.Trim();
        if (value is not int v) return "—";

        var body = unit == LevelUnit.Fl ? $"FL{v}" : $"{v} ft";
        return constraint switch
        {
            LevelConstraint.AtOrAbove => body + "↑",
            LevelConstraint.AtOrBelow => body + "↓",
            _ => body,
        };
    }
}
