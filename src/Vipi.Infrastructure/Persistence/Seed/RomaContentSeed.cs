using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;

namespace Vipi.Infrastructure.Persistence.Seed;

/// <summary>
/// Seed di contenuto demo (dati finti ma realistici) per la vIPI di Roma ACC, derivato fedelmente dal
/// mockup v2 (schermata "doc"): separazioni sep-box, AoR con settori/configurazioni, tabelle unificate
/// config/frequenze, coordinamenti annidati con tip, SCCAM e aree regolamentate. Idempotente.
/// </summary>
public static class RomaContentSeed
{
    private static readonly IAiracService Airac = new AiracService();

    public static async Task SeedAsync(VipiDbContext db, CancellationToken ct = default)
    {
        var acc = await db.Sectors.FirstOrDefaultAsync(p => p.Callsign == "LIRR_NE_CTR", ct);
        if (acc is null) return;
        if (acc.DocumentId is not null) return;

        var now = DateTime.UtcNow;
        var cycle = Airac.GetCycle(now);

        var doc = new Document
        {
            Type = DocumentType.Vipi,
            Title = "vIPI — Roma ACC",
            Language = Language.It,
            Status = DocumentStatus.Published,
            LastUpdatedUtc = now,
            LastUpdatedAiracCycle = cycle,
        };
        var ver = new DocumentVersion
        {
            Document = doc, VersionNumber = 1, Status = DocumentStatus.Published,
            CreatedByUserId = 0, CreatedUtc = now, AiracCycle = cycle, Note = "Seed demo F2",
        };
        doc.Versions.Add(ver);
        db.Documents.Add(doc);

        var b = new Builder(db, ver);

        // 1 — Separazioni radar richieste (sep-box Standard + Ridotta)
        var sep = b.Section("Separazioni radar richieste", BlockSection.Separations, 1);
        b.Separations(sep, new
        {
            groups = new object[]
            {
                new { title = "Standard", items = new[] { Item("Laterale", "5 NM"), Item("Verticale", "1000 ft") } },
                new { title = "Ridotta",  items = new[] { Item("Laterale", "3 NM"), Item("Verticale", "1000 ft") },
                      warning = new { title = "Condizioni di riduzione",
                          text = "Applicabile **solo** in avvicinamento a LIRF entro 40 NM dal radar, con copertura radar continua e identificazione confermata. Da non applicare in caso di degrado del segnale." } },
            }
        });

        // 2 — Area di responsabilità (AoR con toggle settori + configurazioni)
        var aor = b.Section("Area di responsabilità (AoR)", BlockSection.Aor, 2);
        b.Prose(aor, BlockTier.Extended,
            "Settori alti di Roma ACC. Seleziona quali settori mostrare per capire interagenze e sovrapposizioni. Le shape provengono dal database IVAO (placeholder in questa fase).");
        b.Aor(aor, new
        {
            sectors = new object[]
            {
                new { key = "ne", label = "LIRR_NE", color = "#0D2C99", fill = 0.18, points = "40,60 200,40 230,150 90,200 30,150", text = "NE", tx = 110, ty = 120, tcolor = "#0D2C99" },
                new { key = "ew", label = "LIRR_EW", color = "#3C55AC", fill = 0.22, points = "200,40 400,70 410,180 260,210 230,150", text = "EW", tx = 320, ty = 130, tcolor = "#3C55AC" },
                new { key = "su", label = "LIRR_SU", color = "#7EA2D6", fill = 0.30, points = "90,200 260,210 250,275 110,270", text = "SU", tx = 170, ty = 245, tcolor = "#2c5d99" },
            },
            configs = new object[]
            {
                new { name = "Config 1 · Tutto unificato (NE)", rows = "0",       secs = "ne,ew,su" },
                new { name = "Config 2 · NE + TS",              rows = "1,2",     secs = "ne,ew" },
                new { name = "Config 3 · Piena",                rows = "3,4,5,6", secs = "ne,ew,su" },
            }
        });

        // 3 — Configurazioni operative (tabella unificata, evidenziabile dalle configurazioni AoR)
        var cfg = b.Section("Configurazioni operative", BlockSection.OperationalSettings, 3);
        b.Table(cfg, BlockTier.Extended, new
        {
            tableId = "cfg-ops",
            columns = new[] { "Settore Unificato", "Settore", "Center Point", "Range" },
            unified = true,
            rows = new object[]
            {
                Row("0", "NE", "NE (NE+EW+SU+TS Unificato)", "GINEL", "140"),
                Row("1", "NE", "NE (NE+SU Unificato)", "TEANO", "120"),
                Row("2", "NE", "EW (EW+TS)", "OST", "110"),
                Row("3", "NE", "NE", "TEANO", "120"),
                Row("4", "NE", "SU", "TEANO", "120"),
                Row("5", "EW", "EW", "OST", "110"),
                Row("6", "EW", "TS", "LAT", "100"),
            }
        });

        // 4 — Frequenze (unificata, principale ★) — Ridotta
        var freq = b.Section("Frequenze", BlockSection.Frequencies, 4);
        b.Table(freq, BlockTier.Reduced, new
        {
            columns = new[] { "Settore unico", "Posizione", "Callsign", "Frequenza" },
            unified = true,
            rows = new object[]
            {
                FreqRow("NE", true, "Roma Radar NE", "LIRR_NE_CTR", "128.800"),
                FreqRow("NE", false, "Roma Radar SU", "LIRR_SU_CTR", "125.100"),
                FreqRow("EW", true, "Roma Radar EW", "LIRR_EW_CTR", "133.250"),
                FreqRow("EW", false, "Roma Radar TS", "LIRR_TS_CTR", "132.700"),
            }
        });

        // 5 — Minime di vettoramento (placeholder future)
        var min = b.Section("Minime di vettoramento", BlockSection.Separations, 5);
        b.Callout(min, CalloutKind.Warning, "Da definire", BlockTier.Extended,
            "🛠️ Sono **mappe** (carte MVA), non tabelle. Saranno importate dal sectorfile della divisione su GitHub; il parsing è rimandato.");

        // 6 — Coordinamenti
        var coord = b.Section("Coordinamenti", BlockSection.Coordination, 6);

        var grpAcc = b.Section("Settori ACC", BlockSection.Coordination, 1, coord);
        var ne = b.Section("Settore NE", BlockSection.Coordination, 1, grpAcc);
        var neDest = b.Section("Traffico Dest LIRF", BlockSection.Coordination, 1, ne);
        b.Prose(neDest, BlockTier.Extended,
            "**Roma NE trasferisce** il traffico in arrivo su LIRF agli avvicinamenti, in discesa per i CoP pubblicati. **Roma NE riceve** dai settori confinanti il traffico in salita coordinato per livello.");
        b.CopTable(neDest, Cop("VALMA", "FL130-", "LIRF_APP"), Cop("ELKAP", "FL150-", "LIRF_APP"));
        var neDep = b.Section("Traffico DEP LIRF", BlockSection.Coordination, 2, ne);
        b.CopTable(neDep, Cop("VALMA", "FL280+", "LIMM_WS2"), Cop("TARQ", "FL250+", "LIRR_EW"));
        var neOvf = b.Section("Traffico OVF (sorvoli)", BlockSection.Coordination, 3, ne);
        b.CopTable(neOvf, Cop("ELB", "per aerovia", "LIMM_WS2"));

        var ew = b.Section("Settore EW", BlockSection.Coordination, 2, grpAcc);
        var ewDest = b.Section("Traffico Dest LIRN (Napoli)", BlockSection.Coordination, 1, ew);
        b.Prose(ewDest, BlockTier.Extended,
            "**Roma EW trasferisce** gli arrivi LIRN all'avvicinamento, in discesa per i CoP costieri.");
        b.CopTable(ewDest, Cop("PESET", "FL120-", "LIRN_APP"), Cop("TEANO", "FL150-", "LIRN_APP"));
        b.Callout(ewDest, CalloutKind.Info, "Nota", BlockTier.Extended,
            "In presenza di militare attivo (vedi Aree regolamentate) instradare via TEANO.");

        var grpApp = b.Section("Settori APP", BlockSection.Coordination, 2, coord);
        var tw1 = b.Section("Settore TW1", BlockSection.Coordination, 1, grpApp);
        var tw1Dest = b.Section("Traffico Dest LIRF", BlockSection.Coordination, 1, tw1);
        b.Prose(tw1Dest, BlockTier.Extended,
            "**Roma TW1 trasferisce** il traffico in finale alla torre; **riceve** dalle uscite pista i rullaggi coordinati.");
        b.CopTable(tw1Dest, Cop("—", "2500 ft", "LIRF_TWR"));
        b.Tip(tw1Dest, "Controllo della velocità", new[]
        {
            "Il traffico che segue una STAR RNAV1 seguirà i limiti di velocità pubblicati.",
            "Il traffico sotto vettoramento osserverà: 230 KT IAS a FL100 o al di sotto.",
            "210 KT IAS a 20 NM dalla TDZ sull'avvicinamento diretto per RWY 16L/R.",
            "190 KT IAS a 12 NM dalla TDZ. 160 KT IAS a 5 NM dalla TDZ.",
        });
        var tw1Vfr = b.Section("VFR", BlockSection.Coordination, 2, tw1);
        b.Prose(tw1Vfr, BlockTier.Extended,
            "Gestione VFR: traffico consegnato alla TWR per l'inserimento nel circuito; separazione VFR/IFR non fornita salvo SVFR. Punti di riporto e codici nella vIPI dell'aeroporto.");

        var grpVloa = b.Section("vLOA con ACC esteri", BlockSection.Coordination, 3, coord);
        var vloaTun = b.Section("🌍 vLOA · DTTC (Tunisi)", BlockSection.Coordination, 1, grpVloa);
        b.Callout(vloaTun, CalloutKind.Info, "Copia del documento vLOA LIRR ↔ DTTC", BlockTier.Extended,
            "Transfer points e livelli concordati con Tunis ACC (sola lettura). La vLOA completa sarà collegabile da qui.");

        // 7 — Settore SCCAM (sezione a sé)
        var sccam = b.Section("Settore SCCAM", BlockSection.Other, 7);
        b.Prose(sccam, BlockTier.Extended,
            "Settore di coordinamento con la Circolazione Aerea Militare (CAM). La shape della sua **AoR è importata dal database IVAO**, allineata all'AIRAC corrente.");
        b.Area(sccam, new
        {
            label = "SCCAM", color = "#0D2C99", fill = 0.14,
            points = "36,40 200,28 228,108 120,150 30,108", from = "GND", to = "FL460",
            desc = "AoR dal database IVAO. La copertura verticale e i confini si aggiornano con la shape sorgente.",
        });
        b.Callout(sccam, CalloutKind.Info, "Nota", BlockTier.Extended,
            "Le descrizioni sono testo libero curato dagli editor; l'AoR invece non è editabile a mano e proviene sempre dal database IVAO.");

        // 8 — Aree regolamentate (una sotto-sezione per area)
        var areas = b.Section("Aree regolamentate", BlockSection.AreasCorridors, 8);
        var r64 = b.Section("R-64 · CUNEO", BlockSection.AreasCorridors, 1, areas);
        b.Area(r64, new
        {
            label = "R-64", color = "#E93434", fill = 0.16,
            points = "40,30 200,24 220,120 90,150 30,100", from = "GND", to = "FL195",
            desc = "Area attiva per attività militare. Coordinare l'attraversamento con il settore competente quando attiva.",
        });
        var r65 = b.Section("R-65 · LOLA", BlockSection.AreasCorridors, 2, areas);
        b.Area(r65, new
        {
            label = "R-65", color = "#E93434", fill = 0.16,
            points = "60,30 210,40 200,130 80,140 40,80", from = "FL100", to = "FL280",
            desc = "Area di lavoro in quota. Quando attiva, evitare l'attraversamento dei sorvoli in salita: usare i CoP alternativi.",
        });

        await db.SaveChangesAsync(ct);
        doc.CurrentVersionId = ver.Id;

        // Scope multi-settore: la vIPI ACC descrive i settori d'area di Roma (NE primario + EW, SU, ES, TS).
        var scopeCallsigns = new[] { "LIRR_NE_CTR", "LIRR_EW_CTR", "LIRR_SU_CTR", "LIRR_ES_CTR", "LIRR_TS_CTR" };
        var scope = await db.Sectors.Where(s => scopeCallsigns.Contains(s.Callsign) && s.DocumentId == null).ToListAsync(ct);
        foreach (var s in scope)
        {
            s.DocumentId = doc.Id;
            s.IsPrimary = s.Callsign == "LIRR_NE_CTR";
        }
        await db.SaveChangesAsync(ct);
    }

