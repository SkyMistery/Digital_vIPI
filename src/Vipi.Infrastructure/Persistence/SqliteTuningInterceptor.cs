using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Tampone concorrenza SQLite (audit A1): a ogni apertura di connessione abilita <b>WAL</b> (i lettori non
/// bloccano lo scrittore) e imposta <b>busy_timeout</b> (uno scrittore aspetta il lock invece di fallire subito
/// con «database is locked»). È una mitigazione: la vera scala multi-utente è il cutover a Postgres (ADR-0007).
/// </summary>
public sealed class SqliteTuningInterceptor : DbConnectionInterceptor
{
    private const int BusyTimeoutMs = 5000;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => Apply(connection);

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken ct = default)
    {
        Apply(connection);
        return Task.CompletedTask;
    }

    private static void Apply(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        // WAL è persistente sul file (idempotente); busy_timeout è per-connessione (va impostato a ogni apertura).
        cmd.CommandText = $"PRAGMA journal_mode=WAL; PRAGMA busy_timeout={BusyTimeoutMs};";
        cmd.ExecuteNonQuery();
    }
}
