namespace Aip;

/// <summary>Il genere di un ostacolo di ENR 5.4.</summary>
public enum ObstacleKind
{
    /// <summary>Un ostacolo puntuale (ENR 5.4.1, sigla <c>T……</c>): aerogeneratori, antenne, tralicci, ciminiere.</summary>
    Point,

    /// <summary>Una piattaforma in mare o simile (ENR 5.4.2, sigla <c>P……</c>).</summary>
    Platform,

    /// <summary>Un ostacolo lineare (ENR 5.4.3, sigla <c>L……</c>): elettrodotti, funivie, cavi, fra un inizio e una fine.</summary>
    Line,
}

/// <summary>
/// Un ostacolo alla navigazione aerea di ENR 5.4 (carta «L'AIP intero», passo 4), com'è stampato nelle tabelle per
/// regione: dove sta (per un ostacolo lineare l'inizio e la fine), che cos'è in italiano e in inglese, le altezze in metri
/// e in piedi, la segnalazione ICAO di giorno e di notte. I numeri come l'AIP li dà; null dove la colonna non c'è.
/// </summary>
public sealed record Obstacle
{
    /// <summary>La sezione dell'AIP: <c>ENR 5.4.1</c>, <c>ENR 5.4.2</c>, <c>ENR 5.4.3</c>.</summary>
    public required string Section { get; init; }

    public required ObstacleKind Kind { get; init; }

    /// <summary>La sigla dell'AIP: <c>T004043</c>, <c>P000001</c>, <c>L003539</c>.</summary>
    public required string Designator { get; init; }

    /// <summary>La regione della tabella: <c>ABRUZZO</c> (le piattaforme non ne hanno).</summary>
    public string? Region { get; init; }

    public string? Province { get; init; }

    public string? Town { get; init; }

    /// <summary>La località: <c>CASTEL FRAIANO</c>, <c>MARE ADRIATICO</c>.</summary>
    public string? Site { get; init; }

    /// <summary>Il tipo in italiano: <c>AEROGENERATORE</c>, <c>ELETTRODOTTO</c>.</summary>
    public string? Type { get; init; }

    /// <summary>Il tipo in inglese: <c>AEOLIC PROPELLER</c>, <c>POWER LINE</c>.</summary>
    public string? TypeEnglish { get; init; }

    /// <summary>Dove sta; per un ostacolo lineare, l'inizio.</summary>
    public required Position Position { get; init; }

    /// <summary>La fine di un ostacolo lineare.</summary>
    public Position? End { get; init; }

    /// <summary>La lunghezza di un ostacolo lineare, in metri.</summary>
    public double? LengthM { get; init; }

    /// <summary>L'altezza sul terreno (AGL); per un ostacolo lineare quella massima del cavo.</summary>
    public double? HeightM { get; init; }

    public double? HeightFt { get; init; }

    /// <summary>L'elevazione della cima sul mare (AMSL), dove l'AIP la dà (puntuali e piattaforme).</summary>
    public double? ElevationM { get; init; }

    public double? ElevationFt { get; init; }

    /// <summary>Di un ostacolo lineare: l'elevazione del punto più basso (AMSL).</summary>
    public double? LowestM { get; init; }

    public double? LowestFt { get; init; }

    /// <summary>Di un ostacolo lineare: l'elevazione del punto più alto (AMSL).</summary>
    public double? HighestM { get; init; }

    public double? HighestFt { get; init; }

    /// <summary>La segnalazione ICAO di giorno com'è stampata: <c>YES</c>, <c>NO</c>, <c>Note RMKs</c> (vedi le note).</summary>
    public string? DayMarking { get; init; }

    /// <summary>La segnalazione ICAO di notte com'è stampata.</summary>
    public string? NightMarking { get; init; }

    /// <summary>Di una piattaforma, il nome: <c>BARBARA T2</c> (null se l'AIP scrive NIL).</summary>
    public string? Name { get; init; }

    /// <summary>Le note della tabella delle note in fondo alla regione (ostacoli lineari), per sigla.</summary>
    public string? Remarks { get; init; }

    public AipPage? Page { get; init; }
}
