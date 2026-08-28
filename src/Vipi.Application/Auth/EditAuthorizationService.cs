using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Auth;

/// <summary>
/// Chi può cosa. Il livello di una persona è <b>un numero ordinato</b> (<see cref="VipiRole"/>) e ogni
/// cancello è un confronto <c>&gt;=</c>. Carta <c>docs/feature/2026-08-28-autorizzazioni-a-livelli.md</c>.
///
/// <para>Il livello è <c>max(quello garantito dalle posizioni staff IVAO, la promozione a mano)</c>:
/// <see cref="RoleResolver"/> per il primo, <see cref="IRoleOverrides"/> per la seconda. Verifica sempre
/// server-side: quello che la pagina nasconde, il servizio deve comunque rifiutarlo.</para>
/// </summary>
public interface IEditAuthorizationService
{
    /// <summary>Il livello effettivo dell'utente corrente. Anonimo = <see cref="VipiRole.User"/>.</summary>
    VipiRole Role { get; }

    /// <summary>Direzione della divisione e fondatori: sorgenti, incarichi, audit, diagnostica, permessi.</summary>
    bool IsAdmin { get; }

    /// <summary>
    /// Chief d'ACC e chi sta sopra: il contenuto documentale, tutto.
    ///
    /// <para>⚠️ <b>I predicati derivati e il cancello hanno un'implementazione di default</b>, e non è
    /// pigrizia: sono la <i>stessa</i> domanda posta a soglie diverse, e nessuna implementazione ha una
    /// ragione legittima per rispondere in modo suo. Scriverli in ogni classe significherebbe soltanto
    /// offrire ventitré occasioni di sbagliare un <c>&gt;=</c> sul permesso più alto del prodotto.</para>
    /// </summary>
    bool IsEditor => Role >= VipiRole.Editor;

    /// <inheritdoc cref="IsEditor"/>
    bool IsDivisionStaff => Role >= VipiRole.DivisionStaff;

    int? CurrentUserId { get; }
    string? CurrentName { get; }

    Task EnsureCanEditAccAsync(string accCode, CancellationToken ct = default);
    Task EnsureCanEditDocumentAsync(int documentId, CancellationToken ct = default);

    /// <summary>Check non-throwing per la UI: true se l'utente può editare la ACC (livello o concessione).</summary>
    Task<bool> CanEditAccAsync(string accCode, CancellationToken ct = default);

    /// <summary>Check non-throwing per la UI: true se l'utente può editare il documento.</summary>
    Task<bool> CanEditDocumentAsync(int documentId, CancellationToken ct = default);

    /// <summary>
    /// Vero se l'utente ha qualcosa da editare: è la domanda della BARRA, che deve solo decidere se
    /// accendere il tasto «Modifica».
    ///
    /// <para>⚠️ Non è «può editare almeno un documento», ed è voluto: chi ha una concessione su una ACC
    /// che non ha ancora documenti vede il tasto e trova un elenco vuoto — che è il posto giusto dove
    /// scoprirlo. La domanda vecchia (<c>ListEditableDocumentsAsync().Count &gt; 0</c>) costava una query
    /// per documento <b>a ogni pagina</b>, e la pagava solo l'utente loggato non-admin.</para>
    /// </summary>
    Task<bool> CanEditAnythingAsync(CancellationToken ct = default);

    // Gestione grant (solo admin)
    Task<IReadOnlyList<GrantRow>> ListGrantsAsync(CancellationToken ct = default);
    Task<int> AddGrantAsync(int UserId, string? displayName, string accCode, CancellationToken ct = default);
    Task RevokeGrantAsync(int grantId, CancellationToken ct = default);

    /// <summary>Rifiuta se il livello effettivo è sotto <paramref name="minimo"/>. È il cancello, in una riga.</summary>
    /// <inheritdoc cref="IsEditor" path="/summary/para"/>
    void EnsureAtLeast(VipiRole minimo)
    {
        if (Role < minimo) throw new EditNotAllowedException();
    }

    /// <inheritdoc cref="EnsureAtLeast"/>
    void EnsureAdmin();
}

/// <inheritdoc cref="IEditAuthorizationService"/>
public sealed class EditAuthorizationService : IEditAuthorizationService
{
    private readonly ICurrentUserProvider _user;
    private readonly IEditGrantRepository _grants;
    private readonly RoleResolver _resolver;
    private readonly IRoleOverrides _overrides;

    // L'utente risolto una volta per scope. `_risolto` distingue «non ancora chiesto» da «chiesto, e non
    // c'è nessuno»: senza, l'anonimo rifarebbe il giro a ogni lettura, che è proprio il caso peggiore.
    private CurrentUser? _corrente;
    private bool _risolto;
    private VipiRole? _role;

    public EditAuthorizationService(
        ICurrentUserProvider user,
        IEditGrantRepository grants,
        RoleResolver resolver,
        IRoleOverrides overrides)
    {
        _user = user;
        _grants = grants;
        _resolver = resolver;
        _overrides = overrides;
    }

