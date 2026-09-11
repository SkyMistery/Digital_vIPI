using Vipi.Application.Content;
using Vipi.Application.Weather;
using Vipi.Ui.Shared;

namespace Vipi.Ui.Components.Doc;

/// <summary>Quale pista è in uso <b>adesso</b>, in partenza e in arrivo, e da quale pista parte il filtro SID.</summary>
/// <param name="Regola">La regola che sta vincendo; <c>null</c> = nessuna regola vale, e la pista l'ha scelta il vento.</param>
// ⚠️ HashSet e non IReadOnlySet: <AirportRunways> dichiara così i suoi parametri, e un'interfaccia qui
// costringerebbe ogni chiamante a una conversione. Sono insiemi di sola lettura per convenzione.
public sealed record PistaInUsoAdesso(RunwayRuleResult? Regola, HashSet<string> Dep, HashSet<string> Arr, string? SidRwy);

/// <summary>
/// Il calcolo della pista in uso, per i documenti che parlano di uno scalo: la vIPI d'aeroporto e il vSOP
/// militare.
///
/// <para>⚠️ <b>Un posto solo, e dall'11 settembre 2026 serve a due.</b> Stava dentro
/// <see cref="AirportMemberLoader"/>; le regole piste sono entrate nel vSOP militare, e la stessa domanda —
/// «su che pista si decolla adesso?» — scritta due volte darebbe prima o poi due risposte diverse sullo stesso
/// campo, a seconda della pagina da cui lo si guarda.</para>
///
/// <para>⚠️ Si valuta sulle regole <b>vive</b>, non su quelle eventualmente congelate: «quale pista è in uso
/// adesso» è una domanda sul presente, e resta tale anche mentre si sfoglia un ciclo passato.</para>
/// </summary>
public static class PistaInUso
{
    /// <param name="profilo">L'anagrafica dello scalo: porta le regole. <c>null</c> = scalo non in anagrafica.</param>
    /// <param name="runways">Gli identificativi delle piste, per il ripiego sul vento quando nessuna regola vale.</param>
    public static PistaInUsoAdesso Calcola(AirportData? profilo, AirportSidView sids, IReadOnlyList<string> runways,
                                           int? windDir, int windKt, ParsedMetar? metar)
    {
        var wet = (metar?.HasRain ?? false) || (metar?.HasSnow ?? false);
        var ruleResult = profilo is { Rules.Count: > 0 }
            ? RunwaySuggestion.EvaluateRules(profilo.Rules.Select(AirportViewFormat.MapRule).ToList(),
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

        return new PistaInUsoAdesso(ruleResult, dep, arr, sidRwy);
    }

    private static IEnumerable<string> Identificativi(string? csv) => (csv ?? "")
        .Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
