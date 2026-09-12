using System.Text.RegularExpressions;

namespace Vipi.Application.Stats;

/// <summary>
/// Quel che si legge dall'ATIS oltre alle piste: la <b>lettera</b> e l'<b>ora</b> del bollettino.
/// </summary>
/// <param name="Lettera">Una lettera sola (<c>C</c>), dal nome NATO o dalla lettera nuda. Vuota = non dichiarata.</param>
/// <param name="Orario">L'ora scritta nell'ATIS in forma <c>HH:MMz</c>. Vuota = non dichiarata.</param>
public readonly record struct AtisInfo(string Lettera, string Orario)
{
    public static readonly AtisInfo Nessuno = new("", "");
    public bool Vuoto => Lettera.Length == 0 && Orario.Length == 0;
}

/// <summary>
/// Legge lettera e ora dal testo di un ATIS.
///
/// <para>Stessa famiglia di <see cref="AtisRunways"/>, e stessa ragione di esistere: non c'è nessun campo
/// strutturato, c'è una frase. E stessa regola quando la frase non si riconosce — <b>non si dice niente</b>:
/// una lettera indovinata è peggio di una lettera mancante, perché è quella che il pilota ripete.</para>
///
/// <para>Puro e deterministico, nessun I/O.</para>
/// </summary>
public static partial class AtisText
{
    [GeneratedRegex(@"\bINFORMATION\s+([A-Z]+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LetteraRe();

    [GeneratedRegex(@"\bAT\s+(\d{4})\b|\b(\d{4})Z\b", RegexOptions.IgnoreCase)]
    private static partial Regex OraRe();

    /// <summary>
    /// Il nome NATO alla sua lettera. ⚠️ <c>JULIET</c> e <c>JULIETT</c> stanno tutt'e due: si scrive nei due
    /// modi, e chi legge non deve accorgersene.
    /// </summary>
    private static readonly Dictionary<string, char> Nato = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ALPHA"] = 'A', ["ALFA"] = 'A', ["BRAVO"] = 'B', ["CHARLIE"] = 'C', ["DELTA"] = 'D', ["ECHO"] = 'E',
        ["FOXTROT"] = 'F', ["GOLF"] = 'G', ["HOTEL"] = 'H', ["INDIA"] = 'I', ["JULIET"] = 'J', ["JULIETT"] = 'J',
        ["KILO"] = 'K', ["LIMA"] = 'L', ["MIKE"] = 'M', ["NOVEMBER"] = 'N', ["OSCAR"] = 'O', ["PAPA"] = 'P',
        ["QUEBEC"] = 'Q', ["ROMEO"] = 'R', ["SIERRA"] = 'S', ["TANGO"] = 'T', ["UNIFORM"] = 'U',
        ["VICTOR"] = 'V', ["WHISKEY"] = 'W', ["WHISKY"] = 'W', ["XRAY"] = 'X', ["YANKEE"] = 'Y', ["ZULU"] = 'Z',
    };

    /// <summary>Lettera e ora dalle righe di un ATIS.</summary>
    public static AtisInfo Leggi(IEnumerable<string>? righe)
    {
        var testo = Testo(righe);
        if (testo.Length == 0) return AtisInfo.Nessuno;

        var lettera = "";
        if (LetteraRe().Match(testo) is { Success: true } ml)
        {
            var parola = ml.Groups[1].Value;
            lettera = Nato.TryGetValue(parola, out var n) ? n.ToString()
                    : parola.Length == 1 ? parola.ToUpperInvariant()
                    : "";
        }

        var orario = "";
        if (OraRe().Match(testo) is { Success: true } mo)
        {
            var cifre = mo.Groups[1].Success ? mo.Groups[1].Value : mo.Groups[2].Value;
            if (int.TryParse(cifre[..2], out var h) && int.TryParse(cifre[2..], out var m)
                && h is >= 0 and <= 23 && m is >= 0 and <= 59)
                orario = $"{cifre[..2]}:{cifre[2..]}z";
        }

        return new AtisInfo(lettera, orario);
    }

    /// <summary>
    /// Il testo dell'ATIS su una riga sola, per mostrarlo a chi legge.
    ///
    /// <para>⚠️ La <b>prima</b> riga si salta: è l'indirizzo del server voce, non testo ATIS. Lo dice la
    /// sorgente ed è verificabile a occhio su qualunque fotografia della rete; mostrarla vorrebbe dire
    /// aprire il messaggio ATIS e trovarci in cima un URL.</para>
    /// </summary>
    public static string Testo(IEnumerable<string>? righe)
    {
        if (righe is null) return "";
        var lista = righe.Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
        if (lista.Count == 0) return "";
        var utili = lista.Count > 1 ? lista.Skip(1) : lista;
        return string.Join(' ', utili).Replace('\t', ' ').Trim();
    }
}
