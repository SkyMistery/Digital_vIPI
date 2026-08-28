using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using static Vipi.Application.Messaggio;

namespace Vipi.Application.Content;

/// <summary>
/// Use-case degli incarichi editoriali. Admin: crea/assegna/monitora tutto. Editor: vede i propri incarichi, ne
/// aggiorna lo stato, e può auto-assegnarsi task (liberi propri, o su documenti che può editare). Ciclo AIRAC
/// corrente usato per evidenziare i ritardi.
/// </summary>
public interface IEditorTaskService
{
    Task<IReadOnlyList<EditorTask>> ListMineAsync(CancellationToken ct = default);
    Task<IReadOnlyList<EditorTask>> ListAllAsync(CancellationToken ct = default);

    /// <summary>Crea un incarico. Admin: assegna a chiunque. Editor: solo a sé stesso, e se il task è legato a un
    /// documento deve poterlo editare.</summary>
    Task<int> CreateAsync(EditorTaskInput input, CancellationToken ct = default);

    /// <summary>Cambia stato. Admin sempre; l'assegnatario sul proprio incarico.</summary>
    Task UpdateStatusAsync(int id, EditorTaskStatus status, CancellationToken ct = default);

    /// <summary>Riassegna (solo admin).</summary>
    Task AssignAsync(int id, int assigneeUserId, string? assigneeName, CancellationToken ct = default);

    /// <summary>Elimina. Admin sempre; il creatore sul proprio incarico.</summary>
    Task DeleteAsync(int id, CancellationToken ct = default);

    string CurrentCycle();

    /// <summary>I prossimi cicli AIRAC (per il selettore scadenza).</summary>
    IReadOnlyList<AiracCycleInfo> UpcomingCycles(int count);

    /// <summary>Vero se l'incarico è in ritardo: ha una scadenza AIRAC passata e non è concluso.</summary>
    bool IsOverdue(EditorTask t);
}

/// <inheritdoc cref="IEditorTaskService"/>
public sealed class EditorTaskService : IEditorTaskService
{
    private readonly IEditorTaskRepository _repo;
    private readonly IEditAuthorizationService _authz;
    private readonly IReleaseRepository _releases;
    private readonly IAiracService _airac;

    public EditorTaskService(IEditorTaskRepository repo, IEditAuthorizationService authz,
        IReleaseRepository releases, IAiracService airac)
    {
        _repo = repo;
        _authz = authz;
        _releases = releases;
        _airac = airac;
    }

    public Task<IReadOnlyList<EditorTask>> ListMineAsync(CancellationToken ct = default)
    {
        // ⚠️ Senza identità l'elenco è VUOTO, non «quello del VID 0». Il `?? 0` di prima era innocuo solo
        // finché nessun incarico aveva l'assegnatario 0 — e da questa pagina se ne creavano (vedi CreateAsync).
        if (_authz.CurrentUserId is not int uid || uid <= 0)
            return Task.FromResult<IReadOnlyList<EditorTask>>(Array.Empty<EditorTask>());
        return _repo.ListByAssigneeAsync(uid, ct);
    }

    public Task<IReadOnlyList<EditorTask>> ListAllAsync(CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        return _repo.ListAllAsync(ct);
    }

    public async Task<int> CreateAsync(EditorTaskInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
            throw new Aor.ValidationException("Titolo obbligatorio.", "Task_Err_TitleRequired");

        // ⚠️ Un incarico assegnato al VID 0 non è di nessuno: non compare negli incarichi di nessun utente
        // (nessuno ha VID 0), si vede solo nell'elenco admin, e non lo si può nemmeno riassegnare. Nasceva
        // premendo «Crea» senza scegliere la persona, perché l'opzione «Seleziona» vale 0.
        if (input.AssigneeUserId <= 0)
            throw new Aor.ValidationException("Scegli a chi assegnare l'incarico.", "Task_Err_AssigneeRequired");

        var me = _authz.CurrentUserId ?? throw new Aor.ValidationException(Lingua("Non autenticato.", "Not signed in."), "Task_Err_NotAuthenticated");

        // Non admin: può assegnare SOLO a sé stesso e, se il task è legato a un documento, deve poterlo editare.
        if (!_authz.IsAdmin)
        {
            if (input.AssigneeUserId != me)
                throw new Aor.ValidationException("Solo un admin può assegnare incarichi ad altri.", "Task_Err_AssignOnlySelf");
            await EnsureCanEditTargetAsync(input.TargetType, input.TargetKey, ct);
        }
        return await _repo.AddAsync(input, me, ct);
    }

    public async Task UpdateStatusAsync(int id, EditorTaskStatus status, CancellationToken ct = default)
    {
        var t = await _repo.GetAsync(id, ct) ?? throw new Aor.ValidationException("Incarico inesistente.", "Task_Err_NotFound");
        var me = _authz.CurrentUserId ?? 0;
        if (!_authz.IsAdmin && t.AssigneeUserId != me)
            throw new Aor.ValidationException("Puoi aggiornare solo i tuoi incarichi.", "Task_Err_UpdateOnlyMine");
        await _repo.UpdateStatusAsync(id, status, me, ct);
    }

    public async Task AssignAsync(int id, int assigneeUserId, string? assigneeName, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        if (assigneeUserId <= 0)
            throw new Aor.ValidationException("Scegli a chi assegnare l'incarico.", "Task_Err_AssigneeRequired");
        await _repo.AssignAsync(id, assigneeUserId, assigneeName, _authz.CurrentUserId ?? 0, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var t = await _repo.GetAsync(id, ct) ?? throw new Aor.ValidationException("Incarico inesistente.", "Task_Err_NotFound");
        var me = _authz.CurrentUserId ?? 0;
        if (!_authz.IsAdmin && t.CreatedByUserId != me)
            throw new Aor.ValidationException("Puoi eliminare solo gli incarichi che hai creato.", "Task_Err_DeleteOnlyMine");
        await _repo.DeleteAsync(id, me, ct);
    }

    public string CurrentCycle() => _airac.GetCycle(DateTime.UtcNow);

    // Parte dal ciclo SUCCESSIVO (il corrente non è una scadenza futura utile). Salta il primo di NextCycles.
    public IReadOnlyList<AiracCycleInfo> UpcomingCycles(int count) =>
        _airac.NextCycles(DateTime.UtcNow, count + 1).Skip(1).ToList();

    public bool IsOverdue(EditorTask t)
    {
        if (t.Status == EditorTaskStatus.Done || string.IsNullOrWhiteSpace(t.DueAiracCycle)) return false;
        try { return _airac.EffectiveUtcForCycle(t.DueAiracCycle) < _airac.EffectiveUtcForCycle(CurrentCycle()); }
        catch (ArgumentException) { return false; }
    }

    private async Task EnsureCanEditTargetAsync(ReleaseTargetType? type, string? key, CancellationToken ct)
    {
        if (type is null || string.IsNullOrWhiteSpace(key)) return;   // incarico libero: nessun documento da autorizzare
        var acc = await _releases.GetAuthAccCodeAsync(type.Value, key!, ct);
        if (acc is null) throw new Aor.ValidationException("Documento collegato inesistente.", "Task_Err_TargetMissing");
        await _authz.EnsureCanEditAccAsync(acc, ct);
    }
}