    // --- helper per i dati strutturati ---
    private static object Item(string label, string value) => new { label, value };
    private static object Row(string r, string group, string c1, string c2, string c3) =>
        new { r, group, cells = new[] { c1, c2, c3 } };
    private static object FreqRow(string group, bool primary, string pos, string cs, string mhz) =>
        new { group, primary, star = primary, cells = new[] { pos, cs, mhz } };
    private static object Cop(string cop, string fl, string next) => new { cells = new[] { cop, fl, next } };

    /// <summary>Builder che incapsula ordine, profondità e collegamento alla versione.</summary>
    private sealed class Builder
    {
        private static readonly JsonSerializerOptions Json = new() { };
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

        public void Prose(DocumentSection s, BlockTier tier, string markdown) =>
            Add(s, BlockFormat.Prose, tier, BlockVisibility.Always, body: markdown);

        public void Callout(DocumentSection s, CalloutKind kind, string title, BlockTier tier, string markdown) =>
            Add(s, BlockFormat.Callout, tier, BlockVisibility.Always, body: markdown, callout: kind,
                bodyJson: Serialize(new { title }));

        public void Separations(DocumentSection s, object data) =>
            Add(s, BlockFormat.Table, BlockTier.Extended, BlockVisibility.Always,
                bodyJson: Serialize(new { variant = "separations", data }));

