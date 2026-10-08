using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.Aor;

/// <summary>
/// Costruisce la <see cref="Topology"/> pura (Application) leggendo l'anagrafica di una ACC dal DB: padri,
/// righe di ripiego e bande. La logica AoR resta DB-agnostica.
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
        // Tutti i settori attivi, padre = ParentSectorId (può puntare cross-ACC, Round 20).
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
            Fallbacks = await RipieghiAsync(ct),
            Bands = await BandeAsync(ct),
            Configurazioni = await ConfigurazioniQuery.TutteAsync(_db, ct),
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

        return new Topology
        {
            Sectors = allCallsigns,
            Parent = parent,
            Fallbacks = await RipieghiAsync(ct),
            Bands = await BandeAsync(ct),
            // ⚠️ Tutte, anche per una sola ACC: come i ripieghi, sono poche righe e filtrarle non serve a niente.
            Configurazioni = await ConfigurazioniQuery.TutteAsync(_db, ct),
        };
    }

    /// <summary>
    /// La banda dichiarata di ogni settore visibile dei due cataloghi, in piedi.
    ///
    /// <para>⚠️ Di <b>tutti</b>, come i ripieghi: una riga può portare il cielo di un settore a un altro centro,
    /// e la sua fascia si confronta con la banda di chi cede. ⚠️ Lo stesso callsign nei due cataloghi: vince il
    /// primo, cioè il settore d'area — la stessa scelta di <c>EffectiveHierarchy.ParentMap</c>.</para>
    /// </summary>
    private async Task<IReadOnlyDictionary<string, (int? BaseFeet, int? TopFeet)>> BandeAsync(CancellationToken ct)
    {
        var limiti = (await _db.AccSectors.AsNoTracking().Where(x => !x.IsHidden)
                .Select(x => new { x.ComposePosition, x.LowerLimit, x.UpperLimit }).ToListAsync(ct))
            .Concat(await _db.AirportSectors.AsNoTracking().Where(x => !x.IsHidden)
                .Select(x => new { x.ComposePosition, x.LowerLimit, x.UpperLimit }).ToListAsync(ct));

        var bande = new Dictionary<string, (int? BaseFeet, int? TopFeet)>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in limiti)
            bande.TryAdd(l.ComposePosition, AorFlBand.FeetOfLimits(l.LowerLimit, l.UpperLimit));
        return bande;
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
            .Select(r => new { r.SectorCallsign, r.TargetCallsign, r.BaseFeet, r.TopFeet, r.TargetKind })
            .ToListAsync(ct);

        var dichiarate = righe
            .GroupBy(r => r.SectorCallsign, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<FallbackRow>)g
                    .Select(r => new FallbackRow(r.TargetCallsign, r.BaseFeet, r.TopFeet, r.TargetKind)).ToList(),
                StringComparer.OrdinalIgnoreCase);

        // + le righe automatiche «APP militare → MIL_CTR fratello» (carta 2026-09-24-mil-solo-traffico-militare):
        // qui, e non in chi risolve, perché da qui passano la ricaduta dei trasferimenti e il rinvio della Diagnostica.
        return RipiegoMilitare.ConAutomatiche(dichiarate, await RipieghiMilitariQuery.FratelliAsync(_db, ct));
    }
}
