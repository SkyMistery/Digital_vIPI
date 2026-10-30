using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aip;

/// <summary>Il tipo di oggetto che una correzione tocca: una lista del modello.</summary>
public enum CorrectionKind
{
    Airspace,
    Route,
    Holding,
    Navaid,
    Point,
    Light,
    Aerodrome,
    Hazard,

    /// <summary>
    /// Una procedura di AD 2 (carta «Gli aeroporti nel Lab», S4): una parte di un avvicinamento ridisegnata sopra la carta
    /// posata, dove la carta è un'immagine e il PDF non ha la tabella dei tratti.
    /// </summary>
    Procedure,
}

/// <summary>Che cosa fa una correzione: correggere un oggetto del PDF, aggiungerne uno che il PDF non ha, toglierne uno.</summary>
public enum CorrectionOp
{
    Change,
    Add,
    Remove,
}

/// <summary>
/// Una correzione dell'AIP espanso (carta «Le correzioni dell'AIP espanso»): un errore dell'AIP stesso, o un'aggiunta da
/// un'altra fonte (AIP SUP, AIC…). Tocca <b>un oggetto intero</b> del modello, trovato per <see cref="Kind"/> e
/// <see cref="Key"/>.
/// <para>Dura da un ciclo all'altro finché non la supera un PDF (l'oggetto del PDF non è più <see cref="Before"/>) o
/// un'altra correzione sullo stesso oggetto: allora <see cref="Superseded"/> dice da chi.</para>
/// </summary>
public sealed record AipCorrection
{
    /// <summary>Chi è: <c>2026-10-19-1</c>. Unico nel file.</summary>
    public required string Id { get; init; }

    public required CorrectionKind Kind { get; init; }

    /// <summary>La chiave dell'oggetto, quella di <see cref="AipCorrections.KeyOf{T}"/>: <c>LI R107A</c>, <c>PIS VOR/DME</c>.</summary>
    public required string Key { get; init; }

    public required CorrectionOp Op { get; init; }

    /// <summary>L'oggetto com'era nel PDF quando la correzione è nata (per <c>change</c> e <c>remove</c>). Se manca, lo scrive la prima applicazione.</summary>
    public JsonObject? Before { get; init; }

    /// <summary>L'oggetto dopo la correzione, nel JSON del modello (per <c>change</c> e <c>add</c>).</summary>
    public JsonObject? After { get; init; }

    /// <summary>Perché: obbligatorio.</summary>
    public string? Reason { get; init; }

    /// <summary>La fonte: «errore AIP, segnalato a ENAV il …», «AIP SUP 123/26». Obbligatoria.</summary>
    public string? Source { get; init; }

    /// <summary>Il ciclo in cui è nata (<c>2611</c>). Se manca, lo scrive la prima applicazione.</summary>
    public string? Since { get; init; }

    /// <summary>Da chi è stata superata: <c>AIP 2612</c>, o l'<see cref="Id"/> della correzione dopo. Null se è in piedi.</summary>
    public string? Superseded { get; init; }
}

/// <summary>Il file delle correzioni di uno Stato: <c>correzioni\li.json</c> nel repo dell'AIP espanso, valido per tutti i cicli.</summary>
public sealed record AipCorrectionFile
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string State { get; init; }

    /// <summary>Le correzioni nell'ordine in cui sono nate: fra due sullo stesso oggetto vale l'ultima.</summary>
    public IReadOnlyList<AipCorrection> Corrections { get; init; } = [];
}

/// <summary>Com'è andata una correzione in un'applicazione.</summary>
public enum CorrectionOutcomeKind
{
    /// <summary>Applicata.</summary>
    Applied,

    /// <summary>Superata adesso dal PDF: l'oggetto è cambiato (o arrivato, o sparito) rispetto a quando è nata.</summary>
    SupersededByAip,

    /// <summary>Superata adesso da una correzione dopo, sullo stesso oggetto.</summary>
    SupersededByCorrection,

    /// <summary>Non si applica: <see cref="CorrectionOutcome.Problem"/> dice perché. Resta in piedi, da sistemare.</summary>
    Invalid,
}

/// <summary>Perché una correzione non si applica. ⚠️ Un codice, non una frase: il testo lo scrive chi lo mostra.</summary>
public enum CorrectionProblem
{
    MissingReason,
    MissingSource,
    MissingAfter,

    /// <summary><c>after</c> non è un oggetto del modello di quel tipo.</summary>
    UnreadableAfter,

