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

/// <summary>
/// Una cella RVR: la <b>testata</b> a cui appartiene e il suo valore.
///
/// <para>🔴 Le celle erano <c>TDZ · MID · END</c>, come sul quadro reale — che ha tre sensori lungo la
/// pista. Il METAR non li ha: dà un valore <b>per testata</b>. Quel «MID» era la <i>media</i> delle due,
/// cioè un numero che nessuno ha misurato sotto il nome di un sensore che non abbiamo. Tolto il 12 settembre
/// 2026 su decisione del committente, con la stessa regola che aveva già fermato il vento: un dato che non
/// si misura non si disegna.</para>
///
/// <para>⚠️ Le celle sono quindi <b>una per testata</b> — due su una pista normale, una su una testata
/// spaiata — e non più tre fisse. Una testata di cui il bollettino non dice l'RVR resta a <c>///</c>: c'è la
/// cella, perché la pista c'è, e manca il valore.</para>
/// </summary>
public sealed record AwosRvrCella(string Testata, string Valore);

/// <summary>
/// Le scritte del quadro che <b>non</b> si muovono fra una lettura e l'altra, composte una volta sola.
///
/// <para>🔴 Esiste per chiudere una classe di difetto, non per comodità. Fino alla revisione del 12 settembre
/// 2026 queste stringhe erano scritte <b>due volte</b> — in C# per il primo disegno e in JavaScript per gli
/// aggiornamenti — e una delle due coppie era <b>già divergita</b>: il riquadro WX mostrava «light shower
/// rain» al caricamento e «-SHRA» un minuto dopo. Le altre quattro aspettavano il loro turno.</para>
///
/// <para>⚠️ Restano fuori le caselle del <b>vento</b> — direzione, velocità, traverso, coda: dipendono dalla
/// <b>testata</b> su cui si proiettano (due per striscia) e, dal 15 settembre 2026, variano un poco attorno al
/// bollettino pannello per pannello. Le scrive il modulo, che sa a quale pannello sta parlando.</para>
/// </summary>
public sealed record AwosScritte(string RigaAttiva, string LvpTesto, string LvpClasse, string LvpTitolo,
                                 IReadOnlyList<IReadOnlyList<AwosRvrCella>> Rvr);

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
    /// <param name="meccanicaVisibile">Chi guarda è staff (DivisionStaff in su): vede DA DOVE viene la pista in
    /// uso. Obbligatorio e senza default: le due porte (pagina, endpoint) devono dichiararlo tutte e due.</param>
    public static AwosScritte Scritte(AwosView v, bool meccanicaVisibile) => new(
        RigaAttiva(v.Attiva, meccanicaVisibile),
        LvpTesto(v.Lvp.Valutazione),
        LvpClasse(v.Lvp.Valutazione),
        LvpTitolo(v.Lvp.Valutazione),
        v.Piste.Select(s => Rvr(s, v.Metar)).ToList());

    /// <summary>
    /// La vista come la riceve chi guarda. Al pubblico si toglie il <see cref="AwosActive.Dettaglio"/> — il nome
    /// della regola, o il callsign dell'ATIS — che il JSON dell'endpoint porterebbe altrimenti con sé.
    /// <para>⚠️ Non basta non scriverlo a schermo: l'endpoint pubblico serializza la vista intera, e un nome di
    /// regola nascosto nella pagina ma presente nel JSON non sarebbe nascosto.</para>
    /// </summary>
    public static AwosView PerChiGuarda(AwosView v, bool meccanicaVisibile) =>
        meccanicaVisibile ? v : v with { Attiva = v.Attiva with { Dettaglio = null } };

    /// <summary>
    /// La riga che dice quale pista è in uso e, allo staff, <b>chi l'ha decisa</b>.
    /// <para>⚠️ Al pubblico la provenienza NON si scrive — né la regola, né il vento, né l'ATIS — come nella
    /// vIPI, dove <c>AirportRunways.MostraProvenienza</c> è solo per DivisionStaff (committente, 15 settembre
    /// 2026). È meccanica di servizio: al pubblico serve la pista, non il perché.</para>
    /// </summary>
    public static string RigaAttiva(AwosActive a, bool meccanicaVisibile)
    {
        if (!meccanicaVisibile)
        {
            if (a.Sorgente == AwosRunwaySource.Nessuna) return "RWY IN USE: —";
            return $"RWY IN USE: {Piste(a)}";
        }

        var da = a.Sorgente switch
        {
            AwosRunwaySource.Atis => $"from ATIS{(a.Dettaglio is null ? "" : " " + a.Dettaglio)}",
            AwosRunwaySource.Regola => $"from rule {a.Dettaglio}",
            AwosRunwaySource.Vento => "from wind (max headwind)",
            _ => "no runway in use — calm or unknown wind",
        };
        if (a.Sorgente == AwosRunwaySource.Nessuna) return $"RWY IN USE: — · {da}";
        return $"RWY IN USE: {Piste(a)} · {da}";
    }

    private static string Piste(AwosActive a)
    {
        var dep = string.Join('/', a.Dep);
        var arr = string.Join('/', a.Arr);
        return string.Equals(dep, arr, StringComparison.OrdinalIgnoreCase)
            ? dep
            : $"{(dep.Length > 0 ? dep : "—")} DEP · {(arr.Length > 0 ? arr : "—")} ARR";
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
    /// Le celle RVR di una striscia: <b>una per testata</b>, con l'ident che le dà il nome.
    /// <para>🔴 Tre casi, e non due (decisione del committente, 15 settembre 2026 — ribalta quella del 12):</para>
    /// <list type="bullet">
    /// <item>il bollettino <b>non ha nessun RVR</b> ⇒ <c>P2000</c> su ogni cella. È ciò che il gruppo assente
    /// dice: l'RVR si riporta quando la visibilità scende, e un METAR che non ne riporta nessuno sta dicendo
    /// «sopra la scala» su tutte le piste. Scrivere <c>///</c> lì si leggeva «sensore guasto».</item>
    /// <item>il bollettino ha RVR per <b>altre</b> piste ma non per questa ⇒ <c>///</c>: qui l'assenza non si
    /// può più leggere come «sopra la scala», perché sulle altre la visibilità è bassa.</item>
    /// <item><b>nessun bollettino</b> ⇒ <c>///</c>: non c'è niente da dire.</item>
    /// </list>
    /// <para>⚠️ Resta tolta la cella «MID»: era la media delle due testate, un numero che nessuno ha misurato.</para>
    /// </summary>
    public static IReadOnlyList<AwosRvrCella> Rvr(AwosStrip s, ParsedMetar? metar)
    {
        var teste = s.Right is null ? new[] { s.Left } : new[] { s.Left, s.Right };
        return teste.Select(e => new AwosRvrCella(e.Ident, Valore(e, metar))).ToList();
    }

    private static string Valore(AwosEnd end, ParsedMetar? metar)
    {
        if (metar is null) return "///";
        if (metar.RvrGroups.Count == 0) return "P2000";
        var g = metar.RvrGroups.FirstOrDefault(r =>
            string.Equals(r.Runway, end.Ident, StringComparison.OrdinalIgnoreCase));
        return g is null ? "///" : Scrivi(g);
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
