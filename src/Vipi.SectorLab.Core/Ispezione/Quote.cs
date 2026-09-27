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

    /// <summary>2500 → «2 500 ft»: le migliaia con lo spazio, come nelle carte (il punto sembrerebbe un decimale).</summary>
    private static string InPiediLeggibili(int piedi)
        => piedi.ToString("#,0", new NumberFormatInfo { NumberGroupSeparator = " " }) + " ft";

    [GeneratedRegex(@"^FL(?<n>\d{1,3})$")]
    private static partial Regex LivelloDiVolo();

    [GeneratedRegex(@"^(?<n>-?\d+)(FT|')$")]
    private static partial Regex InPiedi();
}
