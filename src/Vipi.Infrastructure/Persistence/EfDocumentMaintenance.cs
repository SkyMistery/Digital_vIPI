using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="IDocumentMaintenance"/>
public sealed class EfDocumentMaintenance : IDocumentMaintenance
{
    private const string HiddenSectionsProperty = "HiddenSections";
    private const string MinimaKey = "minima";
    private const string ValidityKey = "validity";

    /// <summary>L'etichetta esatta che il seminatore scriveva, e il valore che la accompagnava: «AIRAC » e quattro
    /// cifre. Solo questa coppia si tocca — vedi <see cref="IDocumentMaintenance.ClearVloaSeededAiracRowAsync"/>.</summary>
    private const string SeededAiracLabel = "Effective from";
    private static readonly System.Text.RegularExpressions.Regex SeededAiracValue =
        new(@"^AIRAC\s*\d{4}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase
            | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    private const string PurposeKey = "purpose";
    private const string PurposeTitle = "Purpose";

    private readonly VipiDbContext _db;

    public EfDocumentMaintenance(VipiDbContext db) => _db = db;

    public async Task<int> ReconcileCustomSectionKeysAsync(CancellationToken ct = default)
    {
        // Solo le sezioni con la chiave storica ambigua: le nuove nascono già univoche (doc 11 §3a).
        var legacy = await _db.DocumentSections
            .Where(s => s.SectionKey == SectionKeys.LegacyCustom)
            .ToListAsync(ct);
        if (legacy.Count == 0) return 0;

        foreach (var s in legacy)
        {
            s.SectionKey = SectionKeys.NewCustom();
            s.RowVersion = Guid.NewGuid().ToByteArray();
        }
        await _db.SaveChangesAsync(ct);
        return legacy.Count;
    }

    public async Task<int> MigrateHiddenSectionsAsync(CancellationToken ct = default)
    {
        var touched = await FromDocumentProfilesAsync(ct);
        touched += await FromAccBlockMetaAsync(ct);
        return touched;
    }

    public async Task<int> ReconcileVloaSectionKeysAsync(CancellationToken ct = default)
    {
        var touched = await ReconcileCoordinationDirectionsAsync(ct);
        touched += await ReconcilePurposeKeyAsync(ct);
        if (touched > 0) await _db.SaveChangesAsync(ct);
        return touched;
    }

    /// <summary>Le due figlie di «coordination» prendono una chiave per direzione, nell'ordine in cui il registro
    /// le semina (prima Home→vicino). Idempotente: cerca solo le figlie che ripetono ancora la chiave del padre.</summary>
    private async Task<int> ReconcileCoordinationDirectionsAsync(CancellationToken ct)
    {
        var parentIds = await _db.DocumentSections
            .Where(s => s.SectionKey == SectionKeys.Coordination && s.ParentSectionId == null)
            .Select(s => s.Id).ToListAsync(ct);
        if (parentIds.Count == 0) return 0;

        var children = await _db.DocumentSections
            .Where(s => s.ParentSectionId != null && parentIds.Contains(s.ParentSectionId!.Value)
                        && s.SectionKey == SectionKeys.Coordination)
            .ToListAsync(ct);
        if (children.Count == 0) return 0;

        var touched = 0;
        foreach (var group in children.GroupBy(s => s.ParentSectionId!.Value))
        {
            var ordered = group.OrderBy(s => s.Order).ThenBy(s => s.Id).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                // Oltre le due direzioni non c'è nulla da riconciliare: una terza figlia con la chiave del padre
                // non è una direzione, e inventarle un verso sarebbe peggio che lasciarla libera.
                if (i > 1) break;
                ordered[i].SectionKey = i == 0 ? SectionKeys.CoordinationOut : SectionKeys.CoordinationIn;
                ordered[i].RowVersion = Guid.NewGuid().ToByteArray();
                touched++;
            }
        }

        // I blocchi delle direzioni non li rende nessuno (il corpo lo produce la pagina): erano i due paragrafi
        // seminati dal registro, scritti nel DB di ogni vLOA e invisibili ovunque.
        var directionIds = children.Select(s => s.Id).ToList();
        var orphanBlocks = await _db.ContentBlocks.Where(b => directionIds.Contains(b.SectionId)).ToListAsync(ct);
        if (orphanBlocks.Count > 0) _db.ContentBlocks.RemoveRange(orphanBlocks);

        return touched;
    }

