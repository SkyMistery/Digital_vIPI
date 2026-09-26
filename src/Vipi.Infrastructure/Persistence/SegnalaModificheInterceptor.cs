using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vipi.Application.Content;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Dice al raccoglitore delle modifiche (<see cref="IModificheInAttesa"/>) che qualcuno ha scritto dati che
/// possono cambiare un documento pubblicato, e di che <b>famiglia</b> (<see cref="FamiglieDiModifica"/>). Da lì il
/// giro della deriva riparte poco dopo, invece che il giorno dopo. Carta
/// <c>docs/feature/2026-09-23-da-fare-per-cambiamento.md</c> §4.
///
/// <para><b>Perché un interceptor e non una chiamata nei servizi</b>: è lo stesso conto di
/// <see cref="BumpCatalogoStazioniInterceptor"/>. Chi scrive dati che finiscono in un documento sono decine di servizi
/// — editor di sezioni, trasferimenti, aeroporti, import — e un «avvisa la deriva» in ognuno sarebbe l'elenco che
/// si dimentica al primo servizio nuovo. Il salvataggio lo vedono tutti.</para>
///
/// <para>⚠️ Si segnala a scrittura <b>avvenuta</b>, e dentro una transazione a transazione <b>confermata</b>: una
/// modifica annullata non deve far ripartire il giro, e una non ancora visibile gli farebbe leggere i dati di
/// prima (T-036, lo stesso guasto pagato dal catalogo delle stazioni).</para>
///
/// <para>⚠️ Montato su TUTTI E TRE i provider, come l'altro: dimenticarne uno vuol dire un ambiente in cui la lista
/// torna ad aspettare il giro notturno, e nessun test che gira sull'altro provider lo direbbe.</para>
/// </summary>
public sealed class SegnalaModificheInterceptor : SaveChangesInterceptor, IDbTransactionInterceptor
{
    private readonly IModificheInAttesa _attesa;

    /// <summary>Le famiglie scritte da un contesto, fra il «sto per salvare» e il «salvato / confermato».</summary>
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<DbContext, HashSet<string>> _inSospeso = new();

    public SegnalaModificheInterceptor(IModificheInAttesa attesa) => _attesa = attesa;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Annota(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Annota(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        DopoIlSalvataggio(eventData.Context);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        DopoIlSalvataggio(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Scarta(eventData.Context);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Scarta(eventData.Context);
        return Task.CompletedTask;
    }

    public void TransactionCommitted(System.Data.Common.DbTransaction transaction, TransactionEndEventData eventData) =>
        Consegna(eventData.Context);

    public Task TransactionCommittedAsync(System.Data.Common.DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Consegna(eventData.Context);
        return Task.CompletedTask;
    }

    public void TransactionRolledBack(System.Data.Common.DbTransaction transaction, TransactionEndEventData eventData) =>
        Scarta(eventData.Context);

    public Task TransactionRolledBackAsync(System.Data.Common.DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Scarta(eventData.Context);
        return Task.CompletedTask;
    }

    private void Annota(DbContext? contesto)
    {
        if (contesto is null) return;
        HashSet<string>? famiglie = null;
        foreach (var voce in contesto.ChangeTracker.Entries())
        {
            if (voce.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
            if (FamiglieDiModifica.Di(voce.Entity) is not string f) continue;
            famiglie ??= _inSospeso.GetValue(contesto, _ => new HashSet<string>(StringComparer.Ordinal));
            famiglie.Add(f);
        }
    }

    private void DopoIlSalvataggio(DbContext? contesto)
    {
        // Dentro una transazione la scrittura non è ancora vista dagli altri: si aspetta la conferma.
        if (contesto is null || contesto.Database.CurrentTransaction is not null) return;
        Consegna(contesto);
    }

    private void Consegna(DbContext? contesto)
    {
        if (contesto is null || !_inSospeso.TryGetValue(contesto, out var famiglie)) return;
        _inSospeso.Remove(contesto);
        if (famiglie.Count > 0) _attesa.Segnala(famiglie, DateTime.UtcNow);
    }

    private void Scarta(DbContext? contesto)
    {
        if (contesto is not null) _inSospeso.Remove(contesto);
    }

    public void TransactionFailed(System.Data.Common.DbTransaction transaction, TransactionErrorEventData eventData) => Scarta(eventData.Context);
    public Task TransactionFailedAsync(System.Data.Common.DbTransaction transaction, TransactionErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Scarta(eventData.Context);
        return Task.CompletedTask;
    }
}
