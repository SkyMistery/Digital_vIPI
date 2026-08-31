using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.Aor;

/// <summary>
/// Costruisce la <see cref="Topology"/> pura (Application) leggendo l'anagrafica di una ACC dal DB.
/// Qui vive la conoscenza del formato JSON delle <c>UnificationRule</c>; la logica AoR resta DB-agnostica.
/// Implementa <see cref="ITopologyProvider"/> (porta usata da Application/UI).
/// </summary>
public sealed class TopologyBuilder : ITopologyProvider
{
    private readonly VipiDbContext _db;

    public TopologyBuilder(VipiDbContext db) => _db = db;

    public async Task<Topology?> BuildByAccCodeAsync(string accCode, CancellationToken ct = default)
    {
        var accId = await _db.Accs.Where(f => f.Code == accCode).Select(f => (int?)f.Id).FirstOrDefaultAsync(ct);
        return accId is int id ? await BuildAsync(id, ct) : null;
    }

    public async Task<Topology> BuildGlobalAsync(CancellationToken ct = default)
    {
        // Tutti i settori attivi, padre = ParentSectorId (può puntare cross-ACC, Round 20). Niente regole.
        var sectors = await _db.Sectors.Where(s => s.IsActive)
            .Select(s => new { s.Id, s.Callsign, s.ParentSectorId }).ToListAsync(ct);
        var callsignById = sectors.ToDictionary(s => s.Id, s => s.Callsign);

        var parent = sectors
            .Where(s => s.ParentSectorId is int pid && callsignById.ContainsKey(pid))
            .ToDictionary(s => s.Callsign, s => callsignById[s.ParentSectorId!.Value],
                StringComparer.OrdinalIgnoreCase);

        return new Topology
        {
            Sectors = sectors.Select(s => s.Callsign).ToList(),
            Parent = parent,
            Rules = Array.Empty<UnificationRuleSpec>(),
            Fallbacks = await RipieghiAsync(ct),
        };
    }

    public async Task<Topology> BuildAsync(int accId, CancellationToken ct = default)
    {
        // Settore == posizione: callsign + padre (contenimento ad albero). Solo settori ATTIVI: un settore nascosto
        // in /services/vsop/admin/acc viene disattivato dalla proiezione (IsActive=false) e deve sparire da AoR/coordinamenti/
        // config del documento ACC (coerente con BuildGlobalAsync e con le query di EfAccDerivationRepository).
        var sectors = await _db.Sectors.Where(s => s.AccId == accId && s.IsActive)
            .Select(s => new { s.Id, s.Callsign, s.ParentSectorId }).ToListAsync(ct);
        var callsignById = sectors.ToDictionary(s => s.Id, s => s.Callsign);

        var allCallsigns = sectors.Select(s => s.Callsign).ToList();

        var parent = sectors
            .Where(s => s.ParentSectorId is int pid && callsignById.ContainsKey(pid))
            .ToDictionary(s => s.Callsign, s => callsignById[s.ParentSectorId!.Value],
                StringComparer.OrdinalIgnoreCase);

        var rules = await _db.UnificationRules.Where(u => u.AccId == accId && u.IsActive)
            .OrderBy(u => u.Priority).ToListAsync(ct);

        var ruleSpecs = rules.Select(r => new UnificationRuleSpec(
            r.Name,
            r.Priority,
            ParseRequiredOnline(r.ConditionJson),
            ParseAssignment(r.AssignmentJson))).ToList();

        return new Topology
        {
            Sectors = allCallsigns,
            Parent = parent,
            Rules = ruleSpecs,
            Fallbacks = await RipieghiAsync(ct),
        };
    }

    /// <summary>
    /// Le righe di ripiego dichiarate, per settore e già in ordine.
    ///
    /// <para>⚠️ Si leggono <b>tutte</b>, anche costruendo la topologia di una sola ACC: una riga può mandare
    /// il traffico a un settore di un altro centro, esattamente come il padre di copertura, che è cross-ACC
    /// dal Round 20. Filtrarle per ACC vorrebbe dire perdere proprio i ripieghi di confine.</para>
    ///
    /// <para>La tabella nasce vuota e resta piccola (una manciata di righe per divisione): non c'è niente da
    /// paginare né da mettere in cache.</para>
    /// </summary>
    private async Task<IReadOnlyDictionary<string, IReadOnlyList<FallbackRow>>> RipieghiAsync(CancellationToken ct)
    {
        var righe = await _db.SectorFallbacks.AsNoTracking()
            .OrderBy(r => r.SectorCallsign).ThenBy(r => r.Order)
            .Select(r => new { r.SectorCallsign, r.TargetCallsign, r.BaseFeet, r.TopFeet })
            .ToListAsync(ct);

        return righe
            .GroupBy(r => r.SectorCallsign, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<FallbackRow>)g
                    .Select(r => new FallbackRow(r.TargetCallsign, r.BaseFeet, r.TopFeet)).ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyCollection<string> ParseRequiredOnline(string json)
    {
        // Forma attesa: {"online":["LIMM_WS5_CTR", ...]}
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("online", out var arr) && arr.ValueKind == JsonValueKind.Array)
                return arr.EnumerateArray().Select(e => e.GetString()!).Where(s => s is not null).ToList();
        }
        catch (JsonException) { /* regola malformata → condizione vuota (mai attivata) */ }
        return Array.Empty<string>();
    }

    private static IReadOnlyDictionary<string, string> ParseAssignment(string json)
    {
        // Forma attesa: {"WS2":"LIMM_WS2_CTR","ES2":"LIMM_WS2_CTR", ...}
        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (map is not null)
                return new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException) { }
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
