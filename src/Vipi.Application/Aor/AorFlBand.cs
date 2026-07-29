namespace Vipi.Application.Aor;

/// <summary>
/// Normalizza i limiti di quota di un settore (<c>LowerLimit</c>/<c>UpperLimit</c>, <see cref="int"/>? in piedi o FL,
/// unità NON tracciata a schema) in una banda di Flight Level per l'estrusione 3D dell'AoR. Regole:
/// <list type="bullet">
///   <item>Lower <c>null</c> = suolo → <see cref="Ground"/> (GND, FL 0); Upper <c>null</c> = illimitato →
///     <see cref="Unlimited"/> (UNL, tetto convenzionale FL 660).</item>
///   <item>valore &gt; 660 = interpretato come PIEDI → ÷100 (es. 19500 ft → FL 195); ≤ 660 = già FL.</item>
/// </list>
/// Euristica piedi/FL: senza campo unità l'overlap 0..660 è ambiguo, ma sopra 660 è certamente piedi (nessun FL
/// operativo supera l'UNL). Garantisce sempre <c>Top &gt; Bottom</c> (banda degenere → Top = Bottom + 1).
/// PURA/deterministica, nessun I/O.
/// </summary>
public static class AorFlBand
{
    /// <summary>FL del suolo (limite inferiore quando <c>LowerLimit</c> è null).</summary>
    public const int Ground = 0;

    /// <summary>FL tetto convenzionale «UNL» (limite superiore quando <c>UpperLimit</c> è null).</summary>
    public const int Unlimited = 660;

    /// <summary>Banda FL (Bottom, Top) per l'estrusione, dai limiti grezzi del settore.</summary>
    public static (int Bottom, int Top) Normalize(int? lower, int? upper)
    {
        var bottom = lower is { } lo ? ToFl(lo) : Ground;
        var top = upper is { } up ? ToFl(up) : Unlimited;
        if (top <= bottom) top = bottom + 1;
        return (bottom, top);
    }

    private static int ToFl(int v)
    {
        if (v < 0) v = 0;
        return v > Unlimited ? (int)System.Math.Round(v / 100.0) : v;
    }
}
