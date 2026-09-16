using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Vipi.Application.Airspace;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// <b>Il ponte fra le colonne della forma e <c>SectorShapeParts</c></b> — fase A di S11 (carta
/// <c>docs/refactor/15-shape-del-settore-una-porta-sola.md</c> §4-bis). Finché le colonne sono la verità, i pezzi
/// delle fonti di catalogo ne sono la <b>copia</b>, e questa classe è l'unico posto che la scrive.
///
/// <para>⚠️ <b>Perché dal contesto e non dagli scrittori.</b> Le colonne le scrivono otto percorsi, e le quote
/// altri cinque: un ponte chiamato da ognuno sarebbe tredici occasioni di dimenticarsene, e il guasto non si
/// vedrebbe a schermo finché le letture non passano ai pezzi. Il contesto vede ogni salvataggio.</para>
///
/// <para>⚠️ <b>Le fonti di catalogo si escludono, e l'AIP non si tocca.</b> Il ponte scrive i pezzi di
/// <c>Source</c>/<c>Sectorfile</c>/<c>Synthetic</c> come dice <see cref="CatalogShapeMirror"/> e toglie quelli
/// delle altre due; i pezzi <c>Aip</c> (l'ATZ automatica) li cambia solo per seguire un callsign rinominato. Un
/// settore <b>eliminato</b> perde tutti i suoi pezzi: è il gesto esplicito della pagina che elimina.</para>
///
/// <para>⚠️ <b>Non è atomico col salvataggio che lo scatena</b>: le righe nuove non hanno un id finché non sono
/// salvate. Se il secondo salvataggio cadesse, la passata d'avvio (<see cref="AllineaTuttoAsync"/>) rimette a
/// posto, e la Diagnostica conta le righe disallineate. Dentro una transazione esplicita i due salvataggi ci
/// stanno entrambi.</para>
///
/// <para>▶ Se ne va in fase C, con le colonne.</para>
/// </summary>
internal static class PonteDelleForme
{
    /// <summary>Le proprietà che, cambiate, cambiano i pezzi.</summary>
    private static readonly string[] Rilevanti =
    {
        nameof(AccSector.RegionMapPolygon), nameof(AccSector.RegionMapPolygonInForce), nameof(AccSector.ShapeAiracCycle),
        nameof(AccSector.ShapeForcePublished), nameof(AccSector.ShapeSource), nameof(AccSector.LowerLimit),
        nameof(AccSector.UpperLimit), nameof(AccSector.ComposePosition), nameof(AirportSector.IsShapeSynthetic),
    };

    /// <summary>Una riga di catalogo toccata da un salvataggio, presa PRIMA che il salvataggio ne cambi lo stato.</summary>
    internal sealed record Toccata(object Entita, SourceCatalog Catalogo, bool Eliminata, int IdSeEliminata);

    /// <summary>Le righe di catalogo che il prossimo salvataggio aggiunge, elimina o modifica in un campo rilevante.</summary>
    internal static List<Toccata> Raccogli(ChangeTracker tracker)
    {
        var esito = new List<Toccata>();
        foreach (var e in tracker.Entries())
        {
            var catalogo = e.Entity switch
            {
                AccSector => SourceCatalog.Subcenter,
                AirportSector => SourceCatalog.AirportPosition,
                _ => (SourceCatalog?)null,
            };
            if (catalogo is null) continue;

            switch (e.State)
            {
                case EntityState.Added:
                    esito.Add(new Toccata(e.Entity, catalogo.Value, false, 0));
                    break;
                case EntityState.Deleted:
                    esito.Add(new Toccata(e.Entity, catalogo.Value, true, Id(e.Entity)));
                    break;
                case EntityState.Modified when Rilevanti.Any(p => e.Metadata.FindProperty(p) is not null && e.Property(p).IsModified):
                    esito.Add(new Toccata(e.Entity, catalogo.Value, false, 0));
                    break;
            }
        }
        return esito;
    }

    /// <summary>Allinea i pezzi delle righe toccate. Non salva: chi chiama salva.</summary>
    internal static async Task AllineaAsync(VipiDbContext db, IReadOnlyList<Toccata> toccate, CancellationToken ct)
    {
        foreach (var gruppo in toccate.GroupBy(t => t.Catalogo))
        {
            var ids = gruppo.Select(t => t.Eliminata ? t.IdSeEliminata : Id(t.Entita)).Distinct().ToList();
            var esistenti = await db.SectorShapeParts
                .Where(p => p.Catalog == gruppo.Key && ids.Contains(p.SectorId))
                .ToListAsync(ct);
            var perSettore = esistenti.ToLookup(p => p.SectorId);

            foreach (var t in gruppo)
            {
                if (t.Eliminata)
                {
                    db.SectorShapeParts.RemoveRange(perSettore[t.IdSeEliminata]);
                    continue;
                }
                var (id, callsign, riga) = Leggi(t.Entita);
                Applica(db, gruppo.Key, id, callsign, riga, perSettore[id].ToList(), DateTime.UtcNow);
            }
        }
    }