    /// <summary>«Purpose» nasceva con una chiave libera perché il catalogo non la conosceva: la si riconosce per
    /// titolo — è l'unico appiglio rimasto, ed è legittimo in una riconciliazione one-shot — e solo dentro le vLOA.</summary>
    private async Task<int> ReconcilePurposeKeyAsync(CancellationToken ct)
    {
        var candidates = await _db.DocumentSections
            .Where(s => s.ParentSectionId == null
                        && s.Title == PurposeTitle
                        && s.DocumentVersion!.Document!.Type == Vipi.Domain.DocumentType.Vloa)
            .ToListAsync(ct);

        var touched = 0;
        foreach (var s in candidates)
        {
            if (!SectionKeys.IsCustom(s.SectionKey)) continue;   // già riconciliata
            s.SectionKey = PurposeKey;
            s.RowVersion = Guid.NewGuid().ToByteArray();
            touched++;
        }
        return touched;
    }

    public async Task<int> LinkAirportDocumentsAsync(CancellationToken ct = default)
    {
        // Solo quelli ancora scollegati: il giro è idempotente e non tocca chi è già a posto.
        var airports = await _db.Airports.Where(a => a.DocumentId == null).ToListAsync(ct);
        if (airports.Count == 0) return 0;

        var ids = airports.Select(a => a.Id).ToList();
        // Il documento dove viveva prima: su un settore d'aeroporto che NON sia un APP non remotizzato — quello
        // ha un documento suo, e prenderlo qui farebbe descrivere l'aeroporto dal documento dell'APP.
        var perAeroporto = await _db.Sectors.AsNoTracking()
            .Where(s => s.AirportId != null && ids.Contains(s.AirportId!.Value) && s.DocumentId != null)
            .AirportDocSectors()
            // Il primario per primo: dove i settori sono più d'uno è quello che il rebuild aveva eletto.
            .OrderByDescending(s => s.IsPrimary).ThenBy(s => s.Id)
            .Select(s => new { AirportId = s.AirportId!.Value, DocumentId = s.DocumentId!.Value })
            .ToListAsync(ct);

        var mappa = perAeroporto
            .GroupBy(x => x.AirportId)
            .ToDictionary(g => g.Key, g => g.First().DocumentId);

        var collegati = 0;
        foreach (var a in airports)
            if (mappa.TryGetValue(a.Id, out var docId)) { a.DocumentId = docId; collegati++; }

        if (collegati > 0) await _db.SaveChangesAsync(ct);
        return collegati;
    }

    public async Task<int> ClearVloaSeededAiracRowAsync(CancellationToken ct = default)
    {
        var blocchi = await _db.ContentBlocks
            .Include(b => b.Section)
            .Where(b => b.Section!.SectionKey == ValidityKey
                        && b.Format == BlockFormat.Table
                        && b.BodyJson != null && b.BodyJson != ""
                        && b.DocumentVersion!.Document!.Type == DocumentType.Vloa)
            .ToListAsync(ct);
        if (blocchi.Count == 0) return 0;

        var tolte = 0;
        var vuoti = new List<ContentBlock>();
        foreach (var b in blocchi)
        {
            if (!TogliRigaAirac(b.BodyJson!, out var json, out var n)) continue;
            tolte += n;
            b.BodyJson = json;
            if (SenzaRighe(json)) vuoti.Add(b);
        }

        if (tolte == 0) return 0;
        if (vuoti.Count > 0) _db.ContentBlocks.RemoveRange(vuoti);
        await _db.SaveChangesAsync(ct);
        return tolte;
    }

    /// <summary>Toglie dal JSON della tabella le righe seminate «Effective from | AIRAC ####». Ritorna false se
    /// non c'era niente da togliere (o se il JSON non è una tabella leggibile: non è compito nostro ripararlo).</summary>
    private static bool TogliRigaAirac(string bodyJson, out string json, out int tolte)
    {
        json = bodyJson;
        tolte = 0;
        JsonNode? root;
        try { root = JsonNode.Parse(bodyJson); }
        catch (JsonException) { return false; }
        if (root is not JsonObject obj || obj["rows"] is not JsonArray rows) return false;

        // Si itera all'indietro: togliere dal fondo non sposta gli indici di quelle ancora da guardare.
        for (var i = rows.Count - 1; i >= 0; i--)
        {
            if (rows[i] is not JsonObject riga || riga["cells"] is not JsonArray celle || celle.Count < 2) continue;
            var prima = celle[0]?.GetValue<string>()?.Trim();
            var seconda = celle[1]?.GetValue<string>()?.Trim();
            if (!string.Equals(prima, SeededAiracLabel, StringComparison.OrdinalIgnoreCase)) continue;
            if (seconda is null || !SeededAiracValue.IsMatch(seconda)) continue;
            rows.RemoveAt(i);
            tolte++;
        }

        if (tolte == 0) return false;
        json = obj.ToJsonString();
        return true;
    }

