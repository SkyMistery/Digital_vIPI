using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Application.Auth;

/// <summary>
/// Roster degli staffisti IT per il picker permessi. Si popola quando un membro con posizioni staff IT
/// si logga (<see cref="RecordLoginAsync"/>), e viene ri-verificato periodicamente via API IVAO
/// (<see cref="VerifyAllAsync"/>): chi non è più staff IT viene disattivato. Compromesso scelto perché
/// l'enumerazione completa della divisione non è leggibile col token app (endpoint 500); il profilo del
/// singolo UserId (<c>/v2/users/{UserId}</c>) invece sì. Lo staffista deve loggarsi almeno una volta per comparire.
/// </summary>
public interface IStaffRosterService
{
    /// <summary>Registra/aggiorna l'utente al login se ha posizioni staff IT. No-op per i non-staff.</summary>
    Task RecordLoginAsync(CurrentUser user, CancellationToken ct = default);

    /// <summary>Staffisti IT attivi, per il dropdown della pagina permessi.</summary>
    Task<IReadOnlyList<StaffRosterEntry>> ListActiveAsync(CancellationToken ct = default);

    /// <summary>Ri-verifica via API tutti i UserId del roster; disattiva chi non è più staff IT. Ritorna i disattivati.</summary>
    Task<int> VerifyAllAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IStaffRosterService"/>
internal sealed class StaffRosterService : IStaffRosterService
{
    private readonly IStaffRosterRepository _repo;
    private readonly IUserDirectory _ivao;
    private readonly Regex _codiceDellaDivisione;

    public StaffRosterService(IStaffRosterRepository repo, IUserDirectory ivao, IOptions<DivisionOptions> division)
    {
        _repo = repo;
        _ivao = ivao;
        _codiceDellaDivisione = CodiceDellaDivisione(division.Value);
    }

    /// <summary>
    /// Un codice staff «della divisione»: quelli di divisione (<c>IT-AOA1</c>) <b>e</b> quelli d'ACC
    /// (<c>LIBB-CH</c>, <c>LIRR-CHA1</c>).
    ///
    /// <para>🔴 <b>Perché anche i secondi (22 settembre 2026).</b> Si guardava il solo prefisso <c>IT-</c>, e i
    /// chief d'ACC hanno il prefisso dell'ACC: <see cref="RoleResolver"/> li faceva Redattori, ma nel roster non
    /// entravano mai — né in Diagnostica, né nel picker dei permessi. Due chief nominati il 21-set
    /// (<c>LIPP-CH</c>, <c>LIBB-CH</c>) si sono loggati e non comparivano da nessuna parte; e la
    /// verifica giornaliera, per la stessa ragione, avrebbe disattivato un chief che ci fosse entrato.</para>
    ///
    /// <para>Il criterio qui è l'<b>appartenenza</b>, non il livello: un <c>LIRR-CHA1</c> che nessun pattern fa
    /// Redattore resta nel roster, ed è proprio in Diagnostica che si vede che non combacia. Fuori restano i
    /// codici di altre divisioni e quelli del quartier generale (<c>HPM</c>).</para>
    /// </summary>
    internal static Regex CodiceDellaDivisione(DivisionOptions division)
    {
        var acc = division.IcaoPrefixes
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => Regex.Escape(p.Trim()) + "[A-Z0-9]+")
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var prefissi = new[] { Regex.Escape(division.Code) }.Concat(acc);
        return new Regex($"^({string.Join("|", prefissi)})-[A-Z0-9]+$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private bool IsDivisionStaffCode(string code) => _codiceDellaDivisione.IsMatch(code.Trim());

    public async Task RecordLoginAsync(CurrentUser user, CancellationToken ct = default)
    {
        var positions = user.StaffPositions.Where(IsDivisionStaffCode).ToList();
        if (positions.Count == 0) return;   // non è staffista della divisione: non entra nel roster
        await _repo.UpsertLoginAsync(user.UserId, user.Name, positions, ct);
    }

    public Task<IReadOnlyList<StaffRosterEntry>> ListActiveAsync(CancellationToken ct = default) =>
        _repo.ListActiveAsync(ct);

    public async Task<int> VerifyAllAsync(CancellationToken ct = default)
    {
        var vids = await _repo.ListAllUserIdsAsync(ct);
        var deactivated = 0;
        foreach (var UserId in vids)
        {
            var info = await _ivao.GetUserAsync(UserId, ct);
            if (info is null) continue;     // errore/transitorio: non modifico lo stato

            var positions = info.StaffPositionCodes.Where(IsDivisionStaffCode).ToList();
            if (info.IsStaff && positions.Count > 0)
            {
                await _repo.UpdateVerifiedAsync(UserId, info.Nickname, info.AtcRating, positions, ct);
            }
            else
            {
                await _repo.DeactivateAsync(UserId, ct);
                deactivated++;
            }
        }
        return deactivated;
    }
}
