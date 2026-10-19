namespace Aip;

/// <summary>Il genere di un pericolo puntuale di ENR 5.3.</summary>
public enum HazardKind
{
    /// <summary>Lancio di palloni per radiosondaggi (ENR 5.3.1).</summary>
    Balloon,

    /// <summary>Emissione di raggi laser o fasci luminosi (ENR 5.3.2).</summary>
    Laser,

    /// <summary>Vulcano attivo (ENR 5.3.3).</summary>
    Volcano,
}

/// <summary>
/// Un pericolo di ENR 5.3 (carta F6, «finiamo con l'importer»): un luogo, non un'area con limiti — dove sta, il raggio se
/// l'AIP lo dà («Cerchio di raggio 16KM centrato su»), la quota interessata, l'orario e le note com'è stampati.
/// </summary>
public sealed record Hazard
{
    /// <summary>La sezione dell'AIP: <c>ENR 5.3.1</c>.</summary>
    public required string Section { get; init; }

    public required HazardKind Kind { get; init; }

    /// <summary>Il nome com'è stampato: <c>AREA ETNEA (A)</c>, <c>Lampedusa</c>, <c>ETNA</c>.</summary>
    public required string Name { get; init; }

    public required Position Position { get; init; }

    /// <summary>Il raggio del cerchio, dove l'AIP lo dà.</summary>
    public Distance? Radius { get; init; }

    /// <summary>La quota interessata o l'altezza com'è stampata: <c>20000 m</c>, <c>GND/ 30 KM</c>, <c>3330m 10922ft</c>.</summary>
    public string? Vertical { get; init; }

    /// <summary>L'orario com'è stampato: <c>H24</c>, <c>0000 0600 1200 1800</c>.</summary>
    public string? Hours { get; init; }

    /// <summary>Le note com'è stampate.</summary>
    public string? Remarks { get; init; }

    public AipPage? Page { get; init; }
}
