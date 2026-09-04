using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Translation;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Translation;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// A che punto è la traduzione, calcolato (carta <c>docs/feature/2026-09-04-stato-traduzione.md</c>).
///
/// <para>
/// ⚠️ <b>Nessuna tabella di stato, per scelta.</b> Quel che si sa di un documento è la differenza fra i suoi
/// segmenti e le impronte in memoria: si calcola. Una tabella sarebbe il secondo posto dove sapere la stessa
/// cosa, e si disallineerebbe al primo documento eliminato o ripristinato — è la stessa scelta, e lo stesso
/// motivo, di <see cref="EfTranslatableCorpus"/>.
/// </para>
///
/// <para>
/// Il conto regge perché è stato <b>misurato</b>: il 4 settembre 2026 una passata su tutto il <c>vipi.db</c>
/// — 26 documenti, 696 titoli di sezione, 218 blocchi, incrocio con le 313 voci di memoria — è costata
/// <b>45 ms</b>. Qualunque forma «una query per riga» sarebbe più lenta <i>e</i> più complicata.
/// </para>
///
/// <para>
/// ⚠️ <b>Le letture sono in blocco, e non è un vezzo</b>: questo servizio lo chiamano pagine Blazor, e una
/// query per documento sarebbe la corsa sul <c>DbContext</c> del circuito già pagata sei volte.
/// </para>
/// </summary>
public sealed class EfStatoTraduzione : IStatoTraduzione
{
    private readonly VipiDbContext _db;
    private readonly ITranslationMemory _memoria;
    private readonly IGlossaryStore _glossario;
    private readonly IDocumentAdminRepository _documenti;

    public EfStatoTraduzione(VipiDbContext db, ITranslationMemory memoria, IGlossaryStore glossario,
        IDocumentAdminRepository documenti)
    {
        _db = db;
        _memoria = memoria;
        _glossario = glossario;
        _documenti = documenti;
    }

    public async Task<QuadroStatoTraduzione> QuadroAsync(CancellationToken ct = default)
    {
        var righe = await RigheAsync(null, ct).ConfigureAwait(false);
        var fuori = await FuoriDaiDocumentiAsync(ct).ConfigureAwait(false);
        return new QuadroStatoTraduzione(righe, fuori);
    }

    public async Task<RigaStatoTraduzione?> DocumentoAsync(int documentId, CancellationToken ct = default)
    {
        var righe = await RigheAsync(documentId, ct).ConfigureAwait(false);
        return righe.Count == 0 ? null : righe[0];
    }

    // ---- Il cuore: una passata, che serve tutti e due gli ingressi -------------------------------------

