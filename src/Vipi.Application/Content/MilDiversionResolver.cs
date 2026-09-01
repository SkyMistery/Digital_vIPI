using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <summary>
/// Dalle righe salvate nel documento alla tabella che si legge: nomi degli scali dall'archivio,
/// radioassistenze dall'anagrafica (carta vSOP militari §12f).
///
/// <para>
/// ⚠️ <b>Un posto solo, e serve a due chiamanti</b>: il servizio che mostra la sezione e la <b>cattura
/// Frozen</b> che la fotografa alla release. Se la risoluzione stesse in tutt'e due, un giorno la release
/// congelerebbe una tabella diversa da quella che il viewer disegna — e nessuno se ne accorgerebbe, perché
/// le due si guardano in momenti diversi.
/// </para>
/// <para>
/// ⚠️ <b>Due interrogazioni in tutto</b>, non due per riga: i nomi degli scali in una, le radioassistenze di
/// tutte le righe in un'altra. Una tabella di dieci alternati con tre navaid ciascuno è una pagina pubblica,
/// non una schermata d'amministrazione.
/// </para>
/// </summary>
public static class MilDiversionResolver
{
    public static async Task<IReadOnlyList<MilDiversionView>> ResolveAsync(
        IReadOnlyList<MilDiversionPayload.Riga> righe,
        INavaidCatalog anagrafica, IAirportNameLookup? aeroporti, CancellationToken ct = default)
    {
        if (righe.Count == 0) return Array.Empty<MilDiversionView>();

        var nomi = aeroporti is null
            ? new Dictionary<string, string>()
            : await aeroporti.NamesAsync(righe.Select(r => r.Icao).ToList(), ct).ConfigureAwait(false);

        var chiavi = MilDiversionPayload.ChiaviNavaid(righe);
        var navaid = chiavi.Count == 0
            ? Array.Empty<NavaidRow>()
            : await anagrafica.GetManyAsync(chiavi, ct).ConfigureAwait(false);

        // ⚠️ L'indice si fa sulla TERNA, non su codice+famiglia: GRO sta due volte fra i VHF — un VOR senza
        // canale e un TACAN col solo 35Y — e una chiave a due campi li confonde. Confonderli qui vorrebbe dire
        // stampare su un SOP la frequenza dell'impianto sbagliato, e con due righe citate nella stessa tabella
        // il dizionario non si sarebbe nemmeno costruito.
        // ⚠️ E si scrive con l'indicizzatore, non con ToDictionary: due chiavi che si normalizzano allo stesso
        // impianto tornano due volte, e sarebbe un'eccezione al posto di una tabella.
        var perChiave = new Dictionary<NavaidKey, NavaidRow>();
        foreach (var n in navaid) perChiave[n.Key] = n;

        return righe.Select(r => new MilDiversionView(
            r.Icao,
            // ⚠️ Vince l'ARCHIVIO: quello è il dato vero, e il nome nel documento è il ripiego per gli scali
            // esteri che non abbiamo. Il contrario congelerebbe nel documento un nome che poi cambia.
            nomi.TryGetValue(r.Icao, out var nome) ? nome : (r.Name ?? ""),
            r.Navaids
                .Select(n => perChiave.TryGetValue(new NavaidKey(n.Code, n.Kind, n.Channel), out var v) ? v : null)
                .Where(v => v is not null).Select(v => v!).ToList(),
            r.Bearing, r.Distance)).ToList();
    }
}
