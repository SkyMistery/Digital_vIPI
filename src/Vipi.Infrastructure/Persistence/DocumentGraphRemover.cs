using Microsoft.EntityFrameworkCore;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Toglie di mezzo il <b>corpo</b> di un documento — versioni, sezioni, blocchi — prima che il documento
/// stesso venga cancellato.
///
/// <para><b>Perché non basta la cascata del database.</b> Le due chiavi esterne che tengono insieme il corpo
/// sono <c>RESTRICT</c>: <c>DocumentSections.ParentSectionId</c> verso sé stessa e
/// <c>ContentBlocks.SectionId</c>. Un <c>DELETE</c> sul documento fa scattare la cascata sulle versioni, la
/// cascata prova a togliere le sezioni <b>tutte insieme</b>, e il database non sa che le figlie vanno prima
/// delle madri: risponde <c>FOREIGN KEY constraint failed</c>. È il messaggio che parla di vincoli a chi
/// voleva solo togliere un documento.</para>
///
/// <para>⚠️ <b>Il difetto si nasconde nei test.</b> Se le righe sono state create nello stesso contesto sono
/// ancora <i>tracciate</i>, ed EF ordina le cancellazioni da sé: il test passa. Nell'applicazione il
/// documento si rilegge da zero, i figli non li conosce nessuno, e la cascata tocca al database. È così che
/// la pagina Documenti ha convissuto con questo difetto — ogni vIPI ha dieci sezioni di cui nove figlie, e
/// quindi <b>nessun</b> documento vero si sarebbe cancellato.</para>
///
/// <para>Il rimedio è caricare il corpo: da tracciato, EF ordina le cancellazioni per dipendenza e il
/// database non deve indovinare niente.</para>
/// </summary>
internal static class DocumentGraphRemover
{
    /// <summary>
    /// Carica e marca per la cancellazione versioni, sezioni e blocchi del documento. <b>Non</b> salva: chi
    /// chiama toglie anche il documento e scrive tutto in un solo <c>SaveChanges</c>, cioè in una sola
    /// transazione implicita.
    /// </summary>
    public static async Task StageAsync(VipiDbContext db, int documentId, CancellationToken ct = default)
    {
        var versioni = await db.DocumentVersions
            .Where(v => v.DocumentId == documentId)
            .Select(v => v.Id)
            .ToListAsync(ct);
        if (versioni.Count == 0) return;

        // I blocchi per primi: puntano alle sezioni con un vincolo che non perdona.
        var blocchi = await db.ContentBlocks.Where(b => versioni.Contains(b.DocumentVersionId)).ToListAsync(ct);
        if (blocchi.Count > 0) db.ContentBlocks.RemoveRange(blocchi);

        // Poi le sezioni, dalle più profonde: l'ordine esplicito non serve a EF (che sa risalire il grafo
        // tracciato) ma serve a chi legge — dice qual è la dipendenza vera.
        var sezioni = await db.DocumentSections.Where(s => versioni.Contains(s.DocumentVersionId)).ToListAsync(ct);
        foreach (var s in sezioni.OrderByDescending(x => x.Depth)) db.DocumentSections.Remove(s);

        var righe = await db.DocumentVersions.Where(v => v.DocumentId == documentId).ToListAsync(ct);
        db.DocumentVersions.RemoveRange(righe);
    }
}
