using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;

namespace Vipi.Infrastructure.Persistence.Seed;

/// <summary>
/// Seed demo della vLOA bilaterale LIRR ↔ DTTC (Roma ↔ Tunisi). Crea (se mancano) una ACC/posizione
/// estera minimale DTTC, poi un Document Type=Vloa con due DocumentParty (Home=LIRR, Neighbour=DTTC)
/// e le sezioni EN rese dalla stessa pipeline documentale. Idempotente.
/// </summary>
public static class RomaVloaSeed
{
    private static readonly IAiracService Airac = new AiracService();

    public static async Task SeedAsync(VipiDbContext db, CancellationToken ct = default)
    {
        var home = await db.Sectors.FirstOrDefaultAsync(p => p.Callsign == "LIRR_NE_CTR", ct);
        if (home is null) return;
        if (await db.Documents.AnyAsync(d => d.Type == DocumentType.Vloa, ct)) return;

        // ACC + settore estero DTTC (minimal, per la parte Neighbour).
        var dttcAcc = await db.Accs.FirstOrDefaultAsync(f => f.Code == "DTTC", ct);
        if (dttcAcc is null)
        {
            dttcAcc = new Acc { Code = "DTTC", Name = "Tunis ACC", CountryPrefix = "DT", IsForeign = true };
            db.Accs.Add(dttcAcc);
            await db.SaveChangesAsync(ct);
        }
        else if (!dttcAcc.IsForeign) { dttcAcc.IsForeign = true; await db.SaveChangesAsync(ct); }
        var dttc = await db.Sectors.FirstOrDefaultAsync(p => p.Callsign == "DTTC_CTR", ct);
        if (dttc is null)
        {
            dttc = new Sector
            {
                AccId = dttcAcc.Id, Callsign = "DTTC_CTR", Type = SectorType.Ctr, Kind = SectorKind.Acc,
                Name = "Tunis Control", DefaultFrequency = "129.300", CoverageOrder = 10,
                ImportedAtUtc = DateTime.UtcNow, IsActive = true,
            };
            db.Sectors.Add(dttc);
            await db.SaveChangesAsync(ct);
        }

        var now = DateTime.UtcNow;
        var cycle = Airac.GetCycle(now);

        var doc = new Document
        {
            Type = DocumentType.Vloa,
            Title = "vLOA — LIRR ↔ DTTC",
            Language = Language.En,
            Status = DocumentStatus.Published,
            LastUpdatedUtc = now,
            LastUpdatedAiracCycle = cycle,
        };
        var ver = new DocumentVersion
        {
            Document = doc, VersionNumber = 1, Status = DocumentStatus.Published,
            CreatedByUserId = 0, CreatedUtc = now, AiracCycle = cycle, Note = "Seed demo vLOA F2",
        };
        doc.Versions.Add(ver);
        doc.Parties.Add(new DocumentParty { Document = doc, SectorId = home.Id, Role = PartyRole.Home });
        doc.Parties.Add(new DocumentParty { Document = doc, SectorId = dttc.Id, Role = PartyRole.Neighbour });
        db.Documents.Add(doc);

        var b = new Builder(db, ver);

        var purpose = b.Section("Purpose", BlockSection.Purpose, 1);
        b.Prose(purpose, "This Letter of Agreement establishes coordination, transfer of control and transfer of communications between **Roma ACC (LIRR)** and **Tunis ACC (DTTC)** for traffic crossing the common boundary.");

        var aor = b.Section("Areas of Responsibility", BlockSection.Aor, 2);
        b.Prose(aor, "Both shapes are imported from the IVAO database; the common boundary is the LIRR/DTTC ACC limit.");
        b.Prose(aor, "**Roma ACC (LIRR):** southern sectors bordering Tunis ACC, GND→FL660. **Tunis ACC (DTTC):** northern Tunis ACC bordering Roma, GND→UNL.");

        var freq = b.Section("Frequencies", BlockSection.Frequencies, 3);
        b.Table(freq, new { columns = new[] { "Unit", "Callsign", "Frequency" }, unified = false,
            rows = new object[] { Star("Roma Radar (South)", "LIRR_S_CTR", "125.100"), Cells("Backup", "—", "Landline / text") } });
        b.Table(freq, new { columns = new[] { "Unit", "Callsign", "Frequency" }, unified = false,
            rows = new object[] { Star("Tunis Control", "DTTC_CTR", "129.300"), Cells("Backup", "—", "Landline / text") } });

        var gen = b.Section("General procedures", BlockSection.OperationalTechnique, 4);
        b.Prose(gen, "Transfer of control takes place at the common boundary unless otherwise agreed. Transfer of communications is initiated **not later than 5 minutes** before the Coordination Point.");
        b.Callout(gen, CalloutKind.Warning, "Reduced coordination", "In case of radar/communication degradation, revert to estimates and verbal handoff at the boundary.");

        var coord = b.Section("Coordination", BlockSection.Coordination, 5);
        var sb = b.Section("LIRR → DTTC (Southbound)", BlockSection.Coordination, 1, coord);
        b.Prose(sb, "**Roma transfers** southbound traffic to Tunis at the CoP, climbing as published.");
        b.Table(sb, new { columns = new[] { "CoP", "Flow", "FL", "Conditions" }, unified = false,
            rows = new object[] { Cells("ESEBA", "SB", "FL350+", "Transfer 10 NM before CoP"), Cells("PESUN", "SB", "FL310+", "Even levels") } });
        var nb = b.Section("DTTC → LIRR (Northbound)", BlockSection.Coordination, 2, coord);
        b.Prose(nb, "**Tunis transfers** northbound traffic to Roma at the CoP, descending as published.");
        b.Table(nb, new { columns = new[] { "CoP", "Flow", "FL", "Conditions" }, unified = false,
            rows = new object[] { Cells("ESEBA", "NB", "FL360-", "Odd levels"), Cells("PESUN", "NB", "FL340-", "Transfer 10 NM before CoP") } });

        var mil = b.Section("Military areas coordination and management", BlockSection.AreasCorridors, 6);
        b.Prose(mil, "Activation and crossing of cross-border military areas adjacent to the common boundary are coordinated between the two units. When the cross-border area D-XX (FL150→FL300) is active, southbound traffic via ESEBA is rerouted via PESUN.");

        var val = b.Section("Validity and Revision", BlockSection.Validity, 7);
        b.Table(val, new { columns = new[] { "Item", "Value" }, unified = false,
            rows = new object[] { Cells("Effective from", $"AIRAC {cycle}"), Cells("Review cycle", "Bilateral, at least annually"), Cells("Italian signatory", "LIRR CH / AOD") } });

        await db.SaveChangesAsync(ct);
        doc.CurrentVersionId = ver.Id;
        await db.SaveChangesAsync(ct);
    }