    private static bool SenzaRighe(string json)
    {
        try { return JsonNode.Parse(json) is JsonObject o && (o["rows"] as JsonArray)?.Count is null or 0; }
        catch (JsonException) { return false; }
    }

    public async Task<int> ClearMinimaPlaceholderBlocksAsync(CancellationToken ct = default)
    {
        // Solo i placeholder: blocco senza testo E senza JSON. Un blocco con contenuto è roba di un editore e resta.
        var stale = await _db.ContentBlocks
            .Where(b => b.Section!.SectionKey == MinimaKey
                        && (b.Body == null || b.Body == "")
                        && (b.BodyJson == null || b.BodyJson == ""))
            .ToListAsync(ct);
        if (stale.Count == 0) return 0;

        _db.ContentBlocks.RemoveRange(stale);
        await _db.SaveChangesAsync(ct);
        return stale.Count;
    }

    /// <summary>APP (chiavi) e vLOA (titoli): un'unica colonna <c>DocumentProfiles.HiddenSectionsJson</c>, per
    /// documento e non versionata. Si marcano le sezioni di TUTTE le versioni del documento (l'intento «nascosta» è
    /// del documento, non di una singola bozza), poi si azzera la sorgente: senza, rieseguire la migrazione
    /// rimetterebbe nascosto ciò che nel frattempo l'editore ha rimesso pubblico.</summary>
    private async Task<int> FromDocumentProfilesAsync(CancellationToken ct)
    {
        var profiles = await _db.DocumentProfiles
            .Where(p => p.HiddenSectionsJson != null && p.HiddenSectionsJson != "")
            .ToListAsync(ct);
        if (profiles.Count == 0) return 0;

        var touched = 0;
        foreach (var profile in profiles)
        {
            var hidden = ParseStrings(profile.HiddenSectionsJson);
            profile.HiddenSectionsJson = null;
            if (hidden.Count == 0) continue;

            var sections = await _db.DocumentSections
                .Where(s => s.DocumentVersion!.DocumentId == profile.DocumentId)
                .ToListAsync(ct);
            touched += MarkHidden(sections, hidden);
        }
        await _db.SaveChangesAsync(ct);
        return touched;
    }

    /// <summary>vIPI ACC: le chiavi nascoste stavano nel blockmeta (BodyJson del blocco proprio della sezione-blocco)
    /// e valgono per le sezioni figlie di QUEL blocco. Il JSON viene riscritto senza la proprietà, così la migrazione
    /// resta idempotente.</summary>
    private async Task<int> FromAccBlockMetaAsync(CancellationToken ct)
    {
        var candidates = await _db.ContentBlocks
            .Include(b => b.Section)
            .Where(b => b.BodyJson != null && b.BodyJson.Contains(HiddenSectionsProperty)
                        && b.Section!.ParentSectionId == null)
            .ToListAsync(ct);
        if (candidates.Count == 0) return 0;

        var touched = 0;
        foreach (var block in candidates)
        {
            if (ParseObject(block.BodyJson) is not { } meta) continue;
            if (meta[HiddenSectionsProperty] is not JsonArray array) continue;

            var hidden = array.Select(n => n?.GetValue<string>()).OfType<string>().ToList();
            meta.Remove(HiddenSectionsProperty);
            block.BodyJson = meta.ToJsonString();
            block.RowVersion = Guid.NewGuid().ToByteArray();
            if (hidden.Count == 0) continue;

            var children = await _db.DocumentSections
                .Where(s => s.ParentSectionId == block.SectionId)
                .ToListAsync(ct);
            touched += MarkHidden(children, hidden);
        }
        await _db.SaveChangesAsync(ct);
        return touched;
    }

