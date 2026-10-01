using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Application.Abstractions;

/// <summary>Una voce del pacchetto dell'evento come la legge chi sta sopra la persistenza: SENZA i byte.</summary>
public sealed record EventKitItemRow(int Id, string Label, string Note, EventKitItemKind Kind, string Url,
    string FileName, int ByteSize, int SortOrder, DateTime CreatedUtc, string CreatedByName);

/// <summary>Il pacchetto com'è in tabella: la testata e le voci in ordine.</summary>
public sealed record EventKitSnapshot(EventKit Testata, IReadOnlyList<EventKitItemRow> Voci);

/// <summary>Il file di una voce, coi suoi byte: lo chiede solo l'endpoint che lo serve.</summary>
public sealed record EventKitFile(string FileName, byte[] Bytes);

/// <summary>Persistenza del pacchetto dell'evento (carta <c>docs/feature/2026-09-30-profili-evento.md</c>).</summary>
public interface IEventKitRepository
{
    /// <summary>La testata (senza voci caricate dentro) e le voci senza byte; null se non è mai stato creato.</summary>
    Task<EventKitSnapshot?> LoadAsync(CancellationToken ct = default);

    /// <summary>Scrive nome, interruttore e date. Se il pacchetto non c'è ancora, lo crea.</summary>
    Task SaveHeaderAsync(string nome, bool attivo, DateTime? daUtc, DateTime? aUtc, int userId, string userName,
        DateTime adessoUtc, CancellationToken ct = default);

    /// <summary>Scrive il testo dei VID degli account dell'evento. Se il pacchetto non c'è ancora, lo crea (spento).</summary>
    Task SaveVidAsync(string? testo, int userId, string userName, DateTime adessoUtc, CancellationToken ct = default);

    /// <summary>Aggiunge una voce in fondo all'elenco; il pacchetto lo crea se manca (spento, senza nome).</summary>
    Task<int> AddItemAsync(EventKitItem voce, CancellationToken ct = default);

    Task<int> CountItemsAsync(CancellationToken ct = default);

    /// <summary>Toglie una voce, coi suoi byte. False se non c'è.</summary>
    Task<bool> DeleteItemAsync(int id, CancellationToken ct = default);

    /// <summary>Scambia la voce con la precedente (<paramref name="verso"/> &lt; 0) o la seguente. False se non si muove.</summary>
    Task<bool> MoveItemAsync(int id, int verso, CancellationToken ct = default);

    /// <summary>I byte di una voce di tipo file; null se non c'è o se è un link.</summary>
    Task<EventKitFile?> FileAsync(int id, CancellationToken ct = default);
}
