using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Un ente ATC come lo legge chi sta sopra la persistenza (vedi <c>Vipi.Domain.Entities.AtcUnit</c>).
/// </summary>
/// <param name="Code">Chiave di pubblicazione e indirizzo pubblico: non cambia mai.</param>
/// <param name="Positions">Le posizioni IVAO, la principale per prima.</param>
public sealed record AtcUnitRow(int Id, string Code, string Name, string AccCode, AtcUnitMode Mode, int? DocumentId,
    IReadOnlyList<string> Positions)
{
    /// <summary>Da dove parte la derivazione (frequenze, AoR, coordinamenti): la posizione principale, o il codice
    /// se l'ente non ne ha più nessuna.</summary>
    public string Seme => Positions.Count > 0 ? Positions[0] : Code;
}

/// <summary>
/// Gli enti ATC: l'aggancio della vIPI APP al posto del nominativo (S49, committente, 29 settembre 2026).
/// </summary>
public interface IAtcUnitRepository
{
    /// <summary>
    /// L'ente che risponde a <paramref name="key"/>: per <b>codice</b>, oppure per una sua <b>posizione</b>
    /// (<c>?app=LIRE_TWR</c> trova l'ente di Pratica, il cui codice è <c>LIRE_APP</c>). null se nessuno.
    /// </summary>
    Task<AtcUnitRow?> FindAsync(string key, CancellationToken ct = default);

    /// <summary>Quali di questi nominativi IVAO manda ancora: un settore ATTIVO con quel nome (S53).</summary>
    Task<IReadOnlySet<string>> ActiveCallsignsAsync(IReadOnlyCollection<string> callsigns, CancellationToken ct = default);

    /// <summary>L'ente per id; null se non c'è.</summary>
    Task<AtcUnitRow?> GetAsync(int unitId, CancellationToken ct = default);

    /// <summary>Tutti gli enti, per l'ACC indicato o tutti.</summary>
    Task<IReadOnlyList<AtcUnitRow>> ListAsync(string? accCode = null, CancellationToken ct = default);

    /// <summary>
    /// Crea l'ente (se non c'è) e il suo documento (se non c'è), e ritorna l'id del documento. Idempotente.
    /// </summary>
    /// <param name="code">Codice dell'ente; per un ente nuovo, il nominativo dell'APP da cui nasce.</param>
    Task<int> EnsureDocumentAsync(string code, string name, string accCode, SectionProfile profile,
        int authorUserId, CancellationToken ct = default);

    /// <summary>Aggiunge una posizione in coda. Rifiuta un nominativo che è già di un altro ente.</summary>
    Task AddPositionAsync(int unitId, string callsign, CancellationToken ct = default);

    /// <summary>Toglie una posizione. L'ente resta, anche senza posizioni: il documento è suo.</summary>
    Task RemovePositionAsync(int unitId, string callsign, CancellationToken ct = default);

    /// <summary>Porta una posizione in testa: diventa la principale, da cui parte la derivazione.</summary>
    Task MakePrimaryAsync(int unitId, string callsign, CancellationToken ct = default);

    /// <summary>Cambia il nome dell'ente (il codice no).</summary>
    Task RenameAsync(int unitId, string name, CancellationToken ct = default);

    /// <summary>Dove vive il contenuto dell'ente: documento proprio o vIPI dell'ACC (S50).</summary>
    Task SetModeAsync(int unitId, AtcUnitMode mode, CancellationToken ct = default);

    /// <summary>Rinomina di un nominativo IVAO (stessa identità di sorgente): la posizione segue.</summary>
    Task RenamePositionAsync(string oldCallsign, string newCallsign, CancellationToken ct = default);
}