    private async Task<IReadOnlyList<RigaStatoTraduzione>> RigheAsync(int? soloQuesto, CancellationToken ct)
    {
        var documenti = await _db.Documents.AsNoTracking()
            .Where(d => soloQuesto == null || d.Id == soloQuesto)
            .Select(d => new { d.Id, d.Title, d.Language, d.LanguageLocked, d.CurrentVersionId })
            .ToListAsync(ct).ConfigureAwait(false);
        if (documenti.Count == 0) return Array.Empty<RigaStatoTraduzione>();

        var ids = documenti.Select(d => d.Id).ToList();

        // ⚠️ La versione di LAVORO, con la stessa regola di `EfEditingRepository.LoadForEditAsync`: bozza più
        // recente, sennò la pubblicata corrente, sennò l'ultima che c'è. Due regole diverse per «quale
        // versione sto guardando» vorrebbero dire che il pannello dell'editor e questa tabella contano frasi
        // di due documenti diversi, e nessuno dei due numeri sarebbe sbagliato abbastanza da farsi notare.
        var versioni = await _db.DocumentVersions.AsNoTracking()
            .Where(v => ids.Contains(v.DocumentId))
            .Select(v => new { v.Id, v.DocumentId, v.Status, v.VersionNumber })
            .ToListAsync(ct).ConfigureAwait(false);

        var lavoroPerDoc = new Dictionary<int, int>();
        foreach (var d in documenti)
        {
            var sue = versioni.Where(v => v.DocumentId == d.Id).ToList();
            if (sue.Count == 0) continue;

            var scelta = sue.Where(v => v.Status == DocumentStatus.Draft)
                            .OrderByDescending(v => v.VersionNumber).FirstOrDefault()
                ?? sue.FirstOrDefault(v => v.Id == d.CurrentVersionId)
                ?? sue.OrderByDescending(v => v.VersionNumber).First();

            lavoroPerDoc[d.Id] = scelta.Id;
        }

        var versioniDiLavoro = lavoroPerDoc.Values.ToList();

        var titoli = await _db.DocumentSections.AsNoTracking()
            .Where(s => versioniDiLavoro.Contains(s.DocumentVersionId))
            .Select(s => new { s.DocumentVersionId, s.Title })
            .ToListAsync(ct).ConfigureAwait(false);

        var blocchi = await _db.ContentBlocks.AsNoTracking()
            .Where(b => versioniDiLavoro.Contains(b.DocumentVersionId))
            .Select(b => new { b.DocumentVersionId, b.Body, b.BodyJson })
            .ToListAsync(ct).ConfigureAwait(false);

        var titoliPerVersione = titoli.GroupBy(t => t.DocumentVersionId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Title).ToList());
        var blocchiPerVersione = blocchi.GroupBy(b => b.DocumentVersionId)
            .ToDictionary(g => g.Key, g => g.Select(x => (x.Body, x.BodyJson)).ToList());

        // Gli snapshot in vigore, e a quale documento appartengono. ⚠️ Il payload NON porta l'id del
        // documento: la corrispondenza (famiglia, chiave) → documento la sa il registro dei descrittori, che
        // è anche l'unico posto in cui è scritta. Ricavarla qui sarebbe un secondo racconto.
        var efficaci = await SnapshotEfficaciAsync(ids, ct).ConfigureAwait(false);

        // ---- I segmenti di tutti, e UNA lettura di memoria per verso ----------------------------------
        var perDocumento = new List<(int Id, string Titolo, string Da, string A, bool Bloccata,
            HashSet<string> Bozza, RawDocument? Snapshot)>();

        foreach (var d in documenti)
        {
            var da = DocumentTranslator.CodiceSorgente(d.Language, Language.It);
            // Le lingue sono due e le due versioni dicono la stessa cosa: la direzione di un documento è
            // sempre «l'opposta della sua», e non è una scelta di chi guarda la tabella.
            var a = da == "it" ? "en" : "it";

            var segmenti = new HashSet<string>(StringComparer.Ordinal);
            if (lavoroPerDoc.TryGetValue(d.Id, out var versione))
            {
                if (titoliPerVersione.TryGetValue(versione, out var suoiTitoli))
                    foreach (var t in suoiTitoli)
                        foreach (var s in DocumentTranslator.Aggiungi(t))
                            segmenti.Add(s);

                if (blocchiPerVersione.TryGetValue(versione, out var suoiBlocchi))
                    foreach (var (body, json) in suoiBlocchi)
                    {
                        foreach (var p in TextSegmenter.SplitProse(body))
                            if (TranslationText.HasSomethingToTranslate(p)) segmenti.Add(p);
                        foreach (var c in TextSegmenter.SplitJson(json))
                        {
                            var norm = TranslationText.Normalize(c);
                            if (TranslationText.HasSomethingToTranslate(norm)) segmenti.Add(norm);
                        }
                    }
            }

            efficaci.TryGetValue(d.Id, out var snapshot);
            perDocumento.Add((d.Id, d.Title, da, a, d.LanguageLocked, segmenti, snapshot));
        }

        var note = await NotePerVersoAsync(perDocumento, ct).ConfigureAwait(false);

