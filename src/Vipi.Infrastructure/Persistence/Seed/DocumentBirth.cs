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
        foreach (var d in SectionCatalog.For(profile).OrderBy(d => d.Order))
        {
            var section = new DocumentSection
            {
                DocumentVersion = version,
                ParentSection = null,
                Title = d.Title,
                Order = order++,
                Depth = 0,
                SectionKey = d.Key,
                RowVersion = Guid.NewGuid().ToByteArray(),
                RenderMode = live(d.Key) ? RenderMode.Live : RenderMode.Frozen,
            };
            version.Sections.Add(section);
            db.DocumentSections.Add(section);

            if (!conSegnaposto || !SectionCatalog.IsHostRendered(profile, d.Key)) continue;
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
        }

        // ⚠️ `CurrentVersionId` NON si imposta qui, e non è una dimenticanza: Document e DocumentVersion si
        // puntano a vicenda, e assegnarlo prima del salvataggio fa vedere a EF una dipendenza CIRCOLARE fra
        // le due chiavi esterne — «circular dependency was detected in the data to be saved». Chi lo vuole lo
        // scrive DOPO il primo SaveChanges, quando gli Id esistono.
        return (doc, version);
    }
}
