using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Services;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Le righe da cui nasce l'albero di copertura EFFETTIVO: settori ACC, posizioni d'aeroporto (ATIS escluso) e il
/// padre di ogni scalo. È l'ingresso di <see cref="EffectiveHierarchy.ParentMap(IReadOnlyList{HierarchyCatalogRow}, IReadOnlyDictionary{string, string?})"/>.
///
/// <para>⚠️ <b>Una lettura sola.</b> Fino al 21 settembre 2026 queste tre query erano scritte due volte, nella
/// pagina Struttura (<c>EfHierarchyEditingService</c>) e nel report di consistenza, e i documenti collegati
/// (§A109) sarebbero stati la terza. La porta unica è <c>ParentMap</c>; ma se le righe che le si danno divergono,
/// i due alberi tornano a essere due — ed è proprio il difetto che la porta unica doveva chiudere.</para>
/// </summary>
internal static class EffectiveHierarchyRows
{
    /// <summary>Le righe e il padre di ogni scalo.</summary>
    /// <param name="padreDi">Riscrive in memoria il padre SCRITTO di un nodo (tipo, id, padre letto → padre da
    /// usare). Serve alla guardia anti-ciclo, che valida un cambio non ancora salvato. Null = stato attuale.</param>
    internal static async Task<(List<HierarchyCatalogRow> Righe, Dictionary<string, string?> PadreScalo)> LoadAsync(
        VipiDbContext db, Func<HierarchyNodeKind, int, string?, string?>? padreDi, CancellationToken ct)
    {
        string? Padre(HierarchyNodeKind kind, int id, string? scritto) => padreDi is null ? scritto : padreDi(kind, id, scritto);

        var righe = new List<HierarchyCatalogRow>();

        foreach (var s in await db.AccSectors.AsNoTracking()
                     .Select(s => new { s.Id, s.ComposePosition, s.ParentCallsign }).ToListAsync(ct))
            righe.Add(new HierarchyCatalogRow(s.ComposePosition,
                Padre(HierarchyNodeKind.Acc, s.Id, s.ParentCallsign), null, SectorType.Ctr, IsHidden: false));

        // L'ATIS non è una posizione di controllo e non è un nodo: la proiezione lo esclude.
        foreach (var s in await db.AirportSectors.AsNoTracking()
                     .Where(s => s.Position == null || s.Position.ToUpper() != "ATIS")
                     .Select(s => new { s.Id, s.ComposePosition, s.AirportIcao, s.Position, s.ParentCallsign, s.IsHidden })
                     .ToListAsync(ct))
            righe.Add(new HierarchyCatalogRow(s.ComposePosition,
                Padre(HierarchyNodeKind.AirportPosition, s.Id, s.ParentCallsign),
                s.AirportIcao, EffectiveHierarchy.TypeOfPosition(s.Position), s.IsHidden));

        var padreScalo = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in await db.Airports.AsNoTracking()
                     .Select(a => new { a.Id, a.Icao, a.ParentCallsign }).ToListAsync(ct))
            padreScalo[a.Icao] = Padre(HierarchyNodeKind.Airport, a.Id, a.ParentCallsign);

        return (righe, padreScalo);
    }
}
