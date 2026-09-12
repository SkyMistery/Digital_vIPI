using Vipi.Application.Content;
using Vipi.Application.Weather;
using Vipi.Ui.Shared;

namespace Vipi.Ui.Components.Doc;

/// <summary>Quale pista è in uso <b>adesso</b>, in partenza e in arrivo, e da quale pista parte il filtro SID.</summary>
/// <param name="Regola">La regola che sta vincendo; <c>null</c> = nessuna regola vale, e la pista l'ha scelta il vento.</param>
/// <param name="SulleRegoleMostrate">
/// Vero se il verdetto è stato calcolato sulle <b>stesse</b> regole che il lettore ha davanti nella tabella.
/// <para>È falso solo leggendo una release scattata prima del 12 settembre 2026, che non porta le regole in
/// forma calcolabile: lì si ricade sulle regole vive — il comportamento di prima — e la pastiglia «adesso»
/// non si mostra, perché indicherebbe una riga di un'altra lista.</para>
/// </param>
// ⚠️ HashSet e non IReadOnlySet: <AirportRunways> dichiara così i suoi parametri, e un'interfaccia qui
// costringerebbe ogni chiamante a una conversione. Sono insiemi di sola lettura per convenzione.
public sealed record PistaInUsoAdesso(RunwayRuleResult? Regola, HashSet<string> Dep, HashSet<string> Arr,
                                      string? SidRwy, bool SulleRegoleMostrate);

/// <summary>
/// Il calcolo della pista in uso, per i documenti che parlano di uno scalo: la vIPI d'aeroporto e il vSOP
/// militare.
///
/// <para>⚠️ <b>Un posto solo, e dall'11 settembre 2026 serve a due.</b> Stava dentro
/// <see cref="AirportMemberLoader"/>; le regole piste sono entrate nel vSOP militare, e la stessa domanda —
/// «su che pista si decolla adesso?» — scritta due volte darebbe prima o poi due risposte diverse sullo stesso
/// campo, a seconda della pagina da cui lo si guarda.</para>
///
/// <para>⚠️ Un posto solo per i due DOCUMENTI, non ancora per tutto il sito: <c>AirportQuickPanel</c> e
/// <c>AirportListPanel</c> (la vista rapida e l'elenco degli aeroporti) hanno ancora una copia loro dello
/// stesso calcolo, con le piste lette dall'anagrafica viva invece che dalle derivate. Preesistente, rilevato
/// dalla revisione dell'11 settembre 2026.</para>
///
/// <para>⚠️ <b>Si valuta sulle regole che il lettore sta guardando</b>, non su quelle vive: la tabella segue
/// la sezione (Frozen = la fotografia della release, Live = adesso) e dal 12 settembre 2026 il verdetto la
/// segue con lei. Prima si valutava sempre il vivo, e su una sezione congelata bastava cambiare una regola
/// dopo la pubblicazione perché la pastiglia «adesso» indicasse un'altra riga e le Piste marcassero una pista
/// che la tabella pubblicata non spiega. <b>Il vento invece è sempre quello di adesso</b>: quello non è
/// contenuto del documento, e congelarlo sarebbe meteo scaduto.</para>
/// </summary>
public static class PistaInUso
{
    /// <param name="regole">
    /// Le regole su cui valutare: quelle della <b>sezione mostrata</b> (<see cref="AirportRulesView.Regole"/>),
    /// così il verdetto non può contraddire la tabella che il lettore legge.
    /// <para><c>null</c> = la sezione non sa dirlo (release vecchia): il chiamante passa allora le regole vive
    /// in <paramref name="viveDiRipiego"/>, e l'esito lo dichiara con <see cref="PistaInUsoAdesso.SulleRegoleMostrate"/>.</para>
    /// </param>
    /// <param name="viveDiRipiego">Le regole dell'anagrafica di adesso, usate solo quando <paramref name="regole"/> è null.</param>
    /// <param name="runways">Gli identificativi delle piste, per il ripiego sul vento quando nessuna regola vale.</param>
    public static PistaInUsoAdesso Calcola(IReadOnlyList<RunwayRuleRow>? regole, AirportSidView sids,
                                           IReadOnlyList<string> runways, int? windDir, int windKt,
                                           ParsedMetar? metar, IReadOnlyList<RunwayRuleRow>? viveDiRipiego = null)
    {
        var mostrate = regole is not null;
        var daValutare = regole ?? viveDiRipiego ?? Array.Empty<RunwayRuleRow>();

        var wet = (metar?.HasRain ?? false) || (metar?.HasSnow ?? false);
        var ruleResult = daValutare.Count > 0
            ? RunwaySuggestion.EvaluateRules(RegoleDiPista.Valutabili(daValutare),
                                             windDir, windKt, wet, DateTime.UtcNow)
            : null;
        var sugg = RunwaySuggestion.Suggest(runways, windDir, windKt);

        var dep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var arr = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ruleResult is not null)
        {
            foreach (var id in Identificativi(ruleResult.Dep)) dep.Add(id);
            foreach (var id in Identificativi(ruleResult.Arr)) arr.Add(id);
        }
        else if (sugg.Best is not null)
        {
            dep.Add((sugg.DepIdent ?? sugg.Best.Ident).Trim());
            arr.Add((sugg.ArrIdent ?? sugg.Best.Ident).Trim());
        }

        // Seme del filtro SID = pista in uso in partenza (se ha SID), altrimenti la prima pista con SID.
        // ⚠️ Qui non c'è nessun «se il lettore ha già scelto»: quella scelta vive nell'isola <AirportSids>,
        // e fingere di conoscerla è esattamente ciò che teneva le chip ferme.
        var sidRwys = Components.App.AirportSids.RunwaysOf(sids);
        var sidRwy = sidRwys.FirstOrDefault(r => dep.Contains(r)) ?? sidRwys.FirstOrDefault();

        return new PistaInUsoAdesso(ruleResult, dep, arr, sidRwy, mostrate);
    }

    private static IEnumerable<string> Identificativi(string? csv) => (csv ?? "")
        .Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