    /// <summary>Il PDF ha più oggetti con quella chiave: non si sa quale toccare.</summary>
    DuplicateKey,

    /// <summary>Una <c>change</c> o una <c>remove</c> nuova (senza <c>before</c>) su un oggetto che il PDF non ha.</summary>
    NotInAip,

    /// <summary>Un <see cref="AipCorrection.Id"/> che c'è già nel file.</summary>
    DuplicateId,
}

public sealed record CorrectionOutcome(string Id, CorrectionKind Kind, string Key, CorrectionOutcomeKind Outcome, CorrectionProblem? Problem = null, string? SupersededBy = null);

/// <summary>Il risultato di un'applicazione: il modello corretto, il file delle correzioni aggiornato e com'è andata ciascuna.</summary>
public sealed record CorrectionResult(AipDocument Document, AipCorrectionFile File, IReadOnlyList<CorrectionOutcome> Outcomes)
{
    /// <summary>Vero se il file delle correzioni va riscritto: un <c>before</c> o un <c>since</c> scritto, una superata.</summary>
    public bool FileChanged { get; init; }
}

/// <summary>
/// Le correzioni sopra il modello letto dai PDF. Il modello in entrata è <b>solo</b> quel che i PDF dicono; quello in uscita
/// ha in più le correzioni in piedi. I confronti con <c>before</c> si fanno senza la pagina: una pagina ristampata con la
/// data nuova e lo stesso dato non supera niente.
/// </summary>
public static class AipCorrections
{
    // Compatto e col testo com'è: serve solo a confrontare due oggetti, scritti dallo stesso serializzatore.
    private static readonly JsonSerializerOptions Confronto = new(AipJson.Options) { WriteIndented = false };

    /// <summary>La chiave di un oggetto del modello, quella che una correzione scrive in <see cref="AipCorrection.Key"/>.</summary>
    public static string KeyOf<T>(T voce) => voce switch
    {
        Airspace a => a.Key,
        Route r => r.Designator,
        Holding h => HoldingKey(h),
        Navaid n => n.Key,
        SignificantPoint p => p.Designator,
        GroundLight l => l.Name,
        Aerodrome a => a.Icao,
        Hazard h => h.Name,
        Procedure p => p.Key,
        _ => throw new ArgumentException($"Nessuna chiave per {typeof(T).Name}.", nameof(voce)),
    };

    /// <summary>
    /// Un punto può avere più attese: la chiave è punto, tipo della radioassistenza (ENR 3.4.2), rotta d'avvicinamento e
    /// verso: <c>ABRUS 250 left</c>, <c>PIS VOR/DME 027 right</c>. Un'attesa di uno scalo (AD 2, AD 3) ha davanti la sua sezione,
    /// perché la stessa attesa può essere anche in ENR 3.4: <c>AD 2 LIRN ISKIA 057 right</c>.
    /// </summary>
    public static string HoldingKey(Holding h) =>
        $"{(h.Section.StartsWith("AD ", StringComparison.Ordinal) ? h.Section + " " : "")}{h.Fix}{(h.FixType is null ? "" : " " + h.FixType)} {h.InboundTrack:000} {h.Turn.ToString().ToLowerInvariant()}";

    /// <summary>L'oggetto nel JSON del modello, come lo scrive l'AIP espanso.</summary>
    public static JsonObject ToNode<T>(T voce) =>
        JsonSerializer.SerializeToNode(voce, AipJson.Options)!.AsObject();

    /// <summary>Applica le correzioni in piedi al modello letto dai PDF.</summary>
    public static CorrectionResult Apply(AipDocument pdf, AipCorrectionFile file)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(file);

        var elenchi = new Dictionary<CorrectionKind, IElenco>
        {
            [CorrectionKind.Airspace] = new Elenco<Airspace>(pdf.Airspaces),
            [CorrectionKind.Route] = new Elenco<Route>(pdf.Routes),
            [CorrectionKind.Holding] = new Elenco<Holding>(pdf.Holdings),
            [CorrectionKind.Navaid] = new Elenco<Navaid>(pdf.Navaids),
            [CorrectionKind.Point] = new Elenco<SignificantPoint>(pdf.Points),
            [CorrectionKind.Light] = new Elenco<GroundLight>(pdf.Lights),
            [CorrectionKind.Aerodrome] = new Elenco<Aerodrome>(pdf.Aerodromes),
            [CorrectionKind.Hazard] = new Elenco<Hazard>(pdf.Hazards),
            [CorrectionKind.Procedure] = new Elenco<Procedure>(pdf.Procedures),
        };