    private static object Cells(params string[] cells) => new { cells };
    private static object Star(string unit, string cs, string mhz) => new { star = true, cells = new[] { unit, cs, mhz } };

    /// <summary>Builder con supporto sezioni annidate (per i Coordinamenti).</summary>
    private sealed class Builder
    {
        private readonly VipiDbContext _db;
        private readonly DocumentVersion _ver;
        public Builder(VipiDbContext db, DocumentVersion ver) { _db = db; _ver = ver; }

        public DocumentSection Section(string title, BlockSection kind, int order, DocumentSection? parent = null)
        {
            var s = new DocumentSection
            {
                DocumentVersion = _ver, ParentSection = parent, Title = title, Order = order,
                Depth = parent is null ? 0 : parent.Depth + 1, SectionKey = SectionCatalogBridge.KeyFor(kind) ?? SectionKeys.NewCustom(),
            };
            _ver.Sections.Add(s);
            _db.DocumentSections.Add(s);
            return s;
        }

        public void Prose(DocumentSection s, string markdown) =>
            Add(s, BlockFormat.Prose, body: markdown);

        public void Callout(DocumentSection s, CalloutKind kind, string title, string markdown) =>
            Add(s, BlockFormat.Callout, body: markdown, callout: kind, bodyJson: JsonSerializer.Serialize(new { title }));

        public void Table(DocumentSection s, object data) =>
            Add(s, BlockFormat.Table, bodyJson: JsonSerializer.Serialize(data));

        private void Add(DocumentSection s, BlockFormat format,
            string? body = null, string? bodyJson = null, CalloutKind? callout = null)
        {
            var block = new ContentBlock
            {
                DocumentVersion = _ver, Section = s, Order = s.Blocks.Count + 1,
                Tier = BlockTier.Reduced, Format = format, Visibility = BlockVisibility.Always,
                CalloutKind = callout, Body = body, BodyJson = bodyJson,
            };
            s.Blocks.Add(block);
            _ver.Blocks.Add(block);
            _db.ContentBlocks.Add(block);
        }
    }
}
