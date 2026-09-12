using System.Text.RegularExpressions;
using Vipi.Application.Content;
using Vipi.Application.Weather;

namespace Vipi.Application.Awos;

/// <summary>
/// Il cuore deterministico del quadro: dalle righe dell'anagrafica alle strisce di pista, e dal QNH al TL.
/// Nessun IO, così si prova senza banco.
/// </summary>
public static partial class AwosComposition
{
    [GeneratedRegex(@"^(\d{1,2})([LRC]?)$")]
    private static partial Regex IdentRe();

    /// <summary>Quanto due rotte possono discostarsi da 180° e valere ancora come «la stessa pista».</summary>
    private const int TolleranzaOppostaDeg = 20;

    /// <summary>
    /// Le strisce da disegnare: una per <b>pista fisica</b>, accoppiando le testate per rotta opposta.
    ///
    /// <para>⚠️ È questo che manda in pensione i tre file del prototipo (uno per numero di piste): Fiumicino
    /// non ha codice suo, ha tre righe di pista. L'accoppiamento è sulla <b>rotta</b> e non sull'ident
    /// (16L↔34R, non 16L↔16R), perché è la rotta a dire che sono due capi dello stesso nastro.</para>
    ///
    /// <para>A sinistra va la testata dall'ident numericamente <b>minore</b> — la convenzione del quadro
    /// reale (07 a sinistra, 25 a destra) e del prototipo.</para>
    /// </summary>
    public static IReadOnlyList<AwosStrip> Strisce(IEnumerable<RunwayRow> piste)
    {
        var teste = piste
            .Select(r => (Ident: (r.Ident ?? "").Trim().ToUpperInvariant(), Row: r))
            .Where(x => IdentRe().IsMatch(x.Ident))
            .Select(x => new
            {
                x.Ident,
                Numero = int.Parse(IdentRe().Match(x.Ident).Groups[1].Value),
                Lato = IdentRe().Match(x.Ident).Groups[2].Value,
                Rotta = Rotta(x.Ident, x.Row.Bearing),
                Elev = x.Row.ThresholdElevationFt,
            })
            .GroupBy(x => x.Ident).Select(g => g.First())   // ident ripetuto: una sola testata
            .OrderBy(x => x.Numero).ThenBy(x => x.Ident, StringComparer.Ordinal)
            .ToList();

        var strisce = new List<AwosStrip>();
        var usate = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var t in teste)
        {
            if (!usate.Add(t.Ident)) continue;

            // 🔴 Fra le candidate con la rotta opposta si prende PRIMA quella col lato SPECULARE (L↔R): la
            // 16L di Fiumicino è lo stesso nastro della 34R, non della 34L, perché girandosi la sinistra
            // diventa destra. Trovato dal dato vero il 12 settembre 2026 — la prima versione prendeva la
            // prima rotta opposta libera e appaiava 16L con 34L, cioè due piste diverse disegnate come una.
            var candidate = teste.Where(o => !usate.Contains(o.Ident) && EOpposta(t.Rotta, o.Rotta)).ToList();
            var opposta = candidate.FirstOrDefault(o => o.Lato == LatoSpeculare(t.Lato)) ?? candidate.FirstOrDefault();
            var sinistra = new AwosEnd(t.Ident, t.Rotta, t.Elev);
            if (opposta is null) { strisce.Add(new AwosStrip(sinistra, null)); continue; }

            usate.Add(opposta.Ident);
            strisce.Add(new AwosStrip(sinistra, new AwosEnd(opposta.Ident, opposta.Rotta, opposta.Elev)));
        }

