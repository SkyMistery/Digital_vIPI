using Vipi.Application.Abstractions;
using Vipi.Application.Routing;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Application.Content;

/// <summary>Dove si va a lavorare su un incarico: l'URL dell'editor del documento collegato, e il titolo che
/// quel documento ha <b>adesso</b>. Url null = il documento non c'è più (o non è raggiungibile).</summary>
public sealed record EditorTaskLink(string? Url, string? TitoloCorrente);

/// <summary>Un documento collegabile a un incarico: la coppia (tipo, chiave) con cui lo si ritrova, e
/// l'etichetta da scrivere sull'incarico.</summary>
public sealed record EditorTaskTargetOption(ReleaseTargetType Type, string Key, string Etichetta);

/// <summary>
/// Il read-model delle due pagine degli incarichi: per ogni bersaglio, dove porta il tasto «Apri documento».
///
/// <para><b>Perché un servizio e non una funzione statica.</b> Fino al 22 agosto 2026 il link lo costruiva
/// <c>TaskDocLink</c>, che dalla sola chiave di release sapeva fare l'URL <b>solo</b> per la vIPI ACC —
/// perché è l'unico tipo la cui chiave contiene il codice ACC. Per aeroporti, APP e vLOA rimandava a
/// <c>/services/vsop/versions</c>: un tasto che dice «Apri documento» e porta a un elenco, in tre casi su quattro.
/// L'ACC (e per la vLOA il codice del vicino) sono un <b>dato</b>, non qualcosa che si deduce da una
/// stringa: si leggono, e poi l'URL lo fa <see cref="IDocRoutesRegistry"/>, che è già l'unico posto dove
/// stanno le rotte dei documenti (regola 139: un formattatore per tipo di dato, non uno per pagina).</para>
///
/// <para>⚠️ <b>Una query per pagina, non una per riga</b> (regola 136): l'elenco unificato dei documenti
/// arriva in un colpo e si indicizza per (tipo, chiave). Se nessun incarico a schermo è legato a un
/// documento, non si interroga niente.</para>
///
/// <para>Il titolo corrente serve al <c>title</c> del link, non all'etichetta: l'etichetta resta quella
/// <b>scritta nell'incarico</b>, che è ciò che il documento si chiamava quando l'incarico fu dato.</para>
/// </summary>
public interface IEditorTaskLinksService
{
    Task<IReadOnlyDictionary<(ReleaseTargetType Type, string Key), EditorTaskLink>> ForAsync(
        IEnumerable<EditorTask> tasks, CancellationToken ct = default);

    /// <summary>
    /// I documenti che si possono collegare a un incarico, <b>con la chiave che li ritrova</b>.
    ///
    /// <para>⚠️ Sta qui, e non nella pagina, per un difetto pagato: la pagina si costruiva la chiave da sé
    /// (<c>$"{acc}|"</c> per la vIPI ACC) mentre la chiave vera è <c>{acc}|{callsign del settore primario}</c>.
    /// Un incarico creato così puntava a un documento che <b>non esiste</b>: il collegamento non si risolveva
    /// mai, e la pagina diceva «il documento collegato non esiste più» — che era falso. Chi sceglie e chi
    /// ritrova devono leggere la <b>stessa</b> chiave, dallo stesso elenco.</para>
    /// </summary>
    Task<IReadOnlyList<EditorTaskTargetOption>> OpzioniAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IEditorTaskLinksService"/>
public sealed class EditorTaskLinksService : IEditorTaskLinksService
{
    private readonly IDocumentAdminRepository _docs;
    private readonly IDocRoutesRegistry _routes;

    public EditorTaskLinksService(IDocumentAdminRepository docs, IDocRoutesRegistry routes)
    {
        _docs = docs;
        _routes = routes;
    }

    public async Task<IReadOnlyDictionary<(ReleaseTargetType Type, string Key), EditorTaskLink>> ForAsync(
        IEnumerable<EditorTask> tasks, CancellationToken ct = default)
    {
        var bersagli = tasks
            .Where(t => t.TargetType is not null && !string.IsNullOrWhiteSpace(t.TargetKey))
            .Select(t => (Type: t.TargetType!.Value, Key: t.TargetKey!))
            .ToHashSet();

        var mappa = new Dictionary<(ReleaseTargetType, string), EditorTaskLink>();
        if (bersagli.Count == 0) return mappa;   // niente incarichi legati a documenti: nessuna query

        foreach (var d in await _docs.ListAsync(ct))
        {
            var chiave = (d.ReleaseTarget, d.ReleaseKey);
            if (!bersagli.Contains(chiave) || mappa.ContainsKey(chiave)) continue;
            var acc = (d.AccCode ?? d.Scope).ToLowerInvariant();
            mappa[chiave] = new EditorTaskLink(
                _routes.For(d.ReleaseTarget).EditorUrl(acc, d.ReleaseKey, d.NeighbourCode, d.DocumentId),
                d.Title);
        }
        return mappa;
    }

    public async Task<IReadOnlyList<EditorTaskTargetOption>> OpzioniAsync(CancellationToken ct = default) =>
        (await _docs.ListAsync(ct))
            // ⚠️ Chiave vuota = documento che nessuna ricerca ritrova (nel DB di sviluppo ce n'è uno: un
            // aeroporto il cui settore primario non porta l'ICAO). Offrirlo sarebbe offrire un collegamento
            // che nasce gia' rotto — la tendina e' una comodita', e una comodita' non deve mentire.
            .Where(d => !d.IsHidden && !string.IsNullOrWhiteSpace(d.ReleaseKey))
            .Select(d => new EditorTaskTargetOption(d.ReleaseTarget, d.ReleaseKey, $"{Tipo(d.Kind)} · {d.Title}"))
            .OrderBy(o => o.Etichetta, StringComparer.OrdinalIgnoreCase)
            .ToList();

    // Il nome del tipo per la tendina. Resta grezzo: e' un'etichetta che finisce SCRITTA nell'incarico
    // (TargetLabel), e quella non si ritraduce dopo — un incarico dato in italiano non deve cambiare nome
    // perche' chi lo legge ha l'interfaccia in inglese.
    private static string Tipo(ManagedDocKind kind) => kind switch
    {
        ManagedDocKind.AccVipi => "vIPI ACC",
        ManagedDocKind.AirportVipi => "vIPI aeroporto",
        ManagedDocKind.AppVipi => "vIPI APP",
        ManagedDocKind.Vloa => "vLOA",
        _ => kind.ToString(),
    };
}