    /// <summary>Marca le sezioni identificate dalle voci storiche: per chiave (ACC/APP) o per titolo (vLOA). La voce
    /// ambigua <c>custom</c> non identifica più nulla dopo la riconciliazione delle chiavi, quindi si espande a TUTTE
    /// le sezioni libere: conservativo, non scopre in pubblico ciò che l'editore riteneva nascosto.</summary>
    private static int MarkHidden(IReadOnlyList<DocumentSection> sections, IReadOnlyList<string> hidden)
    {
        var byKeyOrTitle = new HashSet<string>(hidden, StringComparer.OrdinalIgnoreCase);
        var allCustom = byKeyOrTitle.Contains(SectionKeys.LegacyCustom);

        var touched = 0;
        foreach (var s in sections)
        {
            if (s.IsHidden) continue;
            var match = byKeyOrTitle.Contains(s.SectionKey)
                        || byKeyOrTitle.Contains(s.Title)
                        || (allCustom && SectionKeys.IsCustom(s.SectionKey));
            if (!match) continue;
            s.IsHidden = true;
            s.RowVersion = Guid.NewGuid().ToByteArray();
            touched++;
        }
        return touched;
    }

    private static List<string> ParseStrings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch (JsonException) { return new List<string>(); }
    }

    private static JsonObject? ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonNode.Parse(json) as JsonObject; }
        catch (JsonException) { return null; }
    }

    public async Task<int> AddMissingCatalogSectionsAsync(CancellationToken ct = default)
    {
        // APP standalone, vLOA e — dalla carta 2026-08-26 — AEROPORTI. Resta fuori la sola vIPI ACC, che ha le
        // sezioni sotto i BLOCCHI: lì la rete a view-time dell'assembler continua a coprirla, e serve anche agli
        // snapshot di release vecchi, che non si riscrivono.
        var airportDocIds = await _db.Airports.Where(a => a.DocumentId != null)
            .Select(a => a.DocumentId!.Value).ToListAsync(ct);
        var airports = airportDocIds.ToHashSet();
        var docs = await _db.Documents
            .Include(d => d.Sectors)
            .Where(d => d.Type == Vipi.Domain.DocumentType.Vloa
                        || airportDocIds.Contains(d.Id)
                        || d.Sectors.Any(x => x.IsPrimary && x.Type == SectorType.App
                                              && x.ApproachKind == ApproachKind.Standalone))
            .ToListAsync(ct);
        if (docs.Count == 0) return 0;

        var added = 0;
        foreach (var doc in docs)
        {
            var profile = doc.Type == Vipi.Domain.DocumentType.Vloa ? SectionProfile.Vloa
                : airports.Contains(doc.Id) ? SectionProfile.Airport
                : SectionProfile.App;

            var versionId = await _db.DocumentVersions
                .Where(v => v.DocumentId == doc.Id)
                .OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.Id).FirstOrDefaultAsync(ct);
            if (versionId is null) continue;

            var roots = await _db.DocumentSections
                .Where(x => x.DocumentVersionId == versionId && x.ParentSectionId == null)
                .OrderBy(x => x.Order).ToListAsync(ct);

            var present = roots.Select(x => x.SectionKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = SectionCatalog.For(profile).Where(d => !present.Contains(d.Key)).OrderBy(d => d.Order).ToList();
            if (missing.Count == 0) continue;

            var version = await _db.DocumentVersions.FirstAsync(v => v.Id == versionId, ct);
            foreach (var desc in missing)
            {
                var section = new DocumentSection
                {
                    DocumentVersion = version,
                    ParentSection = null,
                    Title = desc.Title,
                    Order = 0,   // riassegnato sotto, insieme a tutti
                    Depth = 0,
                    SectionKey = desc.Key,
                    RowVersion = Guid.NewGuid().ToByteArray(),
                    // Una sezione «sempre live» non deve nascere Frozen nemmeno quando arriva da qui: il default
                    // della colonna e' Frozen, e il meteo congelato e' meteo scaduto (carta 2026-08-26 §1a).
                    RenderMode = SectionCatalog.IsAlwaysLive(desc.Key) ? RenderMode.Live : RenderMode.Frozen,
                };
                // Inserita PRIMA della prima sezione fissa che nel catalogo viene dopo di lei; se non ce n'è, in
                // coda. Accodarle e basta metterebbe «Purpose» in fondo a una lettera d'accordo.
                var at = roots.FindIndex(x => SectionCatalog.Find(profile, x.SectionKey) is { } f && f.Order > desc.Order);
                roots.Insert(at < 0 ? roots.Count : at, section);
                _db.DocumentSections.Add(section);
                added++;
            }

            for (var i = 0; i < roots.Count; i++)
            {
                if (roots[i].Order == i + 1) continue;
                roots[i].Order = i + 1;
                roots[i].RowVersion = Guid.NewGuid().ToByteArray();
            }
        }

        if (added > 0) await _db.SaveChangesAsync(ct);
        return added;
    }

    // ---- carta 2026-08-26: i documenti d'aeroporto gia' scritti ----
    // ⚠️ La mappa titolo→chiave NON sta qui: sta in AirportLegacySections, perche' ha DUE lettori. Questo passo
    // riscrive i documenti di LAVORO una volta per tutte; il viewer deve leggere anche gli snapshot di release,
    // e quelli non si riscrivono mai. Due copie della stessa mappa sarebbero due verita' sullo stesso archivio.

    public async Task<int> ReconcileAirportSectionKeysAsync(CancellationToken ct = default)
    {
        var scali = await _db.Airports.Where(a => a.DocumentId != null)
            .Select(a => new { a.Id, DocumentId = a.DocumentId!.Value }).ToListAsync(ct);
        if (scali.Count == 0) return 0;

        var toccate = 0;
        foreach (var scalo in scali)
        {
            var versionId = await _db.DocumentVersions
                .Where(v => v.DocumentId == scalo.DocumentId)
                .OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.Id).FirstOrDefaultAsync(ct);
            if (versionId is not int vid) continue;

            var version = await _db.DocumentVersions.FirstAsync(v => v.Id == vid, ct);
            var roots = await _db.DocumentSections.Include(x => x.Blocks)
                .Where(x => x.DocumentVersionId == vid && x.ParentSectionId == null)
                .OrderBy(x => x.Order).ToListAsync(ct);

            toccate += ReconcileCookedSections(roots);
            toccate += await MoveExtraSectionsIntoDocumentAsync(scalo.Id, version, roots, ct);
        }

        if (toccate > 0) await _db.SaveChangesAsync(ct);
        return toccate;
    }

    /// <summary>
    /// Passi 1 e 2: la sezione cotta prende la sua chiave di catalogo e <b>perde i blocchi</b>, perche' da qui in
    /// poi il corpo lo produce la pagina derivandolo dalle tabelle del profilo.
    /// <para>⚠️ Si guardano solo le sezioni con chiave LIBERA: quelle cotte nascevano tutte cosi' (il builder
    /// chiedeva la chiave per <c>BlockSection.Airport</c>, che non ne ha una, e ricadeva su una guid nuova). Una
    /// chiave di catalogo gia' presente non si tocca, e una seconda sezione con lo stesso titolo nemmeno: la
    /// riconciliazione ne rivendica <b>una sola</b> per chiave.</para>
    /// </summary>
    private int ReconcileCookedSections(List<DocumentSection> roots)
    {
        var gia = roots.Select(x => x.SectionKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fatte = 0;
        foreach (var s in roots)
        {
            var titolo = (s.Title ?? "").Trim();
            var toccata = false;

            // Passo 1 — la chiave. Solo le sezioni con chiave LIBERA: «Frequencies» e «SID» ce l'avevano gia'
            // giusta, e una chiave di catalogo non si sovrascrive.
            if (SectionKeys.IsCustom(s.SectionKey)
                && AirportLegacySections.KeyForCookedTitle(titolo) is { } key
                && gia.Add(key))
            {
                s.SectionKey = key;
                s.Title = SectionCatalog.Find(SectionProfile.Airport, key)?.Title ?? titolo;
                toccata = true;
            }

            // Passo 1-bis — il TITOLO di una sezione di catalogo lo decide il catalogo, e vale anche per quelle
            // che la chiave giusta ce l'avevano già: «Frequencies» e «SID» non passano dal rinomina di sopra, e
            // senza questo ramo il documento resterebbe metà in italiano e metà in inglese. Una sezione fissa non
            // si rinomina a mano (IsMandatory lo vieta), quindi non c'è nessuna scelta editoriale da rispettare.
            if (SectionCatalog.Find(SectionProfile.Airport, s.SectionKey) is { } desc && s.Title != desc.Title)
            {
                s.Title = desc.Title;
                toccata = true;
            }

            // Passo 2 — i blocchi. Vale per OGNI sezione il cui corpo lo produce ora la pagina, non solo per
            // quelle appena rinominate: «Frequencies» aveva la chiave giusta fin dall'inizio e la sua tabella
            // cotta dentro, e senza questo ramo resterebbe li' a raddoppiare la tabella derivata.
            if (SectionCatalog.IsHostRendered(SectionProfile.Airport, s.SectionKey) && s.Blocks.Count > 0)
            {
                // Rimossi dal CONTESTO, non solo dalla collezione: staccarli e basta lascerebbe a EF una riga
                // con la chiave esterna da azzerare, che e' non-nullabile — e la SaveChanges morirebbe.
                _db.ContentBlocks.RemoveRange(s.Blocks);
                s.Blocks.Clear();
                toccata = true;
            }

            if (!toccata) continue;
            s.RowVersion = Guid.NewGuid().ToByteArray();
            fatte++;
        }
        return fatte;
    }

    /// <summary>
    /// Passo 3: le sezioni editoriali libere dell'aeroporto smettono di vivere in una tabella a parte
    /// (<c>AirportExtraSection</c>) e diventano sezioni del documento, una chiave <c>custom:{guid}</c> ciascuna.
    /// <para>⚠️ La sorgente e' la TABELLA, non le sezioni gia' cotte: la pagina pubblica leggeva gli extra dal
    /// profilo <b>live</b>, quindi la copia nel documento poteva essere vecchia di un rebuild. E' un TRASLOCO —
    /// le righe si cancellano dopo averle portate dentro, ed e' anche cio' che rende il passo idempotente.</para>
    /// </summary>
    private async Task<int> MoveExtraSectionsIntoDocumentAsync(
        int airportId, DocumentVersion version, List<DocumentSection> roots, CancellationToken ct)
    {
        var righe = await _db.AirportExtraSections.Where(x => x.AirportId == airportId)
            .OrderBy(x => x.Order).ToListAsync(ct);
        var vecchie = roots.Where(x => string.Equals(x.SectionKey, AirportLegacySections.ExtraKey, StringComparison.OrdinalIgnoreCase)).ToList();
        if (righe.Count == 0 && vecchie.Count == 0) return 0;

        // Via le copie cotte: si riscrivono dalla tabella, che e' la versione vera.
        foreach (var v in vecchie)
        {
            _db.ContentBlocks.RemoveRange(v.Blocks);
            _db.DocumentSections.Remove(v);
            roots.Remove(v);
        }

        var order = roots.Count == 0 ? 0 : roots.Max(x => x.Order);
        foreach (var riga in righe)
        {
            var sezione = new DocumentSection
            {
                DocumentVersion = version,
                ParentSection = null,
                Title = string.IsNullOrWhiteSpace(riga.Title) ? "Sezione" : riga.Title,
                Order = ++order,
                Depth = 0,
                SectionKey = SectionKeys.NewCustom(),
                RowVersion = Guid.NewGuid().ToByteArray(),
            };
            _db.DocumentSections.Add(sezione);
            roots.Add(sezione);

            var n = 0;
            foreach (var blk in ExtraBlocks.Parse(riga.Body))
            {
                var blocco = ToContentBlock(version, sezione, blk, ++n);
                if (blocco is null) { n--; continue; }
                _db.ContentBlocks.Add(blocco);
            }
        }

        _db.AirportExtraSections.RemoveRange(righe);
        return vecchie.Count + righe.Count;
    }

    /// <summary>Un blocco dell'envelope degli extra in un blocco del documento. Null = da scartare (stessa regola
    /// della cottura: prosa senza testo o immagine senza riferimento non entravano nel documento).</summary>
    private static ContentBlock? ToContentBlock(DocumentVersion version, DocumentSection section, ExtraBlock blk, int order)
    {
        string? body = null, bodyJson = null;
        CalloutKind? callout = null;
        var format = blk.Format;
        switch (blk.Format)
        {
            case BlockFormat.Callout when !string.IsNullOrWhiteSpace(blk.Text):
                body = blk.Text; callout = blk.CalloutKind;
                bodyJson = JsonSerializer.Serialize(new { title = "" });
                break;
            case BlockFormat.Table when !string.IsNullOrWhiteSpace(blk.TableJson):
                bodyJson = blk.TableJson;
                break;
            case BlockFormat.Image when MediaRef.Parse(blk.ImageJson) is not null:
                body = blk.Text; bodyJson = blk.ImageJson;
                break;
            case BlockFormat.Prose or BlockFormat.List when !string.IsNullOrWhiteSpace(blk.Text):
                body = blk.Text; format = BlockFormat.Prose;
                break;
            default:
                return null;
        }

        return new ContentBlock
        {
            DocumentVersion = version, Section = section, Order = order,
            Tier = BlockTier.Extended, Format = format, Visibility = BlockVisibility.Always,
            CalloutKind = callout, Body = body, BodyJson = bodyJson,
            RowVersion = Guid.NewGuid().ToByteArray(),
        };
    }
}
