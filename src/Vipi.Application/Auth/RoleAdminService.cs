using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Auth;

/// <summary>
/// Una persona nella pagina dei permessi: quello che le dà IVAO, quello che le abbiamo dato noi, e la
/// somma dei due.
/// </summary>
/// <param name="Floor">Il livello garantito dalle posizioni staff. <b>Non si scende sotto</b>.</param>
/// <param name="Override">La promozione a mano, se c'è.</param>
/// <param name="Effective">Il livello che vale davvero: <c>max(Floor, Override)</c>.</param>
public sealed record RoleRow(
    int UserId,
    string? DisplayName,
    IReadOnlyList<string> StaffPositions,
    VipiRole Floor,
    VipiRole? Override,
    VipiRole Effective,
    DateTime? GrantedAtUtc,
    int? GrantedByUserId,
    string? Note)
{
    /// <summary>Vero se questa riga esiste solo per una promozione: la persona non è staff di nessuno.</summary>
    public bool SoloPromossa => StaffPositions.Count == 0;
}

/// <summary>
/// La gestione dei livelli: chi c'è, e come si promuove o si declassa. Carta
/// <c>docs/feature/2026-08-28-autorizzazioni-a-livelli.md</c> §5.
///
/// <para>Sostituisce la gestione delle concessioni per ACC, morta il 28 agosto 2026 insieme al concetto
/// che le reggeva.</para>
/// </summary>
public interface IRoleAdminService
{
    /// <summary>
    /// Tutte le persone che il sistema conosce: gli staffisti visti ai login <b>più</b> chiunque abbia una
    /// promozione a mano — anche se non è staff di nessuno, che è esattamente il caso per cui la
    /// promozione esiste.
    /// </summary>
    Task<IReadOnlyList<RoleRow>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Assegna un livello a mano.
    ///
    /// <para>⚠️ <b>Sotto il pavimento non è vietato: è inerte</b>, perché il livello effettivo è il
    /// <c>max</c>. Qui però si rifiuta lo stesso, e non per pignoleria: un comando che accetta e non fa
    /// niente è peggio di un comando che dice di no. La pagina disabilita quei livelli; questo è il
    /// controllo server-side che le corrisponde.</para>
    ///
    /// <para>⚠️ E non ci si declassa da soli: è il modo esatto in cui un admin si chiude fuori.</para>
    /// </summary>
    Task SetAsync(int userId, VipiRole level, string? note, CancellationToken ct = default);

    /// <summary>Toglie la promozione: la persona torna a quello che le dà la sua posizione staff.</summary>
    Task RemoveAsync(int userId, CancellationToken ct = default);
}

/// <inheritdoc cref="IRoleAdminService"/>
public sealed class RoleAdminService : IRoleAdminService
{
    private readonly IStaffRosterRepository _roster;
    private readonly IRoleOverrideStore _store;
    private readonly IRoleOverrides _cache;
    private readonly RoleResolver _resolver;
    private readonly IEditAuthorizationService _authz;

    public RoleAdminService(IStaffRosterRepository roster, IRoleOverrideStore store, IRoleOverrides cache,
        RoleResolver resolver, IEditAuthorizationService authz)
    {
        _roster = roster;
        _store = store;
        _cache = cache;
        _resolver = resolver;
        _authz = authz;
    }

    public async Task<IReadOnlyList<RoleRow>> ListAsync(CancellationToken ct = default)
    {
        _authz.EnsureAdmin();

        var promozioni = (await _store.ListAsync(ct)).ToDictionary(o => o.UserId);
        var staffisti = await _roster.ListActiveAsync(ct);

        var righe = staffisti
            .Select(s => Riga(s.UserId, s.DisplayName, s.StaffPositions, promozioni))
            .ToList();

        // ⚠️ Chi ha una promozione ma NON è nel roster va aggiunto, non saltato: è il socio qualunque
        // promosso a mano, cioè il caso per cui la promozione esiste. Saltarlo significherebbe che la
        // pagina dei permessi non mostra un permesso che ha dato lei.
        var noti = staffisti.Select(s => s.UserId).ToHashSet();
        righe.AddRange(promozioni.Values
            .Where(o => !noti.Contains(o.UserId))
            .Select(o => Riga(o.UserId, o.DisplayName, Array.Empty<string>(), promozioni)));

        return righe
            .OrderByDescending(r => r.Effective)
            .ThenBy(r => r.DisplayName ?? r.UserId.ToString(), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task SetAsync(int userId, VipiRole level, string? note, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        if (userId <= 0) throw new Aor.ValidationException(Messaggio.Lingua(
            "VID non valido.", "Not a valid VID."));

        if (userId == _authz.CurrentUserId)
            throw new Aor.ValidationException(Messaggio.Lingua(
                "Non si cambia il proprio livello: è il modo esatto in cui ci si chiude fuori.",
                "You cannot change your own level: that is exactly how one locks oneself out."));

        if (_resolver.Founders.Contains(userId))
            throw new Aor.ValidationException(Messaggio.Lingua(
                "Chi ha costruito il sistema è admin per configurazione: qui non si tocca.",
                "The system's author is an admin by configuration: not editable here."));

        var pavimento = await PavimentoAsync(userId, ct);
        if (level < pavimento)
            throw new Aor.ValidationException(Messaggio.Lingua(
                $"La sua posizione staff gli garantisce già «{pavimento}»: un livello più basso non farebbe niente.",
                $"Their IVAO staff position already grants «{pavimento}»: a lower level would do nothing."));

        var nome = (await _roster.FindAsync(userId, ct))?.DisplayName;
        await _store.SetAsync(userId, level, _authz.CurrentUserId ?? 0, nome, note, ct);
        await _cache.ReloadAsync(ct);   // senza questa, la promozione non fa effetto fino al riavvio
    }

    public async Task RemoveAsync(int userId, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();

        if (userId == _authz.CurrentUserId)
            throw new Aor.ValidationException(Messaggio.Lingua(
                "Non si cambia il proprio livello: è il modo esatto in cui ci si chiude fuori.",
                "You cannot change your own level: that is exactly how one locks oneself out."));

        await _store.RemoveAsync(userId, _authz.CurrentUserId ?? 0, ct);
        await _cache.ReloadAsync(ct);
    }

    private RoleRow Riga(int userId, string? nome, IReadOnlyList<string> posizioni,
        IReadOnlyDictionary<int, RoleOverrideRow> promozioni)
    {
        var pavimento = _resolver.Resolve(userId, posizioni);
        var promozione = promozioni.TryGetValue(userId, out var o) ? o : null;

        return new RoleRow(
            userId, nome, posizioni, pavimento, promozione?.Level,
            _resolver.Effective(userId, posizioni, promozione?.Level),
            promozione?.GrantedAtUtc, promozione?.GrantedByUserId, promozione?.Note);
    }

    private async Task<VipiRole> PavimentoAsync(int userId, CancellationToken ct)
    {
        // ⚠️ Le posizioni staff si ripescano dal roster, che le raccoglie ai login: per chi non è mai
        // entrato non si sanno, e il pavimento risulta User. È giusto così — quel che sappiamo è quello —
        // e al primo login vero il `max` rimette le cose a posto da solo.
        var s = await _roster.FindAsync(userId, ct);
        return _resolver.Resolve(userId, s?.StaffPositions ?? Array.Empty<string>());
    }
}
