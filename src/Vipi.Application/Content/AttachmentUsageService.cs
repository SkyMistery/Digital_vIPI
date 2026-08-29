using Vipi.Application.Abstractions;
using Vipi.Application.Media;
using Vipi.Application.Routing;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Chi cita cosa nella biblioteca: legge i testi dove un riferimento può comparire, li scandaglia col
/// <see cref="AttachmentReferenceScanner"/> e attribuisce ogni citazione al documento che la contiene.
///
/// <para>Le regole stanno su <see cref="IAttachmentUsage"/>; qui c'è come si applicano.</para>
///
/// <para>⚠️ <b>Niente prosa qui dentro.</b> La citazione porta i <i>fatti</i> — pubblicato o no, quale ciclo
/// AIRAC — e la frase la compone la pagina, che sa in che lingua sta parlando. Una stringa italiana cablata
/// qui uscirebbe italiana anche nella versione inglese, ed è già successo.</para>
/// </summary>
public sealed class AttachmentUsageService : IAttachmentUsage
{
    private readonly IAttachmentTextSource _testi;
    private readonly IDocumentAdminRepository _documenti;
    private readonly IDocRoutesRegistry _rotte;

    public AttachmentUsageService(IAttachmentTextSource testi, IDocumentAdminRepository documenti,
        IDocRoutesRegistry rotte)
    {
        _testi = testi;
        _documenti = documenti;
        _rotte = rotte;
    }

    public async Task<IReadOnlyDictionary<string, AttachmentUsage>> AllAsync(CancellationToken ct = default)
    {
        var testi = await _testi.ReadAllAsync(ct);

        // Una passata sola sui testi: scandagliare è la parte cara, e rifarla per rispondere alla stessa
        // domanda due volte sarebbe la stessa svista che rende lente le pagine che leggono in un ciclo.
        var trovati = new List<(AttachmentText Testo, List<string> Slug)>();
        foreach (var t in testi)
        {
            var slug = AttachmentReferenceScanner.Scan(t.Text).ToList();
            if (slug.Count > 0) trovati.Add((t, slug));
        }

        // Nessun riferimento in giro: non si disturba nemmeno l'elenco dei documenti. È il caso normale
        // finché la biblioteca è nuova, ed è anche quello in cui la pagina si apre più spesso.
        if (trovati.Count == 0) return new Dictionary<string, AttachmentUsage>(StringComparer.Ordinal);

        var gestiti = await _documenti.ListAsync(ct);

        var perDocumento = new Dictionary<int, ManagedDoc>();
        foreach (var d in gestiti)
            if (d.DocumentId is int id)
                perDocumento[id] = d;

        // ⚠️ Le release NON portano un DocumentId: sono identificate dalla coppia (tipo, chiave), le stesse
        // dei bersagli di pubblicazione. Senza questo secondo indice ogni citazione dentro un documento
        // PUBBLICATO resterebbe senza nome e senza link — cioè proprio quelle che pesano sulla decisione.
        var perBersaglio = new Dictionary<(ReleaseTargetType, string), ManagedDoc>();
        foreach (var d in gestiti)
            perBersaglio[(d.ReleaseTarget, d.ReleaseKey.ToLowerInvariant())] = d;

        var perSlug = new Dictionary<string, List<AttachmentCitation>>(StringComparer.Ordinal);
        foreach (var (testo, slugs) in trovati)
        {
            var citazione = Citazione(testo, Documento(testo, perDocumento, perBersaglio));

            foreach (var slug in slugs)
            {
                if (!perSlug.TryGetValue(slug, out var elenco))
                    perSlug[slug] = elenco = new List<AttachmentCitation>();

                // ⚠️ Lo stesso documento può citare lo stesso allegato in dieci blocchi: a chi deve decidere se
                // cancellare interessa QUALI documenti cambiano, non quante volte. Dieci righe uguali
                // renderebbero illeggibile proprio la schermata che esiste per far decidere.
                if (!elenco.Contains(citazione)) elenco.Add(citazione);
            }
        }

        return perSlug.ToDictionary(
            kv => kv.Key,
            kv => new AttachmentUsage(kv.Key, Ordina(kv.Value)),
            StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<AttachmentCitation>> WhereUsedAsync(string slug, CancellationToken ct = default)
    {
        var chiave = AttachmentRules.Norm(slug).ToLowerInvariant();
        if (chiave.Length == 0) return Array.Empty<AttachmentCitation>();

        var tutte = await AllAsync(ct);
        return tutte.TryGetValue(chiave, out var uso) ? uso.Citations : Array.Empty<AttachmentCitation>();
    }

    /// <summary>
    /// Le citazioni <b>pubblicate</b> prima: sono quelle che il lettore vede adesso, quindi quelle che pesano
    /// sulla decisione. Una bozza si corregge prima di pubblicarla, una release pubblicata no.
    /// </summary>
    private static IReadOnlyList<AttachmentCitation> Ordina(List<AttachmentCitation> citazioni) =>
        citazioni
            .OrderBy(c => c.Source == AttachmentCitationSource.Release || c.IsPublished ? 0 : 1)
            .ThenBy(c => c.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>Il documento che contiene questo testo, per l'una o per l'altra via.</summary>
    private static ManagedDoc? Documento(AttachmentText testo,
        IReadOnlyDictionary<int, ManagedDoc> perDocumento,
        IReadOnlyDictionary<(ReleaseTargetType, string), ManagedDoc> perBersaglio)
    {
        if (testo.DocumentId is int id && perDocumento.TryGetValue(id, out var doc)) return doc;

        if (testo.ReleaseTarget is { } tipo && testo.ReleaseKey is { Length: > 0 } chiave
            && perBersaglio.TryGetValue((tipo, chiave.ToLowerInvariant()), out var perRelease))
            return perRelease;

        return null;
    }

    private AttachmentCitation Citazione(AttachmentText testo, ManagedDoc? doc) =>
        doc is null
            // Senza documento resta l'etichetta del posto (l'ICAO della sezione extra, la chiave del blocco
            // condiviso): una riga senza nome non si può né capire né andare a correggere.
            ? new AttachmentCitation(testo.Source, testo.Label ?? "—")
            : new AttachmentCitation(testo.Source, doc.Title, Url(doc), doc.IsPublished, doc.EffectiveCycle);

    /// <summary>Dove si va a correggere: l'<b>editor</b>, non la pagina pubblica. Chi apre questa lista deve
    /// togliere o cambiare una citazione, non leggerla.</summary>
    private string? Url(ManagedDoc d) =>
        _rotte.For(d.ReleaseTarget).EditorUrl(
            (d.AccCode ?? "").ToLowerInvariant(), d.ReleaseKey, d.NeighbourCode, d.DocumentId);
}
