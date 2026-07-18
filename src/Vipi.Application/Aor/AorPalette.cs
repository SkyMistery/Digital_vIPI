namespace Vipi.Application.Aor;

/// <summary>
/// Palette unica degli anelli AoR (ciclata per indice). Fonte condivisa tra la derivazione
/// ACC (<c>AccDerivationService</c>) e APP (<c>AppDocumentService</c>): blu IVAO + varianti,
/// coerente col mockup. Un solo ordine canonico evita drift cromatico tra le viste.
/// </summary>
public static class AorPalette
{
    /// <summary>Colori esadecimali degli anelli, in ordine di ciclatura.</summary>
    public static readonly IReadOnlyList<string> Colors = new[]
    {
        "#0D2C99", "#3C55AC", "#7EA2D6", "#5B8C5A", "#C77D3C", "#8E5BA6", "#B0413E",
    };

    /// <summary>Colore dell'anello per l'indice dato (ciclato modulo lunghezza palette).</summary>
    public static string ColorAt(int index) => Colors[((index % Colors.Count) + Colors.Count) % Colors.Count];
}