    /// <summary>
    /// La passata d'avvio: ogni riga di catalogo, e i pezzi orfani di settori che non esistono più. Torna quanti
    /// settori ha dovuto correggere. Idempotente: a regime zero, e non scrive niente.
    /// </summary>
    internal static async Task<int> AllineaTuttoAsync(VipiDbContext db, CancellationToken ct)
    {
        var (righe, pezzi) = await CaricaTuttoAsync(db, tracciati: true, ct);
        var ora = DateTime.UtcNow;
        var corretti = 0;

        var perSettore = pezzi.ToLookup(p => (p.Catalog, p.SectorId));
        foreach (var r in righe)
            if (Applica(db, r.Catalogo, r.Id, r.Callsign, r.Riga, perSettore[(r.Catalogo, r.Id)].ToList(), ora))
                corretti++;

        var vivi = righe.Select(r => (r.Catalogo, r.Id)).ToHashSet();
        var orfani = pezzi.Where(p => !vivi.Contains((p.Catalog, p.SectorId))).ToList();
        if (orfani.Count > 0)
        {
            db.SectorShapeParts.RemoveRange(orfani);
            corretti += orfani.Select(p => (p.Catalog, p.SectorId)).Distinct().Count();
        }

        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(ct);
        return corretti;
    }

    /// <summary>Quanti settori hanno pezzi diversi da quel che dicono le colonne (orfani compresi). Solo lettura.</summary>
    internal static async Task<IReadOnlyList<string>> DisallineatiAsync(VipiDbContext db, CancellationToken ct)
    {
        var (righe, pezzi) = await CaricaTuttoAsync(db, tracciati: false, ct);
        var perSettore = pezzi.ToLookup(p => (p.Catalog, p.SectorId));
        var esito = new List<string>();

        foreach (var r in righe)
        {
            var attuali = perSettore[(r.Catalogo, r.Id)].ToList();
            if (!Uguali(attuali.Where(p => p.Source != ShapeSource.Aip), Attesi(r.Catalogo, r.Id, r.Callsign, r.Riga))
                || attuali.Any(p => p.Callsign != r.Callsign))
                esito.Add(r.Callsign);
        }

        var vivi = righe.Select(r => (r.Catalogo, r.Id)).ToHashSet();
        esito.AddRange(pezzi.Where(p => !vivi.Contains((p.Catalog, p.SectorId))).Select(p => p.Callsign).Distinct());
        return esito;
    }

    private sealed record RigaDiCatalogo(SourceCatalog Catalogo, int Id, string Callsign, CatalogShapeRow Riga);

    private static async Task<(List<RigaDiCatalogo>, List<SectorShapePart>)> CaricaTuttoAsync(
        VipiDbContext db, bool tracciati, CancellationToken ct)
    {
        var acc = await db.AccSectors.AsNoTracking()
            .Select(x => new { x.Id, x.ComposePosition, x.RegionMapPolygon, x.RegionMapPolygonInForce, x.ShapeAiracCycle,
                x.ShapeSource, x.ShapeForcePublished, x.LowerLimit, x.UpperLimit })
            .ToListAsync(ct);
        var apt = await db.AirportSectors.AsNoTracking()
            .Select(x => new { x.Id, x.ComposePosition, x.RegionMapPolygon, x.RegionMapPolygonInForce, x.ShapeAiracCycle,
                x.ShapeSource, x.ShapeForcePublished, x.IsShapeSynthetic, x.LowerLimit, x.UpperLimit })
            .ToListAsync(ct);

        var righe = acc.Select(x => new RigaDiCatalogo(SourceCatalog.Subcenter, x.Id, Norm(x.ComposePosition),
                new CatalogShapeRow(x.RegionMapPolygon, x.RegionMapPolygonInForce, x.ShapeAiracCycle, x.ShapeSource,
                    x.ShapeForcePublished, false, x.LowerLimit, x.UpperLimit)))
            .Concat(apt.Select(x => new RigaDiCatalogo(SourceCatalog.AirportPosition, x.Id, Norm(x.ComposePosition),
                new CatalogShapeRow(x.RegionMapPolygon, x.RegionMapPolygonInForce, x.ShapeAiracCycle, x.ShapeSource,
                    x.ShapeForcePublished, x.IsShapeSynthetic, x.LowerLimit, x.UpperLimit))))
            .ToList();

        var query = tracciati ? db.SectorShapeParts : db.SectorShapeParts.AsNoTracking();
        return (righe, await query.ToListAsync(ct));
    }

