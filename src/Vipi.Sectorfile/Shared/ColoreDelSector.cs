using System.Drawing;
using System.Globalization;

namespace Vipi.Sectorfile.Shared;

/// <summary>Come è scritto un colore nel sector (manuale IVAO del sector, «Colour Definitions»).</summary>
public enum FormaDelColore
{
    /// <summary><c>#RRGGBB</c>: opaco.</summary>
    Esadecimale,

    /// <summary><c>#AARRGGBB</c>, da Aurora 1.4.1: l'opacità si vede solo con <i>Smooth Drawing</i>.</summary>
    EsadecimaleConOpacita,

    /// <summary><c>R,G,B</c> (decimali).</summary>
    Rgb,

    /// <summary><c>%R:G:B</c> (decimali), la forma degli esempi di <c>[DEFINE]</c>.</summary>
    Percento,
}

/// <summary>
/// Un valore di colore scritto in un file del sector: nei <c>.def</c>, nelle teste dei <c>.tfl</c>/<c>.pol</c>, nel 5°
/// campo dei <c>.geo</c>. Le quattro forme sono quelle del manuale IVAO del sector («Colour Definitions»):
/// <c>#18b76c</c>, <c>#08466717</c> (AARRGGBB), <c>24,183,108</c>, <c>%48:98:0</c>. Un nome (<c>TAXIWAY</c>) non è
/// un valore: lo risolve <c>colors.def</c> o lo schema di Aurora, non questa classe.
/// </summary>
public static class ColoreDelSector
{
    /// <summary>Legge un valore in una delle quattro forme; falso per un nome o un testo che non è un colore.</summary>
    public static bool TryLeggi(string? testo, out Color colore, out FormaDelColore forma)
    {
        colore = Color.Empty;
        forma = FormaDelColore.Esadecimale;
        if (string.IsNullOrWhiteSpace(testo))
            return false;

        string t = testo.Trim();
        if (t.StartsWith('#'))
        {
            string cifre = t[1..];
            if ((cifre.Length != 6 && cifre.Length != 8)
                || !uint.TryParse(cifre, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint valore))
                return false;

            if (cifre.Length == 6)
            {
                colore = Color.FromArgb(0xFF, (int)(valore >> 16) & 0xFF, (int)(valore >> 8) & 0xFF, (int)valore & 0xFF);
                return true;
            }

            forma = FormaDelColore.EsadecimaleConOpacita;
            colore = Color.FromArgb((int)(valore >> 24) & 0xFF, (int)(valore >> 16) & 0xFF, (int)(valore >> 8) & 0xFF,
                                    (int)valore & 0xFF);
            return true;
        }

        bool percento = t.StartsWith('%');
        string[] parti = (percento ? t[1..] : t).Split(percento ? ':' : ',');
        if (parti.Length != 3)
            return false;

        var canali = new int[3];
        for (int i = 0; i < 3; i++)
        {
            if (!int.TryParse(parti[i].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out canali[i])
                || canali[i] > 255)
                return false;
        }

        forma = percento ? FormaDelColore.Percento : FormaDelColore.Rgb;
        colore = Color.FromArgb(0xFF, canali[0], canali[1], canali[2]);
        return true;
    }

    /// <summary>Come <see cref="TryLeggi(string?, out Color, out FormaDelColore)"/>, senza la forma.</summary>
    public static bool TryLeggi(string? testo, out Color colore) => TryLeggi(testo, out colore, out _);

    /// <summary>
    /// Il valore da scrivere in una riga: <c>#RRGGBB</c> maiuscolo, o <c>#AARRGGBB</c> se il colore non è opaco
    /// (le maiuscole sono quelle di <c>colors.def</c>).
    /// </summary>
    public static string Scrivi(Color colore)
        => colore.A == 0xFF
            ? string.Create(CultureInfo.InvariantCulture, $"#{colore.R:X2}{colore.G:X2}{colore.B:X2}")
            : string.Create(CultureInfo.InvariantCulture, $"#{colore.A:X2}{colore.R:X2}{colore.G:X2}{colore.B:X2}");
}
