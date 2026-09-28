using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Sectorfile;

/// <summary>
/// Un file <c>.mva</c> come l'ha letto la cache: il testo (null se il file non c'è) e la carta che ne esce.
/// </summary>
/// <remarks>⚠️ Il testo sta qui e non dentro <see cref="MvaChart"/>: la carta finisce congelata nelle release, e un
/// file intero dentro ogni sezione congelata sarebbe peso senza motivo.</remarks>
public sealed class MvaFile(string? testo, MvaChart carta)
{
    public string? Testo { get; } = testo;
    public MvaChart Carta { get; } = carta;

    /// <summary>Vero quando il testo è già stato confrontato con quello ricordato (U-037): basta una volta per
    /// caricamento, non una per lettura.</summary>
    public bool Riconciliato { get; set; }
}
