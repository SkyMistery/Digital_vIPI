using System.Text.Json;
using System.Text.Json.Serialization;
using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <summary>
/// Che cosa il <b>documento</b> dice della sezione «Radioassistenze»: <b>quali</b> righe cita e in
/// <b>che ordine</b>. I valori — frequenza, canale, coordinate — non stanno qui: stanno nell'anagrafica di
/// divisione, ed è tutto il punto (carta vSOP militari §12b).
///
/// <para>
/// ⚠️ <b>L'ordine è contenuto</b>, non presentazione: in un SOP le radioassistenze si elencano come le vuole
/// chi scrive — prima quella del campo, poi le altre — e ordinarle per codice butterebbe via una scelta
/// editoriale. Per questo il payload è una <b>lista</b> e non un insieme.
/// </para>
/// <para>
/// ⚠️ Le righe si citano per <b>identità di dominio</b> (codice + natura) e non per id di archivio: uno
/// snapshot di release con dentro degli interi non si legge, e non sopravviverebbe a un travaso del database
/// — che in questo progetto è già successo tre volte.
/// </para>
/// </summary>
public sealed class MilNavaidsPayload
{
    /// <summary>Il discriminatore del blocco, come per gli altri payload con <c>variant</c>.</summary>
    public const string Variante = "milnavaids";

    [JsonPropertyName("variant")]
    public string Variant { get; init; } = Variante;

    [JsonPropertyName("rows")]
    public IReadOnlyList<Riga> Rows { get; init; } = Array.Empty<Riga>();

    /// <summary>
    /// Una riga citata: l'identità, e nient'altro.
    /// <para>⚠️ Tre campi e non due: il <b>canale</b> è nell'identità perché lo stesso codice, nella stessa
    /// famiglia, è legittimamente due impianti — Grosseto ha un VOR e un TACAN puro nello stesso file.</para>
    /// </summary>
    public sealed class Riga
    {
        [JsonPropertyName("code")] public string Code { get; init; } = "";
        [JsonPropertyName("kind")] public string Kind { get; init; } = "";
        [JsonPropertyName("channel")] public string? Channel { get; init; }
    }

    private static readonly JsonSerializerOptions Opzioni = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Le identità citate dal blocco. JSON assente, vuoto o illeggibile ⇒ <b>nessuna riga</b>: una tabella
    /// vuota è un documento da compilare, e va detta così — non con un errore in faccia a chi legge.
    /// </summary>
    public static IReadOnlyList<NavaidKey> Leggi(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<NavaidKey>();
        try
        {
            var p = JsonSerializer.Deserialize<MilNavaidsPayload>(json, Opzioni);
            return (p?.Rows ?? Array.Empty<Riga>())
                .Where(r => !string.IsNullOrWhiteSpace(r.Code) && !string.IsNullOrWhiteSpace(r.Kind))
                .Select(r => new NavaidKey(NavaidRules.Norm(r.Code), NavaidRules.Norm(r.Kind),
                    NavaidRules.Valore(r.Channel)))
                .ToList();
        }
        catch (JsonException) { return Array.Empty<NavaidKey>(); }
    }

    /// <summary>Il JSON da salvare, o <c>null</c> quando non c'è nessuna riga: null e «lista vuota» devono
    /// essere la stessa cosa in archivio, o «non c'è niente» avrebbe due forme.</summary>
    public static string? Scrivi(IReadOnlyList<NavaidKey> righe)
    {
        var pulite = righe
            .Where(k => !string.IsNullOrWhiteSpace(k.Code) && !string.IsNullOrWhiteSpace(k.Kind))
            .Select(k => new Riga
            {
                Code = NavaidRules.Norm(k.Code), Kind = NavaidRules.Norm(k.Kind),
                Channel = NavaidRules.Valore(k.Channel),
            })
            .ToList();
        return pulite.Count == 0 ? null : JsonSerializer.Serialize(new MilNavaidsPayload { Rows = pulite });
    }
}