        // ---- Il protettore: solo se serve davvero -----------------------------------------------------
        // ⚠️ Si costruisce SOLO se qualcosa manca. Il caso normale è «non manca niente» (misurato: 281
        // segmenti, 0 mancanti), e costruirlo comunque vorrebbe dire due query — nomi e glossario — su ogni
        // apertura di una pagina che quasi sempre non ne ha bisogno.
        var serveIlProtettore = perDocumento.Any(d =>
            !d.Bloccata && d.Bozza.Any(s => !note[(d.Da, d.A)].ContainsKey(TranslationText.Hash(s))));

        var protettori = serveIlProtettore
            ? await ProtettoriAsync(perDocumento.Select(d => (d.Da, d.A)).Distinct().ToList(), ct).ConfigureAwait(false)
            : new Dictionary<(string, string), TextProtector>();

        var righe = new List<RigaStatoTraduzione>(perDocumento.Count);
        foreach (var d in perDocumento)
        {
            var memoriaViva = note[(d.Da, d.A)];

            var bozza = Copri(d.Bozza, h => memoriaViva.TryGetValue(h, out var t) ? t : null);

            // ⚠️ Quanti mancanti vogliono una PERSONA: il protettore li rifiuta perché portano un dato
            // personale, e nessun giro li prenderà mai. Contarli insieme agli altri farebbe un contatore che
            // non può arrivare a zero — cioè un allarme che si impara a saltare.
            var aMano = 0;
            if (!d.Bloccata && protettori.TryGetValue((d.Da, d.A), out var protettore))
                foreach (var s in d.Bozza)
                    if (!memoriaViva.ContainsKey(TranslationText.Hash(s)) && !protettore.Protect(s).Safe)
                        aMano++;

            // ---- L'altra copertura: quel che vede CHI LEGGE, adesso ----------------------------------
            var pubblicato = TranslationCoverage.Nessuna;
            var congela = false;
            if (d.Snapshot is { } snap)
            {
                var congelate = DocumentTranslator.Congelate(snap.Translations, d.A);
                congela = congelate is { Count: > 0 };

                var segmentiPubblicati = new HashSet<string>(
                    DocumentTranslator.SegmentiGrezzi(snap), StringComparer.Ordinal);

                // ⚠️ La stessa preferenza del lettore vero (`DocumentTranslator.NoteAsync`): dove lo snapshot
                // ha una voce vince la voce, dove non ha niente si legge la memoria viva. «Dove», non «se» —
                // e senza questa riga la tabella direbbe «40%» di un documento che a schermo è intero.
                pubblicato = Copri(segmentiPubblicati, h =>
                {
                    if (congelate is not null && congelate.TryGetValue(h, out var c) && c.HasText)
                        return new KnownTranslation(c.Text,
                            c.Reviewed ? TranslationOrigin.Human : TranslationOrigin.Machine, c.Reviewed);
                    return memoriaViva.TryGetValue(h, out var t) ? t : null;
                });
            }

            righe.Add(new RigaStatoTraduzione(
                d.Id, d.Titolo, d.Da, d.A, d.Bloccata,
                d.Bloccata ? TranslationCoverage.Nessuna : bozza,
                d.Bloccata ? 0 : aMano,
                d.Bloccata ? TranslationCoverage.Nessuna : pubblicato,
                HaReleaseEfficace: d.Snapshot is not null,
                ReleaseCongela: congela));
        }

