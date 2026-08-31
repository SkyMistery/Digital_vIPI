using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using static Vipi.Application.Messaggio;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="ISectorFallbackService"/>
public sealed class EfSectorFallbackService : ISectorFallbackService
{
    private readonly VipiDbContext _db;
    private readonly IEditAuthorizationService _authz;
    private readonly ITopologyProvider _topology;
    private readonly ISectorVolumeCatalog _volumi;

    public EfSectorFallbackService(VipiDbContext db, IEditAuthorizationService authz,
        ITopologyProvider topology, ISectorVolumeCatalog volumi)
    {
        _db = db;
        _authz = authz;
        _topology = topology;
        _volumi = volumi;
    }

    public async Task<IReadOnlyList<FallbackRowEdit>> ListAsync(string sectorCallsign, CancellationToken ct = default) =>
        (await _db.SectorFallbacks.AsNoTracking()
            .Where(r => r.SectorCallsign == sectorCallsign)
            .OrderBy(r => r.Order)
            .Select(r => new { r.TargetCallsign, r.BaseFeet, r.TopFeet })
            .ToListAsync(ct))
        .Select(r => new FallbackRowEdit(r.TargetCallsign, r.BaseFeet, r.TopFeet))
        .ToList();

    public async Task ReplaceAsync(string sectorCallsign, IReadOnlyList<FallbackRowEdit> rows, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);

        if (string.IsNullOrWhiteSpace(sectorCallsign))
            throw new ValidationException(Lingua("Settore non indicato.", "No sector given."));

        var noti = await CallsignNotiAsync(ct);
        if (!noti.Contains(sectorCallsign))
            throw new ValidationException(Lingua(
                $"Il settore «{sectorCallsign}» non esiste nei cataloghi.",
                $"Sector «{sectorCallsign}» does not exist in the catalogues."));

        var pulite = new List<FallbackRowEdit>();
        foreach (var r in rows)
        {
            var target = r.TargetCallsign?.Trim();
            if (string.IsNullOrWhiteSpace(target)) continue;      // riga lasciata a metà nell'editor: si scarta

            if (!noti.Contains(target))
                throw new ValidationException(Lingua(
                    $"Il ripiego «{target}» non esiste nei cataloghi.",
                    $"Fallback «{target}» does not exist in the catalogues."));

            if (string.Equals(target, sectorCallsign, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException(Lingua(
                    "Un settore non può ripiegare su sé stesso.",
                    "A sector cannot fall back on itself."));

            // ⚠️ Il tetto è ESCLUSO: con piede == tetto la fascia è vuota e la riga non varrebbe MAI — una
            // configurazione che sembra scritta e non fa niente, che è il difetto peggiore di tutti.
            if (r.BaseFeet is int b && r.TopFeet is int t && b >= t)
                throw new ValidationException(Lingua(
                    $"Fascia vuota su «{target}»: il piede ({b} ft) non è sotto il tetto ({t} ft).",
                    $"Empty band on «{target}»: the base ({b} ft) is not below the top ({t} ft)."));

            pulite.Add(r with { TargetCallsign = target });
        }

        var vecchie = await _db.SectorFallbacks.Where(r => r.SectorCallsign == sectorCallsign).ToListAsync(ct);
        _db.SectorFallbacks.RemoveRange(vecchie);

        for (var i = 0; i < pulite.Count; i++)
            _db.SectorFallbacks.Add(new SectorFallback
            {
                SectorCallsign = sectorCallsign,
                Order = i,
                TargetCallsign = pulite[i].TargetCallsign,
                BaseFeet = pulite[i].BaseFeet,
                TopFeet = pulite[i].TopFeet,
            });

        AuditScribe.Write(_db, _authz.CurrentUserId ?? 0, AuditAction.HierarchyChange, "SectorFallback",
            sectorCallsign,
            new { Settore = sectorCallsign, Righe = pulite.Select(p => $"{p.TargetCallsign} [{p.BaseFeet?.ToString() ?? "SFC"}–{p.TopFeet?.ToString() ?? "UNL"}]").ToList() });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FallbackSuggestion>> SuggestAsync(string sectorCallsign, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sectorCallsign)) return Array.Empty<FallbackSuggestion>();

        var bande = (await _volumi.GetAllAsync(ct))
            .Select(v => FallbackSuggestions.BandOf(v.Callsign, v.Parts.Select(p => (p.BaseFeet, p.TopFeet))))
            .ToList();

        var topo = await _topology.BuildGlobalAsync(ct);
        var antenati = new HashSet<string>(topo.Ancestors(sectorCallsign), StringComparer.OrdinalIgnoreCase);

        // Chi è GIÀ dichiarato non si ripropone: la proposta serve a riempire la tabella, non a ripeterla.
        var gia = (await ListAsync(sectorCallsign, ct))
            .Select(r => r.TargetCallsign).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return FallbackSuggestions.For(sectorCallsign, bande, antenati)
            .Where(p => !gia.Contains(p.TargetCallsign))
            .ToList();
    }

    /// <summary>I callsign che possono comparire in una riga: le chiavi naturali dei due cataloghi.</summary>
    private async Task<HashSet<string>> CallsignNotiAsync(CancellationToken ct) =>
        (await _db.AccSectors.AsNoTracking().Select(s => s.ComposePosition).ToListAsync(ct))
        .Concat(await _db.AirportSectors.AsNoTracking().Select(s => s.ComposePosition).ToListAsync(ct))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
