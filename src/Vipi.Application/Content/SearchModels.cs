using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Filtro per tipo nella ricerca full-text. <see cref="Vipi"/> = la vIPI di ACC; gli APP non remotizzati
/// hanno il loro (doc 13 §3e), prima finivano mescolati alle ACC.</summary>
/// <summary>
/// Il taglio della ricerca. ⚠️ <c>Mil</c> è in CODA: qui l'ordinale non è persistito da nessuna parte, ma la
/// regola vale lo stesso — la ricerca ricorda il taglio nella query string, e spostare un valore
/// reinterpreterebbe i collegamenti salvati.
/// <para>⚠️ Un tipo di documento senza un taglio suo <b>non sparisce</b> — <c>All</c> lo mostra — ma diventa
/// impossibile cercarlo da solo, ed è quello che è successo ai vSOP militari fino al 29 agosto 2026: erano
/// anche esclusi da «Aeroporti», perché quel taglio guarda il TIPO di release e non l'ICAO.</para>
/// </summary>
public enum SearchScope { All, Vipi, App, Airport, Vloa, Mil }

/// <summary>Singolo risultato di ricerca con contesto e deep-link.</summary>
public sealed class SearchHit
{
    public required string DocTitle { get; init; }
    public required DocumentType DocType { get; init; }
    /// <summary>Percorso leggibile, es. "vIPI Roma ACC › Coordinamenti › Settore NE".</summary>
    public required string Where { get; init; }
    /// <summary>Estratto con il termine evidenziato (testo grezzo; l'evidenziazione la fa la UI in modo sicuro).</summary>
    public required string Snippet { get; init; }
    /// <summary>Rotta di consultazione + ancora di sezione, es. "/services/vsop/lirr/vipi#s-23".</summary>
    public required string Url { get; init; }
}
