namespace Vipi.Domain.Entities;

/// <summary>Un documento vIPI o vLOA. I contenuti vivono nelle versioni. SPEC_Modello_Dati §3.9.</summary>
public class Document
{
    public int Id { get; set; }
    public DocumentType Type { get; set; }
    public string Title { get; set; } = default!;
    public Language Language { get; set; }             // It (vIPI) | En (vLOA) — fisso
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    /// <summary>
    /// Civile o militare (carta <c>2026-08-27-vsop-militari.md</c> §1a). Stesso <see cref="Document"/>,
    /// stesse versioni, stesso motore di release: cambia il <b>profilo di sezioni</b> e il bersaglio di
    /// pubblicazione, così le due edizioni dello stesso scalo hanno cicli AIRAC indipendenti.
    /// <para>⚠️ È il discriminatore che impedisce a un documento militare di finire nel catch-all
    /// dell'aeroporto. Vedi <see cref="DocumentEdition"/>.</para>
    /// </summary>
    public DocumentEdition Edition { get; set; }
    public int? CurrentVersionId { get; set; }         // versione pubblicata corrente
    public DocumentVersion? CurrentVersion { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
    public string LastUpdatedAiracCycle { get; set; } = default!; // calcolato da AiracService, es. "2606"
    public int? FeaturedRank { get; set; }

    /// <summary>
    /// L'aeroporto che questo documento descrive, se è una vIPI d'aeroporto. null per ACC, APP e vLOA, che
    /// descrivono un SETTORE (<see cref="Sectors"/>/<see cref="Parties"/>) e non uno scalo.
    /// <para>Uno a uno: un documento d'aeroporto non può descriverne due, e l'indice unico su
    /// <c>Airports.DocumentId</c> è lì per impedirlo invece di sperarlo.</para>
    /// </summary>
    public Airport? Airport { get; set; }             // ordine "in evidenza" (1..3) nella card vLOA della landing ACC; null = non in evidenza

    /// <summary>
    /// L'aeroporto di cui questo documento è l'edizione <b>militare</b> (<c>Airport.MilDocumentId</c>).
    /// null per i documenti civili e per le vSOP militari di APP.
    /// <para>⚠️ Non è un doppione di <see cref="Airport"/>: quella dice «di quale scalo sono la vIPI
    /// civile», questa «di quale scalo sono il vSOP militare». Su un documento ne è valorizzata al più
    /// <b>una</b>, e quale delle due lo dice <see cref="Edition"/>.</para>
    /// <para>Serve a <c>IReleaseTarget.TryDescribe</c>, che decide guardando il documento in mano e non ha
    /// modo di interrogare il database.</para>
    /// </summary>
    public Airport? MilAirport { get; set; }

    /// <summary>
    /// I settori di cui questo documento è l'edizione <b>militare</b> (<c>Sector.MilDocumentId</c>).
    /// <para>⚠️ Sono una collezione DIVERSA da <see cref="Sectors"/>: quelli puntano al documento col
    /// legame civile. Cercare il settore primario di un documento militare dentro <c>Sectors</c> non
    /// troverebbe niente, e il documento risulterebbe irraggiungibile.</para>
    /// </summary>
    public ICollection<Sector> MilSectors { get; set; } = new List<Sector>();

    /// <summary>Nascosto dal pubblico (reversibile): il documento resta con la sua storia ma i loader pubblici lo escludono.</summary>
    public bool IsHidden { get; set; }

    // ⚠️ Le segnalazioni di revisione NON stanno più qui. Fino al 25 agosto 2026 erano due colonne —
    // NeedsReviewUtc + ReviewReason — cioè UN motivo solo: il secondo evento sovrascriveva il primo, che
    // spariva senza traccia. Ora vivono in DocumentImpact, una riga per fatto, con la loro chiusura.
    // Carta docs/feature/2026-08-25-documenti-da-rivedere.md §4.

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
