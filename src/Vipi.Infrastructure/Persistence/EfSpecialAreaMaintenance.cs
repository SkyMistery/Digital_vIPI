using System.Data;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="ISpecialAreaMaintenance"/>
public sealed class EfSpecialAreaMaintenance : ISpecialAreaMaintenance
{
    private readonly VipiDbContext _db;
    private readonly Vipi.Application.Abstractions.IImportStateStore _states;

    public EfSpecialAreaMaintenance(VipiDbContext db, Vipi.Application.Abstractions.IImportStateStore states)
    {
        _db = db;
        _states = states;
    }

    public async Task<int> OptOutForeignAreasAsync(CancellationToken ct = default)
    {
        var category = Vipi.Application.Abstractions.ImportCategories.SpecialAreaForeignOptOut;
        if (await _states.GetLastSuccessAsync(category, ct) is not null) return 0;   // già fatto: non ripetere

        var foreign = await _db.Accs.Where(a => a.IsForeign).ToListAsync(ct);
        foreach (var acc in foreign) acc.SpecialAreasEnabled = false;
        await _db.SaveChangesAsync(ct);

        var codes = foreign.Select(a => a.Code).ToList();
        var links = await _db.SpecialAreaCenters.Where(l => codes.Contains(l.CenterId)).ToListAsync(ct);
        if (links.Count > 0)
        {
            _db.SpecialAreaCenters.RemoveRange(links);
            await _db.SaveChangesAsync(ct);

            // Le aree rimaste senza alcun ente non servono più a nessuno: quelle condivise con un ACC domestico
            // (es. una R nazionale elencata anche dal militare italiano) hanno ancora il loro legame e restano.
            var orphans = await _db.SpecialAreas.Where(a => !a.Centers.Any()).ToListAsync(ct);
            if (orphans.Count > 0)
            {
                _db.SpecialAreas.RemoveRange(orphans);
                await _db.SaveChangesAsync(ct);
            }
        }

        await _states.MarkSuccessAsync(category, DateTime.UtcNow, ct);
        return links.Count;
    }

    public async Task<int> BackfillAreaCentersAsync(CancellationToken ct = default)
    {
        // La colonna storica non è più nel modello EF: si legge in SQL grezzo. Se non c'è (schema già nuovo, o DB
        // creato da zero con EnsureCreated) la lettura fallisce ed è esattamente il caso «niente da fare».
        var legacy = await ReadLegacyPairsAsync(ct);
        if (legacy.Count == 0)
        {
            await DropLegacyColumnAsync(ct);   // può esistere vuota su Postgres: va tolta comunque
            return 0;
        }

        var known = (await _db.Accs.Select(a => a.Code).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = (await _db.SpecialAreaCenters.Select(l => l.IvaoId + "|" + l.CenterId).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var (ivaoId, centerId) in legacy)
        {
            if (!known.Contains(centerId)) continue;                  // ACC sparito: il legame non è ricostruibile
            if (!existing.Add(ivaoId + "|" + centerId)) continue;      // già presente (riesecuzione)
            _db.SpecialAreaCenters.Add(new SpecialAreaCenter { IvaoId = ivaoId, CenterId = centerId });
            added++;
        }
        if (added > 0) await _db.SaveChangesAsync(ct);

        await DropLegacyColumnAsync(ct);
        return added;
    }

    // Coppie (IvaoId, CenterId) dalla colonna storica. Lista vuota se la colonna non esiste più.
    private async Task<List<(string IvaoId, string CenterId)>> ReadLegacyPairsAsync(CancellationToken ct)
    {
        var pairs = new List<(string, string)>();
        var conn = _db.Database.GetDbConnection();
        var mustClose = conn.State != ConnectionState.Open;
        try
        {
            if (mustClose) await _db.Database.OpenConnectionAsync(ct);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT \"IvaoId\", \"CenterId\" FROM \"SpecialAreas\" WHERE \"CenterId\" IS NOT NULL";
            using var r = await ((System.Data.Common.DbCommand)cmd).ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                pairs.Add((r.GetString(0), r.GetString(1).Trim().ToUpperInvariant()));
        }
        catch (Exception)
        {
            return new List<(string, string)>();   // colonna assente: schema già nuovo
        }
        finally
        {
            if (mustClose) await _db.Database.CloseConnectionAsync();
        }
        return pairs;
    }

    /// <summary>
    /// Toglie la colonna storica dove la migrazione EF non arriva: su Postgres lo schema lo allinea
    /// <c>PostgresSchemaReconciler</c>, che aggiunge e non rimuove — una <c>CenterId</c> NOT NULL rimasta lì
    /// farebbe fallire ogni inserimento di area nuova. Best-effort e idempotente.
    /// </summary>
    private async Task DropLegacyColumnAsync(CancellationToken ct)
    {
        if (_db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) != true) return;
        try
        {
            await _db.Database.ExecuteSqlRawAsync("ALTER TABLE \"SpecialAreas\" DROP COLUMN IF EXISTS \"CenterId\"", ct);
        }
        catch (Exception)
        {
            // Nessun rimedio possibile qui: il chiamante logga, e il probe di drift dello schema lo segnala.
        }
    }
}