    /// <summary>
    /// L'utente corrente, risolto <b>una volta per scope</b>.
    ///
    /// <para><b>Perché memoizzare.</b> Ogni chiamata a <c>ICurrentUserProvider.Get()</c> rilegge i claim
    /// dall'<c>HttpContext</c> e, per <c>userStaffPositions</c>, <b>rifà il parse di un array JSON</b>
    /// (vedi <c>HostIdentityCurrentUserProvider</c>). Le pagine chiamano <c>IsAdmin</c> dentro il markup:
    /// <c>StrutturaPage</c> lo valuta sette volte per render, e una di quelle sta dentro il <c>foreach</c>
    /// sui nodi della gerarchia — su ~300 callsign sono ~300 parse JSON <b>a ogni ridisegno</b>, per
    /// rispondere sempre la stessa cosa.</para>
    ///
    /// <para><b>Perché è sicuro.</b> Il servizio è <c>Scoped</c>. Su una richiesta HTTP lo scope è la
    /// richiesta, e l'identità non cambia a metà. In un circuito Blazor lo scope è il circuito, e lì
    /// l'identità <b>era già</b> di fatto fissa: viene dall'<c>HttpContext</c> della richiesta di upgrade,
    /// che resta quella per tutta la vita della connessione. Un login o un logout aprono una pagina nuova,
    /// quindi un circuito nuovo, quindi uno scope nuovo.</para>
    /// </summary>
    private CurrentUser? Corrente
    {
        get
        {
            if (_risolto) return _corrente;
            _corrente = _user.Get();
            _risolto = true;
            return _corrente;
        }
    }

    /// <summary>
    /// Il livello effettivo, memoizzato per scope come l'identità.
    ///
    /// <para>⚠️ <b>Nessuna query.</b> Le posizioni staff vengono dai claim e la promozione a mano dal
    /// fotogramma in memoria: rispondere «che livello ha questa persona?» non tocca il database. È la
    /// condizione perché la domanda si possa fare dentro il markup, dove si fa.</para>
    /// </summary>
    public VipiRole Role => _role ??= _resolver.Effective(Corrente, _overrides.For(Corrente?.UserId ?? 0));

    public bool IsAdmin => Role >= VipiRole.Admin;
    public bool IsEditor => Role >= VipiRole.Editor;
    public bool IsDivisionStaff => Role >= VipiRole.DivisionStaff;

    public int? CurrentUserId => Corrente?.UserId;
    public string? CurrentName => Corrente?.Name;

    public async Task EnsureCanEditAccAsync(string accCode, CancellationToken ct = default)
    {
        if (IsEditor) return;
        var u = Corrente;
        if (u is not null && await _grants.HasGrantAsync(u.UserId, accCode, ct)) return;
        throw new EditNotAllowedException();
    }

    public async Task<bool> CanEditAccAsync(string accCode, CancellationToken ct = default)
    {
        if (IsEditor) return true;
        var u = Corrente;
        return u is not null && await _grants.HasGrantAsync(u.UserId, accCode, ct);
    }

    public async Task EnsureCanEditDocumentAsync(int documentId, CancellationToken ct = default)
    {
        if (IsEditor) return;
        var acc = await _grants.GetDocumentAccCodeAsync(documentId, ct)
            ?? throw new EditNotAllowedException();
        await EnsureCanEditAccAsync(acc, ct);
    }

    public async Task<bool> CanEditDocumentAsync(int documentId, CancellationToken ct = default)
    {
        if (IsEditor) return true;
        var acc = await _grants.GetDocumentAccCodeAsync(documentId, ct);
        return acc is not null && await CanEditAccAsync(acc, ct);
    }

    public async Task<bool> CanEditAnythingAsync(CancellationToken ct = default)
    {
        if (IsEditor) return true;
        return Corrente is { } u && await _grants.HasAnyGrantAsync(u.UserId, ct);
    }

    public Task<IReadOnlyList<GrantRow>> ListGrantsAsync(CancellationToken ct = default)
    {
        EnsureAdmin();
        return _grants.ListAsync(ct);
    }

    public Task<int> AddGrantAsync(int UserId, string? displayName, string accCode, CancellationToken ct = default)
    {
        EnsureAdmin();
        return _grants.AddAsync(UserId, displayName, accCode, CurrentUserId ?? 0, ct);
    }

    public Task RevokeGrantAsync(int grantId, CancellationToken ct = default)
    {
        EnsureAdmin();
        return _grants.RevokeAsync(grantId, CurrentUserId ?? 0, ct);
    }

    public void EnsureAtLeast(VipiRole minimo)
    {
        if (Role < minimo) throw new EditNotAllowedException();
    }

    public void EnsureAdmin() => EnsureAtLeast(VipiRole.Admin);
}
