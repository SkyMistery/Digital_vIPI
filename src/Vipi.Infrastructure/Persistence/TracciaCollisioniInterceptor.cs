using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vipi.Application.Diagnostica;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Annuncia a <see cref="CollisioniDbContext"/> l'inizio e la fine di ogni comando, così che una seconda
/// operazione sullo stesso contesto lasci scritto <b>chi c'era già</b> — la metà della storia che lo stack
/// dell'eccezione non contiene (vedi <c>docs/lavori-aperti.md</c> §E9).
///
/// <para>⚠️ Un lettore si chiude quando viene <b>disposto</b>, non quando il comando «ha eseguito»: la
/// sezione critica di EF dura fino a lì, e chiudere prima farebbe apparire libero un contesto che è ancora
/// occupato — cioè mancherebbe proprio le collisioni che ci interessano.</para>
///
/// <para>Non tocca il comando, non tocca il risultato: se qui dentro qualcosa va storto, va storto in
/// silenzio dalla parte della diagnostica.</para>
/// </summary>
public sealed class TracciaCollisioniInterceptor : DbCommandInterceptor
{
    private static object? Contesto(DbContextEventData d) => d.Context;

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        if (Contesto(eventData) is { } c) CollisioniDbContext.Apre(c, command.CommandText);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (Contesto(eventData) is { } c) CollisioniDbContext.Apre(c, command.CommandText);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult DataReaderDisposing(
        DbCommand command, DataReaderDisposingEventData eventData, InterceptionResult result)
    {
        if (Contesto(eventData) is { } c) CollisioniDbContext.Chiude(c, command.CommandText);
        return result;
    }

    public override InterceptionResult DataReaderClosing(
        DbCommand command, DataReaderClosingEventData eventData, InterceptionResult result)
    {
        if (Contesto(eventData) is { } c) CollisioniDbContext.Chiude(c, command.CommandText);
        return result;
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        if (Contesto(eventData) is { } c) CollisioniDbContext.Chiude(c, command.CommandText);
    }

    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken ct = default)
    {
        if (Contesto(eventData) is { } c) CollisioniDbContext.Chiude(c, command.CommandText);
        return Task.CompletedTask;
    }

    // Scritture e scalari: qui non c'è nessun lettore da aspettare, la coppia inizio/fine basta.
    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        if (Contesto(eventData) is { } c) CollisioniDbContext.Chiude(c, command.CommandText);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken ct = default)
    {
        if (Contesto(eventData) is { } c) CollisioniDbContext.Chiude(c, command.CommandText);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        if (Contesto(eventData) is { } c) CollisioniDbContext.Apre(c, command.CommandText);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (Contesto(eventData) is { } c) CollisioniDbContext.Apre(c, command.CommandText);
        return ValueTask.FromResult(result);
    }
}
