namespace Vipi.Domain;

/// <summary>Formattazione condivisa del livello di un punto di trasferimento (editor + resa documento).</summary>
public static class LevelFormatting
{
    /// <summary>Rende il livello come testo: «FL130-», «FL280+ ↑ (dispari)», «2500 ft (pari)», «per aerovia» o «—».
    /// Il vincolo di livello è reso col segno «+» (≥) / «-» (≤); lo stato verticale con la freccia «↑» (salita) /
    /// «↓» (discesa); la parità (regola semicircolare) è appesa fra parentesi quando non è <see cref="LevelParity.Any"/>.</summary>
    public static string Format(int? value, LevelUnit unit, LevelConstraint constraint, string? special,
        LevelParity parity = LevelParity.Any, TransferVerticalState verticalState = TransferVerticalState.Unspecified) =>
        AppendParity(AppendState(Body(value, unit, constraint, special), verticalState), parity);

    private static string Body(int? value, LevelUnit unit, LevelConstraint constraint, string? special)
    {
        if (constraint == LevelConstraint.Special)
            return string.IsNullOrWhiteSpace(special) ? "—" : special.Trim();
        if (value is not int v) return "—";

        var body = unit == LevelUnit.Fl ? $"FL{v}" : $"{v} ft";
        // Segno del vincolo di livello (NON è lo stato verticale): «+» = a/o sopra, «-» = a/o sotto.
        return constraint switch
        {
            LevelConstraint.AtOrAbove => body + "+",
            LevelConstraint.AtOrBelow => body + "-",
            _ => body,
        };
    }

    // Freccia dello stato verticale (dimensione indipendente dal vincolo): «↑» salita, «↓» discesa.
    // Stabile/non specificato non aggiungono simbolo.
    private static string AppendState(string body, TransferVerticalState state) => state switch
    {
        TransferVerticalState.Climbing => $"{body} ↑",
        TransferVerticalState.Descending => $"{body} ↓",
        _ => body,
    };

    /// <summary>Etichetta italiana della parità: «pari» / «dispari» / "" per Any.</summary>
    public static string ParityLabel(LevelParity parity) => parity switch
    {
        LevelParity.Even => "pari",
        LevelParity.Odd => "dispari",
        _ => "",
    };

    private static string AppendParity(string body, LevelParity parity) =>
        parity == LevelParity.Any ? body : $"{body} ({ParityLabel(parity)})";
}
