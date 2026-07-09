using Vipi.Domain;

namespace Vipi.Domain.Entities;

/// <summary>
/// Coppia di ACC confinanti candidata a diventare una vLOA. Prodotta dall'import degli ACC dei paesi vicini
/// (<c>Neighbours:CountryIds</c>) + calcolo di adiacenza geometrica dei settori: solo le coppie con settori
/// adiacenti (o da risolvere a mano perché senza poligono) vengono persistite qui — i dati esteri NON confinanti
/// non entrano nel DB. L'admin conferma/rifiuta; alla conferma si materializzano ACC/settore esteri e si genera
/// la vLOA (<see cref="VloaDocumentId"/>). Chiave naturale: (HomeAccCode, ForeignAccCode).
/// </summary>
public class NeighbourCandidate
{
    public int Id { get; set; }

    /// <summary>ACC della divisione (italiano), es. "LIMM". Lato Home (editabile) della futura vLOA.</summary>
    public string HomeAccCode { get; set; } = default!;

    /// <summary>ACC estero confinante, es. "LFMM". Lato Neighbour (read-only) della futura vLOA.</summary>
    public string ForeignAccCode { get; set; } = default!;

    /// <summary>Nome leggibile dell'ACC estero (da IVAO), es. "Marseille ACC".</summary>
    public string ForeignAccName { get; set; } = default!;

    /// <summary>CountryId IVAO del paese vicino (es. "FR"). Prefisso ICAO derivato per l'ACC estero.</summary>
    public string CountryId { get; set; } = default!;

    /// <summary>Callsign del settore-radice estero da materializzare come <c>Sector</c> (es. "LFMM_CTR").</summary>
    public string ForeignRootCallsign { get; set; } = default!;

    /// <summary>Poligono shape (JSON grezzo) del settore estero rappresentativo. Serve al calcolo adiacenza e a
    /// popolare la mappa; l'admin può incollarlo a mano quando IVAO non lo espone (fallback no-poligono).</summary>
    public string? RegionMapPolygon { get; set; }

    /// <summary>Distanza minima (NM) tra i bordi dei settori adiacenti IT/estero: confidence dell'adiacenza.
    /// null = coppia inserita/risolta a mano (poligono estero mancante).</summary>
    public double? MinDistanceNm { get; set; }

    /// <summary>Quante coppie di settori IT↔estero risultano adiacenti in questa coppia di ACC.</summary>
    public int AdjacentSectorCount { get; set; }

    /// <summary>Callsign (ComposePosition) dei settori domestici confinanti in questa coppia (JSON array). Persistiti
    /// dall'import per la derivazione della vLOA (AoR/frequenze), evitando il ricalcolo IVAO in fase di vista.</summary>
    public string? AdjacentHomeCallsigns { get; set; }

    /// <summary>Callsign (ComposePosition) dei settori esteri confinanti in questa coppia (JSON array).</summary>
    public string? AdjacentForeignCallsigns { get; set; }

    public NeighbourCandidateStatus Status { get; set; } = NeighbourCandidateStatus.Pending;

    /// <summary>Id della vLOA generata da questa coppia (idempotenza: se valorizzato, non rigenerare).</summary>
    public int? VloaDocumentId { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}
