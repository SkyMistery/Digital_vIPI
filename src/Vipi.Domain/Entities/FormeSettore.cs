namespace Vipi.Domain.Entities;

/// <summary>
/// Un <b>pezzo</b> della forma di un settore: un anello <b>e le sue quote</b>, con la fonte che lo ha scritto.
///
/// <para>Prende il posto della colonna <c>RegionMapPolygon</c> — che teneva <b>un anello solo</b>, e con esso
/// due quote sciolte in <c>LowerLimit</c>/<c>UpperLimit</c> — e nasce da un caso vero: Amendola è di due zone
/// e Catania di sette, ognuna con la sua banda. Carta
/// <c>docs/refactor/15-shape-del-settore-una-porta-sola.md</c>.</para>
///
/// <para>⚠️ <b>Le quote stanno DENTRO il pezzo, ed è tutto il progetto.</b> Chi ha in mano un anello ha già in
/// mano le sue quote: «prendere il confine laterale da una fonte e quello verticale da un'altra» non è una
/// cosa da evitare con attenzione, è una cosa che <b>non si può scrivere</b>.</para>
///
/// <para>⚠️ <b>La regola d'oro</b>, che vive nella firma di <c>ISectorShapeParts.ReplacePartsAsync</c>: un
/// import scrive solo i pezzi della <b>propria</b> fonte e non cancella mai quelli di un'altra. È ciò che
/// rende l'aggancio all'AIP <b>reversibile</b>: i pezzi di IVAO non se ne vanno mentre l'AIP è attivo, quindi
/// lo sgancio non ha niente da ri-importare.</para>
/// </summary>
public class SectorShapePart
{
    public int Id { get; set; }

    /// <summary>Da quale catalogo viene il settore: subcenter di un ACC o postazione d'aeroporto.</summary>
    public SourceCatalog Catalog { get; set; }

    /// <summary>
    /// L'id nel catalogo. ⚠️ È questo l'indirizzo del settore, non il callsign: i callsign si rinominano, e
    /// alla sorgente i due cataloghi sono due sequenze che si sovrappongono.
    /// </summary>
    public int SectorId { get; set; }

    /// <summary>Il callsign, maiuscolo: come si cerca e come si mostra. Denormalizzato di proposito.</summary>
    public string Callsign { get; set; } = default!;

    /// <summary>Chi ha scritto questo pezzo. È la colonna su cui la regola d'oro fa perno.</summary>
    public ShapeSource Source { get; set; }

    /// <summary>In vigore o in attesa del ciclo AIRAC.</summary>
    public ShapePartState State { get; set; }

    /// <summary>L'ordine di disegno dentro la propria fonte: le zone di un CTR hanno un ordine scelto.</summary>
    public int Ordinal { get; set; }

    /// <summary>L'anello, nella forma <c>regionMapPolygon</c> (lon prima di lat): si dà in pasto a
    /// <c>PolygonGeometry</c> e ad <c>AorPolygonProjector</c> senza conversioni.</summary>
    public string PolygonJson { get; set; } = default!;

    /// <summary>Base in piedi; <c>null</c> = suolo.</summary>
    public int? BaseFeet { get; set; }

    /// <summary>Tetto in piedi; <c>null</c> = illimitato.</summary>
    public int? TopFeet { get; set; }

    /// <summary>Rispetto a cosa è la base: suolo, mare, terreno o livello di volo.</summary>
    public AirspaceDatum BaseDatum { get; set; }

    public AirspaceDatum TopDatum { get; set; }

    /// <summary>La base come la dice la fonte: <c>GND</c>, <c>7000 FT AMSL</c>, <c>FL105</c>. Si mostra
    /// accanto al numero, perché il numero da solo perde il datum.</summary>
    public string BaseRaw { get; set; } = "";

    public string TopRaw { get; set; } = "";

    /// <summary>
    /// Il ciclo AIRAC (YYNN) dal quale l'insieme <c>Pending</c> entra in vigore. <c>null</c> su ogni pezzo
    /// <c>InForce</c>: lì il ciclo è già arrivato.
    /// </summary>
    public string? AiracCycle { get; set; }

    /// <summary>Pubblica in anticipo sul ciclo, per decisione umana (la correzione di un errore).</summary>
    public bool ForcePublished { get; set; }

    /// <summary>
    /// Da dove viene il pezzo, quando la fonte sa dirlo: per l'AIP è la <b>chiave naturale</b> del volume
    /// (<c>FAMIGLIA|NOME|BASE|TETTO</c>), la stessa che cita l'aggancio. Serve a ritrovare l'originale e a
    /// dire quale pezzo è rimasto scoperto quando un file nuovo non lo porta più.
    /// </summary>
    public string? SourceRef { get; set; }

    public DateTime WrittenUtc { get; set; }
}
