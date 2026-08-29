using System.Globalization;
using System.Text.RegularExpressions;

namespace Vipi.Application.Coordinates;

/// <summary>
/// La coppia di coordinate <b>sessagesimale</b> come la scrivono i SOP militari:
/// <c>N41°32'05.07''E015°43'42.47''</c> — emisfero davanti, gradi/primi/secondi coi simboli, i due secondi
/// chiusi da <b>due apici</b>, e la longitudine attaccata alla latitudine.
///
/// <para>
/// ⚠️ <b>Non è il DMS di Aurora</b> (<see cref="DmsCoordinate"/>, <c>N041.32.51.500</c>) e non è nessuna
/// delle tredici forme che legge il convertitore: quella coi simboli, in <c>CoordinateParser</c>, vuole
/// l'emisfero <i>dietro</i> e i secondi chiusi da un doppio apice (<c>41°59'26.5"N</c>). Questa è la forma
/// **che si scrive a mano nella tabella delle radioassistenze**, ed è l'unica accettata lì: il committente ha
/// chiesto «sessagesimale soltanto», perché è la forma in cui il dato sta sui documenti di partenza e
/// riscriverlo in un'altra è il modo di sbagliarlo.
/// </para>
///
/// <para>In archivio si tengono i <b>gradi decimali</b>, che sono la forma che una mappa sa usare; questa
/// classe è la porta d'ingresso e d'uscita. Un giro completo (scrivi → salva → rileggi) restituisce il
/// testo di partenza a meno dell'arrotondamento ai centesimi di secondo, che è ~30 cm.</para>
/// </summary>
public static class SexagesimalPair
{
    /// <summary>
    /// Un angolo con emisfero davanti: <c>N41°32'05.07''</c>. I secondi si chiudono con due apici, un doppio
    /// apice, il simbolo tipografico, o niente — chi scrive a mano usa quel che ha sulla tastiera, e rifiutare
    /// una forma equivalente sarebbe pedanteria che costa tempo a chi compila.
    /// </summary>
    private const string Pezzo =
        @"(?<h{0}>[NSEW])\s*(?<d{0}>\d{{1,3}})\s*°\s*(?<m{0}>\d{{1,2}})\s*['′]\s*(?<s{0}>\d{{1,2}}(?:[.,]\d+)?)\s*(?:''|""|″|′′)?";

    private static readonly Regex RxCoppia = new(
        "^\\s*" + string.Format(CultureInfo.InvariantCulture, Pezzo, 1) + "\\s*[,;/]?\\s*"
        + string.Format(CultureInfo.InvariantCulture, Pezzo, 2) + "\\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Legge la coppia. Falso se il testo non è in questa forma — e <b>falso è la risposta giusta</b> anche
    /// per un decimale valido: qui si accetta il sessagesimale e basta.
    /// <para>⚠️ L'ordine è tollerante: <c>N…E…</c> o <c>E…N…</c>. Chi copia da un documento non guarda
    /// l'ordine, guarda le lettere, e le lettere lo dicono senza ambiguità.</para>
    /// </summary>
    public static bool TryParse(string? testo, out double lat, out double lon)
    {
        lat = lon = 0;
        if (string.IsNullOrWhiteSpace(testo)) return false;

        var m = RxCoppia.Match(testo.Trim());
        if (!m.Success) return false;

        if (!LeggiAngolo(m, "1", out var a, out var ha)) return false;
        if (!LeggiAngolo(m, "2", out var b, out var hb)) return false;

        // Una coppia è una latitudine e una longitudine: due N o due E non sono un punto.
        var aLat = ha is 'N' or 'S';
        var bLat = hb is 'N' or 'S';
        if (aLat == bLat) return false;

        (lat, lon) = aLat ? (a, b) : (b, a);
        return Math.Abs(lat) <= 90 && Math.Abs(lon) <= 180;
    }

    private static bool LeggiAngolo(Match m, string i, out double gradi, out char emisfero)
    {
        gradi = 0;
        emisfero = char.ToUpperInvariant(m.Groups["h" + i].Value[0]);

        var d = int.Parse(m.Groups["d" + i].Value, CultureInfo.InvariantCulture);
        var min = int.Parse(m.Groups["m" + i].Value, CultureInfo.InvariantCulture);
        var sec = double.Parse(m.Groups["s" + i].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);

        // ⚠️ 61 primi non sono «quasi 62»: sono un errore di battitura, e passarli darebbe un punto plausibile
        // e sbagliato — il difetto che una coordinata non deve mai avere.
        if (min > 59 || sec >= 60) return false;

        var v = d + min / 60.0 + sec / 3600.0;
        gradi = emisfero is 'S' or 'W' ? -v : v;
        return true;
    }

    /// <summary>
    /// Scrive la coppia nella forma dei SOP: <c>N41°32'05.07''E015°43'42.47''</c>.
    /// <para>⚠️ Gradi su <b>due</b> cifre per la latitudine e <b>tre</b> per la longitudine, come nell'esempio
    /// del committente: incolonnate in tabella si leggono, e una longitudine a due cifre si scambia per una
    /// latitudine a colpo d'occhio.</para>
    /// </summary>
    public static string Format(double lat, double lon) =>
        Angolo(lat, isLatitudine: true) + Angolo(lon, isLatitudine: false);

    /// <summary>Un angolo solo, per chi ne mostra uno per volta.</summary>
    public static string Angolo(double gradi, bool isLatitudine)
    {
        var hemi = isLatitudine ? (gradi < 0 ? 'S' : 'N') : (gradi < 0 ? 'W' : 'E');

        // ⚠️ Un arrotondamento solo, in centesimi di secondo: arrotondare i secondi e formattare la frazione
        // a parte produce `…59.60''`, un sessagesimale che non esiste. Stessa trappola già pagata in
        // DmsCoordinate.Format, che arrotonda in millisecondi d'arco.
        var totale = (long)Math.Round(Math.Abs(gradi) * 360_000.0, MidpointRounding.AwayFromZero);
        var cent = totale % 100; totale /= 100;
        var sec = totale % 60; totale /= 60;
        var min = totale % 60;
        var deg = totale / 60;

        var cifre = isLatitudine ? "00" : "000";
        return string.Create(CultureInfo.InvariantCulture,
            $"{hemi}{deg.ToString(cifre, CultureInfo.InvariantCulture)}°{min:00}'{sec:00}.{cent:00}''");
    }
}