    /// <summary>
    /// Porta i pezzi di un settore a quel che dice la riga. Torna vero se ha dovuto cambiare qualcosa.
    /// <paramref name="attuali"/> sono TUTTI i pezzi del settore, AIP compresi (tracciati).
    /// </summary>
    private static bool Applica(VipiDbContext db, SourceCatalog catalogo, int id, string callsign, CatalogShapeRow riga,
        List<SectorShapePart> attuali, DateTime ora)
    {
        var cambiato = false;

        // Un callsign rinominato si porta dietro TUTTI i pezzi: il risolutore li cerca per callsign.
        foreach (var p in attuali.Where(p => p.Callsign != callsign))
        {
            p.Callsign = callsign;
            cambiato = true;
        }

        var diCatalogo = attuali.Where(p => p.Source != ShapeSource.Aip).ToList();
        var attesi = Attesi(catalogo, id, callsign, riga);
        if (Uguali(diCatalogo, attesi)) return cambiato;

        db.SectorShapeParts.RemoveRange(diCatalogo);
        foreach (var p in attesi)
        {
            p.WrittenUtc = ora;
            db.SectorShapeParts.Add(p);
        }
        return true;
    }

    private static List<SectorShapePart> Attesi(SourceCatalog catalogo, int id, string callsign, CatalogShapeRow riga)
    {
        var desiderati = CatalogShapeMirror.Desired(riga);
        if (desiderati is null) return new List<SectorShapePart>();

        var esito = new List<SectorShapePart>
        {
            Riga(catalogo, id, callsign, desiderati.Source, ShapePartState.InForce, desiderati.InForce, null, false),
        };
        if (desiderati.Pending is not null)
            esito.Add(Riga(catalogo, id, callsign, desiderati.Source, ShapePartState.Pending, desiderati.Pending,
                desiderati.PendingCycle, desiderati.ForcePublished));
        return esito;
    }

    private static SectorShapePart Riga(SourceCatalog catalogo, int id, string callsign, ShapeSource fonte,
        ShapePartState stato, ShapePart p, string? ciclo, bool forzata) => new()
    {
        Catalog = catalogo,
        SectorId = id,
        Callsign = callsign,
        Source = fonte,
        State = stato,
        Ordinal = 0,
        PolygonJson = p.PolygonJson,
        BaseFeet = p.BaseFeet,
        TopFeet = p.TopFeet,
        BaseDatum = p.BaseDatum,
        TopDatum = p.TopDatum,
        BaseRaw = p.BaseRaw,
        TopRaw = p.TopRaw,
        AiracCycle = ciclo,
        ForcePublished = forzata,
        SourceRef = p.SourceRef,
    };

    /// <summary>Stesso contenuto, a meno di id e data di scrittura.</summary>
    private static bool Uguali(IEnumerable<SectorShapePart> a, IReadOnlyList<SectorShapePart> b)
    {
        var chiave = (SectorShapePart p) => (p.Source, p.State, p.Ordinal, p.PolygonJson, p.BaseFeet, p.TopFeet,
            p.BaseDatum, p.TopDatum, p.BaseRaw, p.TopRaw, p.AiracCycle, p.ForcePublished, p.SourceRef);
        var sa = a.Select(chiave).OrderBy(k => k.Source).ThenBy(k => k.State).ThenBy(k => k.Ordinal).ToList();
        var sb = b.Select(chiave).OrderBy(k => k.Source).ThenBy(k => k.State).ThenBy(k => k.Ordinal).ToList();
        return sa.SequenceEqual(sb);
    }

    private static (int Id, string Callsign, CatalogShapeRow Riga) Leggi(object entita) => entita switch
    {
        AccSector s => (s.Id, Norm(s.ComposePosition), new CatalogShapeRow(s.RegionMapPolygon, s.RegionMapPolygonInForce,
            s.ShapeAiracCycle, s.ShapeSource, s.ShapeForcePublished, false, s.LowerLimit, s.UpperLimit)),
        AirportSector s => (s.Id, Norm(s.ComposePosition), new CatalogShapeRow(s.RegionMapPolygon, s.RegionMapPolygonInForce,
            s.ShapeAiracCycle, s.ShapeSource, s.ShapeForcePublished, s.IsShapeSynthetic, s.LowerLimit, s.UpperLimit)),
        _ => throw new ArgumentException("Non è una riga di catalogo.", nameof(entita)),
    };

    private static int Id(object entita) => entita switch
    {
        AccSector s => s.Id,
        AirportSector s => s.Id,
        _ => 0,
    };

    private static string Norm(string? s) => (s ?? "").Trim().ToUpperInvariant();
}
