using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Il <b>travaso</b> delle configurazioni dal documento alla Struttura: per ogni gruppo che in Struttura non ha
/// ancora un elenco, copia quello scritto nella versione di lavoro del suo documento. Carta
/// <c>docs/feature/2026-10-08-configurazioni-possibili.md</c> §5.
///
/// <para><b>Perché esiste.</b> Fino all'8 ottobre 2026 le configurazioni erano il <c>BodyJson</c> della sezione
/// <c>configurations</c> della vIPI. Spostando la scrittura in Struttura, la tabella nasce vuota: senza questo
/// passo Milano perderebbe le sue quattro configurazioni alla prima pubblicazione — e la Diagnostica
/// continuerebbe a dare il falso «Trasferimento senza ripiego» finché qualcuno non le riscrive.</para>
///
/// <para>⚠️ <b>Idempotente, e non riporta indietro niente.</b> Un gruppo che ha già una riga non si tocca, nemmeno
/// se la riga è un elenco vuoto: vuol dire che lì qualcuno ha già deciso (<c>ReplaceAsync</c> la lascia apposta).
/// Un gruppo il cui documento non ha configurazioni non riceve nessuna riga: non c'è niente da portare.</para>
///
/// <para>⚠️ <b>Non cancella niente dal documento.</b> Il <c>BodyJson</c> resta dov'è: le release già uscite lo
/// leggono dal loro snapshot, e quelle nuove lo ignorano per il segno
/// <see cref="DocReleasePayload.ConfigurazioniDallaStruttura"/>. Un passo che non distrugge si può rifare.</para>
/// </summary>
internal static class TravasoDelleConfigurazioni
{
    private const string Chiave = "configurations";
    private static readonly StringComparer OIC = StringComparer.OrdinalIgnoreCase;

    /// <returns>Quanti gruppi hanno ricevuto il loro elenco.</returns>
    public static async Task<int> EseguiAsync(VipiDbContext db, CancellationToken ct)
    {
        var gia = (await db.SectorConfigurationSets.AsNoTracking()
                .Select(r => new { r.GroupKind, r.GroupCode }).ToListAsync(ct))
            .Select(r => (r.GroupKind, Codice: r.GroupCode.ToUpperInvariant()))
            .ToHashSet();

        var enti = await db.AtcUnits.AsNoTracking()
            .Select(u => new { u.Code, u.DocumentId, u.GroupKey, u.AccId })
            .ToListAsync(ct);

        var nuovi = new List<SectorConfigurationSet>();
        void Porta(ConfigurationGroupKind genere, string codice, List<AccConfiguration> elenco)
        {
            var chiave = (genere, codice.ToUpperInvariant());
            // ⚠️ Solo elenchi con almeno un settore aperto: una «New configuration» mai riempita non è un
            // elenco, e una riga vuota qui direbbe «già deciso» di un gruppo su cui nessuno ha deciso niente.
            if (gia.Contains(chiave) || !elenco.Any(c => c.Open.Count > 0)) return;
            gia.Add(chiave);
            nuovi.Add(new SectorConfigurationSet
            {
                GroupKind = genere, GroupCode = chiave.Item2,
                BodyJson = ConfigurazioniJson.Scrivi(elenco), UpdatedAtUtc = DateTime.UtcNow,
            });
        }

        // ---- 1. Le vIPI degli ACC: il blocco Aerovia (settori d'area) e i gruppi APP (i loro enti). ----
        var accs = await db.Accs.AsNoTracking().Where(a => !a.IsForeign).Select(a => new { a.Id, a.Code }).ToListAsync(ct);
        foreach (var acc in accs)
        {
            var codiceAcc = acc.Code.ToUpperInvariant();
            // Stesso criterio di EfAccDerivationRepository.ResolveAccDocumentIdentityAsync: il documento sta
            // sul CTR radice primario dell'ACC.
            var docId = await db.Sectors.AsNoTracking()
                .Where(s => s.AccId == acc.Id && s.Type == SectorType.Ctr && s.ParentSectorId == null && s.IsActive)
                .OrderBy(s => s.CoverageOrder).ThenBy(s => s.Callsign)
                .Select(s => s.DocumentId).FirstOrDefaultAsync(ct);
            if (docId is null) continue;

            var sezioni = await SezioniDiLavoroAsync(db, docId.Value, ct);
            foreach (var blocco in sezioni.Where(s => s.ParentSectionId == null))
            {
                var figlia = sezioni.FirstOrDefault(s => s.ParentSectionId == blocco.Id && OIC.Equals(s.SectionKey, Chiave));
                if (figlia is null) continue;

                var meta = Leggi<AccBlockMeta>(await JsonDiAsync(db, blocco.Id, ct));
                var aerovia = meta?.Kind == AccBlockKind.Aerovia
                              || (meta is null && OIC.Equals(blocco.SectionKey, "aerovia"));
                var elenco = ConfigurazioniJson.Leggi(await JsonDiAsync(db, figlia.Id, ct));

                if (aerovia)
                {
                    // MIL e FSS non stanno nelle configurazioni dal 21 settembre 2026, ma quelle scritte prima
                    // li portano ancora: si tolgono qui come li toglieva AccDocumentAssembler alla lettura.
                    foreach (var c in elenco)
                        c.Open.RemoveAll(o => AccFamigliaAorRegola.Di(o.Callsign) != FamigliaAor.Ordinaria);
                    Porta(ConfigurationGroupKind.AccArea, codiceAcc, elenco);
                    continue;
                }

                var chiaveBlocco = string.IsNullOrWhiteSpace(meta?.Key) ? blocco.SectionKey : meta!.Key;
                var ente = enti.FirstOrDefault(u => u.AccId == acc.Id && OIC.Equals(u.GroupKey, chiaveBlocco));
                if (ente is not null) Porta(ConfigurationGroupKind.AtcUnit, ente.Code, elenco);
                // Un gruppo senza ente (nessun membro) non ha un gruppo in Struttura: non c'è dove portarlo.
            }
        }

        // ---- 2. Gli enti con la vIPI APP propria. ----
        foreach (var ente in enti.Where(u => u.DocumentId is not null))
        {
            if (gia.Contains((ConfigurationGroupKind.AtcUnit, ente.Code.ToUpperInvariant()))) continue;
            var sezioni = await SezioniDiLavoroAsync(db, ente.DocumentId!.Value, ct);
            var sezione = sezioni.Where(s => OIC.Equals(s.SectionKey, Chiave)).OrderBy(s => s.Depth).ThenBy(s => s.Order).FirstOrDefault();
            if (sezione is null) continue;
            Porta(ConfigurationGroupKind.AtcUnit, ente.Code, ConfigurazioniJson.Leggi(await JsonDiAsync(db, sezione.Id, ct)));
        }

        if (nuovi.Count == 0) return 0;
        db.SectorConfigurationSets.AddRange(nuovi);
        await db.SaveChangesAsync(ct);
        return nuovi.Count;
    }

