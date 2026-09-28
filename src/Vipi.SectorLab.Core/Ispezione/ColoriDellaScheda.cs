using System.Drawing;
using System.Globalization;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Ispezione;

/// <summary>Un nome di <c>colors.def</c> da scegliere nella scheda, col suo colore e (se lo ha) il significato.</summary>
public sealed record NomeDiColore(string Nome, string Esadecimale, string? Significato);

/// <summary>
/// Un colore scritto in un campo, come lo spiega la scheda.
/// </summary>
/// <param name="Esadecimale">Il colore per il campione e il selettore, <c>#rrggbb</c> minuscolo (come lo vuole
/// <c>&lt;input type=color&gt;</c>); null se non si sa che colore è.</param>
/// <param name="Opacita">L'opacità in percento (100 = pieno).</param>
/// <param name="Nome">Il nome di <c>colors.def</c>, se è un nome.</param>
/// <param name="Avviso">Cosa dire accanto al campo: il nome che nessuno conosce, l'opacità che vuole Smooth Drawing.</param>
public sealed record ColoreLetto(string? Esadecimale, int Opacita, string? Nome, string? Avviso)
{
    /// <summary>Vero se il valore non è né un nome di <c>colors.def</c> né un colore.</summary>
    public bool Sconosciuto => Esadecimale is null;
}

/// <summary>
/// Il selettore dei colori della scheda (lotto «Subito» slice 4, D2 e I1): un nome di <c>colors.def</c> del master, o un
/// colore scelto (<c>#RRGGBB</c>), o con l'opacità (<c>#AARRGGBB</c>, che si vede solo con <i>Smooth Drawing</i>). Le
/// teste di <c>.tfl</c> e <c>.pol</c> sono di <c>[FILLCOLOR]</c>: lì i nomi sono quelli di <c>colors.def</c>, lo schema
/// di Aurora non conta (come sulla mappa, <see cref="Mappa.ColoriDellaMappa"/>).
/// </summary>
public static class ColoriDellaScheda
{
    /// <summary>L'avviso del manuale IVAO del sector su <c>#AARRGGBB</c>.</summary>
    public const string SoloConSmoothDrawing =
        "l'opacità si vede solo con Smooth Drawing acceso (Aurora: PVD → OTHER → Smooth Drawing); senza, il colore è pieno";

    /// <summary>Legge il valore del campo coi nomi di <c>colors.def</c> del master.</summary>
    public static ColoreLetto Leggi(string? scritto, ColorPalette definiti)
    {
        ArgumentNullException.ThrowIfNull(definiti);
        string testo = (scritto ?? string.Empty).Trim();
        if (testo.Length == 0)
            return new ColoreLetto(null, 100, null, "vuoto: Aurora non ha un colore da usare");

        if (definiti.TryResolve(testo, out var definito))
            return Letto(definito.Value, definito.Name, $"{definito.Name} di colors.def");

        if (ColoreDelSector.TryLeggi(testo, out Color valore, out _))
            return Letto(valore, null, null);

        return new ColoreLetto(null, 100, null, $"«{testo}» non è un nome di colors.def né un colore: Aurora non sa come disegnarlo");
    }

    private static ColoreLetto Letto(Color colore, string? nome, string? chi)
    {
        int opacita = (int)Math.Round(colore.A * 100 / 255.0, MidpointRounding.AwayFromZero);
        string? avviso = colore.A == 0xFF ? null
            : $"{(chi is null ? "" : chi + ", ")}opacità {opacita}%: {SoloConSmoothDrawing}";
        return new ColoreLetto(Minuscolo(colore), opacita, nome, avviso);
    }

    /// <summary>I nomi di <c>colors.def</c> in ordine alfabetico, col significato che il campo dà loro (i riempimenti dei <c>.pol</c>).</summary>
    public static IReadOnlyList<NomeDiColore> Nomi(ColorPalette definiti, IReadOnlyList<ValoreFisso>? significati = null)
    {
        ArgumentNullException.ThrowIfNull(definiti);
        return
        [
            .. definiti.Entries.Values
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .Select(d => new NomeDiColore(d.Name, Minuscolo(d.Value),
                    significati?.FirstOrDefault(v => string.Equals(v.Valore, d.Name, StringComparison.OrdinalIgnoreCase))?.Significato)),
        ];
    }

    /// <summary>
    /// Il valore da scrivere per un colore del selettore (<c>#rrggbb</c>) e un'opacità in percento: <c>#RRGGBB</c> se è
    /// piena, se no <c>#AARRGGBB</c>. Null se il colore non si legge.
    /// </summary>
    public static string? DaScrivere(string? esadecimale, int opacita)
    {
        if (!ColoreDelSector.TryLeggi(esadecimale, out Color colore))
            return null;
        int alfa = (int)Math.Round(Math.Clamp(opacita, 0, 100) * 255 / 100.0, MidpointRounding.AwayFromZero);
        return ColoreDelSector.Scrivi(Color.FromArgb(alfa, colore.R, colore.G, colore.B));
    }

    private static string Minuscolo(Color c) => string.Create(CultureInfo.InvariantCulture, $"#{c.R:x2}{c.G:x2}{c.B:x2}");
}
