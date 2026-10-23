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
/// I gesti sulle correzioni dell'AIP espanso, dal Lab (slice 2 le aree, slice 3 gli altri tipi): «Correggi», «Aggiungi»,
/// «Togli» una voce, e «Ritira» una correzione. Lavorano sul modello del ciclo <b>già corretto</b> (quello che la mappa mostra) e sul file delle correzioni,
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
        AipEspanso.Scrivi(espanso, ciclo, fatta.Modello);
    }

    /// <summary>La correzione in piedi che ha fatto l'area con quella chiave: vedi <see cref="Attiva(AipCorrectionFile, CorrectionKind, string)"/>.</summary>
    public static AipCorrection? Attiva(AipCorrectionFile file, string chiave) => Attiva(file, CorrectionKind.Airspace, chiave);

    /// <summary>
    /// La correzione in piedi che ha fatto la voce del modello di quel tipo con quella chiave: una <c>change</c> o una
    /// <c>add</c> il cui <c>after</c> è quella voce. Null se la voce è com'è nel PDF.
    /// </summary>
    public static AipCorrection? Attiva(AipCorrectionFile file, CorrectionKind tipo, string chiave)
    {
        ArgumentNullException.ThrowIfNull(file);
        return file.Corrections.LastOrDefault(c => c.Superseded is null && c.Kind == tipo
                                                   && c.Op != CorrectionOp.Remove && c.After is { } dopo && ChiaveDi(dopo, tipo) == chiave);
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

    /// <summary>«Correggi» un'area: vedi <see cref="Correggi(AipDocument, AipCorrectionFile, CorrectionKind, string, object, string?, string?, string, DateOnly)"/>.</summary>
    public static (CorrezioneFatta? Fatta, string? Perche) Correggi(AipDocument modello, AipCorrectionFile file, string chiave, Airspace nuova,
        string? perche, string? fonte, string ciclo, DateOnly oggi) =>
        Correggi(modello, file, CorrectionKind.Airspace, chiave, nuova, perche, fonte, ciclo, oggi);

    /// <summary>«Correggi»: la voce del modello di quel tipo con quella chiave diventa <paramref name="nuova"/>.</summary>
    public static (CorrezioneFatta? Fatta, string? Perche) Correggi(AipDocument modello, AipCorrectionFile file, CorrectionKind tipo, string chiave, object nuova,
        string? perche, string? fonte, string ciclo, DateOnly oggi)
    {
        ArgumentNullException.ThrowIfNull(modello);
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(nuova);
        if (Mancano(perche, fonte) is { } manca) return (null, manca);
        var voci = Voci(modello, tipo);
        if (Indice(voci, chiave) is not { } indice) return (null, $"{chiave}: non è nell'AIP espanso del ciclo {ciclo}.");
        var chiaveNuova = AipCorrections.KeyOf(nuova);
        if (chiaveNuova != chiave && Indice(voci, chiaveNuova) is not null)
            return (null, $"C'è già {Nome(tipo)} {chiaveNuova} nell'AIP espanso: correggi quella.");

        var correzioni = file.Corrections.ToList();
        var attiva = Attiva(file, tipo, chiave);
        var id = NuovoId(file, oggi);
        AipCorrection correzione;
        if (attiva is { Op: CorrectionOp.Add })
            correzione = Nuova(id, tipo, chiaveNuova, CorrectionOp.Add, null, AipCorrections.ToNode(nuova), perche!, fonte!, ciclo);
        else
            correzione = Nuova(id, tipo, attiva?.Key ?? chiave, CorrectionOp.Change,
                attiva?.Before?.DeepClone().AsObject() ?? AipCorrections.ToNode(voci[indice]), AipCorrections.ToNode(nuova), perche!, fonte!, ciclo);
        Supera(correzioni, attiva, id);
        correzioni.Add(correzione);

        voci[indice] = nuova;
        return (Fatta(modello, tipo, voci, file, correzioni, $"Correzione dell'AIP espanso: {chiave}", perche!, fonte!), null);
    }

    /// <summary>«Aggiungi un'area»: vedi <see cref="Aggiungi(AipDocument, AipCorrectionFile, CorrectionKind, object, string?, string?, string, DateOnly)"/>.</summary>
    public static (CorrezioneFatta? Fatta, string? Perche) Aggiungi(AipDocument modello, AipCorrectionFile file, Airspace nuova,
        string? perche, string? fonte, string ciclo, DateOnly oggi) =>
        Aggiungi(modello, file, CorrectionKind.Airspace, nuova, perche, fonte, ciclo, oggi);

    /// <summary>«Aggiungi»: una voce che il PDF non ha (un'altra fonte: AIP SUP, AIC…).</summary>
    public static (CorrezioneFatta? Fatta, string? Perche) Aggiungi(AipDocument modello, AipCorrectionFile file, CorrectionKind tipo, object nuova,
        string? perche, string? fonte, string ciclo, DateOnly oggi)
    {
        ArgumentNullException.ThrowIfNull(modello);
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(nuova);
        if (Mancano(perche, fonte) is { } manca) return (null, manca);
        var voci = Voci(modello, tipo);
        var chiave = AipCorrections.KeyOf(nuova);
        if (Indice(voci, chiave) is not null)
            return (null, $"C'è già {Nome(tipo)} {chiave} nell'AIP espanso: correggi quella.");
        if (file.Corrections.Any(c => c.Superseded is null && c.Kind == tipo && c.Op == CorrectionOp.Remove && c.Key == chiave))
            return (null, $"{chiave} è stata tolta da una correzione: ritira quella, se va rimessa com'è nel PDF.");

        var id = NuovoId(file, oggi);
        var correzioni = file.Corrections.ToList();
        correzioni.Add(Nuova(id, tipo, chiave, CorrectionOp.Add, null, AipCorrections.ToNode(nuova), perche!, fonte!, ciclo));
        voci.Add(nuova);
        // Segnalazioni 2611, passo 7: una radioassistenza aggiunta (una stazione straniera, AJO) serve alla prosa delle procedure,
        // che si legge con «Rileggi».
        var fatta = Fatta(modello, tipo, voci, file, correzioni, $"Aggiunta all'AIP espanso: {chiave}", perche!, fonte!);
        return (tipo == CorrectionKind.Navaid ? fatta with { Messaggio = fatta.Messaggio + ". Le procedure che la nominano si leggono al prossimo «Rileggi»." } : fatta, null);
    }

    /// <summary>«Togli» un'area: vedi <see cref="Togli(AipDocument, AipCorrectionFile, CorrectionKind, string, string?, string?, string, DateOnly)"/>.</summary>
    public static (CorrezioneFatta? Fatta, string? Perche) Togli(AipDocument modello, AipCorrectionFile file, string chiave,
        string? perche, string? fonte, string ciclo, DateOnly oggi) =>
        Togli(modello, file, CorrectionKind.Airspace, chiave, perche, fonte, ciclo, oggi);

    /// <summary>
    /// «Togli»: la voce esce dall'AIP espanso. Una voce che c'era solo per un'aggiunta non ha bisogno di una rimozione: si
    /// ritira l'aggiunta.
    /// </summary>
    public static (CorrezioneFatta? Fatta, string? Perche) Togli(AipDocument modello, AipCorrectionFile file, CorrectionKind tipo, string chiave,
        string? perche, string? fonte, string ciclo, DateOnly oggi)
    {
        ArgumentNullException.ThrowIfNull(modello);
        ArgumentNullException.ThrowIfNull(file);
        var attiva = Attiva(file, tipo, chiave);
        if (attiva is { Op: CorrectionOp.Add })
            return Ritira(modello, file, attiva.Id);
        if (Mancano(perche, fonte) is { } manca) return (null, manca);
        var voci = Voci(modello, tipo);
        if (Indice(voci, chiave) is not { } indice) return (null, $"{chiave}: non è nell'AIP espanso del ciclo {ciclo}.");

        var id = NuovoId(file, oggi);
        var correzioni = file.Corrections.ToList();
        Supera(correzioni, attiva, id);
        correzioni.Add(Nuova(id, tipo, attiva?.Key ?? chiave, CorrectionOp.Remove,
            attiva?.Before?.DeepClone().AsObject() ?? AipCorrections.ToNode(voci[indice]), null, perche!, fonte!, ciclo));
        voci.RemoveAt(indice);
        return (Fatta(modello, tipo, voci, file, correzioni, $"Rimozione dall'AIP espanso: {chiave}", perche!, fonte!), null);
    }

    /// <summary>
    /// «Ritira»: la correzione non vale più (<c>superseded: ritirata</c>) e la voce torna com'è nel PDF. Una voce tolta torna in
    /// fondo al suo elenco: al suo posto la rimette la prossima «Leggi».
    /// </summary>
    public static (CorrezioneFatta? Fatta, string? Perche) Ritira(AipDocument modello, AipCorrectionFile file, string id)
    {
        ArgumentNullException.ThrowIfNull(modello);
        ArgumentNullException.ThrowIfNull(file);
        var indiceNelFile = file.Corrections.ToList().FindIndex(c => c.Id == id && c.Superseded is null);
        if (indiceNelFile < 0) return (null, $"La correzione {id} non c'è, o non è più in piedi.");
        var c = file.Corrections[indiceNelFile];
        var nonDice = $"La correzione {id} non dice com'era la voce nel PDF: rileggi i PDF («Leggi»), poi riprova.";

        var voci = Voci(modello, c.Kind);
        switch (c.Op)
        {
            case CorrectionOp.Add:
            case CorrectionOp.Change:
                var dopo = c.After is { } a ? ChiaveDi(a, c.Kind) : null;
                if (dopo is null || Indice(voci, dopo) is not { } dove)
                    return (null, $"{dopo ?? c.Key}: non è nell'AIP espanso così com'è scritto. Rileggi i PDF («Leggi»), poi riprova.");
                if (c.Op == CorrectionOp.Add)
                    voci.RemoveAt(dove);
                else if (Leggi(c.Before, c.Kind) is { } delPdf)
                    voci[dove] = delPdf;
                else
                    return (null, nonDice);
                break;
            case CorrectionOp.Remove:
                if (Leggi(c.Before, c.Kind) is not { } tolta)
                    return (null, nonDice);
                voci.Add(tolta);
                break;
        }

        var correzioni = file.Corrections.ToList();
        correzioni[indiceNelFile] = c with { Superseded = Ritirata };
        return (Fatta(modello, c.Kind, voci, file, correzioni, $"Ritirata la correzione {id} ({c.Key})", null, null), null);
    }

    /// <summary>Il tipo del modello di ogni genere di correzione.</summary>
    public static Type Tipo(CorrectionKind tipo) => tipo switch
    {
        CorrectionKind.Airspace => typeof(Airspace),
        CorrectionKind.Route => typeof(Route),
        CorrectionKind.Holding => typeof(Holding),
        CorrectionKind.Navaid => typeof(Navaid),
        CorrectionKind.Point => typeof(SignificantPoint),
        CorrectionKind.Light => typeof(GroundLight),
        CorrectionKind.Aerodrome => typeof(Aerodrome),
        CorrectionKind.Hazard => typeof(Hazard),
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Le carte ATC SMAC non si correggono dal Lab."),
    };

    /// <summary>Come si chiama una voce di quel tipo, in una frase: «un'area», «un punto».</summary>
    public static string Nome(CorrectionKind tipo) => tipo switch
    {
        CorrectionKind.Airspace => "un'area",
        CorrectionKind.Route => "una rotta",
        CorrectionKind.Holding => "un'attesa",
        CorrectionKind.Navaid => "una radioassistenza",
        CorrectionKind.Point => "un punto",
        CorrectionKind.Light => "una luce",
        CorrectionKind.Aerodrome => "uno scalo",
        CorrectionKind.Hazard => "un pericolo",
        _ => "una voce",
    };

    /// <summary>Le voci del modello di quel tipo, in una lista nuova da cambiare.</summary>
    public static List<object> Voci(AipDocument modello, CorrectionKind tipo)
    {
        ArgumentNullException.ThrowIfNull(modello);
        IEnumerable<object>? voci = tipo switch
        {
            CorrectionKind.Airspace => modello.Airspaces,
            CorrectionKind.Route => modello.Routes,
            CorrectionKind.Holding => modello.Holdings,
            CorrectionKind.Navaid => modello.Navaids,
            CorrectionKind.Point => modello.Points,
            CorrectionKind.Light => modello.Lights,
            CorrectionKind.Aerodrome => modello.Aerodromes,
            CorrectionKind.Hazard => modello.Hazards,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Le carte ATC SMAC non si correggono dal Lab."),
        };
        return [.. voci ?? []];
    }

    private static AipDocument ConVoci(AipDocument modello, CorrectionKind tipo, List<object> voci)
    {
        // Come «Leggi»: un elenco facoltativo vuoto non si scrive.
        IReadOnlyList<T>? Lista<T>() => voci.Count == 0 ? null : [.. voci.Cast<T>()];
        return tipo switch
        {
            CorrectionKind.Airspace => modello with { Airspaces = [.. voci.Cast<Airspace>()] },
            CorrectionKind.Route => modello with { Routes = Lista<Route>() },
            CorrectionKind.Holding => modello with { Holdings = Lista<Holding>() },
            CorrectionKind.Navaid => modello with { Navaids = Lista<Navaid>() },
            CorrectionKind.Point => modello with { Points = Lista<SignificantPoint>() },
            CorrectionKind.Light => modello with { Lights = Lista<GroundLight>() },
            CorrectionKind.Aerodrome => modello with { Aerodromes = Lista<Aerodrome>() },
            CorrectionKind.Hazard => modello with { Hazards = Lista<Hazard>() },
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
        };
    }

    private static CorrezioneFatta Fatta(AipDocument modello, CorrectionKind tipo, List<object> voci, AipCorrectionFile file, List<AipCorrection> correzioni,
        string cosa, string? perche, string? fonte) =>
        new(ConVoci(modello, tipo, voci), file with { Corrections = correzioni },
            perche is null ? cosa : $"{cosa} — {perche.Trim()} ({fonte!.Trim()})");

    private static AipCorrection Nuova(string id, CorrectionKind tipo, string chiave, CorrectionOp op, System.Text.Json.Nodes.JsonObject? prima,
        System.Text.Json.Nodes.JsonObject? dopo, string perche, string fonte, string ciclo) => new()
    {
        Id = id,
        Kind = tipo,
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

    private static int? Indice(List<object> voci, string chiave)
    {
        var i = voci.FindIndex(v => AipCorrections.KeyOf(v) == chiave);
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

    /// <summary>La voce del JSON di una correzione, o null se non si legge come quel tipo.</summary>
    public static object? Leggi(System.Text.Json.Nodes.JsonObject? nodo, CorrectionKind tipo)
    {
        if (nodo is null) return null;
        try
        {
            return nodo.Deserialize(Tipo(tipo), AipJson.Options);
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string? ChiaveDi(System.Text.Json.Nodes.JsonObject voce, CorrectionKind tipo) =>
        Leggi(voce, tipo) is { } letta ? AipCorrections.KeyOf(letta) : null;

    private static string? Pulito(string? testo) => string.IsNullOrWhiteSpace(testo) ? null : testo.Trim();
}
