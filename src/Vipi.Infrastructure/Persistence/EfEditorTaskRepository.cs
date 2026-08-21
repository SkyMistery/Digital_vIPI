using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="IEditorTaskRepository"/>
public sealed class EfEditorTaskRepository : IEditorTaskRepository
{
    private readonly VipiDbContext _db;
    public EfEditorTaskRepository(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<EditorTask>> ListByAssigneeAsync(int userId, CancellationToken ct = default) =>
        await Ordinati(_db.EditorTasks.AsNoTracking().Where(t => t.AssigneeUserId == userId)).ToListAsync(ct);

    public async Task<IReadOnlyList<EditorTask>> ListAllAsync(CancellationToken ct = default) =>
        await Ordinati(_db.EditorTasks.AsNoTracking()).ToListAsync(ct);

    /// <summary>
    /// L'ordine dell'elenco: concluso in fondo, poi priorità, poi scadenza, poi titolo.
    ///
    /// <para>⚠️ <b>Non più l'ultimo tocco.</b> Fino al 22 agosto 2026 si ordinava per <c>UpdatedUtc</c>
    /// discendente, e <c>UpdateStatusAsync</c> riscrive proprio <c>UpdatedUtc</c>: cambiare lo stato di una
    /// riga in mezzo alla tabella la faceva <b>saltare in cima</b>, e sotto il puntatore arrivava un'altra
    /// riga. Con una tendina che scrive al primo cambio, senza conferma e senza undo, il clic successivo
    /// finiva sull'incarico sbagliato. Un elenco su cui si agisce riga per riga non può riordinarsi per
    /// effetto dell'azione.</para>
    ///
    /// <para>La scadenza è un ciclo AIRAC «YYNN»: l'ordine alfabetico <b>è</b> quello cronologico, e chi non
    /// ha scadenza va in fondo al suo gruppo, non davanti a chi ce l'ha. L'Id chiude l'ordine perché due
    /// incarichi identici devono comunque uscire sempre nello stesso ordine.</para>
    /// </summary>
    private static IQueryable<EditorTask> Ordinati(IQueryable<EditorTask> q) =>
        q.OrderBy(t => t.Status == EditorTaskStatus.Done)
            // ⚠️ Gli enum sono persistiti come TESTO (varchar(32), cutover MariaDB): ordinare per `Priority`
            // ordina le PAROLE — «High, Low, Normal» — e il risultato sembra casuale invece che sbagliato.
            // Il rango si scrive a mano, e il confronto per uguaglianza (quello sì) resta leggibile in SQL.
            .ThenBy(t => t.Priority == EditorTaskPriority.High ? 0 : t.Priority == EditorTaskPriority.Normal ? 1 : 2)
            .ThenBy(t => t.DueAiracCycle == null)
            .ThenBy(t => t.DueAiracCycle)
            .ThenBy(t => t.Title)
            .ThenBy(t => t.Id);

    public Task<EditorTask?> GetAsync(int id, CancellationToken ct = default) =>
        _db.EditorTasks.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<int> AddAsync(EditorTaskInput input, int createdByUserId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var t = new EditorTask
        {
            Title = input.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
            AssigneeUserId = input.AssigneeUserId,
            AssigneeName = input.AssigneeName,
            CreatedByUserId = createdByUserId,
            Status = EditorTaskStatus.Todo,
            Priority = input.Priority,
            DueAiracCycle = string.IsNullOrWhiteSpace(input.DueAiracCycle) ? null : input.DueAiracCycle.Trim(),
            TargetType = input.TargetType,
            TargetKey = input.TargetKey,
            TargetLabel = input.TargetLabel,
            CreatedUtc = now,
            UpdatedUtc = now,
        };
        _db.EditorTasks.Add(t);
        // ⚠️ L'Id non c'è ancora: la riga di registro vuole l'incarico salvato per poterlo nominare, e resta
        // comunque nella stessa transazione logica dell'atto (secondo SaveChanges, stesso metodo).
        await _db.SaveChangesAsync(ct);
        AuditScribe.Write(_db, createdByUserId, AuditAction.Create, "EditorTask", t.Id.ToString(), new
        {
            Title = t.Title,
            AssigneeUserId = t.AssigneeUserId,
            AssigneeName = t.AssigneeName,
            Priority = t.Priority.ToString(),
            Due = t.DueAiracCycle,
            Target = t.TargetLabel ?? t.TargetKey,
        });
        await _db.SaveChangesAsync(ct);
        return t.Id;
    }

    /// <summary>⚠️ Il non-evento non si scrive: rimettere lo stato che c'è già non tocca <c>UpdatedUtc</c> e
    /// non salva niente. Non è pignoleria — è la stessa regola del registro di audit (una riga «non è
    /// cambiato niente» su un elenco che cresce per sempre è l'unico modo garantito di renderlo illeggibile),
    /// e qui difende anche l'ora dell'ultimo cambio, che è il dato con cui si capisce se un incarico è fermo.</summary>
    public async Task UpdateStatusAsync(int id, EditorTaskStatus status, int actorUserId, CancellationToken ct = default)
    {
        var t = await _db.EditorTasks.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new InvalidOperationException($"Incarico {id} inesistente.");
        if (t.Status == status) return;
        var prima = t.Status;
        t.Status = status;
        t.UpdatedUtc = DateTime.UtcNow;
        t.CompletedUtc = status == EditorTaskStatus.Done ? DateTime.UtcNow : null;
        AuditScribe.Write(_db, actorUserId, AuditAction.Update, "EditorTask", t.Id.ToString(),
            new { Title = t.Title, Da = prima.ToString(), A = status.ToString() });
        await _db.SaveChangesAsync(ct);
    }

    public async Task AssignAsync(int id, int assigneeUserId, string? assigneeName, int actorUserId, CancellationToken ct = default)
    {
        var t = await _db.EditorTasks.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new InvalidOperationException($"Incarico {id} inesistente.");
        if (t.AssigneeUserId == assigneeUserId && t.AssigneeName == assigneeName) return;   // non-evento
        var (daId, daNome) = (t.AssigneeUserId, t.AssigneeName);
        t.AssigneeUserId = assigneeUserId;
        t.AssigneeName = assigneeName;
        t.UpdatedUtc = DateTime.UtcNow;
        AuditScribe.Write(_db, actorUserId, AuditAction.Update, "EditorTask", t.Id.ToString(),
            new { Title = t.Title, DaUserId = daId, DaNome = daNome, AUserId = assigneeUserId, ANome = assigneeName });
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>⚠️ La riga di registro si scrive <b>prima</b> della cancellazione, quando il titolo e
    /// l'assegnatario sono ancora leggibili: dopo, «eliminato l'incarico 12» non distingue una pulizia da un
    /// incidente, e il titolo non è più recuperabile da nessuna parte (regola 136).</summary>
    public async Task DeleteAsync(int id, int actorUserId, CancellationToken ct = default)
    {
        var t = await _db.EditorTasks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return;
        AuditScribe.Write(_db, actorUserId, AuditAction.Delete, "EditorTask", t.Id.ToString(), new
        {
            Title = t.Title,
            AssigneeUserId = t.AssigneeUserId,
            AssigneeName = t.AssigneeName,
            Stato = t.Status.ToString(),
        });
        _db.EditorTasks.Remove(t);
        await _db.SaveChangesAsync(ct);
    }
}
