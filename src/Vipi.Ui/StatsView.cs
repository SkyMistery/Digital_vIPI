using System.Globalization;

namespace Vipi.Ui;

/// <summary>Un periodo che si può scegliere dalle chip in testa alle pagine delle statistiche.</summary>
/// <param name="Key">Come compare nell'indirizzo (<c>?p=90</c>).</param>
/// <param name="Days">Quanti giorni indietro guardare.</param>
/// <param name="LabelKey">Chiave di risorsa dell'etichetta.</param>
public sealed record StatsPeriod(string Key, int Days, string LabelKey);

/// <summary>Una barra del grafico per mese.</summary>
public sealed record StatsBar(string Label, double Value, string Title, bool Highlight = false);

/// <summary>Una fetta della ciambella.</summary>
public sealed record StatsSlice(string Label, double Value, string CssClass, string Title);

/// <summary>
/// I conti e i formati che le tre pagine delle statistiche facevano ognuna per sé.
///
/// <para>⚠️ <c>Ore</c> e <c>Durata</c> erano copiati in tre <c>.razor</c>: tre copie della stessa regola
/// («sotto il decimo d'ora si scrive che è poco, non che è zero») sono tre posti dove un giorno sarà
/// diversa. La cultura è sempre quella del lettore per i numeri a schermo, e <b>invariante</b> per quelli
/// che finiscono dentro un attributo SVG — lì una virgola decimale rompe il disegno.</para>
/// </summary>
public static class StatsView
{
    /// <summary>
    /// I periodi offerti. ⚠️ «Tutto» è dieci anni e non <c>DateTimeOffset.MinValue</c>: la finestra entra in
    /// conti su <c>DateTime</c> e un minimo assoluto li fa traboccare quando qualcuno ci somma dei giorni.
    /// La sorgente conserva dodici mesi, quindi in pratica «tutto» è «tutto quel che c'è».
    /// </summary>
    public static readonly IReadOnlyList<StatsPeriod> Periods = new[]
    {
        new StatsPeriod("30", 30, "Stats_P30"),
        new StatsPeriod("90", 90, "Stats_P90"),
        new StatsPeriod("365", 366, "Stats_P365"),
        new StatsPeriod("all", 3650, "Stats_PAll"),
    };

    /// <summary>Il periodo chiesto dall'indirizzo; in mancanza, dodici mesi (quel che la sorgente conserva).</summary>
    public static StatsPeriod Period(string? key) =>
        Periods.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase))
        ?? Periods[2];

    /// <summary>
    /// Ore con un decimale. ⚠️ «0,0 ore» per una sessione vera di venti minuti sembra «niente»: sotto il
    /// decimo d'ora si scrive che è poco, non che è zero.
    /// </summary>
    public static string Ore(long secondi) =>
        secondi > 0 && secondi < 360 ? "<0,1" : (secondi / 3600.0).ToString("0.0");

    /// <summary>Durata leggibile: <c>2h 05m</c> sopra l'ora, <c>45m</c> sotto.</summary>
    public static string Durata(int secondi) =>
        secondi >= 3600 ? $"{secondi / 3600}h {(secondi % 3600) / 60:00}m" : $"{secondi / 60}m";

    /// <summary>
    /// Variazione percentuale fra due periodi; <c>null</c> quando il confronto non esiste — e il caso è
    /// frequente, non un'eccezione: chi comincia adesso non ha un «prima», e scrivere «+100%» sarebbe falso.
    /// </summary>
    public static int? Delta(double adesso, double prima) =>
        prima <= 0 ? null : (int)Math.Round((adesso - prima) / prima * 100);

    /// <summary>Classe del colore per tipo di postazione (<c>LIRF_TWR</c> → <c>p-twr</c>).</summary>
    public static string PosClass(string? callsignOrPosition)
    {
        var t = Tipo(callsignOrPosition);
        return t switch
        {
            "DEL" => "p-del",
            "GND" => "p-gnd",
            "TWR" or "AFIS" => "p-twr",
            "APP" or "DEP" => "p-app",
            "CTR" or "FSS" => "p-ctr",
            _ => "p-oth",
        };
    }

    /// <summary>Il suffisso di posizione, da un callsign completo o da una posizione già estratta.</summary>
    public static string Tipo(string? callsignOrPosition)
    {
        if (string.IsNullOrWhiteSpace(callsignOrPosition)) return "";
        var s = callsignOrPosition.Trim().ToUpperInvariant();
        return s.Contains('_') ? s.Split('_')[^1] : s;
    }

    /// <summary>Un numero dentro un attributo SVG: sempre col punto decimale, mai con la virgola.</summary>
    public static string Svg(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Percentuale di una parte sul massimo, per la barra dentro la cella (0–100).</summary>
    public static string Quota(double valore, double massimo) =>
        massimo <= 0 ? "0%" : Svg(Math.Clamp(valore / massimo * 100, 0, 100)) + "%";

    /// <summary>Quota in piedi come la scrive un controllore: <c>FL240</c> sopra la transizione, <c>2 400 ft</c> sotto.</summary>
    public static string Livello(int piedi) =>
        piedi >= 10_000 ? "FL" + (piedi / 100).ToString("000") : piedi.ToString("#,##0") + " ft";
}
