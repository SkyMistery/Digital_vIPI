using Vipi.Application.Weather;

namespace Vipi.Ui.Shared;

/// <summary>
/// Le parti del METAR/TAF che hanno una <b>lingua</b>: il vento calmo e i gruppi di tempo presente. Tutto il
/// resto del bollettino decodificato — gradi, nodi, hPa, <c>FEW 3500</c>, <c>&gt;10 km</c> — è già neutro e
/// non passa di qui.
///
/// <para>⚠️ <b>La lingua arriva come funzione, non come cultura corrente.</b> I tre posti che mostrano questi
/// testi non chiedono la stessa cosa: le due pagine d'aeroporto vogliono la lingua di chi guarda, mentre la
/// sezione METAR di un documento a <b>lingua bloccata</b> vuole quella del documento (carta
/// <c>docs/feature/2026-08-31-lingua-bloccata.md</c> §4) — e per giunta è un'isola interattiva, dove la
/// cultura della richiesta non arriva affatto. Un metodo che leggesse <c>CurrentUICulture</c> da sé
/// sbaglierebbe proprio nel caso che questo giro doveva riparare.</para>
///
/// <para>⚠️ <b>Le chiavi di risorsa sono qui e non nello strato Application</b>: <c>MetarParser</c> torna
/// codici ICAO (<c>RA</c>, <c>BR</c>, <c>SHRA</c>), che sono dati, e la mappa codice→parola è materia di chi
/// disegna. Fino all'11 settembre 2026 stava nel parser, con le parole in italiano dentro: una pagina in
/// inglese diceva «leggera pioggia».</para>
/// </summary>
public static class WxText
{
    /// <summary>
    /// Il vento: «160° / 12 kt», «VRB / 3 kt», o la parola per calmo. La raffica NON è qui — chi la mostra la
    /// scrive accanto, perché i tre chiamanti la vogliono in tre forme diverse (colonna, chip, riga TAF).
    /// </summary>
    /// <param name="t">Da chiave di risorsa a testo, nella lingua che il chiamante ha deciso.</param>
    public static string Wind(ParsedWind? w, Func<string, string> t) =>
        w is null ? "—"
        : w.Calm ? t("Wx_Calm")
        : w.Variable ? $"VRB / {w.SpeedKt} kt"
        : $"{w.DirectionDeg:000}° / {w.SpeedKt} kt";

    /// <summary>
    /// I gruppi di tempo presente in parole, separati da virgola; <c>null</c> se non ce n'è nessuno — così
    /// chi disegna sa che la voce «Tempo» non va scritta affatto, invece di scriverla vuota.
    /// </summary>
    public static string? Weather(IReadOnlyList<WeatherGroup>? gruppi, Func<string, string> t) =>
        gruppi is null || gruppi.Count == 0 ? null : string.Join(", ", gruppi.Select(g => Gruppo(g, t)));

    /// <summary>
    /// Un gruppo solo: intensità + le parole dei codici <b>nell'ordine del token</b> (<c>SHRA</c> → «rovescio
    /// pioggia»). L'ordine non si riordina per farne una frase migliore: <c>FZRA</c> e <c>RAFZ</c> sono cose
    /// diverse, e un decoder che le rimescola per suonare bene mente su un bollettino.
    /// </summary>
    private static string Gruppo(WeatherGroup g, Func<string, string> t)
    {
        var parole = string.Join(" ", g.Codes.Select(c => t("Wx_" + c)));
        var intensita = g.Intensity switch
        {
            WxIntensity.Light => t("Wx_Light"),
            WxIntensity.Heavy => t("Wx_Heavy"),
            WxIntensity.Vicinity => t("Wx_Vicinity"),
            _ => null,
        };
        return intensita is null ? parole : $"{intensita} {parole}";
    }
}
