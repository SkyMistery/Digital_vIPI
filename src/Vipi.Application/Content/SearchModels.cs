using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Filtro per tipo nella ricerca full-text.</summary>
public enum SearchScope { All, Vipi, Airport, Vloa }

/// <summary>Singolo risultato di ricerca con contesto e deep-link.</summary>
public sealed class SearchHit
{
    public required string DocTitle { get; init; }
    public required DocumentType DocType { get; init; }
    /// <summary>Percorso leggibile, es. "vIPI Roma ACC › Coordinamenti › Settore NE".</summary>
    public required string Where { get; init; }
    /// <summary>Estratto con il termine evidenziato (testo grezzo; l'evidenziazione la fa la UI in modo sicuro).</summary>
    public required string Snippet { get; init; }
    /// <summary>Rotta di consultazione + ancora di sezione, es. "/sop/lirr/vipi#s-23".</summary>
    public required string Url { get; init; }
}
