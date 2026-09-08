using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vipi.Application.Content;

/// <summary>
/// Che attività si vola in un'area di lavoro. ⚠️ <b>Flags</b>: se ne possono accendere quante se ne vuole, ed
/// è il caso normale su un poligono grande — si scrive <c>A/A - A/G</c>.
///
/// <para>⚠️ I valori sono <b>bit</b> e non si riordinano né si riciclano: un bit riusato per un'altra
/// attività riscrive il significato di quel che è già salvato. Le nuove si accodano, e basta.</para>
///
/// <para>⚠️ Il nome dell'attività a schermo e la sua chiave nel JSON <b>non stanno qui</b>: stanno una volta
/// sola in <see cref="MilActivityText.Catalogo"/>, che è anche l'ordine in cui la tabella le disegna.</para>
/// </summary>
[Flags]
public enum MilActivity
{
    None = 0,

    /// <summary>Aria-aria.</summary>
    AirToAir = 1 << 0,

    /// <summary>Aria-suolo.</summary>
    AirToGround = 1 << 1,

    /// <summary>Guerra elettronica.</summary>
    Ew = 1 << 2,

    /// <summary>Scoperta radar avanzata (<i>airborne early warning</i>).</summary>
    Aew = 1 << 3,

    /// <summary>Aeromobili a pilotaggio remoto.</summary>
    Rpa = 1 << 4,

    /// <summary>Rifornimento in volo.</summary>
    Aar = 1 << 5,

    /// <summary>Rifornimento in volo per elicotteri.</summary>
    Haar = 1 << 6,

    /// <summary>Pattugliamento aereo da combattimento.</summary>
    Cap = 1 << 7,

    /// <summary>Appoggio aereo ravvicinato.</summary>
    Cas = 1 << 8,

    /// <summary>Lotta antisommergibile.</summary>
    Asw = 1 << 9,

    /// <summary>Atterraggio simulato con motore in avaria (<i>simulated flame-out</i>).</summary>
    Sfo = 1 << 10,

    /// <summary>Voli prova.</summary>
    TestFlights = 1 << 11,

    /// <summary>Addestramento.</summary>
    Training = 1 << 12,

    /// <summary>Lanci con paracadute.</summary>
    Para = 1 << 13,

    /// <summary>Volo a bassa quota.</summary>
    LowLevel = 1 << 14,
}

/// <summary>Come si scrive un'attività in tabella e nel JSON.</summary>
public static class MilActivityText
{
    /// <summary>
    /// Le attività: bandierina, come si legge a schermo, come si salva. <b>Un posto solo</b> — la stessa riga
    /// fa il gettone nell'editor, il testo in sola lettura e la chiave nel JSON.
    ///
    /// <para>⚠️ È anche l'<b>ordine</b> con cui si leggono: sempre questo, mai quello dei clic. Due aree con
    /// le stesse attività devono scriverle uguali, o sembrano diverse.</para>
    ///
    /// <para>⚠️ Le chiavi non contengono il trattino, che è il separatore, né spazi: <c>TEST FLIGHTS</c> si
    /// salva <c>TESTFLIGHTS</c>. Cambiare una chiave qui vuol dire non saper più rileggere i documenti già
    /// salvati — si aggiunge, non si rinomina.</para>
    /// </summary>
    public static readonly IReadOnlyList<(MilActivity Flag, string Etichetta, string Chiave)> Catalogo = new[]
    {
        (MilActivity.AirToAir, "A/A", "AA"),
        (MilActivity.AirToGround, "A/G", "AG"),
        (MilActivity.Ew, "EW", "EW"),
        (MilActivity.Aew, "AEW", "AEW"),
        (MilActivity.Rpa, "RPA", "RPA"),
        (MilActivity.Aar, "AAR", "AAR"),
        (MilActivity.Haar, "HAAR", "HAAR"),
        (MilActivity.Cap, "CAP", "CAP"),
        (MilActivity.Cas, "CAS", "CAS"),
        (MilActivity.Asw, "ASW", "ASW"),
        (MilActivity.Sfo, "SFO", "SFO"),
        (MilActivity.TestFlights, "TEST FLIGHTS", "TESTFLIGHTS"),
        (MilActivity.Training, "TRAINING", "TRAINING"),
        (MilActivity.Para, "PARA", "PARA"),
        (MilActivity.LowLevel, "LOW LEVEL", "LOWLEVEL"),
    };

