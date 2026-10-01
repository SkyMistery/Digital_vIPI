namespace Vipi.Domain.Entities;

/// <summary>
/// Il <b>pacchetto dell'evento</b> (committente, 30 settembre 2026): i profili per postazione e i link (lo zip dello
/// Stand Manager su Drive) che chi controlla un evento scarica prima di aprire. Carta
/// <c>docs/feature/2026-09-30-profili-evento.md</c>.
///
/// <para>⚠️ <b>Uno solo, e riusato.</b> Non c'è un archivio di eventi: lo staff lo accende vicino a un evento, lo spegne
/// dopo, e per quello successivo cambia il nome e le voci che non servono più. Le voci RESTANO da un evento
/// all'altro (decisione del committente): lo Stand Manager, per esempio, è lo stesso ogni volta.</para>
///
/// <para>Visibile al pubblico solo se <see cref="IsActive"/> e dentro la finestra di date, quando c'è (le regole in
/// <c>EventKitRules.Visibile</c>).</para>
/// </summary>
public class EventKit
{
    public int Id { get; set; }

    /// <summary>Il nome dell'evento, come lo legge chi arriva: «Italian Night Ops 2026».</summary>
    public string Name { get; set; } = "";

    /// <summary>L'interruttore. Spento = la pagina pubblica non c'è, qualunque cosa dicano le date.</summary>
    public bool IsActive { get; set; }

    /// <summary>Da quando si vede (UTC); null = subito, appena acceso.</summary>
    public DateTime? StartsUtc { get; set; }

    /// <summary>Fino a quando si vede (UTC, escluso); null = finché non lo si spegne.</summary>
    public DateTime? EndsUtc { get; set; }

    /// <summary>
    /// I VID degli ACCOUNT DELL'EVENTO, come li ha scritti lo staff: uno per riga, con una nota facoltativa accanto
    /// («704798 LIRF_TWR»). Committente, 1 ottobre 2026: durante un evento si controlla con account e VID dati
    /// dall'organizzazione, diversi da quelli con cui si entra nel sito, e senza questa lista la vista live non
    /// riconosce chi sta controllando. Si legge con <c>EventKitRules.LeggiVid</c>; null o vuoto = nessuno.
    /// <para>⚠️ Vale solo mentre il pacchetto si vede (acceso e dentro le date): fuori, la lista non apre niente.</para>
    /// </summary>
    public string? VidEvento { get; set; }

    public DateTime UpdatedUtc { get; set; }
    public int UpdatedByUserId { get; set; }
    public string UpdatedByName { get; set; } = "";

    public List<EventKitItem> Items { get; set; } = new();
}

/// <summary>
/// Una voce del pacchetto: un FILE caricato sul sito (un profilo per postazione) oppure un LINK (Drive).
/// <para>⚠️ I byte stanno nella riga, come per <see cref="MediaAsset"/>: sono file da pochi KB, e un deposito esterno
/// sarebbe un secondo posto da tenere in ordine. Il tetto è quello delle immagini (<c>Media:MaxUploadBytes</c>);
/// quel che non ci sta va su Drive, ed è il caso dello zip dello Stand Manager.</para>
/// </summary>
public class EventKitItem
{
    public int Id { get; set; }

    public int EventKitId { get; set; }
    public EventKit? EventKit { get; set; }

    /// <summary>Che cos'è, per chi scarica: la postazione («LIRF_TWR») o il nome del pacchetto.</summary>
    public string Label { get; set; } = "";

    /// <summary>Una riga facoltativa di spiegazione.</summary>
    public string Note { get; set; } = "";

    public EventKitItemKind Kind { get; set; }

    /// <summary>Solo per <see cref="EventKitItemKind.Link"/>: l'indirizzo, sempre <c>https</c>.</summary>
    public string Url { get; set; } = "";

    // Solo per EventKitItemKind.File
    /// <summary>Il nome del file com'era sul disco di chi l'ha caricato: è il nome con cui si scarica.</summary>
    public string FileName { get; set; } = "";
    public int ByteSize { get; set; }
    public byte[]? Bytes { get; set; }

    /// <summary>L'ordine nell'elenco; a parità, quello di inserimento.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedUtc { get; set; }
    public int CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = "";
}
