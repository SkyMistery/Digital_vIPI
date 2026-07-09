namespace Vipi.Domain.Entities;

/// <summary>Un documento vIPI o vLOA. I contenuti vivono nelle versioni. SPEC_Modello_Dati §3.9.</summary>
public class Document
{
    public int Id { get; set; }
    public DocumentType Type { get; set; }
    public string Title { get; set; } = default!;
    public Language Language { get; set; }             // It (vIPI) | En (vLOA) — fisso
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public int? CurrentVersionId { get; set; }         // versione pubblicata corrente
    public DocumentVersion? CurrentVersion { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
    public string LastUpdatedAiracCycle { get; set; } = default!; // calcolato da AiracService, es. "2606"
    public int? FeaturedRank { get; set; }             // ordine "in evidenza" (1..3) nella card vLOA della landing ACC; null = non in evidenza

    /// <summary>Nascosto dal pubblico (reversibile): il documento resta con la sua storia ma i loader pubblici lo escludono.</summary>
    public bool IsHidden { get; set; }

    public byte[]? RowVersion { get; set; }

    // Lock di editing esclusivo (PIANO sicurezza): impedisce a due editor di lavorare lo stesso documento.
    public int? LockedByUserId { get; set; }
    public string? LockedByName { get; set; }
    public DateTime? LockedAtUtc { get; set; }
    public DateTime? LockExpiresUtc { get; set; }

    public ICollection<DocumentParty> Parties { get; set; } = new List<DocumentParty>();
    public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();

    /// <summary>Settori descritti da questo documento (uno-a-molti). Per le vIPI; uno è IsPrimary. SPEC §3.9.</summary>
    public ICollection<Sector> Sectors { get; set; } = new List<Sector>();
}

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

/// <summary>
/// Release AIRAC di un documento: snapshot delle SOLE scelte editoriali (<see cref="PayloadJson"/>) pubblicato con
/// un ciclo AIRAC di rilascio. Modello unico per tutti i tipi (vLOA/vIPI ACC/APP/Aeroporto): lo stato live resta la
/// bozza sempre aperta, la release ne congela una fotografia. Il pubblico vede la release con
/// <see cref="ReleaseEffectiveUtc"/> &lt;= adesso più recente; le future sono schedulate. I dati derivati
/// (poligoni/frequenze/gerarchia/trasferimenti) NON sono nello snapshot: si renderizzano sempre coi cataloghi correnti.
/// </summary>
public class DocRelease
{
    public int Id { get; set; }
    public ReleaseTargetType TargetType { get; set; }

    /// <summary>Chiave del bersaglio: vLOA = documentId; AccVipi = "{accCode}|{rootCallsign}"; App = callsign settore; Airport = ICAO.</summary>
    public string TargetKey { get; set; } = default!;

    public int VersionNumber { get; set; }                 // progressivo per (TargetType, TargetKey)
    public string ReleaseAiracCycle { get; set; } = default!;   // "YYNN"
    public DateTime ReleaseEffectiveUtc { get; set; }      // chiave di selezione ordinabile (data efficace del ciclo)
    public ReleaseStatus Status { get; set; } = ReleaseStatus.Scheduled;

    /// <summary>Snapshot serializzato delle scelte editoriali (shape dipende dal <see cref="TargetType"/>).</summary>
    public string PayloadJson { get; set; } = default!;

    public int CreatedByUserId { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string? Note { get; set; }
}

/// <summary>Versione immutabile di un documento (audit + diff). SPEC §3.11.</summary>
public class DocumentVersion
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public Document? Document { get; set; }
    public int VersionNumber { get; set; }             // progressivo per documento
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public int CreatedByUserId { get; set; }              // autore (UserId IVAO)
    public DateTime CreatedUtc { get; set; }
    public string AiracCycle { get; set; } = default!;
    public string? Note { get; set; }                  // changelog

    public ICollection<DocumentSection> Sections { get; set; } = new List<DocumentSection>();
    public ICollection<ContentBlock> Blocks { get; set; } = new List<ContentBlock>();
}

/// <summary>Sezione ad albero (annidamento max 3 livelli). Genera la TOC dinamica. SPEC §7.1.</summary>
public class DocumentSection
{
    public int Id { get; set; }
    public int DocumentVersionId { get; set; }
    public DocumentVersion? DocumentVersion { get; set; }
    public int? ParentSectionId { get; set; }          // null = sezione radice
    public DocumentSection? ParentSection { get; set; }
    public string Title { get; set; } = default!;
    public int Order { get; set; }                     // ordine tra fratelli
    public int Depth { get; set; }                     // 0 = radice … max 3 (vincolo applicativo)
    public BlockSection SectionKind { get; set; }

    public byte[]? RowVersion { get; set; }                // concorrenza ottimistica in editing

    public ICollection<DocumentSection> Children { get; set; } = new List<DocumentSection>();
    public ICollection<ContentBlock> Blocks { get; set; } = new List<ContentBlock>();

    /// <summary>Profondità massima consentita per l'albero delle sezioni (SPEC §7.1).</summary>
    public const int MaxDepth = 3;
}

/// <summary>Unità minima di documentazione. Cuore del modello di visibilità (PIANO §20). SPEC §3.12 + §7.2.</summary>
public class ContentBlock
{
    public int Id { get; set; }
    public int DocumentVersionId { get; set; }
    public DocumentVersion? DocumentVersion { get; set; }
    public int SectionId { get; set; }                 // FK→DocumentSection (ex enum Section)
    public DocumentSection? Section { get; set; }
    public int Order { get; set; }
    public BlockTier Tier { get; set; }
    public BlockFormat Format { get; set; }
    public BlockVisibility Visibility { get; set; }
    public bool CollapsedByDefault { get; set; }       // collasso di presentazione in vista ridotta
    public CalloutKind? CalloutKind { get; set; }      // solo se Format=Callout

    public int? ScopeSectorId { get; set; }            // settore a cui il blocco si riferisce
    public Sector? ScopeSector { get; set; }
    public int? FromSectorId { get; set; }             // solo coordinamenti (Handoff relazionale)
    public Sector? FromSector { get; set; }
    public int? ToSectorId { get; set; }               // solo coordinamenti
    public Sector? ToSector { get; set; }

    public int? SharedBlockId { get; set; }            // se riusato per riferimento
    public SharedBlock? SharedBlock { get; set; }
    public string? Body { get; set; }                  // Markdown (prosa); null se usa SharedBlock
    public string? BodyJson { get; set; }              // struttura tabellare (Format=Table)
    public byte[]? RowVersion { get; set; }            // concorrenza ottimistica in editing
}

/// <summary>Contenuto condiviso per riferimento (modifica una volta, aggiorna ovunque). SPEC §3.13.</summary>
public class SharedBlock
{
    public int Id { get; set; }
    public string Key { get; set; } = default!;        // univoco, es. "minime-separazione-generali"
    public string Title { get; set; } = default!;
    public BlockFormat Format { get; set; }
    public string? Body { get; set; }
    public string? BodyJson { get; set; }
    public byte[]? RowVersion { get; set; }
}
