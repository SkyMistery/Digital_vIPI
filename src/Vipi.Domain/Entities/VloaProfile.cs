namespace Vipi.Domain.Entities;

/// <summary>
/// Stato editoriale (data-driven) di una vLOA, 1:1 col <see cref="Document"/>. Analogo di <c>AppProfile</c> per l'APP:
/// le sezioni AoR/Frequenze/Coordinamenti della vLOA sono DERIVATE dai dati (settori confinanti dei due ACC), qui si
/// conservano solo le scelte dello staff: quali settori nascondere dall'AoR, quali frequenze nascondere, l'ordine freq.
/// </summary>
public class VloaProfile
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    /// <summary>Callsign dei settori (home o estero) nascosti dalla mappa AoR (JSON array).</summary>
    public string? HiddenAorSectorsJson { get; set; }

    /// <summary>Callsign dei settori le cui frequenze sono nascoste dalla tabella (JSON array).</summary>
    public string? HiddenFrequenciesJson { get; set; }

    /// <summary>Titoli delle sezioni nascoste dal documento pubblicato (JSON array).</summary>
    public string? HiddenSectionsJson { get; set; }

    /// <summary>Override d'ordine delle frequenze per callsign (JSON di AppFreqOrderOverride).</summary>
    public string? FreqOrderJson { get; set; }

    public byte[]? RowVersion { get; set; }
}
