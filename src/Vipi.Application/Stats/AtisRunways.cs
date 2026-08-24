using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Vipi.Application.Stats;

/// <summary>Le piste dichiarate da un ATIS: arrivi e partenze, come stringhe già normalizzate.</summary>
/// <param name="Arrival">Es. <c>16L/16R</c>; vuoto se l'ATIS non lo dice.</param>
/// <param name="Departure">Es. <c>25</c>; vuoto se l'ATIS non lo dice.</param>
public readonly record struct RunwaysInUse(string Arrival, string Departure)
{
    public static readonly RunwaysInUse Nessuna = new("", "");

    public bool Vuoto => Arrival.Length == 0 && Departure.Length == 0;

    /// <summary>Come si scrive a video: «16L/16R arr · 25 dep», o solo quel che si sa.</summary>
    public override string ToString() => (Arrival, Departure) switch
    {
        ("", "") => "",
        (var a, "") => a,
        ("", var d) => d,
        var (a, d) when a == d => a,
        var (a, d) => $"{a} → {d}",
    };
}

/// <summary>
/// Legge le piste in uso dal testo dell'ATIS.
///
/// <para><b>Perché dal testo.</b> La fotografia della rete porta l'ATIS di <b>ogni</b> ATC (misurato: 71 su
/// 71), e in 48 casi su 71 la pista è scritta lì dentro. Non c'è un campo strutturato: c'è una frase, e le
/// frasi che le divisioni scrivono sono poche e ricorrenti — «<i>Arrival runway 16L 16R departure runway
/// 25</i>», «<i>Runway in use 04R</i>».</para>
///
/// <para>⚠️ <b>Non si legge una volta sola.</b> Le piste cambiano <i>durante</i> il turno: registrare il
/// valore del primo giro e chiamarlo «la pista della sessione» sarebbe falso per metà turno. Chi usa questo
/// parser confronta a ogni giro e scrive una riga nuova <b>quando cambia</b> (nota del committente,
/// 25 agosto 2026).</para>
///
/// <para>Quando la frase non si riconosce si restituisce <see cref="RunwaysInUse.Nessuna"/>: meglio non dire
/// niente che indovinare una pista.</para>
///
/// <para>Puro e deterministico, nessun I/O.</para>
/// </summary>
public static class AtisRunways
{
    // Una pista è due cifre più un suffisso opzionale: 04R, 16L, 25, 34C.
    private const string Pista = @"\d{2}[LRC]?";

    private static readonly Regex Arrivo = new(
        $@"\barr(?:ival|iving)?s?\s+(?:runway|rwy)s?\s+((?:{Pista}[\s,and]*)+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Partenza = new(
        $@"\bdep(?:arture|arting)?s?\s+(?:runway|rwy)s?\s+((?:{Pista}[\s,and]*)+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // «Runway in use 04R», «RWY in use 16L 16R»: una sola dichiarazione che vale per tutti e due i versi.
    private static readonly Regex InUso = new(
        $@"\b(?:runway|rwy)s?\s+in\s+use\s+((?:{Pista}[\s,and]*)+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Numeri = new(Pista, RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Le piste dichiarate nelle righe di un ATIS.</summary>
    public static RunwaysInUse Leggi(IEnumerable<string>? righe)
    {
        if (righe is null) return RunwaysInUse.Nessuna;

        var testo = string.Join(' ', righe.Where(r => !string.IsNullOrWhiteSpace(r)));
        if (testo.Length == 0) return RunwaysInUse.Nessuna;

        var arrivo = Piste(Arrivo, testo);
        var partenza = Piste(Partenza, testo);

        if (arrivo.Length == 0 && partenza.Length == 0)
        {
            // ⚠️ «in use» si guarda solo DOPO arrivi/partenze: la frase di Fiumicino contiene tutt'e due i
            // versi, e una dichiarazione generica letta per prima li appiattirebbe in uno.
            var unica = Piste(InUso, testo);
            return unica.Length == 0 ? RunwaysInUse.Nessuna : new RunwaysInUse(unica, unica);
        }

        return new RunwaysInUse(arrivo, partenza);
    }

    private static string Piste(Regex regola, string testo)
    {
        var m = regola.Match(testo);
        if (!m.Success) return "";

        var trovate = Numeri.Matches(m.Groups[1].Value)
            .Select(x => x.Value.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return string.Join('/', trovate);
    }
}
