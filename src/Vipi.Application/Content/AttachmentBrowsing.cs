using Vipi.Application.Abstractions;
using Vipi.Domain.Entities;

namespace Vipi.Application.Content;

/// <summary>
/// Che cosa si sta guardando nella biblioteca degli allegati: i tre assi in AND, la ricerca libera e il chip
/// «mai usata».
///
/// <para>⚠️ Il terzo asse (<see cref="ScopeKey"/>) è la novità del 4 settembre 2026. Gli altri due c'erano dal
/// primo giorno, ma «gli allegati di Milano» non si poteva chiedere: si poteva chiedere «quelli di un ACC», e
/// gli ACC sono quattro. Con centoventuno voci in archivio quella non è una scorciatoia mancante, è la
/// differenza fra trovare una LoA e scorrere l'elenco.</para>
/// </summary>
public sealed record AttachmentFilter(
    AttachmentKind? Kind = null, AttachmentScope? Scope = null, string? ScopeKey = null,
    string? Search = null, bool OnlyUnused = false);

/// <summary>
/// Una «cartella» dell'elenco: il perimetro e le sue voci, già in ordine.
///
/// <para>⚠️ Non è una cartella in banca dati, ed è voluto: la carta della biblioteca rifiuta l'albero di
/// cartelle perché un albero lo si riempie a mano e ci si archivia male. Questo gruppo invece si <b>ricava</b>
/// dai campi che ci sono già — un allegato non può finire nella cartella sbagliata, perché non c'è nessuna
/// cartella da scegliere.</para>
/// </summary>
public sealed record AttachmentGroup(AttachmentScope Scope, string? ScopeKey, IReadOnlyList<AttachmentRow> Rows)
{
    /// <summary>Identità stabile del gruppo, usata per ricordarsi quali sono chiusi.</summary>
    public string Key => Scope == AttachmentScope.Division ? "div" : $"{Scope}:{ScopeKey}";
}

/// <summary>
/// Filtro, ordine e raggruppamento dell'elenco allegati. <b>Cuore deterministico</b>: niente IO, niente
/// localizzatore, niente componenti — è quel che decide che cosa uno staffista <i>vede</i>, e una voce che
/// sparisce da un filtro sbagliato è una voce che nessuno ritrova e che qualcuno ricarica in doppio.
/// </summary>
public static class AttachmentBrowsing
{
    private const StringComparison OIC = StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// Le voci che passano il filtro, <b>ordinate</b>: perimetro, poi tipo, poi titolo.
    ///
    /// <para>⚠️ L'ordine è parte del filtro e non un vezzo: prima l'elenco usciva nell'ordine del database,
    /// cioè in ordine di caricamento, che a centoventuno voci è nessun ordine.</para>
    /// </summary>
    /// <param name="citata">Vero se qualcuno cita quella voce. Il chip «mai usata» è il suo contrario, e la
    /// risposta la sa solo il chiamante — si ricava leggendo i quattro posti dove un riferimento può stare.</param>
    public static IReadOnlyList<AttachmentRow> Filtra(
        IEnumerable<AttachmentRow> righe, AttachmentFilter f, Func<string, bool> citata)
    {
        var q = righe;
        if (f.Kind is { } k) q = q.Where(r => r.Kind == k);
        if (f.Scope is { } s) q = q.Where(r => r.Scope == s);
        if (f.ScopeKey is { Length: > 0 } key) q = q.Where(r => string.Equals(r.ScopeKey, key, OIC));
        if (f.OnlyUnused) q = q.Where(r => !citata(r.Slug));

        var testo = (f.Search ?? "").Trim();
        if (testo.Length > 0)
            q = q.Where(r => new[] { r.Title, r.Slug, r.ScopeKey, r.Notes }
                .Any(c => c is not null && c.Contains(testo, OIC)));

        return q.OrderBy(r => r.Scope)
                .ThenBy(r => r.ScopeKey ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Kind)
                .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    /// <summary>
    /// Le righe raccolte per perimetro, nell'ordine in cui <see cref="Filtra"/> le ha già messe.
    ///
    /// <para>⚠️ Presuppone l'ordinamento: raggruppa a scorrimento, senza riordinare da capo. Passargli righe
    /// non ordinate produce lo stesso perimetro spezzato in più gruppi — che è un difetto visibile, non
    /// silenzioso, ed è il motivo per cui questo metodo non «aggiusta» l'ordine da sé.</para>
    /// </summary>
    public static IReadOnlyList<AttachmentGroup> Raggruppa(IEnumerable<AttachmentRow> righe) =>
        righe.GroupBy(r => (r.Scope, Chiave: r.ScopeKey ?? ""))
             .Select(g => new AttachmentGroup(g.Key.Scope, g.Key.Chiave.Length == 0 ? null : g.Key.Chiave, g.ToList()))
             .ToList();

    /// <summary>
    /// Le chiavi di perimetro <b>presenti</b> (LIRR, LIMC…), in ordine, con quante voci ciascuna.
    ///
    /// <para>⚠️ Presenti e non «tutte quelle possibili»: i chip dicono dove ci sono davvero allegati, come i
    /// chip pista nell'editor delle SID. Un chip a zero è un clic che non porta da nessuna parte.</para>
    ///
    /// <para>⚠️ Si contano sulle righe filtrate dagli <b>altri</b> assi, mai da sé stesse: contando anche il
    /// filtro chiave l'elenco conterrebbe solo la chiave già scelta e non la si potrebbe più cambiare.</para>
    /// </summary>
    public static IReadOnlyList<(string Chiave, int Quante)> Chiavi(IEnumerable<AttachmentRow> righe) =>
        righe.Where(r => r.ScopeKey is { Length: > 0 })
             .GroupBy(r => r.ScopeKey!, StringComparer.OrdinalIgnoreCase)
             .Select(g => (Chiave: g.Key, Quante: g.Count()))
             .OrderBy(x => x.Chiave, StringComparer.OrdinalIgnoreCase)
             .ToList();
}
