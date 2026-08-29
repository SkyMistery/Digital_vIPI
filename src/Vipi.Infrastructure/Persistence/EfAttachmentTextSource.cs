using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// EF: i testi in cui può comparire il riferimento a un allegato, con la provenienza.
///
/// <para>Sono gli <b>stessi quattro posti</b> che <see cref="EfMediaMaintenance"/> guarda per le immagini, e
/// non è una coincidenza: è la stessa domanda — «questa cosa la cita ancora qualcuno?» — posta su un altro
/// riferimento. Saltarne uno qui significa dire «non la cita nessuno» di una voce che invece è citata, e
/// autorizzare una cancellazione che rompe un documento in silenzio.</para>
///
/// <para>⚠️ <b>Qui si porta anche il documento, là no.</b> Alla pulizia delle immagini basta sapere <i>se</i>
/// uno sha è citato; a chi sta per cancellare un allegato serve sapere <b>quali</b> documenti cambiano —
/// è l'unica informazione con cui si decide, e senza la schermata di conferma sarebbe un «sei sicuro?».</para>
/// </summary>
public sealed class EfAttachmentTextSource : IAttachmentTextSource
{
    private readonly VipiDbContext _db;

    public EfAttachmentTextSource(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<AttachmentText>> ReadAllAsync(CancellationToken ct = default)
    {
        var testi = new List<AttachmentText>();

        // 1) blocchi di TUTTE le versioni — bozze comprese: è il documento che qualcuno sta scrivendo adesso.
        //    ⚠️ Nessun filtro sul formato, al contrario delle immagini: un riferimento sta nel BodyJson di un
        //    blocco Attachment ma anche nel Body in prosa di un paragrafo qualunque, che è la seconda forma di
        //    citazione. Filtrare per formato perderebbe metà dei modi in cui si cita.
        var blocchi = await _db.ContentBlocks.AsNoTracking()
            .Where(b => b.Body != null || b.BodyJson != null)
            .Join(_db.DocumentVersions.AsNoTracking(), b => b.DocumentVersionId, v => v.Id,
                (b, v) => new { b.Body, b.BodyJson, v.DocumentId })
            .ToListAsync(ct);

        foreach (var b in blocchi)
        {
            testi.Add(new AttachmentText(b.Body, AttachmentCitationSource.Document, b.DocumentId));
            testi.Add(new AttachmentText(b.BodyJson, AttachmentCitationSource.Document, b.DocumentId));
        }

        // 2) sezioni extra d'aeroporto: i blocchi stanno serializzati dentro un campo solo.
        //    ⚠️ LEGACY: nessuno vi scrive più, ma finché il trasloco one-shot non ha girato ovunque una
        //    citazione può stare solo lì. Questa lettura se ne va con la tabella.
        var extra = await _db.AirportExtraSections.AsNoTracking()
            .Where(s => s.Body != null)
            .Join(_db.Airports.AsNoTracking(), s => s.AirportId, a => a.Id,
                (s, a) => new { s.Body, a.Icao, s.Title })
            .ToListAsync(ct);

        foreach (var s in extra)
            testi.Add(new AttachmentText(s.Body, AttachmentCitationSource.AirportExtraSection,
                null, $"{s.Icao} · {s.Title}"));

        // 3) payload delle release: le fotografie congelate dei documenti, cioè quel che il pubblico legge
        //    ADESSO. È la citazione che l'occhio non trova, perché non compare in nessuna bozza.
        //    ⚠️ Una release NON porta un DocumentId: si identifica con la coppia (tipo, chiave). Cercarne uno
        //    qui è l'errore che lascia senza nome e senza link proprio le citazioni pubblicate.
        var release = await _db.DocReleases.AsNoTracking()
            .Select(r => new { r.PayloadJson, r.TargetType, r.TargetKey })
            .ToListAsync(ct);

        foreach (var r in release)
            testi.Add(new AttachmentText(r.PayloadJson, AttachmentCitationSource.Release,
                null, null, r.TargetType, r.TargetKey));

        // 4) blocchi condivisi: oggi nessuno li crea, ma il modello li prevede e hanno Body e BodyJson come
        //    gli altri. È il «quarto posto» che rende pericolosa una guardia scritta a occhio: costa una query
        //    guardarlo adesso, costerebbe un documento rotto scoprirlo dopo.
        var condivisi = await _db.SharedBlocks.AsNoTracking()
            .Where(s => s.Body != null || s.BodyJson != null)
            .Select(s => new { s.Body, s.BodyJson, s.Key })
            .ToListAsync(ct);

        foreach (var s in condivisi)
        {
            testi.Add(new AttachmentText(s.Body, AttachmentCitationSource.SharedBlock, null, s.Key));
            testi.Add(new AttachmentText(s.BodyJson, AttachmentCitationSource.SharedBlock, null, s.Key));
        }

        // NON si guarda il registro di audit: racconta che cosa è successo, non che cosa si mostra. Una riga
        // che nomina uno slug cancellato è una traccia storica, non un documento rotto.
        return testi;
    }
}
