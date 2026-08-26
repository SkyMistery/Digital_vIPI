using System.Text.Json;
using System.Text.Json.Nodes;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Una <b>rinomina</b>: la sorgente manda la stessa riga — stesso <see cref="IvaoId"/> — con un nominativo
/// diverso. Non è un'ipotesi da confermare, è un fatto: l'identità è la stessa.
/// </summary>
/// <param name="Catalog">Da quale catalogo, perché l'id da solo è ambiguo fra i due.</param>
/// <param name="IvaoId">L'identità che attraversa la rinomina.</param>
/// <param name="OldCallsign">Il nominativo che avevamo in archivio.</param>
/// <param name="NewCallsign">Quello che la sorgente manda adesso.</param>
public sealed record CallsignRename(SourceCatalog Catalog, int IvaoId, string OldCallsign, string NewCallsign);

/// <summary>Una rinomina che <b>non</b> è stata applicata, e perché. Vedi <see cref="RenameOutcome"/>.</summary>
public sealed record RenameRefused(CallsignRename Rename, string Reason);

/// <summary>Cos'è successo: quel che si è applicato e quel che si è rifiutato di applicare.</summary>
public sealed record RenameOutcome(
    IReadOnlyList<CallsignRename> Applied,
    IReadOnlyList<RenameRefused> Refused)
{
    public static readonly RenameOutcome Nothing =
        new(Array.Empty<CallsignRename>(), Array.Empty<RenameRefused>());

    public bool Any => Applied.Count > 0 || Refused.Count > 0;
}

/// <summary>
/// Applica le rinomine: un motore solo, come <c>IDeletionService</c>, perché il callsign vive in troppi
/// posti perché ognuno se lo aggiorni per conto suo.
///
/// <para>Riscrive, in una transazione: la riga di catalogo, <c>Sector.Callsign</c> (<b>tenendo l'Id</b>, che
/// è tutto il punto), i <c>ParentCallsign</c> che puntavano al vecchio, la chiave di release dei bersagli
/// coinvolti; e scrive l'alias.</para>
/// </summary>
public interface ICallsignRenameService
{
    /// <summary>Applica quel che può e riferisce il resto. Non lancia per una rinomina rifiutata: un import
    /// non deve fermarsi perché una riga su duecento ha una storia strana.</summary>
    Task<RenameOutcome> ApplyAsync(IReadOnlyList<CallsignRename> renames, CancellationToken ct = default);
}

/// <summary>
/// Riconosce le rinomine confrontando quel che abbiamo con quel che la sorgente manda. È una funzione pura
/// — niente IO — perché è la parte che va provata su casi veri, e i casi veri sono tutti di forma.
///
/// <para><b>Perché non serve più un'euristica.</b> Fino al 26 agosto 2026 la rinomina si <i>indovinava</i>:
/// una riga andata in silenzio, una nuova nello stesso perimetro con la stessa posizione, e se il candidato
/// era uno solo si proponeva lo spostamento. Il caso vero che ha chiuso la questione è
/// <c>LIRR_NE1_CTR</c>, nato il 22 agosto 2026 accanto a <c>LIRR_NE_CTR</c> con la <b>stessa frequenza</b>
/// (124.2) e lo stesso nome IVAO («Roma Radar»): un candidato perfetto, e uno <b>sdoppiamento</b>, non una
/// rinomina. L'id lo dice senza margini — 3916 non l'avevamo mai visto, quindi è nato.</para>
/// </summary>
public static class CallsignRenameDetector
{
    /// <param name="catalog">Quale dei due cataloghi si sta importando.</param>
    /// <param name="ours">Cos'abbiamo in archivio: id della sorgente → nominativo attuale.</param>
    /// <param name="fromSource">Cosa manda la sorgente adesso. Le righe senza id si ignorano: non hanno
    /// un'identità da seguire, e per loro il callsign resta l'unica cosa che sappiamo.</param>
    public static IReadOnlyList<CallsignRename> Detect(
        SourceCatalog catalog,
        IReadOnlyDictionary<int, string> ours,
        IEnumerable<(int? IvaoId, string Callsign)> fromSource)
    {
        var renames = new List<CallsignRename>();
        var visti = new HashSet<int>();

        foreach (var (ivaoId, callsign) in fromSource)
        {
            if (ivaoId is not int id) continue;                       // riga senza identità: non c'è niente da seguire
            var nuovo = (callsign ?? "").Trim().ToUpperInvariant();
            if (nuovo.Length == 0) continue;
            if (!visti.Add(id)) continue;                             // la sorgente ha mandato l'id due volte: la prima vince
            if (!ours.TryGetValue(id, out var vecchio)) continue;     // id mai visto: è una riga NUOVA, non una rinomina
            if (string.Equals(vecchio, nuovo, StringComparison.OrdinalIgnoreCase)) continue;

            renames.Add(new CallsignRename(catalog, id, vecchio, nuovo));
        }

        return renames;
    }
}

/// <summary>
/// Traduce un nominativo <b>dismesso</b> in quello di oggi, seguendo la catena delle rinomine.
///
/// <para><b>A cosa serve.</b> Lo storico tiene il callsign come <i>dato</i> e non come puntatore: le sessioni
/// ATC dicono con che nominativo un controllore era connesso quella sera, e riscriverle sarebbe falsificare un
/// fatto. Ma chi legge una statistica non vuole vedere la stessa postazione spezzata in due righe perché a
/// giugno si chiamava in un altro modo. Questa è la traduzione, e si applica in <b>lettura</b>.</para>
///
/// <para>⚠️ È l'unico lettore di <c>CallsignAlias</c>, e deve restare tale: chi volesse sapere «come si chiama
/// questo settore» ha <c>Sector.Callsign</c>, sempre aggiornato. Vedi il commento sull'entità.</para>
/// </summary>
public sealed class CallsignHistory
{
    private readonly Dictionary<string, string> _successore;

