namespace Vipi.Domain.Entities;

/// <summary>Parti di una vLOA (bilaterale). Non usata per le vIPI. SPEC §3.10.</summary>
public class DocumentParty
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public Document? Document { get; set; }
    public int SectorId { get; set; }
    public Sector? Sector { get; set; }
    public PartyRole Role { get; set; }                // Home (IT, editabile) | Neighbour (sola lettura)
}
