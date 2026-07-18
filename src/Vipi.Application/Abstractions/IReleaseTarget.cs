using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Descrittore per-tipo del flusso di pubblicazione (doc 09 §3a). Isola le SOLE parti rimaste per-tipo dopo
/// l'unificazione del modello documento (doc 08): risoluzione d'identità (chiave↔Document↔ACC) e derivazione
/// shape(Document)→<see cref="ManagedDoc"/> per l'elenco unificato.
/// I motori generici (EfReleaseRepository / EfDocumentAdminRepository) consultano il registry invece di switchare
/// sul tipo: aggiungere un tipo di documento = registrare un descrittore, zero switch toccati.
/// </summary>
public interface IReleaseTarget
{
    /// <summary>Discriminatore di release di questo tipo.</summary>
    ReleaseTargetType Type { get; }

    /// <summary>Tipo corrispondente nell'elenco unificato /vsop/versioni.</summary>
    ManagedDocKind ManagedKind { get; }

    /// <summary>Ordine di tentativo in <see cref="TryDescribe"/> (più basso prima). L'aeroporto è il catch-all finale
    /// dei Document vIPI non riconosciuti come APP/ACC → deve avere l'ordine più alto.</summary>
    int DescribeOrder { get; }

    /// <summary>Chiave di release → id del <see cref="Document"/> sorgente. null se non risolvibile.</summary>
    Task<int?> ResolveDocumentIdAsync(string key, CancellationToken ct = default);

    /// <summary>Chiave di release → codice ACC da autorizzare per pubblicare/annullare. null se non risolvibile.</summary>
    Task<string?> AuthAccCodeAsync(string key, CancellationToken ct = default);

    /// <summary>Se il <paramref name="doc"/> (con navigazioni Sectors/Parties caricate) appartiene a questo tipo,
    /// produce il <see cref="ManagedDoc"/> (identità + chiave di release) e ritorna true; altrimenti false.</summary>
    bool TryDescribe(Document doc, bool hasDraft, out ManagedDoc managed);
}
