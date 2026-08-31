using Vipi.Domain;

namespace Vipi.Application.Content;

// ---- Modelli per il percorso di editing (CH/AOD). Distinti dai RawDocument/View di consultazione. ----

/// <summary>Riga di selezione documento nell'editor (vIPI ACC, vIPI aeroporto, vLOA).</summary>
public sealed class DocumentSummary
{
    public required int Id { get; init; }
    public required DocumentType Type { get; init; }
    public required string Title { get; init; }
    public required DocumentStatus Status { get; init; }
    public required string Scope { get; init; }        // es. "LIRR" o "LIRF"
    public required bool HasDraft { get; init; }
    public int? CurrentVersionId { get; init; }

    /// <summary>Vero se il documento descrive un aeroporto (settore primario Kind=Airport): si edita SOLO
    /// dall'editor aeroporto (`/services/vsop/{acc}/airports/editor?icao=`), non dall'editor generico.</summary>
    public bool IsAirport { get; init; }

    /// <summary>Vero se il documento è un APP non remotizzato (settore primario Type=App, ApproachKind=Standalone):
    /// si edita SOLO dall'editor APP dedicato (`/services/vsop/{acc}/apps/editor?app=`); <see cref="Scope"/> è il callsign APP.</summary>
    public bool IsStandaloneApp { get; init; }

    /// <summary>Codice ACC del settore primario (per costruire i link editor).</summary>
    public string? AccCode { get; init; }

    /// <summary>Solo vLOA: codice ACC lato Home (italiano). Per il link all'editor vLOA.</summary>
    public string? HomeAccCode { get; init; }

    /// <summary>Solo vLOA: codice ACC lato Neighbour (estero). Per il link all'editor vLOA (una per coppia).</summary>
    public string? NeighbourAccCode { get; init; }
}

/// <summary>Documento aperto in editing: la versione di lavoro (bozza se esiste, sennò la pubblicata) con tutti i campi modificabili.</summary>
public sealed class EditableDocument
{
    public required int DocumentId { get; init; }
    public required int VersionId { get; init; }
    public required int VersionNumber { get; init; }
    public required DocumentStatus VersionStatus { get; init; }
    public required string Title { get; init; }
    public required IReadOnlyList<EditableSection> Sections { get; init; }

    /// <summary>
    /// La lingua in cui il documento è <b>redatto</b>: da lì parte la traduzione, e in quella lingua
    /// l'editor mostra i testi. ⚠️ Nulla sui documenti salvati prima che il campo esistesse — allora vale
    /// la lingua in cui quella famiglia nasce (<c>DocumentTranslator.CodiceSorgente</c>).
    /// </summary>
    public Vipi.Domain.Language? Language { get; init; }

    /// <summary>
    /// Il documento si legge <b>sempre</b> in <see cref="Language"/>, anche a chi guarda il sito nell'altra
    /// (carta <c>docs/feature/2026-08-31-lingua-bloccata.md</c>). Lo decide chi pubblica, dal pannello di
    /// rilascio.
    /// </summary>
    public bool LanguageLocked { get; init; }

    /// <summary>Vero se la versione di lavoro è una bozza editabile.</summary>
    public bool IsEditable => VersionStatus == DocumentStatus.Draft;
}

public sealed class EditableSection
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public required string SectionKey { get; init; }
    public required int Depth { get; init; }
    public required int Order { get; init; }

    /// <summary>Modalità di resa della sezione (doc 10 §3a): l'editor mostra badge + toggle Live/Frozen sulle sezioni
    /// derivabili. Governa se al publish l'output viene congelato (Frozen) o reso live al view (Live).</summary>
    public RenderMode RenderMode { get; init; } = RenderMode.Frozen;

    /// <summary>Sezione nascosta dal documento pubblicato (doc 11 §3c): l'editor la mostra comunque, marcata.</summary>
    public bool IsHidden { get; init; }

    /// <summary>Sotto-sezione resa prima del corpo del padre (doc 11 §3g); l'editor espone il toggle.</summary>
    public bool BeforeParentBody { get; init; }

    /// <summary>A chi si rivolge la sezione (carta vSOP militari §3). Viaggia nello snapshot con gli altri
    /// flag. <c>Both</c> = per tutti, ed è il default: nessun documento cambia finché nessuno marca.</summary>
    public SectionAudience Audience { get; init; }

    /// <summary>Prosa a CAPOFILA: una frase che introduce la tabella invece di una per clausola.</summary>
    public bool LeadSentence { get; init; }

    public required IReadOnlyList<EditableBlock> Blocks { get; init; }
    public required IReadOnlyList<EditableSection> Children { get; init; }
}

public sealed class EditableBlock
{
    public required int Id { get; init; }
    public required int Order { get; init; }
    public required BlockFormat Format { get; init; }
    // Campi editabili: settabili per il two-way binding dell'editor.
    public required BlockTier Tier { get; set; }
    public required BlockVisibility Visibility { get; set; }
    public CalloutKind? CalloutKind { get; set; }
    public string? Body { get; set; }
    public string? BodyJson { get; set; }
    /// <summary>Token di concorrenza (base64) catturato al caricamento; rispedito su salvataggio.</summary>
    public string? RowVersion { get; set; }
}

/// <summary>Riga dello storico versioni (pagina Bozze &amp; versioni).</summary>
public sealed class VersionInfo
{
    public required int Id { get; init; }
    public required int VersionNumber { get; init; }
    public required DocumentStatus Status { get; init; }
    public required int CreatedByUserId { get; init; }
    public required DateTime CreatedUtc { get; init; }
    public required string AiracCycle { get; init; }
    public string? Note { get; init; }
    public required bool IsCurrent { get; init; }
}

/// <summary>Stato del lock di editing esclusivo di un documento.</summary>
public sealed class LockInfo
{
    /// <summary>Esiste un lock attivo (non scaduto).</summary>
    public required bool Locked { get; init; }
    public int? ByUserId { get; init; }
    public string? ByName { get; init; }
    public DateTime? ExpiresUtc { get; init; }
    /// <summary>Il lock attivo è del UserId corrente (può editare).</summary>
    public required bool IsMine { get; init; }

    public static LockInfo Free() => new() { Locked = false, IsMine = false };
}

/// <summary>Patch dei campi editabili di un blocco (null = invariato non distinguibile da "azzera": per Body/BodyJson si passa sempre il valore voluto).</summary>
public sealed class BlockEdit
{
    public required BlockTier Tier { get; init; }
    public required BlockVisibility Visibility { get; init; }
    public CalloutKind? CalloutKind { get; init; }
    public string? Body { get; init; }
    public string? BodyJson { get; init; }
    /// <summary>Token di concorrenza originale (base64) per il controllo ottimistico in update.</summary>
    public string? RowVersion { get; init; }
}
