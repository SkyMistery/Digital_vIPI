using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence.ReleaseTargets;

/// <summary>Descrittore vIPI aeroporto (doc 09 §3a). Chiave di release = ICAO; Document = quello dei settori foglia
/// dell'aeroporto. Catch-all dei Document vIPI non riconosciuti come APP/ACC (DescribeOrder più alto).</summary>
public sealed class AirportReleaseTarget : IReleaseTarget
{
    private readonly VipiDbContext _db;
    public AirportReleaseTarget(VipiDbContext db) => _db = db;

    public ReleaseTargetType Type => ReleaseTargetType.Airport;
    public int DescribeOrder => 3;

    // Dall'aeroporto: è il legame autoritativo. Prima si passava dai settori, e serviva un filtro per non
    // fotografare l'APP non remotizzato dello stesso ICAO, che ha un documento suo.
    public async Task<int?> ResolveDocumentIdAsync(string key, CancellationToken ct = default) =>
        await _db.Airports.AsNoTracking()
            .Where(a => a.Icao == key).Select(a => a.DocumentId).FirstOrDefaultAsync(ct);

    public async Task<string?> AuthAccCodeAsync(string key, CancellationToken ct = default) =>
        await _db.Airports.AsNoTracking()
            .Where(a => a.Icao == key).Select(a => a.Acc!.Code).FirstOrDefaultAsync(ct);

    public bool TryDescribe(Document doc, bool hasDraft, out ManagedDoc managed)
    {
        managed = default!;
        if (doc.Type != DocumentType.Vipi) return false;
        // Catch-all: Document vIPI non APP/ACC → aeroporto. L'ICAO viene dall'AEROPORTO collegato; l'ACC pure,
        // e non più dal settore — uno scalo col solo APP non remotizzato non ha un settore da cui prenderla.
        // ⚠️ Richiede `.Include(d => d.Airport).ThenInclude(a => a.Acc)` a monte: senza, l'ICAO esce vuoto e il
        // documento diventa irraggiungibile invece di dare errore.
        var icao = doc.Airport?.Icao ?? "";
        managed = new ManagedDoc(ReleaseTargetType.Airport, doc.Title, icao, doc.Airport?.Acc?.Code,
            doc.Status == DocumentStatus.Published, hasDraft, doc.IsHidden,
            ReleaseTargetType.Airport, icao, doc.Id);
        return true;
    }
}
