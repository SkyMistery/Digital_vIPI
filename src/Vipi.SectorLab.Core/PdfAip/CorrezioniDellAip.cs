using System.Text.Json;
using Aip;
using Aip.Pdf;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Quel che l'AOD scrive nel modulo di un'area (carta «Le correzioni dell'AIP espanso», slice 2): il testo dei campi
/// com'è nel modulo. <see cref="Forma"/> vuota = la forma di adesso; altrimenti i limiti laterali come li scrive l'AIP.
/// </summary>
public sealed record ModuloDellArea(
    string Sezione,
    AirspaceType Tipo,
    string Identificativo,
    string? Nome,
    string Superiore,
    string Inferiore,
    string? Classe,
    string? Attivita,
    string? Note,
    string? Forma)
{
    /// <summary>Il modulo riempito con un'area dell'AIP espanso, la forma vuota (= resta com'è).</summary>
    public static ModuloDellArea Di(Airspace a) =>
        new(a.Section, a.Type, a.Designator, a.Name, a.Upper.Text, a.Lower.Text, a.Class, a.Activity, a.Remarks, null);

    /// <summary>Un modulo vuoto per «Aggiungi un'area».</summary>
    public static ModuloDellArea Vuoto() => new("ENR 5.1", AirspaceType.Restricted, "", null, "", "", null, null, null, null);
}

/// <summary>Una correzione fatta: il modello del ciclo e il file delle correzioni come vanno scritti, e il messaggio proposto.</summary>
public sealed record CorrezioneFatta(AipDocument Modello, AipCorrectionFile Correzioni, string Messaggio);

/// <summary>
/// I gesti sulle correzioni dell'AIP espanso, dal Lab (slice 2): «Correggi», «Aggiungi», «Togli» un'area, e «Ritira» una
/// correzione. Lavorano sul modello del ciclo <b>già corretto</b> (quello che la mappa mostra) e sul file delle correzioni,
/// e li lasciano come li lascerebbe una «Leggi» coi PDF: la correzione nuova si applica subito, e <c>before</c> è sempre
/// l'oggetto del PDF (se l'area era già corretta, il <c>before</c> di quella correzione).
/// <para>Il perché e la fonte sono obbligatori (D1): chi legge il file deve sapere da dove viene il dato.</para>
/// </summary>
public static class CorrezioniDellAip
{
    /// <summary>Il valore di <see cref="AipCorrection.Superseded"/> per una correzione ritirata dall'AOD.</summary>
    public const string Ritirata = "ritirata";

    /// <summary>Il file delle correzioni dello Stato, o uno nuovo e vuoto se non c'è ancora.</summary>
    public static AipCorrectionFile Leggi(string espanso, string stato)
    {
        var file = AipEspanso.FileDelleCorrezioni(espanso, stato);
        if (!File.Exists(file))
            return new AipCorrectionFile { State = stato };
        try
        {
            return AipCorrections.Deserialize(File.ReadAllText(file));
        }
        catch (AipJsonException e)
        {
            throw new InvalidDataException($"Il file delle correzioni ({file}) non si legge ({e.Message}).", e);
        }
    }

    /// <summary>Scrive il file delle correzioni e il modello del ciclo, ognuno prima accanto e poi al suo posto.</summary>
    public static void Scrivi(string espanso, string ciclo, CorrezioneFatta fatta)
    {
        ArgumentNullException.ThrowIfNull(fatta);
        AipEspanso.ScriviAlPosto(AipEspanso.FileDelleCorrezioni(espanso, fatta.Modello.State),
            flusso => flusso.Write(System.Text.Encoding.UTF8.GetBytes(AipCorrections.Serialize(fatta.Correzioni))));
        AipEspanso.ScriviAlPosto(AipEspanso.File(espanso, ciclo, fatta.Modello.State), flusso => AipJson.Serialize(flusso, fatta.Modello));
    }

    /// <summary>
    /// La correzione in piedi che ha fatto l'area del modello con quella chiave: una <c>change</c> o una <c>add</c> il cui
    /// <c>after</c> è quell'area. Null se l'area è com'è nel PDF.
    /// </summary>
    public static AipCorrection? Attiva(AipCorrectionFile file, string chiave)
    {
        ArgumentNullException.ThrowIfNull(file);
        return file.Corrections.LastOrDefault(c => c.Superseded is null && c.Kind == CorrectionKind.Airspace
                                                   && c.Op != CorrectionOp.Remove && c.After is { } dopo && ChiaveDi(dopo) == chiave);
    }

    /// <summary>L'area del modulo, o il perché non si può fare. <paramref name="prima"/>: l'area che si corregge (null = nuova).</summary>
    public static (Airspace? Area, string? Perche) Area(ModuloDellArea modulo, Airspace? prima)
    {
        ArgumentNullException.ThrowIfNull(modulo);
        var identificativo = Pulito(modulo.Identificativo);
        var sezione = Pulito(modulo.Sezione);
        var superiore = Pulito(modulo.Superiore);
        var inferiore = Pulito(modulo.Inferiore);
        if (identificativo is null) return (null, "Manca l'identificativo.");
        if (sezione is null) return (null, "Manca la sezione dell'AIP (ENR 5.1, ENR 2.1…).");
        if (superiore is null || inferiore is null) return (null, "Mancano i limiti verticali: superiore e inferiore, come li scrive l'AIP.");

        Geometry forma;
        if (Pulito(modulo.Forma) is { } testo)
        {
            var letta = GeometryText.Read(testo);
            var problemi = letta.Issues.Where(p => p.Kind != GeometryIssueKind.NotClosed).ToList();
            if (problemi.Count > 0)
                return (null, "La forma non si legge: " + string.Join("; ", problemi.Select(p => p.Kind switch
                {
                    GeometryIssueKind.UnknownWords => $"parole che non conosco «{p.Detail}»",
                    GeometryIssueKind.ArcIncomplete => $"all'arco manca {p.Detail}",
                    GeometryIssueKind.BadCoordinate => $"coordinata fuori intervallo «{p.Detail}»",
                    _ => p.Kind.ToString(),
                })) + ".");
            if (letta.Geometries.Count != 1)
                return (null, letta.Geometries.Count == 0
                    ? "La forma non ha coordinate: incolla i limiti laterali come li scrive l'AIP."
                    : $"Il testo incollato fa {letta.Geometries.Count} forme: una per area.");
            forma = letta.Geometries[0];
        }
        else if (prima is not null)
            forma = prima.Geometry;
        else
            return (null, "Manca la forma: incolla i limiti laterali come li scrive l'AIP.");

        // Le fasce restano solo se i limiti sono quelli di prima: con limiti nuovi non si sa più a che fascia vanno.
        var stessiLimiti = prima is not null && prima.Upper.Text == superiore && prima.Lower.Text == inferiore;
        var area = new Airspace
        {
            Section = sezione,
            Type = modulo.Tipo,
            Designator = identificativo,
            Name = Pulito(modulo.Nome),
            Upper = stessiLimiti ? prima!.Upper : LimitText.Parse(superiore),
            Lower = stessiLimiti ? prima!.Lower : LimitText.Parse(inferiore),
            Class = Pulito(modulo.Classe),
            Activity = Pulito(modulo.Attivita),
            Remarks = Pulito(modulo.Note),
            Bands = stessiLimiti ? prima!.Bands : null,
            Unit = prima?.Unit,
            Hours = prima?.Hours,
            TransitionAltitude = prima?.TransitionAltitude,
            Geometry = forma,
            Page = prima?.Page,
        };
        if (prima is not null && area.Equals(prima))
            return (null, "Non è cambiato niente: l'area è uguale a quella di adesso.");
        return (area, null);
    }

    /// <summary>«Correggi»: l'area del modello con quella chiave diventa <paramref name="nuova"/>.</summary>
    public static (CorrezioneFatta? Fatta, string? Perche) Correggi(AipDocument modello, AipCorrectionFile file, string chiave, Airspace nuova,
        string? perche, string? fonte, string ciclo, DateOnly oggi)
    {
        ArgumentNullException.ThrowIfNull(modello);
        ArgumentNullException.ThrowIfNull(nuova);
        if (Mancano(perche, fonte) is { } manca) return (null, manca);
        if (Indice(modello, chiave) is not { } indice) return (null, $"{chiave}: non è nell'AIP espanso del ciclo {ciclo}.");
        if (nuova.Key != chiave && Indice(modello, nuova.Key) is not null)
            return (null, $"C'è già un'area {nuova.Key} nell'AIP espanso: correggi quella.");

        var correzioni = file.Corrections.ToList();
        var attiva = Attiva(file, chiave);
        var id = NuovoId(file, oggi);
        AipCorrection correzione;
        if (attiva is { Op: CorrectionOp.Add })
            correzione = Nuova(id, nuova.Key, CorrectionOp.Add, null, AipCorrections.ToNode(nuova), perche!, fonte!, ciclo);
        else
            correzione = Nuova(id, attiva?.Key ?? chiave, CorrectionOp.Change,
                attiva?.Before?.DeepClone().AsObject() ?? AipCorrections.ToNode(modello.Airspaces[indice]), AipCorrections.ToNode(nuova), perche!, fonte!, ciclo);
        Supera(correzioni, attiva, id);
        correzioni.Add(correzione);

        var aree = modello.Airspaces.ToList();
        aree[indice] = nuova;
        return (Fatta(modello, aree, file, correzioni, $"Correzione dell'AIP espanso: {chiave}", perche!, fonte!), null);
    }

    /// <summary>«Aggiungi un'area»: un'area che il PDF non ha (un'altra fonte: AIP SUP, AIC…).</summary>
    public static (CorrezioneFatta? Fatta, string? Perche) Aggiungi(AipDocument modello, AipCorrectionFile file, Airspace nuova,
        string? perche, string? fonte, string ciclo, DateOnly oggi)
    {
        ArgumentNullException.ThrowIfNull(modello);
        ArgumentNullException.ThrowIfNull(nuova);
        if (Mancano(perche, fonte) is { } manca) return (null, manca);
        if (Indice(modello, nuova.Key) is not null)
            return (null, $"C'è già un'area {nuova.Key} nell'AIP espanso: correggi quella.");
        if (file.Corrections.Any(c => c.Superseded is null && c.Kind == CorrectionKind.Airspace && c.Op == CorrectionOp.Remove && c.Key == nuova.Key))
            return (null, $"{nuova.Key} è stata tolta da una correzione: ritira quella, se va rimessa com'è nel PDF.");

        var id = NuovoId(file, oggi);
        var correzioni = file.Corrections.ToList();
        correzioni.Add(Nuova(id, nuova.Key, CorrectionOp.Add, null, AipCorrections.ToNode(nuova), perche!, fonte!, ciclo));
        return (Fatta(modello, [.. modello.Airspaces, nuova], file, correzioni, $"Aggiunta all'AIP espanso: {nuova.Key}", perche!, fonte!), null);
    }

    /// <summary>
    /// «Togli»: l'area esce dall'AIP espanso. Un'area che c'era solo per un'aggiunta non ha bisogno di una rimozione: si
    /// ritira l'aggiunta.
    /// </summary>
    public static (CorrezioneFatta? Fatta, string? Perche) Togli(AipDocument modello, AipCorrectionFile file, string chiave,
        string? perche, string? fonte, string ciclo, DateOnly oggi)
    {
        ArgumentNullException.ThrowIfNull(modello);
        var attiva = Attiva(file, chiave);
        if (attiva is { Op: CorrectionOp.Add })
            return Ritira(modello, file, attiva.Id);
        if (Mancano(perche, fonte) is { } manca) return (null, manca);
        if (Indice(modello, chiave) is not { } indice) return (null, $"{chiave}: non è nell'AIP espanso del ciclo {ciclo}.");

        var id = NuovoId(file, oggi);
        var correzioni = file.Corrections.ToList();
        Supera(correzioni, attiva, id);
        correzioni.Add(Nuova(id, attiva?.Key ?? chiave, CorrectionOp.Remove,
            attiva?.Before?.DeepClone().AsObject() ?? AipCorrections.ToNode(modello.Airspaces[indice]), null, perche!, fonte!, ciclo));
        var aree = modello.Airspaces.ToList();
        aree.RemoveAt(indice);
        return (Fatta(modello, aree, file, correzioni, $"Rimozione dall'AIP espanso: {chiave}", perche!, fonte!), null);
    }

    /// <summary>
    /// «Ritira»: la correzione non vale più (<c>superseded: ritirata</c>) e l'area torna com'è nel PDF. Un'area tolta torna in
    /// fondo all'elenco: al suo posto la rimette la prossima «Leggi».
    /// </summary>
    public static (CorrezioneFatta? Fatta, string? Perche) Ritira(AipDocument modello, AipCorrectionFile file, string id)
    {
        ArgumentNullException.ThrowIfNull(modello);
        ArgumentNullException.ThrowIfNull(file);
        var indiceNelFile = file.Corrections.ToList().FindIndex(c => c.Id == id && c.Superseded is null);
        if (indiceNelFile < 0) return (null, $"La correzione {id} non c'è, o non è più in piedi.");
        var c = file.Corrections[indiceNelFile];
        if (c.Kind != CorrectionKind.Airspace) return (null, "Dal Lab si ritirano per ora solo le correzioni delle aree.");

        var aree = modello.Airspaces.ToList();
        switch (c.Op)
        {
            case CorrectionOp.Add:
            case CorrectionOp.Change:
                var dopo = c.After is { } a ? ChiaveDi(a) : null;
                var dove = aree.FindIndex(x => x.Key == dopo);
                if (dove < 0) return (null, $"{dopo}: non è nell'AIP espanso così com'è scritto. Rileggi i PDF («Leggi»), poi riprova.");
                if (c.Op == CorrectionOp.Add)
                    aree.RemoveAt(dove);
                else if (c.Before?.Deserialize<Airspace>(AipJson.Options) is { } delPdf)
                    aree[dove] = delPdf;
                else
                    return (null, $"La correzione {id} non dice com'era l'area nel PDF: rileggi i PDF («Leggi»), poi riprova.");
                break;
            case CorrectionOp.Remove:
                if (c.Before?.Deserialize<Airspace>(AipJson.Options) is not { } tolta)
                    return (null, $"La correzione {id} non dice com'era l'area nel PDF: rileggi i PDF («Leggi»), poi riprova.");
                aree.Add(tolta);
                break;
        }

        var correzioni = file.Corrections.ToList();
        correzioni[indiceNelFile] = c with { Superseded = Ritirata };
        return (Fatta(modello, aree, file, correzioni, $"Ritirata la correzione {id} ({c.Key})", null, null), null);
    }

    private static CorrezioneFatta Fatta(AipDocument modello, List<Airspace> aree, AipCorrectionFile file, List<AipCorrection> correzioni,
        string cosa, string? perche, string? fonte) =>
        new(modello with { Airspaces = aree }, file with { Corrections = correzioni },
            perche is null ? cosa : $"{cosa} — {perche.Trim()} ({fonte!.Trim()})");

    private static AipCorrection Nuova(string id, string chiave, CorrectionOp op, System.Text.Json.Nodes.JsonObject? prima,
        System.Text.Json.Nodes.JsonObject? dopo, string perche, string fonte, string ciclo) => new()
    {
        Id = id,
        Kind = CorrectionKind.Airspace,
        Key = chiave,
        Op = op,
        Before = prima,
        After = dopo,
        Reason = perche.Trim(),
        Source = fonte.Trim(),
        Since = ciclo,
    };

    private static void Supera(List<AipCorrection> correzioni, AipCorrection? attiva, string da)
    {
        if (attiva is null) return;
        var i = correzioni.IndexOf(attiva);
        correzioni[i] = attiva with { Superseded = da };
    }

    private static string? Mancano(string? perche, string? fonte) =>
        string.IsNullOrWhiteSpace(perche) ? "Scrivi il perché della correzione: chi legge l'AIP espanso deve saperlo."
        : string.IsNullOrWhiteSpace(fonte) ? "Scrivi la fonte: «errore AIP, segnalato a ENAV il …», «AIP SUP 123/26»…"
        : null;

    private static int? Indice(AipDocument modello, string chiave)
    {
        var i = modello.Airspaces.ToList().FindIndex(a => a.Key == chiave);
        return i < 0 ? null : i;
    }

    /// <summary>Il prossimo id del giorno: <c>2026-10-19-3</c>.</summary>
    public static string NuovoId(AipCorrectionFile file, DateOnly oggi)
    {
        ArgumentNullException.ThrowIfNull(file);
        var prefisso = oggi.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + "-";
        var n = file.Corrections.Select(c => c.Id.StartsWith(prefisso, StringComparison.Ordinal) && int.TryParse(c.Id[prefisso.Length..], out var k) ? k : 0)
            .DefaultIfEmpty(0).Max();
        return prefisso + (n + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string? ChiaveDi(System.Text.Json.Nodes.JsonObject area)
    {
        try
        {
            return area.Deserialize<Airspace>(AipJson.Options)?.Key;
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string? Pulito(string? testo) => string.IsNullOrWhiteSpace(testo) ? null : testo.Trim();
}
