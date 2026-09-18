using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Il documento condiviso degli eventi RFO su database. Carta <c>docs/feature/2026-09-18-ponte-rfo-gate-manager.md</c>.
///
/// <para>🔴 <b>Controllo e scrittura sono UNA istruzione.</b> Per aggiornare:
/// <c>UPDATE … SET version = version + 1 … WHERE event_id = @id AND version = @attesa</c>; se tocca zero righe,
/// qualcuno ha scritto prima. Per creare: un <c>INSERT</c>, e la chiave primaria rifiuta il secondo. È il
/// database a mettere in fila due PUT simultanei (lock di riga su InnoDB e Postgres, lock di scrittura su
/// SQLite): il secondo ritrova la condizione falsa e riceve il 409. Mai «leggo la versione, poi aggiorno».</para>
///
/// <para>⚠️ SQL scritto a mano, con nomi minuscoli senza virgolette: è l'unica forma che gira uguale sui tre
/// provider. L'ora la mette il processo e non il database (<c>UTC_TIMESTAMP(3)</c> esiste solo su MySQL).</para>
///
/// <para>⚠️ Su MariaDB e Postgres c'è il retry dei guasti transitori, e una transazione esplicita deve stare
/// dentro <c>CreateExecutionStrategy()</c> (vedi <see cref="EfUnitOfWork"/>). Se la connessione cade dopo un
/// commit riuscito, il tentativo successivo trova la versione già avanzata e risponde 409 con la busta che
/// contiene la scrittura stessa: il client riapplica la sua modifica su quella, che è innocuo.</para>
/// </summary>
public sealed class EfRfoSharedStateStore : IRfoSharedStateStore
{
    private readonly VipiDbContext _db;
    public EfRfoSharedStateStore(VipiDbContext db) => _db = db;

    public async Task<RfoStateRow?> LoadAsync(string eventId, CancellationToken ct = default)
    {
        var riga = await _db.RfoSharedStates.AsNoTracking()
            .Where(s => s.EventId == eventId)
            .Select(s => new { s.Version, s.Data, s.UpdatedBy, s.UpdatedAt })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        // SQLite e MariaDB rendono un DateTime senza Kind: l'ora è scritta in UTC, e va detto.
        return riga is null
            ? null
            : new RfoStateRow(riga.Version, riga.Data, riga.UpdatedBy, DateTime.SpecifyKind(riga.UpdatedAt, DateTimeKind.Utc));
    }

    public async Task<RfoWriteResult> WriteAsync(string eventId, long expectedVersion, string data, string? updatedBy,
        CancellationToken ct = default)
    {
        if (expectedVersion < 0) throw new ArgumentOutOfRangeException(nameof(expectedVersion));

        // Al millisecondo, come DATETIME(3) del contratto: la busta del 200 e quella riletta dopo devono dire la
        // stessa ora.
        var adesso = DateTime.UtcNow;
        var ora = new DateTime(adesso.Ticks - adesso.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);

        // Creazione su un documento che c'è già — il caso di ogni postazione che parte da zero a evento avviato:
        // 409 subito, senza tentare l'INSERT. Non decide niente: la gara vera (due creazioni nello stesso istante)
        // la decide sempre la chiave primaria, qui sotto. Serve a non far scrivere a EF un «Failed executing
        // DbCommand» in avvisi-log.txt per una risposta normale — visto sul pacchetto 1.33.0, passo 4 del contratto.
        var scritto = expectedVersion == 0 && await EsisteAsync(eventId, ct).ConfigureAwait(false)
            ? false
            : await ScriviAsync(eventId, expectedVersion, data, updatedBy, ora, ct).ConfigureAwait(false);

        if (scritto)
            return new RfoWriteResult(RfoWriteOutcome.Scritto, new RfoStateRow(expectedVersion + 1, data, updatedBy, ora));

        // Fuori dalla transazione, così la lettura vede l'ultima scrittura confermata.
        var attuale = await LoadAsync(eventId, ct).ConfigureAwait(false);
        return attuale is null
            ? new RfoWriteResult(RfoWriteOutcome.NonTrovato, null)
            : new RfoWriteResult(RfoWriteOutcome.Conflitto, attuale);
    }

    /// <summary>La scrittura condizionata: vero se ha scritto, falso se la condizione non reggeva più.</summary>
    private Task<bool> ScriviAsync(string eventId, long expectedVersion, string data, string? updatedBy, DateTime ora,
        CancellationToken ct)
    {
        var strategia = _db.Database.CreateExecutionStrategy();
        return strategia.ExecuteAsync(async token =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(token).ConfigureAwait(false);

            int righe;
            try
            {
                righe = expectedVersion == 0
                    ? await _db.Database.ExecuteSqlInterpolatedAsync(
                        $"INSERT INTO rfo_shared_state (event_id, version, data, updated_by, updated_at) VALUES ({eventId}, 1, {data}, {updatedBy}, {ora})",
                        token).ConfigureAwait(false)
                    : await _db.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE rfo_shared_state SET version = version + 1, data = {data}, updated_by = {updatedBy}, updated_at = {ora} WHERE event_id = {eventId} AND version = {expectedVersion}",
                        token).ConfigureAwait(false);
            }
            catch (DbException) when (expectedVersion == 0)
            {
                // Chiave duplicata: un'altra postazione ha creato il documento un istante prima. Il codice
                // d'errore cambia da provider a provider (1062, 19, 23505), quindi non si guarda: si rilegge.
                // Se la riga non c'è, il guasto era un altro, e risale così com'è.
                await tx.RollbackAsync(token).ConfigureAwait(false);
                if (await EsisteAsync(eventId, token).ConfigureAwait(false)) return false;
                throw;
            }

            if (righe != 1)
            {
                await tx.RollbackAsync(token).ConfigureAwait(false);
                return false;
            }

            // La storia nella stessa transazione: non esiste una versione senza la sua riga.
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO rfo_shared_state_history (event_id, version, data, updated_by, updated_at) SELECT event_id, version, data, updated_by, updated_at FROM rfo_shared_state WHERE event_id = {eventId}",
                token).ConfigureAwait(false);

            await tx.CommitAsync(token).ConfigureAwait(false);
            return true;
        }, ct);
    }

    private Task<bool> EsisteAsync(string eventId, CancellationToken ct) =>
        _db.RfoSharedStates.AsNoTracking().AnyAsync(s => s.EventId == eventId, ct);
}
