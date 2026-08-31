using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vipi.Application.Content;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Spinge <see cref="IStationCatalogVersion"/> quando qualcuno scrive un <see cref="Acc"/> o un
/// <see cref="Airport"/>. Una riga sola, e nessuno se ne può dimenticare.
///
/// <para>🔴 <b>Perché non basta chiamare <c>Bump()</c> nei servizi.</b> Ci si è provato, e il 31 agosto
/// 2026 il conto era questo: <c>Bump()</c> compariva in <b>quattro</b> punti, e i posti che scrivono quelle
/// due tabelle erano <b>undici</b>. Mancava in <c>CreateAcc</c>, <c>DeleteAcc</c>, <c>CreateAirport</c>,
/// <c>DeleteAirport</c>, <c>MoveAirport</c>, <c>SetAirportHidden</c>, nell'intera catena di eliminazione
/// (<c>DeletionService</c>) e nella scrittura delle coordinate dell'aeroporto
/// (<c>EfAirportSectorRepository</c>). Nessuno se n'era accorto perché la cache era <b>scoped</b>: una
/// richiesta SSR ne apre una nuova ogni volta, quindi il dato vecchio durava un istante.</para>
///
/// <para>⚠️ <b>Da quando la cache è di PROCESSO</b> (<see cref="CatalogoStazioni"/>) quello stesso buco
/// varrebbe «finché qualcuno non riavvia»: un amministratore che crea un ACC non lo vedrebbe comparire, né
/// lui né nessun altro. Il rimedio non poteva quindi essere «ricordarsi la riga in sei posti in più»: la
/// spinta va <b>dove avviene la scrittura</b>, che è un posto solo.</para>
///
/// <para>⚠️ <b>Perché si spinge PRIMA del salvataggio e non dopo.</b> Il segnale è un <b>numero da
/// invalidare</b>, non un evento da consegnare: una spinta di troppo costa <b>una rilettura</b> di sette
/// ACC e novantatré aeroporti, una spinta mancata costa un dato sbagliato a schermo finché non si riavvia.
/// Fra i due errori si sceglie il primo, e lo si sceglie <b>apposta</b>: se il salvataggio poi fallisce,
/// abbiamo pagato una query.</para>
/// </summary>
public sealed class BumpCatalogoStazioniInterceptor : SaveChangesInterceptor
{
    private readonly IStationCatalogVersion _versione;

    public BumpCatalogoStazioniInterceptor(IStationCatalogVersion versione) => _versione = versione;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        SpingiSeServe(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        SpingiSeServe(eventData.Context);
        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// ⚠️ <c>Modified</c> conta quanto <c>Added</c> e <c>Deleted</c>: nella mappa degli aeroporti stanno
    /// anche quota, variazione magnetica, IATA, coordinate e i due segni militari, e quelli cambiano con un
    /// <c>UPDATE</c>. Un filtro sul solo inserimento avrebbe lasciato fuori proprio il giro notturno.
    /// </summary>
    private void SpingiSeServe(DbContext? contesto)
    {
        if (contesto is null) return;
        try
        {
            foreach (var voce in contesto.ChangeTracker.Entries())
            {
                if (voce.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
                if (voce.Entity is not (Acc or Airport)) continue;
                _versione.Bump();
                return;   // basta una spinta: il numero dice «rileggi», non «quante volte»
            }
        }
        catch
        {
            // Non si sa fallire in modo utile: senza la spinta il peggio è un dato vecchio, con
            // un'eccezione qui il peggio è un salvataggio perso.
        }
    }
}