    private static readonly IReadOnlyDictionary<string, MilActivity> PerChiave =
        Catalogo.ToDictionary(v => v.Chiave, v => v.Flag, StringComparer.OrdinalIgnoreCase);

    /// <summary>Le attività accese, nell'ordine del catalogo. Nessuna ⇒ stringa vuota.</summary>
    public static string Scrivi(MilActivity a) =>
        string.Join(" - ", Catalogo.Where(v => a.HasFlag(v.Flag)).Select(v => v.Etichetta));

    /// <summary>La forma compatta con cui l'attività sta nel JSON. ⚠️ Si salvano le <b>parole</b> e non il
    /// numero dei flag: un documento si legge anche in SQL davanti a un incidente, e <c>16387</c> non dice niente.</summary>
    public static string Chiave(MilActivity a) =>
        string.Join("-", Catalogo.Where(v => a.HasFlag(v.Flag)).Select(v => v.Chiave));

    /// <summary>
    /// Rilegge la forma compatta. ⚠️ L'ordine dei pezzi non conta e i pezzi sconosciuti si <b>saltano</b>:
    /// così un documento scritto da una versione che conosce un'attività in più si legge lo stesso, senza la
    /// voce che non sappiamo rendere, invece di perdere tutta la riga.
    /// </summary>
    public static MilActivity Leggi(string? chiave)
    {
        var esito = MilActivity.None;
        foreach (var pezzo in (chiave ?? "").Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (PerChiave.TryGetValue(pezzo, out var f)) esito |= f;
        return esito;
    }
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

    /// <summary>Id d'area → attività, come chiavi unite dal trattino: <c>AA</c>, <c>AA-AG</c>, <c>CAS-LOWLEVEL</c>.</summary>
    [JsonPropertyName("activities")] public Dictionary<string, string> Activities { get; init; } = new();

    /// <summary>Id d'area → nota libera: le procedure particolari che né la mappa né l'attività possono dire.</summary>
    [JsonPropertyName("notes")] public Dictionary<string, string> Notes { get; init; } = new();

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

    /// <summary>Le note salvate, per id d'area. JSON assente o illeggibile ⇒ nessuna.</summary>
    public static IReadOnlyDictionary<string, string> LeggiNote(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>();
        try
        {
            var p = JsonSerializer.Deserialize<MilRegulatedPayload>(json, Opzioni);
            var esito = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (id, testo) in p?.Notes ?? new())
                if (!string.IsNullOrWhiteSpace(testo)) esito[id] = testo;
            return esito;
        }
        catch (JsonException) { return new Dictionary<string, string>(); }
    }

    /// <summary>
    /// Selezione, attività e note in un JSON solo. ⚠️ Attività e note delle aree <b>non più selezionate</b> si
    /// scartano: tenerle vorrebbe dire un payload che cresce a ogni ripensamento, e nessuno che sappia più
    /// quali righe contano.
    ///
    /// <para>⚠️ <b>Le note non hanno un valore di scorta e il parametro è obbligatorio, apposta.</b> Chi
    /// riscrive questo oggetto ne riscrive tutto il contenuto: un chiamante che passasse la sola selezione
    /// cancellerebbe le note di tutte le aree, senza un errore e senza che chi le ha scritte tocchi mai
    /// quella tendina. È lo stesso incidente che le attività hanno già evitato una volta; qui lo impedisce
    /// il compilatore, non la memoria di chi scrive il prossimo chiamante.</para>
    /// </summary>
    public static string Scrivi(RegulatedSelection selezione,
        IReadOnlyDictionary<string, MilActivity> attivita, IReadOnlyDictionary<string, string> note)
    {
        var vive = new HashSet<string>(selezione.OwnIds.Concat(selezione.ExtraIds), StringComparer.OrdinalIgnoreCase);
        var mappa = new Dictionary<string, string>();
        foreach (var (id, a) in attivita)
            if (a != MilActivity.None && vive.Contains(id)) mappa[id] = MilActivityText.Chiave(a);

        var noteVive = new Dictionary<string, string>();
        foreach (var (id, testo) in note)
            if (!string.IsNullOrWhiteSpace(testo) && vive.Contains(id)) noteVive[id] = testo.Trim();

        return JsonSerializer.Serialize(new MilRegulatedPayload
        {
            OwnAuto = selezione.OwnAuto,
            OwnIds = selezione.OwnIds,
            ExtraIds = selezione.ExtraIds,
            Activities = mappa,
            Notes = noteVive,
        });
    }
}
