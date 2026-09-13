using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// I clienti delle API su database. Carta <c>docs/feature/2026-09-13-chiavi-api.md</c>.
///
/// <para>⚠️ Nell'audit l'identificativo è il <b>prefisso</b>, non l'id: la riga di audit entra nella stessa
/// <c>SaveChanges</c> della creazione, quando l'id non esiste ancora, ed è il prefisso che si ritrova nei log
/// delle chiamate rifiutate. La chiave, mai.</para>
/// </summary>
public sealed class EfApiClientStore : IApiClientStore
{
    public const string EntityType = "ApiClient";

    private readonly VipiDbContext _db;
    public EfApiClientStore(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<ApiClientRow>> ListAsync(CancellationToken ct = default)
    {
        var righe = await _db.ApiClients.AsNoTracking()
            .OrderByDescending(c => c.CreataUtc).ThenByDescending(c => c.Id)
            .ToListAsync(ct).ConfigureAwait(false);
        return righe.Select(Riga).ToList();
    }

    public async Task<ApiClientRow> AddAsync(string nome, string prefisso, string impronta, IReadOnlyList<string> endpoint,
        int actorUserId, CancellationToken ct = default)
    {
        var riga = new ApiClient
        {
            Nome = nome,
            Prefisso = prefisso,
            ImprontaSha256 = impronta,
            Endpoint = string.Join(",", endpoint),
            CreataDaUserId = actorUserId,
            CreataUtc = DateTime.UtcNow,
        };
        _db.ApiClients.Add(riga);
        AuditScribe.Write(_db, actorUserId, AuditAction.Create, EntityType, prefisso,
            new { Nome = nome, Prefisso = prefisso, Endpoint = riga.Endpoint });
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Riga(riga);
    }

    public async Task<bool> RevocaAsync(int id, int actorUserId, CancellationToken ct = default)
    {
        var riga = await _db.ApiClients.FirstOrDefaultAsync(c => c.Id == id, ct).ConfigureAwait(false);
        if (riga is null || riga.RevocataUtc is not null) return false;

        riga.RevocataUtc = DateTime.UtcNow;
        riga.RevocataDaUserId = actorUserId;
        // Archive e non Delete: la riga resta, è la storia di chi ha potuto leggere cosa.
        AuditScribe.Write(_db, actorUserId, AuditAction.Archive, EntityType, riga.Prefisso,
            new { riga.Nome, riga.Prefisso, riga.Endpoint });
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task<ApiClientRow?> TrovaPerImprontaAsync(string impronta, CancellationToken ct = default)
    {
        var riga = await _db.ApiClients.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ImprontaSha256 == impronta, ct).ConfigureAwait(false);
        return riga is null ? null : Riga(riga);
    }

    public async Task SegnaUsoAsync(int id, DateTime quandoUtc, CancellationToken ct = default)
    {
        var riga = await _db.ApiClients.FirstOrDefaultAsync(c => c.Id == id, ct).ConfigureAwait(false);
        if (riga is null) return;
        riga.UltimoUsoUtc = quandoUtc;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static ApiClientRow Riga(ApiClient c) => new(
        c.Id, c.Nome, c.Prefisso,
        c.Endpoint.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        c.CreataDaUserId, c.CreataUtc, c.RevocataUtc, c.RevocataDaUserId, c.UltimoUsoUtc);
}
