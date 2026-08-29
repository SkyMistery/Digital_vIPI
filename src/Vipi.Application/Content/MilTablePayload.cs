using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vipi.Application.Content;

/// <summary>
/// Una tabella a <b>colonne fisse e celle libere</b>: è la forma di «Nominativi» e «Parcheggi» (carta vSOP
/// militari §12g), dove tutto si scrive a mano e non c'è niente da risolvere su un catalogo.
///
/// <para>
/// ⚠️ <b>Non è la tabella generica dei blocchi</b>, che lascia scegliere anche le colonne. Qui le colonne le
/// decide il <b>profilo</b> — Squadrone/OAT/GAT/QRA, Nome/Numeri/Usato da — perché è quel che rende una
/// sezione di SOP confrontabile fra quindici campi: se ognuno si sceglie le sue, torniamo alla prosa con le
/// righe in mezzo, che è il punto di partenza.
/// </para>
/// <para>
/// ⚠️ <b>Niente si risolve altrove</b>, quindi niente si congela: il contenuto è il payload, il payload è nel
/// documento, e il documento lo fotografa già la release. È la differenza con «Radioassistenze» e
/// «Aeroporti alternati», che vivono sui cataloghi e per questo sono <c>Derived</c>.
/// </para>
/// </summary>
public sealed class MilTablePayload
{
    [JsonPropertyName("variant")]
    public string Variant { get; init; } = "";

    /// <summary>Le righe, ognuna con una cella per colonna.</summary>
    [JsonPropertyName("rows")]
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = Array.Empty<IReadOnlyList<string>>();

    private static readonly JsonSerializerOptions Opzioni = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Le righe salvate, <b>portate a <paramref name="colonne"/> celle</b>: le mancanti si aggiungono vuote,
    /// quelle in più si tagliano.
    /// <para>⚠️ Serve il giorno che una colonna si aggiunge o si toglie dal profilo: senza, un documento
    /// scritto prima renderebbe righe più corte dell'intestazione — e in una tabella HTML una riga corta non
    /// lascia una cella vuota, <b>sposta tutto a sinistra</b>. Il dato sembrerebbe sbagliato invece che
    /// incompleto.</para>
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> Leggi(string? json, int colonne)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<IReadOnlyList<string>>();
        try
        {
            var p = JsonSerializer.Deserialize<MilTablePayload>(json, Opzioni);
            return (p?.Rows ?? Array.Empty<IReadOnlyList<string>>())
                .Select(r => Normalizza(r, colonne))
                .ToList();
        }
        catch (JsonException) { return Array.Empty<IReadOnlyList<string>>(); }
    }

    /// <summary>
    /// Il JSON da salvare, o null quando non c'è <b>nessuna</b> riga.
    ///
    /// <para>
    /// ⚠️ <b>Una riga vuota è una riga</b>, e si salva. Qui c'era il filtro opposto — «una riga tutta vuota è
    /// quel che resta di una cancellata a metà» — e la verifica dal vivo del 30 agosto 2026 ha mostrato che
    /// cosa produce: <b>«Aggiungi riga» non aggiungeva niente</b>. La riga nuova nasce vuota per definizione,
    /// il salvataggio la scartava, il ricarico non la trovava, e a schermo il tasto sembrava rotto. Nessun
    /// errore da nessuna parte.
    /// </para>
    /// <para>Le righe si tolgono col <b>tasto che le toglie</b>: una potatura automatica che indovina cosa
    /// l'utente voleva è la stessa categoria di sorpresa, solo più difficile da vedere.</para>
    /// </summary>
    public static string? Scrivi(string variante, IReadOnlyList<IReadOnlyList<string>> righe, int colonne)
    {
        var pulite = righe.Select(r => Normalizza(r, colonne)).ToList();
        return pulite.Count == 0
            ? null
            : JsonSerializer.Serialize(new MilTablePayload { Variant = variante, Rows = pulite });
    }

    private static IReadOnlyList<string> Normalizza(IReadOnlyList<string>? riga, int colonne)
    {
        var celle = new string[colonne];
        for (var i = 0; i < colonne; i++)
            celle[i] = riga is not null && i < riga.Count ? (riga[i] ?? "").Trim() : "";
        return celle;
    }

    /// <summary>Le varianti in uso, una per sezione: è come si riconosce un blocco di struttura guardando il
    /// database.</summary>
    public const string Nominativi = "milcallsigns";

    public const string Parcheggi = "milparkings";
}