    /// <param name="aliases">Le coppie (dismesso → successivo), nell'ordine che si vuole.</param>
    public CallsignHistory(IEnumerable<(string Old, string New)> aliases)
    {
        _successore = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (vecchio, nuovo) in aliases)
            _successore[vecchio] = nuovo;
    }

    /// <summary>Vero se non c'è nessuna rinomina in archivio: il chiamante può saltare del tutto la traduzione,
    /// che è il caso normale.</summary>
    public bool IsEmpty => _successore.Count == 0;

    /// <summary>
    /// Il nominativo di oggi. Segue la catena — un settore può essere stato rinominato più volte — e si ferma
    /// da sé su un ciclo, che non dovrebbe esistere ma non deve appendere una pagina se esiste.
    /// </summary>
    public string Canonical(string callsign)
    {
        var visti = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cur = callsign;
        while (_successore.TryGetValue(cur, out var next) && visti.Add(cur)) cur = next;
        return cur;
    }
}

/// <summary>
/// Sostituisce un callsign dentro un JSON di configurazione, e <b>solo</b> dove il callsign è un valore
/// intero — mai come pezzo di una stringa più lunga.
///
/// <para><b>Perché serve.</b> I blocchi di contenuto tengono puntatori per nominativo dentro
/// <c>ContentBlock.BodyJson</c>: sul <c>vipi.db</c> vero sono <b>35 righe</b> con forme come
/// <c>{"Callsigns":["LIMF_TWR",…]}</c>, <c>{"MemberCallsigns":[…],"FreqLinkCallsigns":[…]}</c> e
/// <c>{"Open":[{"Callsign":"LIMF_WW0_APP"}],"OpenCallsigns":[…]}</c> — la configurazione dell'AoR e dei
/// gruppi APP. Sono <b>puntatori</b>, non prosa: dopo una rinomina indicherebbero un settore che non
/// risponde più a quel nome.</para>
///
/// <para><b>Perché si cammina il JSON invece di cercare e sostituire.</b> Una sostituzione testuale
/// prenderebbe anche le chiavi, i nomi liberi («Conf 1») e qualunque prefisso: <c>LIRR_NE_CTR</c> è un
/// prefisso di <c>LIRR_NE_CTR2</c>, e chi scrive il primo colpirebbe il secondo. Camminando l'albero si
/// tocca solo un valore stringa che è <b>esattamente</b> il vecchio nominativo.</para>
///
/// <para>⚠️ La <b>prosa</b> non si tocca, mai: se il testo di una sezione nomina il vecchio settore, quello
/// è un giudizio editoriale — la frase intorno può non reggere il nome nuovo — ed è il motivo per cui la
/// rinomina apre comunque una segnalazione (<c>ImpactKind.SectorRenamed</c>).</para>
/// </summary>
public static class JsonCallsignRewriter
{
    /// <summary>Il JSON riscritto, o <c>null</c> se non c'era niente da cambiare (così una riga intatta non
    /// viene riformattata dal round-trip) o se il testo non è JSON valido.</summary>
    public static string? Rewrite(string? json, string oldCallsign, string newCallsign)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        // Nessuna occorrenza nemmeno come sottostringa: non c'è motivo di parsare.
        if (json.IndexOf(oldCallsign, StringComparison.OrdinalIgnoreCase) < 0) return null;

        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (JsonException) { return null; }   // non è JSON: non è roba nostra, non si tocca
        if (root is null) return null;

        // Radice che è già la stringa cercata: non ha un genitore che la possa sostituire.
        if (EIlVecchio(root, oldCallsign)) return JsonValue.Create(newCallsign).ToJsonString();

        return Cammina(root, oldCallsign, newCallsign) ? root.ToJsonString() : null;
    }

    /// <remarks>
    /// La sostituzione la fa sempre il <b>genitore</b>, mai il nodo su di sé: in
    /// <c>System.Text.Json.Nodes</c> un nodo conosce il proprio genitore, e riassegnarne uno che ne ha già
    /// uno lancia «The node already has a parent». Contenitori mutati sul posto, foglie rimpiazzate da chi
    /// le contiene.
    /// </remarks>
    private static bool Cammina(JsonNode node, string vecchio, string nuovo)
    {
        var cambiato = false;
        switch (node)
        {
            case JsonObject obj:
                // ToList(): si riassegnano voci mentre si scorre.
                foreach (var (chiave, valore) in obj.ToList())
                {
                    if (valore is null) continue;
                    if (EIlVecchio(valore, vecchio)) { obj[chiave] = JsonValue.Create(nuovo); cambiato = true; }
                    else cambiato |= Cammina(valore, vecchio, nuovo);
                }
                break;

            case JsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                {
                    if (arr[i] is not { } valore) continue;
                    if (EIlVecchio(valore, vecchio)) { arr[i] = JsonValue.Create(nuovo); cambiato = true; }
                    else cambiato |= Cammina(valore, vecchio, nuovo);
                }
                break;
        }
        return cambiato;
    }

    private static bool EIlVecchio(JsonNode node, string vecchio) =>
        node is JsonValue v && v.TryGetValue<string>(out var s)
        && string.Equals(s, vecchio, StringComparison.OrdinalIgnoreCase);
}
