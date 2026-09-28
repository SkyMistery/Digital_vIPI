using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Infrastructure.Sectorfile;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="ISectorfileGateRepository"/>
public sealed class EfSectorfileGateRepository : ISectorfileGateRepository
{
    private readonly VipiDbContext _db;

    public EfSectorfileGateRepository(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<SectorfileDeferral>> ListAsync(
        ReleaseTargetType target, string key, int? documentId, CancellationToken ct = default)
    {
        var esito = new List<SectorfileDeferral>();
        if (documentId is { } docId) esito.AddRange(await RadioassistenzeAsync(docId, ct));

        var percorsi = await PercorsiMvaAsync(target, (key ?? "").Trim(), ct);
        if (percorsi.Count > 0)
            esito.AddRange((await _db.MvaChartStates.AsNoTracking()
                    .Where(x => percorsi.Contains(x.Path) && x.AiracCycle != null)
                    .Select(x => new { x.Id, x.Path, x.AiracCycle, x.ForcePublished })
                    .ToListAsync(ct))
                .Select(x => new SectorfileDeferral(DeferredKind.CartaMrva, x.Id, x.Path, x.AiracCycle!, x.ForcePublished)));
        return esito;
    }

    /// <summary>
    /// Le radioassistenze con un cambio in attesa che il documento <b>cita</b>. Si parte dalle poche in attesa e si
    /// guarda se il documento le nomina nei suoi blocchi — la stessa lettura di <c>EfNavaidCatalog.CitataDaAsync</c>,
    /// ristretta alle versioni di quel documento.
    /// </summary>
    private async Task<IReadOnlyList<SectorfileDeferral>> RadioassistenzeAsync(int docId, CancellationToken ct)
    {
        var inAttesa = await _db.Navaids.AsNoTracking().Where(n => n.SourceAiracCycle != null).ToListAsync(ct);
        if (inAttesa.Count == 0) return Array.Empty<SectorfileDeferral>();

        var corpi = await _db.ContentBlocks.AsNoTracking()
            .Where(b => b.DocumentVersion!.DocumentId == docId && b.BodyJson != null)
            .Select(b => b.BodyJson!)
            .ToListAsync(ct);
        var citate = new HashSet<NavaidKey>();
        foreach (var corpo in corpi)
        {
            foreach (var k in MilNavaidsPayload.Leggi(corpo)) citate.Add(k);
            foreach (var k in MilDiversionPayload.ChiaviNavaid(MilDiversionPayload.Leggi(corpo))) citate.Add(k);
        }

        return inAttesa
            .Where(n => citate.Contains(new NavaidKey(n.Code, n.Kind, n.Channel)))
            .Select(n => new SectorfileDeferral(DeferredKind.Radioassistenza, n.Id,
                string.Join(' ', new[] { n.Code, n.Kind, n.Channel }.Where(x => !string.IsNullOrWhiteSpace(x))),
                n.SourceAiracCycle!, n.SourceForcePublished))
            .ToList();
    }

    /// <summary>
    /// Le carte MRVA che il documento può mostrare: per una vIPI ACC l'enroute e quelle degli aeroporti della ACC,
    /// per un APP quella del suo aeroporto. Gli altri documenti non hanno una sezione minime.
    /// </summary>
    private async Task<IReadOnlyList<string>> PercorsiMvaAsync(ReleaseTargetType target, string key, CancellationToken ct)
    {
        switch (target)
        {
            case ReleaseTargetType.AccVipi:
            {
                var acc = key.Split('|')[0].Trim().ToUpperInvariant();
                if (acc.Length == 0) return Array.Empty<string>();
                var icao = await _db.Airports.AsNoTracking()
                    .Where(a => a.Acc != null && a.Acc.Code == acc).Select(a => a.Icao).ToListAsync(ct);
                return icao.Select(AuroraMvaProvider.PercorsoAeroporto).Prepend(AuroraMvaProvider.PercorsoAcc(acc)).ToList();
            }
            case ReleaseTargetType.App:
            case ReleaseTargetType.AppMil:
                // Chiave = callsign dell'APP («LIRA_APP»): l'aeroporto sono le prime quattro lettere.
                return key.Length < 4 ? Array.Empty<string>() : new[] { AuroraMvaProvider.PercorsoAeroporto(key[..4]) };
            default:
                return Array.Empty<string>();
        }
    }

    public async Task<int> ForceAsync(IReadOnlyList<(DeferredKind Kind, int Id)> rows, CancellationToken ct = default)
    {
        var navaid = rows.Where(r => r.Kind == DeferredKind.Radioassistenza).Select(r => r.Id).ToList();
        var carte = rows.Where(r => r.Kind == DeferredKind.CartaMrva).Select(r => r.Id).ToList();

        var toccate = 0;
        foreach (var n in await _db.Navaids.Where(n => navaid.Contains(n.Id)).ToListAsync(ct))
        {
            n.SourceForcePublished = true;
            toccate++;
        }
        foreach (var c in await _db.MvaChartStates.Where(c => carte.Contains(c.Id)).ToListAsync(ct))
        {
            c.ForcePublished = true;
            toccate++;
        }
        // ⚠️ Il ciclo resta scritto, come per le aree: la forzatura dice «pubblicala lo stesso», non «è in vigore».
        // Quando il ciclo arriva il giro chiude la pratica e spegne la forzatura da sé.
        if (toccate > 0) await _db.SaveChangesAsync(ct);
        return toccate;
    }
}
