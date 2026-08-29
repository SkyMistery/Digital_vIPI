using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vipi.Application.Content;

/// <summary>
/// Che attività si vola in un'area di lavoro. ⚠️ <b>Flags</b>: le due si possono selezionare insieme, ed è il
/// caso normale su un poligono grande — si scrive <c>A/A - A/G</c>.
/// </summary>
[Flags]
public enum MilActivity
{
    None = 0,

    /// <summary>Aria-aria.</summary>
    AirToAir = 1,

    /// <summary>Aria-suolo.</summary>
    AirToGround = 2,
}

/// <summary>Come si scrive un'attività in tabella.</summary>
public static class MilActivityText
{
    public static string Scrivi(MilActivity a) => a switch
    {
        MilActivity.AirToAir => "A/A",
        MilActivity.AirToGround => "A/G",
        MilActivity.AirToAir | MilActivity.AirToGround => "A/A - A/G",
        _ => "",
    };

    /// <summary>La forma compatta con cui l'attività sta nel JSON. ⚠️ Si salva la <b>parola</b> e non il
    /// numero dei flag: un documento si legge anche in SQL davanti a un incidente, e <c>3</c> non dice niente.</summary>
    public static string Chiave(MilActivity a) => a switch
    {
        MilActivity.AirToAir => "AA",
        MilActivity.AirToGround => "AG",
        MilActivity.AirToAir | MilActivity.AirToGround => "AA-AG",
        _ => "",
    };

    public static MilActivity Leggi(string? chiave) => (chiave ?? "").Trim().ToUpperInvariant() switch
    {
        "AA" => MilActivity.AirToAir,
        "AG" => MilActivity.AirToGround,
        "AA-AG" or "AG-AA" => MilActivity.AirToAir | MilActivity.AirToGround,
        _ => MilActivity.None,
    };
}

/// <summary>
/// Il payload della sezione «Aree di lavoro» del vSOP militare: la selezione delle aree — la <b>stessa</b>
/// che leggono la vIPI ACC e l'APP — più, solo qui, <b>che attività</b> si vola in ognuna (carta §12h).
///
/// <para>
/// ⚠️ <b>Un oggetto solo, non due blocchi.</b> I tre campi della selezione si chiamano come in
/// <see cref="RegulatedSelection"/> apposta: così <c>RegulatedSelectionJson.Parse</c> — che è condiviso con
/// le altre due famiglie — continua a leggerlo senza sapere niente delle attività, che per lui sono una
/// proprietà sconosciuta e le proprietà sconosciute si ignorano. Due blocchi separati avrebbero voluto dire
/// due scritture da tenere in fila, e una delle due che si perde.
/// </para>
/// <para>
/// ⚠️ Le attività si tengono per <b>id d'area</b>, non per posizione: un'area tolta dalla selezione e poi
/// rimessa ritrova la sua attività, e l'ordine delle chip non c'entra niente.
/// </para>
/// </summary>
public sealed class MilRegulatedPayload
{
    [JsonPropertyName("OwnAuto")] public bool OwnAuto { get; init; }
    [JsonPropertyName("OwnIds")] public List<string> OwnIds { get; init; } = new();
    [JsonPropertyName("ExtraIds")] public List<string> ExtraIds { get; init; } = new();

    /// <summary>Id d'area → attività, nella forma <c>AA</c> / <c>AG</c> / <c>AA-AG</c>.</summary>
    [JsonPropertyName("activities")] public Dictionary<string, string> Activities { get; init; } = new();

    private static readonly JsonSerializerOptions Opzioni = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Le attività salvate, per id d'area. JSON assente o illeggibile ⇒ nessuna, che è come
    /// nascono.</summary>
    public static IReadOnlyDictionary<string, MilActivity> LeggiAttivita(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, MilActivity>();
        try
        {
            var p = JsonSerializer.Deserialize<MilRegulatedPayload>(json, Opzioni);
            var esito = new Dictionary<string, MilActivity>(StringComparer.OrdinalIgnoreCase);
            foreach (var (id, valore) in p?.Activities ?? new())
            {
                var a = MilActivityText.Leggi(valore);
                if (a != MilActivity.None) esito[id] = a;
            }
            return esito;
        }
        catch (JsonException) { return new Dictionary<string, MilActivity>(); }
    }

    /// <summary>
    /// Selezione e attività in un JSON solo. ⚠️ Le attività delle aree <b>non più selezionate</b> si
    /// scartano: tenerle vorrebbe dire un payload che cresce a ogni ripensamento, e nessuno che sappia più
    /// quali righe contano.
    /// </summary>
    public static string Scrivi(RegulatedSelection selezione, IReadOnlyDictionary<string, MilActivity> attivita)
    {
        var vive = new HashSet<string>(selezione.OwnIds.Concat(selezione.ExtraIds), StringComparer.OrdinalIgnoreCase);
        var mappa = new Dictionary<string, string>();
        foreach (var (id, a) in attivita)
            if (a != MilActivity.None && vive.Contains(id)) mappa[id] = MilActivityText.Chiave(a);

        return JsonSerializer.Serialize(new MilRegulatedPayload
        {
            OwnAuto = selezione.OwnAuto,
            OwnIds = selezione.OwnIds,
            ExtraIds = selezione.ExtraIds,
            Activities = mappa,
        });
    }
}
