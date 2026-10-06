using System.Globalization;
using System.Text.RegularExpressions;

namespace Vipi.SectorLab.Core.Ispezione;

/// <summary>
/// L'editor della quota (lotto «Subito», slice 3b): l'AOD scrive la quota come la legge sulla carta — <c>FL80</c>,
/// <c>2500ft</c> — o come la vuole il campo, e il Lab la scrive nell'unità del campo: piedi (elevazioni, altitudine di
/// transizione) o centinaia di piedi (le MVA di ACC: <c>25</c> = 2 500 ft).
/// </summary>
public static partial class Quote
{
    /// <summary>
    /// Il testo da scrivere nel campo, o false col perché. Un numero senza unità è già nell'unità del campo (nelle
    /// centinaia, <c>25</c> resta <c>25</c>): è come l'AOD legge il file.
    /// </summary>
    public static bool Leggi(string? testo, bool inCentinaia, out string scritto, out string? perche)
    {
        scritto = "";
        perche = null;
        string t = (testo ?? "").Trim().Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();

        int piedi;
        if (LivelloDiVolo().Match(t) is { Success: true } fl)
        {
            piedi = int.Parse(fl.Groups["n"].Value, CultureInfo.InvariantCulture) * 100;
        }
        else if (InPiedi().Match(t) is { Success: true } ft)
        {
            piedi = int.Parse(ft.Groups["n"].Value, CultureInfo.InvariantCulture);
        }
        else if (int.TryParse(t, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int numero))
        {
            scritto = numero.ToString(CultureInfo.InvariantCulture);
            return true;
        }
        else
        {
            perche = inCentinaia
                ? $"«{testo}» non è una quota: si scrive FL80, 2500ft o in centinaia di piedi (25 = 2 500 ft)."
                : $"«{testo}» non è una quota: si scrive FL80, 2500ft o in piedi (2500).";
            return false;
        }

        if (!inCentinaia)
        {
            scritto = piedi.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (piedi % 100 != 0)
        {
            perche = $"{InPiediLeggibili(piedi)} non si scrive in centinaia di piedi: questo campo vuole 25 per 2 500 ft.";
            return false;
        }

        scritto = (piedi / 100).ToString(CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>Cosa vuol dire il valore di un campo in centinaia («= 2 500 ft»), o null se non è un numero.</summary>
    public static string? Significato(string? valore, bool inCentinaia)
        => inCentinaia && int.TryParse(valore?.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int centinaia)
            ? "= " + InPiediLeggibili(centinaia * 100)
            : null;

    /// <summary>I valori che una zona MVA può avere al posto di una quota, come li scrive il fork.</summary>
    public static IReadOnlyList<string> SpecialiDelleMva { get; } = ["TRL", "NO MINIMA"];

    /// <summary>
    /// La quota di una zona MVA (lotto «Subito» slice 15c; E2, S2): sempre in centinaia di piedi (<c>25</c>), scritta
    /// anche <c>FL85</c>, <c>2500ft</c> o <c>2500</c> (una quota piena senza unità è in piedi: nelle centinaia nessuna
    /// MVA arriva a 1 000); oppure <c>TRL</c>, <c>NO MINIMA</c>, <c>70/TRL</c> (la quota, o il livello di transizione),
    /// <c>*30/40</c> (com'è: una nota della carta).
    /// </summary>
    public static bool LeggiDiMva(string? testo, out string scritto, out string? perche)
    {
        scritto = "";
        perche = null;
        string t = string.Join(' ', (testo ?? "").Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        string compatto = t.Replace(" ", "", StringComparison.Ordinal);
        if (SpecialiDelleMva.FirstOrDefault(s => s.Replace(" ", "", StringComparison.Ordinal) == compatto) is { } speciale)
        {
            scritto = speciale;
            return true;
        }

        if (ConNota().IsMatch(compatto))
        {
            scritto = compatto;
            return true;
        }

        if (compatto.EndsWith("/TRL", StringComparison.Ordinal) && Centinaia(compatto[..^4], out string prima, out _))
        {
            scritto = prima + "/TRL";
            return true;
        }

        if (Centinaia(compatto, out scritto, out string? nonVa))
            return true;
        perche = nonVa ?? $"«{testo}» non è la quota di una MVA: si scrive in centinaia di piedi (25 = 2 500 ft; anche FL85 o 2500ft), oppure TRL, NO MINIMA, 70/TRL.";
        return false;
    }

    // Una quota in centinaia: il numero com'è fino a 660, in piedi sopra (2500 → 25); FL e ft come in Leggi.
    private static bool Centinaia(string compatto, out string scritto, out string? perche)
    {
        scritto = "";
        perche = null;
        if (int.TryParse(compatto, NumberStyles.None, CultureInfo.InvariantCulture, out int numero))
        {
            if (numero <= 660)
            {
                scritto = numero.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            compatto += "FT";
        }

        if (!Leggi(compatto, inCentinaia: true, out scritto, out string? nonVa))
        {
            // «2550 ft non si scrive in centinaia» si capisce; «non è una quota» per un testo qualunque la dice chi chiama.
            perche = nonVa is not null && nonVa.Contains("centinaia di piedi: questo campo", StringComparison.Ordinal)
                ? nonVa + " Oppure TRL, NO MINIMA, 70/TRL."
                : null;
            return false;
        }

        return true;
    }

    /// <summary>Cosa vuol dire la quota di una zona MVA, o null se è vuota.</summary>
    public static string? SignificatoDiMva(string? valore)
    {
        string v = (valore ?? "").Trim().ToUpperInvariant();
        if (v.Length == 0)
            return null;
        if (v == "TRL")
            return "= il livello di transizione";
        if (v == "NO MINIMA")
            return "= nessuna minima di vettoramento";
        if (v.EndsWith("/TRL", StringComparison.Ordinal) && int.TryParse(v[..^4], NumberStyles.None, CultureInfo.InvariantCulture, out int prima))
            return $"= {InPiediLeggibili(prima * 100)}, o il livello di transizione se è più alto";
        if (int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out int numero))
        {
            return numero <= 660
                ? "= " + InPiediLeggibili(numero * 100)
                : $"scritta in piedi ({InPiediLeggibili(numero)}): le MVA si scrivono in centinaia, {numero / 100}";
        }

        if (LivelloDiVolo().Match(v) is { Success: true } fl)
            return $"scritta come livello di volo: le MVA si scrivono in centinaia, {int.Parse(fl.Groups["n"].Value, CultureInfo.InvariantCulture)}";
        return ConNota().IsMatch(v) ? "= una nota della carta, com'è scritta" : "non è una quota in centinaia né un valore speciale (TRL, NO MINIMA, 70/TRL)";
    }

    [GeneratedRegex(@"^\*\d{1,3}(/\d{1,3})?$")]
    private static partial Regex ConNota();

    /// <summary>2500 → «2 500 ft»: le migliaia con lo spazio, come nelle carte (il punto sembrerebbe un decimale).</summary>
    private static string InPiediLeggibili(int piedi)
        => piedi.ToString("#,0", new NumberFormatInfo { NumberGroupSeparator = " " }) + " ft";

    [GeneratedRegex(@"^FL(?<n>\d{1,3})$")]
    private static partial Regex LivelloDiVolo();

    [GeneratedRegex(@"^(?<n>-?\d+)(FT|')$")]
    private static partial Regex InPiedi();
}