        return righe;
    }

    /// <summary>Quanti di questi segmenti hanno una resa, e quanti l'ha guardata una persona.</summary>
    private static TranslationCoverage Copri(HashSet<string> segmenti, Func<string, KnownTranslation?> cerca)
    {
        if (segmenti.Count == 0) return TranslationCoverage.Nessuna;

        int tradotti = 0, riletti = 0;
        var viste = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in segmenti)
        {
            // ⚠️ Si conta per IMPRONTA e non per testo: due segmenti che si normalizzano allo stesso modo
            // sono una frase sola per la memoria, e contarli due volte darebbe una copertura che non
            // corrisponde a nessuna lettura di database.
            var h = TranslationText.Hash(s);
            if (!viste.Add(h)) continue;

            var t = cerca(h);
            if (t is null) continue;
            tradotti++;
            if (t.Reviewed) riletti++;
        }

        return new TranslationCoverage(viste.Count, tradotti, riletti);
    }

    /// <summary>
    /// Una lettura di memoria <b>per verso</b>, per tutte le impronte di tutti i documenti di quel verso —
    /// bozza e snapshot insieme.
    /// </summary>
    private async Task<Dictionary<(string Da, string A), IReadOnlyDictionary<string, KnownTranslation>>>
        NotePerVersoAsync(
            IReadOnlyList<(int Id, string Titolo, string Da, string A, bool Bloccata, HashSet<string> Bozza,
                RawDocument? Snapshot)> documenti,
            CancellationToken ct)
    {
        var note = new Dictionary<(string, string), IReadOnlyDictionary<string, KnownTranslation>>();

        foreach (var gruppo in documenti.GroupBy(d => (d.Da, d.A)))
        {
            var impronte = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in gruppo)
            {
                foreach (var s in d.Bozza) impronte.Add(TranslationText.Hash(s));
                if (d.Snapshot is { } snap)
                    foreach (var s in DocumentTranslator.SegmentiGrezzi(snap))
                        impronte.Add(TranslationText.Hash(s));
            }

            note[gruppo.Key] = impronte.Count == 0
                ? new Dictionary<string, KnownTranslation>(StringComparer.Ordinal)
                : await _memoria.LookupAsync(gruppo.Key.Da, gruppo.Key.A, impronte.ToList(), ct)
                    .ConfigureAwait(false);
        }

        return note;
    }

    private async Task<Dictionary<(string, string), TextProtector>> ProtettoriAsync(
        IReadOnlyList<(string Da, string A)> versi, CancellationToken ct)
    {
        var nomi = await ArnesiDelGiro.NomiDelloStaffAsync(_db, ct).ConfigureAwait(false);
        var protettori = new Dictionary<(string, string), TextProtector>();
        foreach (var (da, a) in versi)
            protettori[(da, a)] = await ArnesiDelGiro
                .ProtettoreAsync(_glossario, nomi, da, a, ct).ConfigureAwait(false);
        return protettori;
    }

    /// <summary>
    /// Lo snapshot della release <b>in vigore adesso</b> di ogni documento, per Id di documento.
    ///
    /// <para>⚠️ La corrispondenza (famiglia, chiave di release) → documento la tiene il registro dei
    /// descrittori: qui si <b>chiede</b>, non si ricostruisce.</para>
    /// </summary>
    private async Task<Dictionary<int, RawDocument>> SnapshotEfficaciAsync(
        IReadOnlyList<int> documentIds, CancellationToken ct)
    {
        var gestiti = (await _documenti.ListAsync(ct).ConfigureAwait(false))
            .Where(m => m.DocumentId is int id && documentIds.Contains(id))
            .ToList();
        if (gestiti.Count == 0) return new Dictionary<int, RawDocument>();

        var chiavi = gestiti.Select(m => m.ReleaseKey).Distinct().ToList();

        var release = await _db.DocReleases.AsNoTracking()
            .Where(r => r.Status == ReleaseStatus.Effective && chiavi.Contains(r.TargetKey))
            .Select(r => new { r.TargetType, r.TargetKey, r.PayloadJson })
            .ToListAsync(ct).ConfigureAwait(false);

        var perChiave = release.ToDictionary(r => (r.TargetType, r.TargetKey), r => r.PayloadJson);

        var snapshot = new Dictionary<int, RawDocument>();
        foreach (var m in gestiti)
        {
            if (m.DocumentId is not int id) continue;
            if (!perChiave.TryGetValue((m.ReleaseTarget, m.ReleaseKey), out var json)) continue;

            // ⚠️ Uno snapshot illeggibile non è un guasto di questa tabella: è una release vecchia o
            // troncata, e la risposta giusta è «di questa non so dire», non un'eccezione su una pagina.
            RawDocument? doc;
            try { doc = JsonSerializer.Deserialize<DocReleasePayload>(json)?.Doc; }
            catch (JsonException) { continue; }

            if (doc is not null) snapshot[id] = doc;
        }

        return snapshot;
    }

    /// <summary>
    /// I testi che <b>non stanno in un documento</b>: descrizioni e attivazioni delle aree regolamentate (la
    /// loro lingua è quella di IVAO, inglese) e le intro di pagina.
    ///
    /// <para>⚠️ Non si attribuiscono a nessun documento e non si sommano alle righe: comparirebbero N volte,
    /// una per ogni documento che li mostra. Ma fuori dal conto sparirebbero del tutto — e sono la metà di
    /// una schermata (misurati: 230 aree per 35 056 caratteri, appena 9 descrizioni e 6 attivazioni
    /// distinte).</para>
    /// </summary>
    private async Task<TranslationCoverage> FuoriDaiDocumentiAsync(CancellationToken ct)
    {
        var segmenti = new HashSet<string>(StringComparer.Ordinal);

        var aree = await _db.SpecialAreas.AsNoTracking()
            .Where(a => a.Description != null || a.ActivationDetails != null)
            .Select(a => new { a.Description, a.ActivationDetails })
            .Distinct()
            .ToListAsync(ct).ConfigureAwait(false);

        foreach (var a in aree)
        {
            var d = TranslationText.Normalize(a.Description);
            if (TranslationText.HasSomethingToTranslate(d)) segmenti.Add(d);
            var att = TranslationText.Normalize(a.ActivationDetails);
            if (TranslationText.HasSomethingToTranslate(att)) segmenti.Add(att);
        }

        // Le intro di pagina, invece, nascono in italiano: stessa riga, verso opposto. ⚠️ La lingua non la
        // indovina questo file — la dichiara `PageIntro.Sorgente`, che è l'unico posto in cui è scritta.
        var intro = new HashSet<string>(StringComparer.Ordinal);
        var json = await _db.SharedBlocks.AsNoTracking()
            .Where(b => b.Key.StartsWith(PageIntro.Prefisso) && b.BodyJson != null)
            .Select(b => b.BodyJson)
            .ToListAsync(ct).ConfigureAwait(false);

        foreach (var j in json)
            foreach (var sezione in PageIntro.ToView(PageIntro.Parse(j)).Sections)
                foreach (var s in DocumentTranslator.SegmentiSezione(sezione))
                    intro.Add(s);

        var introDa = PageIntro.Sorgente == Language.En ? "en" : "it";
        var introA = introDa == "it" ? "en" : "it";

        // Le aree parlano inglese perché le scrive IVAO: il verso è en→it, come nel corpus.
        var copertureAree = await CopriConMemoriaAsync(segmenti, "en", "it", ct).ConfigureAwait(false);
        var copertureIntro = await CopriConMemoriaAsync(intro, introDa, introA, ct).ConfigureAwait(false);

        return new TranslationCoverage(
            copertureAree.Segmenti + copertureIntro.Segmenti,
            copertureAree.Tradotti + copertureIntro.Tradotti,
            copertureAree.Riletti + copertureIntro.Riletti);
    }

    /// <summary>La copertura di un pugno di segmenti, con una lettura di memoria sola.</summary>
    private async Task<TranslationCoverage> CopriConMemoriaAsync(
        HashSet<string> segmenti, string da, string a, CancellationToken ct)
    {
        if (segmenti.Count == 0) return TranslationCoverage.Nessuna;

        var note = await _memoria
            .LookupAsync(da, a, segmenti.Select(TranslationText.Hash).Distinct(StringComparer.Ordinal).ToList(), ct)
            .ConfigureAwait(false);

        return Copri(segmenti, h => note.TryGetValue(h, out var t) ? t : null);
    }
}
