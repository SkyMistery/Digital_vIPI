using Microsoft.Extensions.DependencyInjection;
using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Auth;

/// <summary>
/// Le promozioni a mano, <b>tenute in memoria</b>: la metà scritta a mano del livello di una persona.
/// Carta <c>docs/feature/2026-08-28-autorizzazioni-a-livelli.md</c> §6.
///
/// <para><b>Perché una cache, e perché non è un'ottimizzazione.</b> «Che livello ha questa persona?» è una
/// domanda che arriva a <b>ogni</b> richiesta, e nel layout: è la posizione esatta in cui una query in più
/// ha già prodotto due volte le corse sul <c>DbContext</c> di circuito. Questa funzione toglie
/// <c>HasAnyGrantAsync</c> da lì; leggere l'override con una <c>SELECT</c> per richiesta lo rimetterebbe,
/// con un nome diverso.</para>
///
/// <para><b>Perché si può.</b> La tabella ha una riga per persona promossa a mano: poche decine, sempre.
/// Tenerla intera costa nulla, e l'invalidazione è banale perché la scrive un admin dalla sua pagina.</para>
///
/// <para>⚠️ <b>La lettura è sincrona, e deve restarlo.</b> <c>IsAdmin</c> è valutato dentro il markup —
/// <c>StrutturaPage</c> lo chiede sette volte per render, una dentro il <c>foreach</c> sui nodi. Un livello
/// che si risolvesse con un <c>await</c> non entrerebbe in quei punti senza riscriverli tutti.</para>
/// </summary>
public interface IRoleOverrides
{
    /// <summary>Vero quando il fotogramma in memoria viene da una lettura riuscita.</summary>
    bool Loaded { get; }

    /// <summary>
    /// Il livello scritto a mano per quel VID, o <c>null</c> se non ce n'è.
    ///
    /// <para>⚠️ <c>null</c> significa <b>«nessuna promozione»</b>, non «non lo so»: chi chiama ricade sul
    /// livello garantito dalle posizioni staff, che è il comportamento giusto anche se la tabella non fosse
    /// mai stata letta. Una promozione che non ha ancora fatto effetto è un fastidio; un permesso negato a
    /// chi lo ha per ruolo sarebbe un guasto.</para>
    /// </summary>
    VipiRole? For(int userId);

    /// <summary>Il fotogramma intero: lo mostra la pagina dei permessi e la diagnostica.</summary>
    IReadOnlyDictionary<int, VipiRole> All { get; }

    /// <summary>Rilegge la tabella e sostituisce il fotogramma. La chiama l'avvio e ogni scrittura.</summary>
    Task ReloadAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IRoleOverrides"/>
public sealed class RoleOverrideCache : IRoleOverrides
{
    private static readonly IReadOnlyDictionary<int, VipiRole> Vuoto = new Dictionary<int, VipiRole>();

    private readonly IServiceScopeFactory _scopes;

    // volatile: lo scrive chi ricarica (un admin, o l'avvio), lo leggono tutte le richieste. Il fotogramma
    // si SOSTITUISCE intero e non si modifica mai in posto — così un lettore vede sempre un dizionario
    // coerente, quello di prima o quello di dopo, senza bisogno di un lock in lettura.
    private volatile IReadOnlyDictionary<int, VipiRole> _snapshot = Vuoto;
    private volatile bool _loaded;

    /// <summary>
    /// Il servizio è <b>singleton</b> e lo store è <b>scoped</b> (vive su un <c>DbContext</c>): lo scope se
    /// lo apre la ricarica, che è l'unico momento in cui si tocca il database.
    /// </summary>
    public RoleOverrideCache(IServiceScopeFactory scopes) => _scopes = scopes;

    public bool Loaded => _loaded;

    public IReadOnlyDictionary<int, VipiRole> All => _snapshot;

    public VipiRole? For(int userId) =>
        _snapshot.TryGetValue(userId, out var level) ? level : null;

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IRoleOverrideStore>();

        var righe = await store.ListAsync(ct);
        _snapshot = righe.ToDictionary(r => r.UserId, r => r.Level);
        _loaded = true;
    }
}
