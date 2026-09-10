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

    /// <summary>
    /// La stessa banda, ma per limiti che sono <b>certamente in piedi</b>: nessuna euristica, si divide e
    /// basta.
    ///
    /// <para>🔴 <b>Perché serve una porta a parte.</b> L'euristica di <see cref="Normalize"/> esiste perché i
    /// limiti di <i>settore</i> non dichiarano l'unità. Le <b>aree regolamentate</b> (<c>MinimumAlt</c>/
    /// <c>MaximumAlt</c> dall'API IVAO) e i <b>volumi del KMZ</b> (<c>BaseFeet</c>/<c>TopFeet</c>) la
    /// dichiarano: sono piedi. Passarli dall'euristica rompe tutte le aree <b>basse</b> — e sono proprio
    /// quelle a bassa quota (BOAT) il caso d'uso. Misurato su <c>AT Basilicata</c>, 150 ft – 1000 ft: il
    /// piede restava 150 e veniva letto «FL150», il tetto diventava FL10, e siccome il tetto finiva
    /// <b>sotto</b> il piede la banda degenerava a FL150–FL151. Cioè un prisma disegnato a 15 000 piedi,
    /// alto cento, per un'area che sta fra 150 e 1000 piedi.</para>
    /// </summary>
    public static (int Bottom, int Top) FromFeet(int? lowerFeet, int? upperFeet)
    {
        var bottom = lowerFeet is { } lo ? FeetToFl(lo) : Ground;
        var top = upperFeet is { } up ? FeetToFl(up) : Unlimited;
        if (top <= bottom) top = bottom + 1;
        return (bottom, top);
    }

    /// <summary>
    /// Il testo di una quota <b>in piedi</b>: <c>GND</c> per lo zero, <c>—</c> per il nulla, altrimenti i
    /// piedi con l'unità scritta.
    ///
    /// <para>⚠️ Sta qui e non in due componenti perché la mappa e la tabella che le sta sotto devono dire la
    /// <b>stessa</b> cosa: prima la tabella diceva «150 ft» e la mappa «FL150», della stessa area.</para>
    /// </summary>
    public static string FeetLabel(int? feet) =>
        feet is null ? "—" : feet.Value <= 0 ? "GND" : $"{feet.Value} ft";

    /// <summary>La banda in piedi come si legge: un valore solo se piede e tetto coincidono.</summary>
    public static string FeetBand(int? lowerFeet, int? upperFeet) =>
        lowerFeet == upperFeet ? FeetLabel(upperFeet) : $"{FeetLabel(lowerFeet)} – {FeetLabel(upperFeet)}";

    /// <summary>Piedi → FL, senza euristica: è la conversione, non un'ipotesi.</summary>
    private static int FeetToFl(int feet) =>
        feet <= 0 ? Ground : (int)System.Math.Round(feet / 100.0);

    private static int ToFl(int v)
    {
        if (v < 0) v = 0;
        return v > Unlimited ? (int)System.Math.Round(v / 100.0) : v;
    }
}
