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

    /// <summary>
    /// Le famiglie scritte da un contesto. Due insiemi e non uno (U-196, revisione totale 3): quelle del salvataggio
    /// <b>in volo</b> e quelle dei salvataggi già <b>riusciti</b> dentro la transazione aperta. Un salvataggio che
    /// cade butta solo le sue: con un insieme solo buttava anche quelle dei riusciti, e alla conferma non arrivava
    /// niente.
    /// </summary>
    private sealed class Sospese
    {
        public HashSet<string> InVolo { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Riuscite { get; } = new(StringComparer.Ordinal);
    }

    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<DbContext, Sospese> _inSospeso = new();

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

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => ScartaInVolo(eventData.Context);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        ScartaInVolo(eventData.Context);
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
        Sospese? sospese = null;
        foreach (var voce in contesto.ChangeTracker.Entries())
        {
            if (voce.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
            if (FamiglieDiModifica.Di(voce.Entity) is not string f) continue;
            sospese ??= _inSospeso.GetValue(contesto, _ => new Sospese());
            sospese.InVolo.Add(f);
        }
    }

    private void DopoIlSalvataggio(DbContext? contesto)
    {
        if (contesto is null || !_inSospeso.TryGetValue(contesto, out var sospese)) return;
        sospese.Riuscite.UnionWith(sospese.InVolo);
        sospese.InVolo.Clear();
        // Dentro una transazione la scrittura non è ancora vista dagli altri: si aspetta la conferma.
        if (contesto.Database.CurrentTransaction is null) Consegna(contesto);
    }

    private void Consegna(DbContext? contesto)
    {
        if (contesto is null || !_inSospeso.TryGetValue(contesto, out var sospese)) return;
        _inSospeso.Remove(contesto);
        if (sospese.Riuscite.Count > 0) _attesa.Segnala(sospese.Riuscite, DateTime.UtcNow);
    }

    /// <summary>Un salvataggio caduto: via le sue famiglie, non quelle dei riusciti prima di lui.</summary>
    private void ScartaInVolo(DbContext? contesto)
    {
        if (contesto is not null && _inSospeso.TryGetValue(contesto, out var sospese)) sospese.InVolo.Clear();
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
