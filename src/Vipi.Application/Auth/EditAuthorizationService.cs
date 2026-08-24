using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;

namespace Vipi.Application.Auth;

/// <summary>
/// Autorizzazione all'editing. Admin = staff position che matcha i ruoli admin della divisione
/// (<see cref="DivisionOptions"/>: <c>^{Code}-{ruolo}$</c>, es. IT-DIR/IT-WM/IT-AOC) oppure i pattern
/// espliciti in <see cref="AuthOptions.AdminStaffCodes"/>: editano tutto e gestiscono i grant. Gli altri
/// editano una ACC solo con un <see cref="Vipi.Domain.Entities.EditGrant"/>. Verifica sempre server-side.
/// </summary>
public interface IEditAuthorizationService
{
    bool IsAdmin { get; }
    int? CurrentUserId { get; }
    string? CurrentName { get; }

    Task EnsureCanEditAccAsync(string accCode, CancellationToken ct = default);
    Task EnsureCanEditDocumentAsync(int documentId, CancellationToken ct = default);

    /// <summary>Check non-throwing per la UI: true se l'utente può editare la ACC (admin o grant).</summary>
    Task<bool> CanEditAccAsync(string accCode, CancellationToken ct = default);

    /// <summary>Check non-throwing per la UI: true se l'utente può editare il documento (admin o grant sulla sua ACC).</summary>
    Task<bool> CanEditDocumentAsync(int documentId, CancellationToken ct = default);

    /// <summary>
    /// Vero se l'utente ha qualcosa da editare: admin, oppure almeno una concessione. È la domanda della
    /// BARRA, che deve solo decidere se accendere il tasto «Modifica».
    ///
    /// <para>⚠️ Non è «può editare almeno un documento», ed è voluto: chi ha una concessione su una ACC
    /// che non ha ancora documenti vede il tasto e trova un elenco vuoto — che è il posto giusto dove
    /// scoprirlo. La domanda vecchia (<c>ListEditableDocumentsAsync().Count &gt; 0</c>) costava una query
    /// per documento <b>a ogni pagina</b>, e la pagava solo l'utente loggato non-admin: cioè il socio
    /// qualunque, che di quel tasto non se ne fa niente.</para>
    /// </summary>
    Task<bool> CanEditAnythingAsync(CancellationToken ct = default);

    // Gestione grant (solo admin)
    Task<IReadOnlyList<GrantRow>> ListGrantsAsync(CancellationToken ct = default);
    Task<int> AddGrantAsync(int UserId, string? displayName, string accCode, CancellationToken ct = default);
    Task RevokeGrantAsync(int grantId, CancellationToken ct = default);
    void EnsureAdmin();
}

/// <inheritdoc cref="IEditAuthorizationService"/>
public sealed class EditAuthorizationService : IEditAuthorizationService
{
    private readonly ICurrentUserProvider _user;
    private readonly IEditGrantRepository _grants;
    private readonly Regex[] _adminCodes;

    // L'utente risolto una volta per scope. `_risolto` distingue «non ancora chiesto» da «chiesto, e non
    // c'è nessuno»: senza, l'anonimo rifarebbe il giro a ogni lettura, che è proprio il caso peggiore.
    private CurrentUser? _corrente;
    private bool _risolto;
    private bool? _isAdmin;

    public EditAuthorizationService(
        ICurrentUserProvider user,
        IEditGrantRepository grants,
        IOptions<AuthOptions> options,
        IOptions<DivisionOptions> division)
    {
        _user = user;
        _grants = grants;

        // I pattern stanno in AdminStaffCodes, non qui: li usa anche la diagnostica, e una diagnosi che se li
        // ricalcolasse per conto proprio potrebbe dire «tutto a posto» mentre l'autorizzazione ne usa altri.
        _adminCodes = AdminStaffCodes.Compile(AdminStaffCodes.Patterns(options.Value, division.Value));
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

    public bool IsAdmin =>
        _isAdmin ??= Corrente is { } u && u.StaffPositions.Any(s => _adminCodes.Any(rx => rx.IsMatch(s)));

    public int? CurrentUserId => Corrente?.UserId;
    public string? CurrentName => Corrente?.Name;

    public async Task EnsureCanEditAccAsync(string accCode, CancellationToken ct = default)
    {
        if (IsAdmin) return;
        var u = Corrente;
        if (u is not null && await _grants.HasGrantAsync(u.UserId, accCode, ct)) return;
        throw new EditNotAllowedException();
    }

    public async Task<bool> CanEditAccAsync(string accCode, CancellationToken ct = default)
    {
        if (IsAdmin) return true;
        var u = Corrente;
        return u is not null && await _grants.HasGrantAsync(u.UserId, accCode, ct);
    }

    public async Task EnsureCanEditDocumentAsync(int documentId, CancellationToken ct = default)
    {
        if (IsAdmin) return;
        var acc = await _grants.GetDocumentAccCodeAsync(documentId, ct)
            ?? throw new EditNotAllowedException();
        await EnsureCanEditAccAsync(acc, ct);
    }

    public async Task<bool> CanEditDocumentAsync(int documentId, CancellationToken ct = default)
    {
        if (IsAdmin) return true;
        var acc = await _grants.GetDocumentAccCodeAsync(documentId, ct);
        return acc is not null && await CanEditAccAsync(acc, ct);
    }

    public async Task<bool> CanEditAnythingAsync(CancellationToken ct = default)
    {
        if (IsAdmin) return true;
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

    public void EnsureAdmin()
    {
        if (!IsAdmin) throw new EditNotAllowedException();
    }
}
