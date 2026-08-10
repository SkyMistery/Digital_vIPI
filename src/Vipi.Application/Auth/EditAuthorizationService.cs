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

    public bool IsAdmin
    {
        get
        {
            var u = _user.Get();
            return u is not null && u.StaffPositions.Any(s => _adminCodes.Any(rx => rx.IsMatch(s)));
        }
    }

    public int? CurrentUserId => _user.Get()?.UserId;
    public string? CurrentName => _user.Get()?.Name;

    public async Task EnsureCanEditAccAsync(string accCode, CancellationToken ct = default)
    {
        if (IsAdmin) return;
        var u = _user.Get();
        if (u is not null && await _grants.HasGrantAsync(u.UserId, accCode, ct)) return;
        throw new EditNotAllowedException();
    }

    public async Task<bool> CanEditAccAsync(string accCode, CancellationToken ct = default)
    {
        if (IsAdmin) return true;
        var u = _user.Get();
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
        return _grants.RevokeAsync(grantId, ct);
    }

    public void EnsureAdmin()
    {
        if (!IsAdmin) throw new EditNotAllowedException();
    }
}