    private sealed record Sezione(int Id, int? ParentSectionId, string SectionKey, int Depth, int Order);

    /// <summary>
    /// Le sezioni della <b>versione di lavoro</b>: la bozza più recente, altrimenti la pubblicata corrente,
    /// altrimenti l'ultima — la stessa scelta di <c>EfEditingRepository.ResolveWorkingVersionIdAsync</c>. La
    /// bozza vince perché è quella che l'editor mostrava: è lì che stanno le configurazioni di oggi.
    /// </summary>
    private static async Task<List<Sezione>> SezioniDiLavoroAsync(VipiDbContext db, int documentId, CancellationToken ct)
    {
        var versione = await db.DocumentVersions.AsNoTracking()
                           .Where(v => v.DocumentId == documentId && v.Status == DocumentStatus.Draft)
                           .OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.Id).FirstOrDefaultAsync(ct)
                       ?? await db.Documents.AsNoTracking().Where(d => d.Id == documentId)
                           .Select(d => d.CurrentVersionId).FirstOrDefaultAsync(ct)
                       ?? await db.DocumentVersions.AsNoTracking().Where(v => v.DocumentId == documentId)
                           .OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.Id).FirstOrDefaultAsync(ct);
        if (versione is null) return new List<Sezione>();

        return (await db.DocumentSections.AsNoTracking()
                .Where(s => s.DocumentVersionId == versione)
                .Select(s => new { s.Id, s.ParentSectionId, s.SectionKey, s.Depth, s.Order })
                .ToListAsync(ct))
            .Select(s => new Sezione(s.Id, s.ParentSectionId, s.SectionKey, s.Depth, s.Order))
            .ToList();
    }

    /// <summary>Il JSON di struttura di una sezione: la stessa domanda di <see cref="SectionPayload.Scegli"/>.</summary>
    private static async Task<string?> JsonDiAsync(VipiDbContext db, int sectionId, CancellationToken ct) =>
        SectionPayload.Scegli(await db.ContentBlocks.AsNoTracking()
            .Where(b => b.SectionId == sectionId).OrderBy(b => b.Order).ThenBy(b => b.Id)
            .Select(b => b.BodyJson).ToListAsync(ct));

    private static T? Leggi<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch (JsonException) { return null; }
    }
}
