using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;

namespace Vipi.Infrastructure.Persistence.Seed;

/// <summary>
/// Seed di contenuto demo per la vIPI di un aeroporto (LIRF — Roma Fiumicino), scoped sulla torre
/// LIRF_TWR. Reso dalla stessa pipeline documentale (sezioni + blocchi). METAR/TAF e SID restano
/// stub dichiarati (non-live): i dati veri arriveranno da polling meteo (F3) e sectorfile GitHub (F4).
/// Idempotente: no-op se la vIPI aeroporto esiste già.
/// </summary>
public static class RomaAirportSeed
{
    private static readonly IAiracService Airac = new AiracService();
    public const string TowerCallsign = "LIRF_TWR";

    public static async Task SeedAsync(VipiDbContext db, CancellationToken ct = default)
    {
        var twr = await db.Sectors.FirstOrDefaultAsync(p => p.Callsign == TowerCallsign, ct);
        if (twr is null) return;
        if (twr.DocumentId is not null) return;

        var now = DateTime.UtcNow;
        var cycle = Airac.GetCycle(now);

        var doc = new Document
        {
            Type = DocumentType.Vipi,
            Title = "vIPI — LIRF Roma Fiumicino",
            Language = Language.It,
            Status = DocumentStatus.Published,
            LastUpdatedUtc = now,
            LastUpdatedAiracCycle = cycle,
        };
        var ver = new DocumentVersion
        {
            Document = doc, VersionNumber = 1, Status = DocumentStatus.Published,
            CreatedByUserId = 0, CreatedUtc = now, AiracCycle = cycle, Note = "Seed demo aeroporto F2",
        };
        doc.Versions.Add(ver);
        db.Documents.Add(doc);

        var b = new Builder(db, ver);

        // 1 — METAR & TAF (stub non-live dichiarato)
        var wx = b.Section("METAR & TAF", BlockSection.Airport, 1);
        b.Callout(wx, CalloutKind.Warning, "Weather data not live", BlockTier.Reduced,
            "⏳ Real METAR/TAF will come with weather polling (phase F3). The value below is a **static example**, not updated.");
        b.Prose(wx, BlockTier.Reduced,
            "**METAR (example):** `LIRF 191250Z 16012KT 9999 FEW035 SCT100 26/14 Q1015 NOSIG`");

        // 2 — Transition levels
        var trans = b.Section("Transition levels", BlockSection.Airport, 2);
        b.Prose(trans, BlockTier.Reduced, "**Transition Altitude:** 6000 ft");
        b.Table(trans, BlockTier.Reduced, new
        {
            columns = new[] { "QNH (hPa)", "Transition Level" },
            unified = false,
            rows = new object[]
            {
                Cells("≥ 1031", "FL70"),
                Cells("1014 – 1030", "FL75"),
                Cells("1000 – 1013", "FL80"),
                Cells("< 984", "FL90"),
            }
        });

        // 3 — Frequencies
        var freq = b.Section("Frequencies", BlockSection.Frequencies, 3);
        b.Table(freq, BlockTier.Reduced, new
        {
            columns = new[] { "Name", "Callsign", "Frequency" },
            unified = false,
            rows = new object[]
            {
                Cells("Ground (if active)", "LIRF_GND", "121.700"),
                FreqRow("Tower", "LIRF_TWR", "118.700"),
                Cells("Approach", "LIRF_APP", "119.200"),
            }
        });

        // 4 — Runways
        var rwy = b.Section("Runways", BlockSection.Airport, 4);
        b.Table(rwy, BlockTier.Extended, new
        {
            columns = new[] { "Runway", "TORA", "LDA", "APP procedures", "Patterns", "Circling" },
            unified = false,
            rows = new object[]
            {
                Cells("16L", "3902 m", "3902 m", "ILS CAT III · RNP", "—", "No"),
                Cells("16R", "3900 m", "3900 m", "ILS CAT II · RNP", "Left", "No"),
                Cells("34L", "3900 m", "3900 m", "ILS CAT I · RNP", "Right", "Yes"),
                Cells("07", "3309 m", "3309 m", "RNP", "Left", "Yes"),
                Cells("25", "3309 m", "3309 m", "ILS CAT I", "Right", "Yes"),
            }
        });

        // 5 — SID (stub: from GitHub sectorfile)
        var sid = b.Section("SID", BlockSection.Airport, 5);
        b.Callout(sid, CalloutKind.Info, "Data source: sectorfile on GitHub", BlockTier.Extended,
            "🔄 SIDs are **always** imported from the division sectorfile on GitHub, not entered by hand. The table will align to the sectorfile AIRAC. Parsing deferred (phase F4).");

        await db.SaveChangesAsync(ct);
        doc.CurrentVersionId = ver.Id;
        // Aggancia il settore-torre al documento (scope vIPI uno-a-molti, qui un solo settore primario).
        twr.DocumentId = doc.Id;
        twr.IsPrimary = true;
        await db.SaveChangesAsync(ct);
    }

    private static object Cells(params string[] cells) => new { cells };
    private static object FreqRow(string name, string cs, string mhz) =>
        new { primary = true, star = true, cells = new[] { name, cs, mhz } };

    /// <summary>Builder minimale (stesse convenzioni di RomaContentSeed) per ordine/profondità/versione.</summary>
    private sealed class Builder
    {
        private readonly VipiDbContext _db;
        private readonly DocumentVersion _ver;

        public Builder(VipiDbContext db, DocumentVersion ver) { _db = db; _ver = ver; }

        public DocumentSection Section(string title, BlockSection kind, int order)
        {
            var s = new DocumentSection
            {
                DocumentVersion = _ver, ParentSection = null, Title = title, Order = order,
                Depth = 0, SectionKey = SectionCatalogBridge.KeyFor(kind) ?? "custom",
            };
            _ver.Sections.Add(s);
            _db.DocumentSections.Add(s);
            return s;
        }

        public void Prose(DocumentSection s, BlockTier tier, string markdown) =>
            Add(s, BlockFormat.Prose, tier, body: markdown);

        public void Callout(DocumentSection s, CalloutKind kind, string title, BlockTier tier, string markdown) =>
            Add(s, BlockFormat.Callout, tier, body: markdown, callout: kind,
                bodyJson: JsonSerializer.Serialize(new { title }));

        public void Table(DocumentSection s, BlockTier tier, object data) =>
            Add(s, BlockFormat.Table, tier, bodyJson: JsonSerializer.Serialize(data));

        private void Add(DocumentSection s, BlockFormat format, BlockTier tier,
            string? body = null, string? bodyJson = null, CalloutKind? callout = null)
        {
            var block = new ContentBlock
            {
                DocumentVersion = _ver, Section = s, Order = s.Blocks.Count + 1,
                Tier = tier, Format = format, Visibility = BlockVisibility.Always, CalloutKind = callout,
                Body = body, BodyJson = bodyJson,
            };
            s.Blocks.Add(block);
            _ver.Blocks.Add(block);
            _db.ContentBlocks.Add(block);
        }
    }
}
