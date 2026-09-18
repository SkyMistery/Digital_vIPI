namespace Vipi.Application.Abstractions;

/// <summary>
/// Il documento condiviso di un evento RFO così come esce dal database. <see cref="Data"/> è il testo JSON
/// esatto che la postazione ha mandato: nessuno qui lo apre.
/// </summary>
public sealed record RfoStateRow(long Version, string Data, string? UpdatedBy, DateTime UpdatedAtUtc);

/// <summary>Com'è andata una scrittura condizionata.</summary>
public enum RfoWriteOutcome
{
    /// <summary>La versione attesa era quella giusta: scritto, versione +1.</summary>
    Scritto,

    /// <summary>Qualcuno ha scritto prima: <see cref="RfoWriteResult.Current"/> è la busta di oggi.</summary>
    Conflitto,

    /// <summary>Il client credeva di aggiornare una versione, ma il documento non esiste.</summary>
    NonTrovato,
}

public sealed record RfoWriteResult(RfoWriteOutcome Outcome, RfoStateRow? Current);

/// <summary>
/// Il deposito del ponte RFO Gate Manager. Carta <c>docs/feature/2026-09-18-ponte-rfo-gate-manager.md</c>.
///
/// <para>🔴 <see cref="WriteAsync"/> è <b>una sola istruzione condizionata</b> sulla versione — un INSERT per la
/// creazione, un UPDATE <c>WHERE version = @attesa</c> per l'aggiornamento — mai «leggo, poi aggiorno». Due PUT
/// simultanei con la stessa versione attesa: uno scrive, l'altro riceve <see cref="RfoWriteOutcome.Conflitto"/>.</para>
/// </summary>
public interface IRfoSharedStateStore
{
    Task<RfoStateRow?> LoadAsync(string eventId, CancellationToken ct = default);

    /// <param name="expectedVersion">0 = crea il documento; N &gt; 0 = aggiorna solo se è ancora alla versione N.</param>
    /// <param name="data">JSON già validato come oggetto; si salva così com'è.</param>
    Task<RfoWriteResult> WriteAsync(string eventId, long expectedVersion, string data, string? updatedBy,
        CancellationToken ct = default);
}