        public void Aor(DocumentSection s, object data) =>
            Add(s, BlockFormat.AorMap, BlockTier.Extended, BlockVisibility.Always,
                bodyJson: Serialize(Merge("aor", data)));

        public void Area(DocumentSection s, object data) =>
            Add(s, BlockFormat.AorMap, BlockTier.Extended, BlockVisibility.Always,
                bodyJson: Serialize(Merge("area", data)));

        public void Table(DocumentSection s, BlockTier tier, object data) =>
            Add(s, BlockFormat.Table, tier, BlockVisibility.Always, bodyJson: Serialize(data));

        public void CopTable(DocumentSection s, params object[] rows) =>
            Add(s, BlockFormat.Table, BlockTier.Reduced, BlockVisibility.Always,
                bodyJson: Serialize(new { columns = new[] { "CoP", "FL", "Next" }, unified = false, rows }));

        public void Tip(DocumentSection s, string title, string[] lines) =>
            Add(s, BlockFormat.Prose, BlockTier.Extended, BlockVisibility.Always,
                bodyJson: Serialize(new { variant = "tip", title, lines }));

        private static string Serialize(object o) => JsonSerializer.Serialize(o, Json);

        // unisce {variant} + le proprietà di data in un unico oggetto JSON
        private static Dictionary<string, JsonElement> Merge(string variant, object data)
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Serialize(data))!;
            using var vd = JsonDocument.Parse($"\"{variant}\"");
            dict["variant"] = vd.RootElement.Clone();
            return dict;
        }

        private void Add(DocumentSection s, BlockFormat format, BlockTier tier, BlockVisibility vis,
            string? body = null, string? bodyJson = null, CalloutKind? callout = null)
        {
            var block = new ContentBlock
            {
                DocumentVersion = _ver, Section = s, Order = s.Blocks.Count + 1,
                Tier = tier, Format = format, Visibility = vis, CalloutKind = callout,
                Body = body, BodyJson = bodyJson,
            };
            s.Blocks.Add(block);
            _ver.Blocks.Add(block);
            _db.ContentBlocks.Add(block);
        }
    }
}
