namespace Vipi.Domain.Entities;

/// <summary>
/// Policy globale (riga singola, Id=1) che decide quali categorie di dati arrivano dalla sorgente esterna.
/// Semantica opt-out: <c>true</c> = importato e bloccato (sorgente autorevole, sola lettura per l'utente);
/// <c>false</c> = escluso (gestito a mano, l'import non lo tocca). Default tutto <c>true</c>.
/// Le categorie editoriali (regole pista, SID, livelli TL, link, ecc.) non sono qui: sempre dell'utente.
/// </summary>
public class ImportPolicy
{
    public int Id { get; set; }                                   // riga singola: Id = 1
    public bool ImportTransitionAltitude { get; set; } = true;    // Airport.TransitionAltitudeFt
    public bool ImportRunways { get; set; } = true;               // AirportRunway.Ident/LengthM/Bearing
    public bool ImportSectors { get; set; } = true;               // Sector.Callsign/Type/DefaultFrequency
    public bool ImportSids { get; set; } = true;                  // AirportSid dal sectorfile Aurora (GitHub)
    public DateTime UpdatedUtc { get; set; }
    public int UpdatedByUserId { get; set; }
}

/// <summary>
/// Ultima esecuzione RIUSCITA di un import periodico, per categoria (Acc/AirportSector/SpecialArea/Sid).
/// Persistente: gli hosted service saltano il fetch all'avvio se ancora "fresco" (entro il periodo), evitando
/// di richiamare la sorgente a ogni riavvio dell'app.
/// </summary>
public class ImportState
{
    public string Category { get; set; } = default!;   // chiave naturale
    public DateTime LastSuccessUtc { get; set; }
}
