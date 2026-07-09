using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;

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
        var uid = _authz.CurrentUserId ?? 0;
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
            throw new Aor.ValidationException("Titolo obbligatorio.");
        var me = _authz.CurrentUserId ?? throw new Aor.ValidationException("Non autenticato.");

        // Non admin: può assegnare SOLO a sé stesso e, se il task è legato a un documento, deve poterlo editare.
        if (!_authz.IsAdmin)
        {
            if (input.AssigneeUserId != me)
                throw new Aor.ValidationException("Solo un admin può assegnare incarichi ad altri.");
            await EnsureCanEditTargetAsync(input.TargetType, input.TargetKey, ct);
        }
        return await _repo.AddAsync(input, me, ct);
    }

    public async Task UpdateStatusAsync(int id, EditorTaskStatus status, CancellationToken ct = default)
    {
        var t = await _repo.GetAsync(id, ct) ?? throw new Aor.ValidationException("Incarico inesistente.");
        var me = _authz.CurrentUserId ?? 0;
        if (!_authz.IsAdmin && t.AssigneeUserId != me)
            throw new Aor.ValidationException("Puoi aggiornare solo i tuoi incarichi.");
        await _repo.UpdateStatusAsync(id, status, ct);
    }

    public async Task AssignAsync(int id, int assigneeUserId, string? assigneeName, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        await _repo.AssignAsync(id, assigneeUserId, assigneeName, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var t = await _repo.GetAsync(id, ct) ?? throw new Aor.ValidationException("Incarico inesistente.");
        var me = _authz.CurrentUserId ?? 0;
        if (!_authz.IsAdmin && t.CreatedByUserId != me)
            throw new Aor.ValidationException("Puoi eliminare solo gli incarichi che hai creato.");
        await _repo.DeleteAsync(id, ct);
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
        if (acc is null) throw new Aor.ValidationException("Documento collegato inesistente.");
        await _authz.EnsureCanEditAccAsync(acc, ct);
    }
}
