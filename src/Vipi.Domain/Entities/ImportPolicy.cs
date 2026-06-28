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
    public bool ImportAtis { get; set; } = true;                  // Airport.AtisFrequency
    public bool ImportRunways { get; set; } = true;               // AirportRunway.Ident/LengthM/Bearing
    public bool ImportSectors { get; set; } = true;               // Sector.Callsign/Type/DefaultFrequency
    public DateTime UpdatedUtc { get; set; }
    public int UpdatedByUserId { get; set; }
}
