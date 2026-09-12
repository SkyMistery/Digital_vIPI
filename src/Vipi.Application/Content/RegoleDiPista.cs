using Vipi.Application.Weather;

namespace Vipi.Application.Content;

/// <summary>
/// Dalla riga editoriale della regola pista al modello che il motore valuta (<see cref="RunwaySuggestion"/>).
///
/// <para>⚠️ <b>Stava in <c>Vipi.Ui.Shared.AirportViewFormat.MapRule</c></b>, e le due parti che collega —
/// <see cref="RunwayRuleRow"/> e <see cref="RunwayRuleEval"/> — sono <b>tutt'e due di Application</b>: non
/// c'era niente di UI in quella conversione, e tenerla lassù voleva dire che chi sta in Application e ha in
/// mano delle regole non poteva valutarle senza riscriversela. È esattamente ciò che è successo quando è
/// arrivato il vAWOS (carta 2026-09-12), che le regole le valuta e una pagina non è.</para>
/// </summary>
public static class RegoleDiPista
{
    /// <summary>La riga in forma valutabile. Copia tutto: una colonna dimenticata qui è una regola che non
    /// si applica più, e non lo dice nessuno.</summary>
    public static RunwayRuleEval Valutabile(RunwayRuleRow r) =>
        new(r.DepRunways, r.ArrRunways, r.Name, r.Note, r.MaxTailwindKt, r.MaxCrosswindKt, r.Surface,
            r.TimeFromLocalMin, r.TimeToLocalMin, r.DaysOfWeekMask, r.DateParity,
            r.DateFromMonthDay, r.DateToMonthDay);

    /// <summary>Tutte le righe in forma valutabile, nell'ordine in cui arrivano — che è la loro priorità.</summary>
    public static IReadOnlyList<RunwayRuleEval> Valutabili(IEnumerable<RunwayRuleRow> rows) =>
        rows.Select(Valutabile).ToList();
}