        return strisce;
    }

    /// <summary>
    /// La rotta della testata: quella dell'anagrafica, o — se manca — quella <b>dedotta dall'ident</b>.
    /// <para>⚠️ La deduzione è un ripiego dichiarato, non un valore: LIRF 16L è 159°, non 160. Si usa solo
    /// perché una testata senza rotta non può stare in un quadro che calcola il traverso.</para>
    /// </summary>
    public static int Rotta(string ident, int? bearing)
    {
        if (bearing is int b && b is > 0 and <= 360) return b == 360 ? 360 : b;
        var m = IdentRe().Match(ident);
        var dedotta = m.Success ? int.Parse(m.Groups[1].Value) * 10 : 0;
        return dedotta == 0 ? 360 : dedotta;
    }

    /// <summary>Il lato che la stessa pista ha dall'altro capo: L↔R, C resta C, nessuno resta nessuno.</summary>
    private static string LatoSpeculare(string lato) => lato switch { "L" => "R", "R" => "L", _ => lato };

    private static bool EOpposta(int a, int b)
    {
        var delta = Math.Abs(((a - b + 360) % 360) - 180);
        return delta <= TolleranzaOppostaDeg;
    }

    /// <summary>
    /// Il Transition Level per il QNH corrente, dalla tabella dello scalo.
    /// <para>⚠️ Dalla <b>tabella</b>, non da una formula a fasce come nel prototipo: il TL è dell'AIP di
    /// quell'aeroporto, e quattro soglie cablate valgono per nessuno in particolare. Senza QNH o senza righe:
    /// null, che si legge «non lo so» e non «FL70».</para>
    /// </summary>
    public static string? TransitionLevel(IEnumerable<TlRow> righe, int? qnh)
    {
        if (qnh is not int q) return null;
        foreach (var r in righe)
        {
            if (r.QnhFrom is int da && q < da) continue;
            if (r.QnhTo is int a && q > a) continue;
            if (!string.IsNullOrWhiteSpace(r.Level)) return r.Level.Trim();
        }
        return null;
    }

    /// <summary>
    /// Il QFE di una soglia: QNH meno l'altezza, con la regola di campo di 27 ft per hPa.
    /// <para>È un'approssimazione — la stessa che usa il quadro del prototipo — e vale finché si parla di
    /// aeroporti italiani, tutti sotto i 1 500 ft. Senza elevazione non si stima: null.</para>
    /// </summary>
    public static int? Qfe(int? qnh, int? elevazioneFt) =>
        qnh is int q && elevazioneFt is int e ? (int)Math.Round(q - e / 27.0) : null;

    /// <summary>
    /// La pista in uso e chi l'ha decisa: prima l'ATIS, poi le regole dello scalo, poi il vento.
    ///
    /// <para>⚠️ L'ordine è una decisione (carta §4.5): l'ATIS batte le regole perché dice che cosa sta
    /// <b>succedendo</b>, mentre una regola dice che cosa dovrebbe succedere. Le due cose divergono ogni
    /// volta che una torre tiene la configurazione per un motivo che il METAR non conosce.</para>
    /// </summary>
    /// <param name="atisDep">Piste in partenza lette dall'ATIS di chi presiede, se c'è un ATIS.</param>
    /// <param name="daChi">Il callsign da citare quando la pista viene dall'ATIS.</param>
    public static AwosActive PistaAttiva(
        IReadOnlyList<RunwayRuleRow> regole, IReadOnlyList<string> piste, ParsedMetar? metar,
        IReadOnlyList<string>? atisDep = null, IReadOnlyList<string>? atisArr = null, string? daChi = null)
    {
        if (atisDep is { Count: > 0 } || atisArr is { Count: > 0 })
        {
            var dep = Prima(atisDep) ?? Prima(atisArr);
            var arr = Prima(atisArr) ?? Prima(atisDep);
            return new AwosActive(dep, arr, AwosRunwaySource.Atis, daChi);
        }

        int? dir = metar?.Wind is { Calm: false, DirectionDeg: int d } ? d : null;
        var kt = metar?.Wind?.SpeedKt ?? 0;

        if (regole.Count > 0)
        {
            var bagnata = (metar?.HasRain ?? false) || (metar?.HasSnow ?? false);
            var esito = RunwaySuggestion.EvaluateRules(RegoleDiPista.Valutabili(regole), dir, kt, bagnata, DateTime.UtcNow);
            if (esito is not null)
                return new AwosActive(Prima(Spezza(esito.Dep)), Prima(Spezza(esito.Arr)), AwosRunwaySource.Regola,
                                      esito.RuleName ?? $"#{esito.RuleIndex + 1}");
        }

        if (piste.Count > 0)
        {
            var s = RunwaySuggestion.Suggest(piste, dir, kt);
            if (s.Best is not null)
                return new AwosActive(s.DepIdent ?? s.Best.Ident, s.ArrIdent ?? s.Best.Ident, AwosRunwaySource.Vento, null);
        }

        return AwosActive.Nessuna;
    }

    private static string? Prima(IReadOnlyList<string>? v) => v is { Count: > 0 } ? v[0] : null;

    private static IReadOnlyList<string> Spezza(string? csv) => (csv ?? "")
        .Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
