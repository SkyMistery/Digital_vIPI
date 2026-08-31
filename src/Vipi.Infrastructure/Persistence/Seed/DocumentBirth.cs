using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;

namespace Vipi.Infrastructure.Persistence.Seed;

/// <summary>
/// Come nasce un documento vIPI: lo scheletro (documento + prima versione bozza) e le sezioni del suo profilo
/// di catalogo. Doc 14 §3g.
///
/// <para>
/// ⚠️ <b>Perché è qui e non dentro uno dei due repository.</b> La nascita era scritta due volte —
/// <c>EfEditingRepository.EnsureVipiDocumentAsync</c> per ACC, APP e vLOA, e <c>EfAirportRepository</c> per
/// l'aeroporto — e le due copie <b>non facevano la stessa cosa</b>. Nessun test le confrontava.
/// </para>
///
/// <para>
/// ⚠️ <b>Quel che NON si è unificato, e perché.</b> Fra le due nascite restano due differenze, e sono scelte
/// vere: le <b>SID nascono Live</b> sull'aeroporto (scelta editoriale storica — una SID si mostra sempre
/// aggiornata — non una proprietà del catalogo), e l'aeroporto <b>non mette blocchi segnaposto</b>: non li ha
/// mai avuti, e la pagina disegna le sue sezioni per chiave. Le dichiara il chiamante.
/// </para>
///
/// <para>
/// ✅ <b>La terza differenza è stata chiusa</b> (doc 14 §3i). <c>CurrentVersionId</c> vuol dire «la versione
/// <b>pubblicata</b> corrente»: lo scrive <c>PublishAsync</c>, l'eliminazione lo azzera. Due porte su quattro
/// — l'aeroporto e la vLOA da «ACC confinanti» — lo puntavano invece alla bozza appena creata, e un documento
/// mai pubblicato dichiarava di avere una versione pubblicata che non esisteva. Ora <b>nessuna</b> porta lo
/// imposta alla nascita, e <c>NascitaDocumentoParitaTests</c> lo chiede a tutte e quattro.
/// <br/>
/// ⚠️ L'unico lettore che gli dava l'altro significato — «la versione su cui lavorare», in
/// <c>CurrentSidsSectionAsync</c> — era <b>codice morto</b>: serviva a un congelamento SID dedicato che dal
/// 26 agosto 2026 fa il toggle dell'editor condiviso, e aveva come soli chiamanti quattro righe di test.
/// Tolto quello, il campo ha un significato solo.
/// </para>
///
/// </summary>
public static class DocumentBirth
{
    /// <summary>
    /// Crea il documento e la sua prima versione (bozza), e semina le sezioni del profilo. NON salva e NON
    /// aggancia niente: il legame — al settore o all'aeroporto — lo fa il chiamante, perché è la sola cosa
    /// che cambia davvero fra le quattro famiglie.
    /// </summary>
    /// <param name="nasceLive">Quali sezioni nascono in modalità Live. Il default è «quelle che non si
    /// possono congelare» (<see cref="SectionCatalog.IsAlwaysLive"/>); l'aeroporto ci aggiunge le SID.</param>
    /// <param name="conSegnaposto">Se le sezioni rese dalla pagina ricevono un blocco vuoto che le tiene
    /// visibili anche senza contenuto.</param>
    public static (Document Doc, DocumentVersion Version) Crea(
        VipiDbContext db, IAiracService airac, string title, Language language, SectionProfile profile,
        int authorUserId, Func<string, bool>? nasceLive = null, bool conSegnaposto = true)
    {
        var now = DateTime.UtcNow;
        var cycle = airac.GetCycle(now);

        var doc = new Document
        {
            Type = DocumentType.Vipi,
            Title = title,
            Language = language,
            Status = DocumentStatus.Draft,
            LastUpdatedUtc = now,
            LastUpdatedAiracCycle = cycle,
        };

        var version = new DocumentVersion
        {
            Document = doc,
            VersionNumber = 1,
            Status = DocumentStatus.Draft,
            CreatedByUserId = authorUserId,
            CreatedUtc = now,
            AiracCycle = cycle,
            Note = "Bozza iniziale",
        };
        doc.Versions.Add(version);
        db.Documents.Add(doc);

        var live = nasceLive ?? SectionCatalog.IsAlwaysLive;
        var order = 1;
        // ⚠️ I titoli nascono nella LINGUA DEL DOCUMENTO, non nella lingua del catalogo: un documento
        // creato in inglese con le sezioni intitolate in italiano nascerebbe già mezzo tradotto, e sulle
        // famiglie dove il titolo di catalogo non viene ri-applicato a view-time resterebbe così per sempre.
        var lingua = language == Language.En ? "en" : "it";
        foreach (var d in SectionCatalog.For(profile).OrderBy(d => d.Order))
            Semina(db, version, profile, d, genitore: null, profondita: 0, ordine: order++, live, conSegnaposto, lingua);

        // ⚠️ `CurrentVersionId` NON si imposta qui, e non è una dimenticanza: Document e DocumentVersion si
        // puntano a vicenda, e assegnarlo prima del salvataggio fa vedere a EF una dipendenza CIRCOLARE fra
        // le due chiavi esterne — «circular dependency was detected in the data to be saved». Chi lo vuole lo
        // scrive DOPO il primo SaveChanges, quando gli Id esistono.
        return (doc, version);
    }
    /// <summary>
    /// Semina una sezione del catalogo e, ricorsivamente, le sue sotto-sezioni fisse.
    ///
    /// <para>
    /// ⚠️ <b>La ricorsione è nuova dal 28 agosto 2026</b>, e serviva: fino ad allora nascevano solo le
    /// sezioni di primo livello, perché nessun profilo ne aveva di annidate. I SOP militari hanno quattro
    /// contenitori con figli, e senza questo il documento nascerebbe <b>piatto</b> — venti sezioni di primo
    /// livello invece di sei con dentro le loro.
    /// </para>
    /// <para>
    /// L'<b>ordine dei figli riparte da uno</b> dentro ogni padre: <c>DocumentSection.Order</c> è l'ordine
    /// fra FRATELLI, non una posizione assoluta nel documento.
    /// </para>
    /// </summary>
    private static void Semina(
        VipiDbContext db, DocumentVersion version, SectionProfile profile, SectionDescriptor d,
        DocumentSection? genitore, int profondita, int ordine, Func<string, bool> live, bool conSegnaposto,
        string lingua)
    {
        var section = new DocumentSection
        {
            DocumentVersion = version,
            ParentSection = genitore,
            Title = d.TitleIn(lingua),
            Order = ordine,
            Depth = profondita,
            SectionKey = d.Key,
            RowVersion = Guid.NewGuid().ToByteArray(),
            RenderMode = live(d.Key) ? RenderMode.Live : RenderMode.Frozen,
        };
        version.Sections.Add(section);
        db.DocumentSections.Add(section);

        if (conSegnaposto && SectionCatalog.IsHostRendered(profile, d.Key))
            db.ContentBlocks.Add(new ContentBlock
            {
                DocumentVersion = version,
                Section = section,
                Order = 1,
                Format = BlockFormat.Table,
                Tier = BlockTier.Extended,
                Visibility = BlockVisibility.Always,
                RowVersion = Guid.NewGuid().ToByteArray(),
            });

        if (d.Children is not { Count: > 0 } figli) return;

        // ⚠️ Il vincolo di profondità è applicativo (DocumentSection.MaxDepth), non del database: se un
        // profilo annidasse troppo, il documento nascerebbe fuori regola e il difetto si vedrebbe solo a
        // schermo, in una TOC che non rientra. Meglio fermarsi qui e dirlo.
        if (profondita + 1 > DocumentSection.MaxDepth)
            throw new InvalidOperationException(
                $"Il profilo {profile} annida la sezione «{d.Key}» oltre {DocumentSection.MaxDepth} livelli.");

        var ordineFiglio = 1;
        foreach (var f in figli.OrderBy(x => x.Order))
            Semina(db, version, profile, f, section, profondita + 1, ordineFiglio++, live, conSegnaposto, lingua);
    }
}