        var correzioni = file.Corrections.ToList();
        var esiti = new List<CorrectionOutcome>();
        var cambiato = false;
        var ciclo = pdf.Airac;
        var superataDalPdf = ciclo is null ? "AIP" : $"AIP {ciclo}";

        // Fra due in piedi sullo stesso oggetto vale l'ultima: le prime diventano superate da lei.
        var ultima = new Dictionary<(CorrectionKind, string), int>();
        for (var i = 0; i < correzioni.Count; i++)
            if (correzioni[i].Superseded is null)
                ultima[(correzioni[i].Kind, correzioni[i].Key)] = i;

        var visti = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < correzioni.Count; i++)
        {
            var c = correzioni[i];
            if (c.Superseded is not null)
                continue;

            CorrectionOutcome Esito(CorrectionOutcomeKind come, CorrectionProblem? problema = null, string? da = null) =>
                new(c.Id, c.Kind, c.Key, come, problema, da);

            if (!visti.Add(c.Id))
            {
                esiti.Add(Esito(CorrectionOutcomeKind.Invalid, CorrectionProblem.DuplicateId));
                continue;
            }

            if (ultima[(c.Kind, c.Key)] != i)
            {
                var dopo = correzioni[ultima[(c.Kind, c.Key)]].Id;
                correzioni[i] = c with { Superseded = dopo };
                cambiato = true;
                esiti.Add(Esito(CorrectionOutcomeKind.SupersededByCorrection, da: dopo));
                continue;
            }

            if (Problema(c, elenchi[c.Kind]) is { } problema)
            {
                esiti.Add(Esito(CorrectionOutcomeKind.Invalid, problema));
                continue;
            }

            var elenco = elenchi[c.Kind];
            var quanti = elenco.Conta(c.Key);
            if (quanti > 1)
            {
                esiti.Add(Esito(CorrectionOutcomeKind.Invalid, CorrectionProblem.DuplicateKey));
                continue;
            }

            if (c.Since is null && ciclo is not null)
            {
                c = c with { Since = ciclo };
                correzioni[i] = c;
                cambiato = true;
            }

            if (c.Op == CorrectionOp.Add)
            {
                if (quanti == 1)
                {
                    correzioni[i] = c with { Superseded = superataDalPdf };
                    cambiato = true;
                    esiti.Add(Esito(CorrectionOutcomeKind.SupersededByAip, da: superataDalPdf));
                    continue;
                }

                elenco.Aggiungi(c.After!);
                esiti.Add(Esito(CorrectionOutcomeKind.Applied));
                continue;
            }

            // change e remove: l'oggetto del PDF deve essere ancora quello di quando la correzione è nata.
            if (quanti == 0)
            {
                if (c.Before is null)
                {
                    esiti.Add(Esito(CorrectionOutcomeKind.Invalid, CorrectionProblem.NotInAip));
                    continue;
                }

                correzioni[i] = c with { Superseded = superataDalPdf };
                cambiato = true;
                esiti.Add(Esito(CorrectionOutcomeKind.SupersededByAip, da: superataDalPdf));
                continue;
            }

            var delPdf = elenco.Nodo(c.Key);
            if (c.Before is null)
            {
                c = c with { Before = delPdf };
                correzioni[i] = c;
                cambiato = true;
            }
            else if (!UgualiSenzaPagina(c.Before, delPdf))
            {
                correzioni[i] = c with { Superseded = superataDalPdf };
                cambiato = true;
                esiti.Add(Esito(CorrectionOutcomeKind.SupersededByAip, da: superataDalPdf));
                continue;
            }

            if (c.Op == CorrectionOp.Remove)
                elenco.Togli(c.Key);
            else
                elenco.Sostituisci(c.Key, c.After!);
            esiti.Add(Esito(CorrectionOutcomeKind.Applied));
        }

        var corretto = pdf with
        {
            Airspaces = ((Elenco<Airspace>)elenchi[CorrectionKind.Airspace]).Voci,
            Routes = ((Elenco<Route>)elenchi[CorrectionKind.Route]).Lista(),
            Holdings = ((Elenco<Holding>)elenchi[CorrectionKind.Holding]).Lista(),
            Navaids = ((Elenco<Navaid>)elenchi[CorrectionKind.Navaid]).Lista(),
            Points = ((Elenco<SignificantPoint>)elenchi[CorrectionKind.Point]).Lista(),
            Lights = ((Elenco<GroundLight>)elenchi[CorrectionKind.Light]).Lista(),
            Aerodromes = ((Elenco<Aerodrome>)elenchi[CorrectionKind.Aerodrome]).Lista(),
            Hazards = ((Elenco<Hazard>)elenchi[CorrectionKind.Hazard]).Lista(),
            Procedures = ((Elenco<Procedure>)elenchi[CorrectionKind.Procedure]).Lista(),
        };
        return new CorrectionResult(corretto, file with { Corrections = correzioni }, esiti) { FileChanged = cambiato };
    }

    private static CorrectionProblem? Problema(AipCorrection c, IElenco elenco)
    {
        if (string.IsNullOrWhiteSpace(c.Reason))
            return CorrectionProblem.MissingReason;
        if (string.IsNullOrWhiteSpace(c.Source))
            return CorrectionProblem.MissingSource;
        if (c.Op == CorrectionOp.Remove)
            return null;
        if (c.After is null)
            return CorrectionProblem.MissingAfter;
        return elenco.Leggibile(c.After) ? null : CorrectionProblem.UnreadableAfter;
    }

    private static bool UgualiSenzaPagina(JsonObject a, JsonObject b) =>
        SenzaPagina(a) == SenzaPagina(b);

    private static string SenzaPagina(JsonObject o)
    {
        var copia = o.DeepClone().AsObject();
        copia.Remove("page");
        return copia.ToJsonString(Confronto);
    }

    public static string Serialize(AipCorrectionFile file) => JsonSerializer.Serialize(file, AipJson.Options);

    /// <summary>Rilegge il file; la versione dello schema si legge prima del resto, come per il modello.</summary>
    public static AipCorrectionFile Deserialize(string json)
    {
        JsonDocument letto;
        try
        {
            letto = JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new AipJsonException(AipJsonError.NotJson, e.Message, e);
        }

        using (letto)
        {
            var radice = letto.RootElement;
            if (radice.ValueKind != JsonValueKind.Object
                || !radice.TryGetProperty("schemaVersion", out var versione)
                || versione.ValueKind != JsonValueKind.Number
                || !versione.TryGetInt32(out var numero))
                throw new AipJsonException(AipJsonError.MissingSchemaVersion);
            if (numero < 1 || numero > AipCorrectionFile.CurrentSchemaVersion)
                throw new AipJsonException(AipJsonError.UnsupportedSchemaVersion, numero.ToString(System.Globalization.CultureInfo.InvariantCulture));

            try
            {
                return radice.Deserialize<AipCorrectionFile>(AipJson.Options)
                    ?? throw new AipJsonException(AipJsonError.Invalid, "null");
            }
            catch (JsonException e)
            {
                throw new AipJsonException(AipJsonError.Invalid, e.Message, e);
            }
        }
    }

    private interface IElenco
    {
        int Conta(string chiave);

        JsonObject Nodo(string chiave);

        bool Leggibile(JsonObject dopo);

        void Sostituisci(string chiave, JsonObject dopo);

        void Togli(string chiave);

        void Aggiungi(JsonObject dopo);
    }

    private sealed class Elenco<T>(IReadOnlyList<T>? voci) : IElenco
        where T : class
    {
        public List<T> Voci { get; } = [.. voci ?? []];

        /// <summary>La lista per il modello: null se è vuota, come la scrive «Leggi» (lo schema non cambia).</summary>
        public IReadOnlyList<T>? Lista() => Voci.Count == 0 ? null : Voci;

        public int Conta(string chiave) => Voci.Count(v => KeyOf(v) == chiave);

        public JsonObject Nodo(string chiave) => ToNode(Voci.Single(v => KeyOf(v) == chiave));

        public bool Leggibile(JsonObject dopo)
        {
            try
            {
                return Leggi(dopo) is not null;
            }
            catch (Exception e) when (e is JsonException or NotSupportedException or InvalidOperationException)
            {
                return false;
            }
        }

        public void Sostituisci(string chiave, JsonObject dopo)
        {
            Voci[Voci.FindIndex(v => KeyOf(v) == chiave)] = Leggi(dopo)!;
        }

        public void Togli(string chiave)
        {
            Voci.RemoveAll(v => KeyOf(v) == chiave);
        }

        public void Aggiungi(JsonObject dopo)
        {
            Voci.Add(Leggi(dopo)!);
        }

        private static T? Leggi(JsonObject dopo) => dopo.Deserialize<T>(AipJson.Options);
    }
}
