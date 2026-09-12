using Vipi.Application.Awos;
using Vipi.Application.Weather;

namespace Vipi.Ui.Shared;

/// <summary>
/// Il quadro vAWOS servito a chi lo disegna: la vista più le <b>due sole righe che hanno una lingua</b>.
///
/// <para>⚠️ Perché un involucro invece di due campi dentro <see cref="AwosView"/>: quel record vive in
/// <c>Vipi.Application</c>, che non sa in che lingua guarda chi legge — è la stessa ragione per cui
/// <c>MetarParser</c> torna codici e non parole ([[metar-decodificato-si-traduce-in-ui]]). Le parole si
/// mettono qui, dove il localizzatore c'è.</para>
/// </summary>
public sealed record AwosPayload(AwosView Vista, IReadOnlyList<string> Wx, IReadOnlyList<string> Nubi);

/// <summary>
/// Le righe WX e CLOUD del quadro.
///
/// <para>⚠️ Un posto solo per <b>due</b> chiamanti — la pagina, che le rende al primo disegno, e l'endpoint,
/// che le rimanda a ogni giro. Scritte due volte, la seconda lettura avrebbe potuto contraddire la prima a
/// schermo, sullo stesso bollettino.</para>
/// </summary>
public static class AwosTesto
{
    /// <summary>I gruppi di tempo presente in parole. Il resto del quadro è tutto sigle ICAO, che non si traducono.</summary>
    public static IReadOnlyList<string> TempoPresente(ParsedMetar? m, Func<string, string> t) =>
        m is null
            ? Array.Empty<string>()
            : m.Weather.Select(g => WxText.Weather(new[] { g }, t) ?? g.Raw).ToList();

    /// <summary>
    /// Gli strati di nubi, e in testa la visibilità verticale quando c'è: è la riga che dice «il cielo non
    /// si vede», e viene prima di qualunque strato perché è più bassa di tutti.
    /// </summary>
    public static IReadOnlyList<string> Nubi(ParsedMetar? m)
    {
        if (m is null) return Array.Empty<string>();
        var righe = new List<string>();
        if (m.VerticalVisibilityFt is int vv) righe.Add($"VV {vv} FT");
        righe.AddRange(m.Clouds.Select(c => $"{c.Cover} {c.BaseFt} FT{(c.Type is null ? "" : " " + c.Type)}"));
        return righe;
    }
}
