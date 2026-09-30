using Vipi.Application.Abstractions;
using Vipi.Domain.Entities;

namespace Vipi.Application.Auth;

/// <summary>Una riga dell'elenco di chi è entrato.</summary>
public sealed record AccessoAlSitoRiga(int UserId, string Nome, string? Divisione, string? Acc,
    DateTime PrimoUtc, DateTime UltimoUtc, int Giorni);

/// <summary>L'elenco: le righe mostrate e quante ce ne sono in tutto con quel filtro.</summary>
public sealed record ElencoAccessi(IReadOnlyList<AccessoAlSitoRiga> Righe, int Totale);

/// <summary>Il deposito del registro degli accessi (EF). Carta <c>docs/feature/2026-09-30-registro-accessi.md</c>.</summary>
public interface IRegistroAccessiStore
{
    /// <summary>Crea o aggiorna la riga del VID (<see cref="AccessoAlSito.Registra"/>).</summary>
    Task RegistraAsync(int userId, string nome, string? divisione, string? acc, DateTime oraUtc,
        string? nomeBreve = null, CancellationToken ct = default);

    /// <summary>I nomi brevi dei VID che li hanno («Mario R.»): per la classifica. Chi manca non ha mai fatto il login
    /// dopo il rilascio, o è stato cancellato.</summary>
    Task<IReadOnlyDictionary<int, string>> NomiBreviAsync(IReadOnlyCollection<int> userIds, CancellationToken ct = default);

    /// <summary>Cancella chi non entra da prima di <paramref name="ultimoPrimaDi"/>. Ritorna quante righe.</summary>
    Task<int> PotaAsync(DateTime ultimoPrimaDi, CancellationToken ct = default);

    /// <summary>I più recenti per primi; <paramref name="cerca"/> sul nome o sul VID.</summary>
    Task<ElencoAccessi> ElencoAsync(string? cerca, int limite, CancellationToken ct = default);

    /// <summary>La riga di un VID, o null.</summary>
    Task<AccessoAlSitoRiga?> TrovaAsync(int userId, CancellationToken ct = default);
}

/// <summary>
/// Registra chi entra e dà l'elenco agli amministratori (committente, 30 settembre 2026). Lo chiama il middleware del
/// login (<c>StaffLoginTrackingMiddleware</c>), che scrive al più ogni cinque minuti per VID.
/// </summary>
public interface IRegistroAccessi
{
    Task RegistraAsync(CurrentUser utente, CancellationToken ct = default);

    /// <summary>Solo amministratori: gli altri ricevono un'eccezione, non un elenco vuoto.</summary>
    Task<ElencoAccessi> ElencoAsync(string? cerca, CancellationToken ct = default);

    /// <summary>
    /// I nomi brevi per la classifica della divisione (30 settembre 2026). ⚠️ Senza cancello qui: lo ha la classifica
    /// (staff, o chiunque sia entrato se è pubblica), e il nome breve è quello che l'informativa dice che compare lì.
    /// </summary>
    Task<IReadOnlyDictionary<int, string>> NomiBreviAsync(IReadOnlyCollection<int> userIds, CancellationToken ct = default);

    /// <summary>
    /// La riga di CHI CHIEDE, e di nessun altro: la pagina «I miei dati» (30 settembre 2026). Il VID viene dall'utente
    /// corrente, non da un parametro — così non esiste un modo di leggere la riga di un altro. Null se non è entrato o
    /// se la riga non c'è (ancora, o già cancellata).
    /// </summary>
    Task<AccessoAlSitoRiga?> MieiAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IRegistroAccessi"/>
public sealed class RegistroAccessi : IRegistroAccessi
{
    /// <summary>Quante righe si mostrano al massimo: il resto si trova cercando.</summary>
    public const int Limite = 500;

    /// <summary>
    /// L'ultima potatura, per processo. Si pota al più una volta al giorno, dentro una registrazione: niente servizio
    /// in background per una DELETE al giorno. ⚠️ Statico perché il servizio è scoped.
    /// </summary>
    private static long _ultimaPotaturaTicks;

    private readonly IRegistroAccessiStore _store;
    private readonly IEditAuthorizationService _authz;

    public RegistroAccessi(IRegistroAccessiStore store, IEditAuthorizationService authz)
    {
        _store = store;
        _authz = authz;
    }

    public Task<IReadOnlyDictionary<int, string>> NomiBreviAsync(IReadOnlyCollection<int> userIds, CancellationToken ct = default) =>
        userIds.Count == 0
            ? Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string>())
            : _store.NomiBreviAsync(userIds, ct);

    public Task<AccessoAlSitoRiga?> MieiAsync(CancellationToken ct = default) =>
        _authz.CurrentUserId is int vid ? _store.TrovaAsync(vid, ct) : Task.FromResult<AccessoAlSitoRiga?>(null);

    public async Task RegistraAsync(CurrentUser utente, CancellationToken ct = default)
    {
        var ora = DateTime.UtcNow;
        await _store.RegistraAsync(utente.UserId, utente.Name, utente.Division, utente.Acc, ora, utente.ShortName, ct)
            .ConfigureAwait(false);

        if (DevePotare(ref _ultimaPotaturaTicks, ora))
            await _store.PotaAsync(ora - AccessoAlSito.Conservazione, ct).ConfigureAwait(false);
    }

    public Task<ElencoAccessi> ElencoAsync(string? cerca, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        return _store.ElencoAsync(cerca, Limite, ct);
    }

    /// <summary>Vero una volta ogni ventiquattro ore, anche con richieste concorrenti.</summary>
    internal static bool DevePotare(ref long ultima, DateTime ora)
    {
        var prima = Interlocked.Read(ref ultima);
        if (prima != 0 && ora.Ticks - prima < TimeSpan.TicksPerDay) return false;
        return Interlocked.CompareExchange(ref ultima, ora.Ticks, prima) == prima;
    }
}
