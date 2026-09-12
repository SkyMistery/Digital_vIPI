using Vipi.Application.Awos;
using Vipi.Application.Content;
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
public sealed record AwosPayload(AwosView Vista, IReadOnlyList<string> Wx, IReadOnlyList<string> Nubi,
                                 AwosScritte Scritte);

/// <summary>Le tre celle RVR di una striscia, già scritte.</summary>
public sealed record AwosRvrScritte(string Tdz, string Mid, string End);

/// <summary>
/// Le scritte del quadro che <b>non</b> si muovono fra una lettura e l'altra, composte una volta sola.
///
/// <para>🔴 Esiste per chiudere una classe di difetto, non per comodità. Fino alla revisione del 12 settembre
/// 2026 queste stringhe erano scritte <b>due volte</b> — in C# per il primo disegno e in JavaScript per gli
/// aggiornamenti — e una delle due coppie era <b>già divergita</b>: il riquadro WX mostrava «light shower
/// rain» al caricamento e «-SHRA» un minuto dopo. Le altre quattro aspettavano il loro turno.</para>
///
/// <para>⚠️ Restano fuori, e per forza, le sole due cose che si <b>animano</b>: direzione/velocità del vento
/// e le componenti traverso/coda, che il modulo ricalcola venti volte al secondo interpolando fra due
/// bollettini. Quelle il server non può scriverle: sono diverse a ogni fotogramma.</para>
/// </summary>
public sealed record AwosScritte(string RigaAttiva, string LvpTesto, string LvpClasse, string LvpTitolo,
                                 IReadOnlyList<AwosRvrScritte> Rvr);

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

    /// <summary>Tutte le scritte fisse del quadro, per la pagina e per l'endpoint. Un posto solo.</summary>
    public static AwosScritte Scritte(AwosView v) => new(
        RigaAttiva(v.Attiva),
        LvpTesto(v.Lvp.Valutazione),
        LvpClasse(v.Lvp.Valutazione),
        LvpTitolo(v.Lvp.Valutazione),
        v.Piste.Select(s => Rvr(s, v.Metar)).ToList());

    /// <summary>
    /// La riga che dice quale pista è in uso e <b>chi l'ha decisa</b>. Un quadro che dice la pista senza dire
    /// da dove viene è la ragione per cui quello del prototipo va girato a mano.
    /// </summary>
    public static string RigaAttiva(AwosActive a)
    {
        var da = a.Sorgente switch
        {
            AwosRunwaySource.Atis => $"from ATIS{(a.Dettaglio is null ? "" : " " + a.Dettaglio)}",
            AwosRunwaySource.Regola => $"from rule {a.Dettaglio}",
            AwosRunwaySource.Vento => "from wind (max headwind)",
            _ => "no runway in use — calm or unknown wind",
        };
        if (a.Sorgente == AwosRunwaySource.Nessuna) return $"RWY IN USE: — · {da}";

        var dep = string.Join('/', a.Dep);
        var arr = string.Join('/', a.Arr);
        var piste = string.Equals(dep, arr, StringComparison.OrdinalIgnoreCase)
            ? dep
            : $"{(dep.Length > 0 ? dep : "—")} DEP · {(arr.Length > 0 ? arr : "—")} ARR";
        return $"RWY IN USE: {piste} · {da}";
    }

    /// <summary>La pastiglia LVP: lo stato, e «(standard)» quando la soglia non è dello scalo.</summary>
    public static string LvpTesto(LvpValutazione v)
    {
        var stato = v.Stato switch
        {
            LvpStato.InVigore => "LVP",
            LvpStato.Cancellabile => "LVP CANCEL?",
            LvpStato.Preparazione => "LVP PREP",
            LvpStato.NonApplicabile => "LVP N/A",
            LvpStato.NonValutabile => "LVP —",
            _ => "LVP NIL",
        };
        var acceso = v.Stato is LvpStato.Preparazione or LvpStato.InVigore or LvpStato.Cancellabile;
        return acceso && !v.DaiMinimiDelloScalo ? stato + " (standard)" : stato;
    }

    public static string LvpClasse(LvpValutazione v) => v.Stato switch
    {
        LvpStato.InVigore or LvpStato.Cancellabile => "on",
        LvpStato.Preparazione => "prep",
        _ => "",
    };

    /// <summary>Il perché, per esteso: da dove viene la misura e da dove vengono le soglie.</summary>
    public static string LvpTitolo(LvpValutazione v)
    {
        if (v.Stato == LvpStato.NonApplicabile) return "This airport does not operate LVP.";
        if (v.Stato == LvpStato.NonValutabile) return "No minima or no weather data to compare.";
        var misura = v.Misura switch
        {
            LvpMisura.Rvr => $"RVR {v.RvrM} m",
            LvpMisura.Visibilita => $"visibility {v.RvrM} m (no RVR reported)",
            _ => "no visibility measure",
        };
        var soffitto = v.CeilingFt is int c ? $", ceiling {c} ft" : ", no ceiling";
        var soglie = v.DaiMinimiDelloScalo ? "airport minima" : "standard minima (this airport declares none)";
        var coda = v.Stato == LvpStato.Cancellabile
            ? " LVP were in force and the values are back above the cancellation thresholds."
            : "";
        return $"Suggested from {misura}{soffitto} against {soglie}.{coda} " +
               "Activating or cancelling LVP remains the airport's decision.";
    }

    /// <summary>
    /// Le tre celle RVR di una striscia.
    /// <para>🔴 Un RVR che il bollettino non dà è <c>///</c>, mai <c>P2000</c>: nel prototipo un valore che
    /// nessuno aveva misurato compariva come «oltre 2 000 m» davanti a chi decide se si atterra.</para>
    /// </summary>
    public static AwosRvrScritte Rvr(AwosStrip s, ParsedMetar? metar)
    {
        if (metar is null) return new AwosRvrScritte("///", "///", "///");

        string Per(AwosEnd? end)
        {
            if (end is null) return "///";
            var g = metar.RvrGroups.FirstOrDefault(r =>
                string.Equals(r.Runway, end.Ident, StringComparison.OrdinalIgnoreCase));
            return g is null ? "///" : Scrivi(g);
        }

        // ⚠️ La media SOLO sui valori esatti: mediare un «oltre il fondo scala» vorrebbe dire prendere per
        // misura un limite dello strumento.
        var esatti = metar.RvrGroups
            .Where(r => r.Modifier == RvrModifier.Exact)
            .Where(r => string.Equals(r.Runway, s.Left.Ident, StringComparison.OrdinalIgnoreCase)
                        || (s.Right is not null && string.Equals(r.Runway, s.Right.Ident, StringComparison.OrdinalIgnoreCase)))
            .Select(r => r.ValueM).ToList();
        var mid = esatti.Count == 0 ? "///" : ((int)Math.Round(esatti.Average() / 50.0) * 50).ToString();

        return new AwosRvrScritte(Per(s.Left), mid, Per(s.Right));
    }

    private static string Scrivi(RunwayVisualRange r) =>
        (r.Modifier switch { RvrModifier.Above => "P", RvrModifier.Below => "M", _ => "" })
        + r.ValueM
        + r.Tendency switch { RvrTendency.Up => "U", RvrTendency.Down => "D", RvrTendency.Steady => "N", _ => "" };

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
