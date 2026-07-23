namespace Vipi.Domain.Entities;

/// <summary>
/// Stato editoriale data-driven di un documento vIPI (1:1 col <see cref="Document"/>), per le sezioni DERIVATE.
/// Le sezioni AoR/Frequenze/Coordinamenti sono calcolate live dagli import; qui vivono solo le scelte dello staff che
/// non appartengono al testo: settori/frequenze nascosti, ordine frequenze, link frequenza extra.
/// Side-entity unificata per tutti i tipi (vLOA, APP, …): ha assorbito il vecchio <c>VloaProfile</c> in 08i.
/// Doc refactor 08e/08i.
/// </summary>
public class DocumentProfile
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    /// <summary>Callsign dei settori nascosti dalla mappa AoR (JSON array).</summary>
    public string? HiddenAorSectorsJson { get; set; }

    /// <summary>Callsign dei settori le cui frequenze sono nascoste dalla tabella (JSON array).</summary>
    public string? HiddenFrequenciesJson { get; set; }

    /// <summary>Chiavi (SectionCatalog) delle sezioni nascoste dal documento pubblicato (JSON array).</summary>
    public string? HiddenSectionsJson { get; set; }

    /// <summary>Override d'ordine delle frequenze per callsign (JSON di AppFreqOrderOverride).</summary>
    public string? FreqOrderJson { get; set; }

    /// <summary>Id dei settori sorgente dei link frequenza extra (JSON array di int).</summary>
    public string? FreqLinksJson { get; set; }

    public byte[]? RowVersion { get; set; }
}
