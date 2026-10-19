using Aip;
using Aip.Pdf.Italia;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>Un problema dell'import, col file da cui viene.</summary>
public sealed record ProblemaDellImport(string File, ImportIssue Problema);

/// <summary>
/// Com'è andata «Leggi nell'AIP espanso»: dove è scritto, quanti PDF c'erano e quanti si sanno già leggere, le aree, le
/// date delle pagine lette, e quel che l'importer ha da dire.
/// </summary>
public sealed record EsitoDellAipEspanso(
    string Ciclo,
    string Scritto,
    int Pdf,
    IReadOnlyList<string> Letti,
    IReadOnlyList<string> NonAncora,
    int Aree,
    IReadOnlyList<(DateOnly? Data, int Pagine)> Date,
    IReadOnlyList<ProblemaDellImport> Problemi,
    IReadOnlyList<string> Illeggibili)
{
    /// <summary>Le rotte di ENR 3 lette (slice 2).</summary>
    public int Rotte { get; init; }

    /// <summary>Le attese di ENR 3.4.1 lette (slice 2).</summary>
    public int Attese { get; init; }

    /// <summary>Le radioassistenze di ENR 4.1 lette (slice 3).</summary>
    public int Radioassistenze { get; init; }

    /// <summary>I punti significativi di ENR 4.4 letti (slice 3).</summary>
    public int Punti { get; init; }

    /// <summary>Le luci di ENR 4.5 lette (slice 3).</summary>
    public int Luci { get; init; }

    /// <summary>Gli scali di AD 2 letti (slice 5).</summary>
    public int Scali { get; init; }

    /// <summary>I pericoli puntuali di ENR 5.3 letti.</summary>
    public int Pericoli { get; init; }

    /// <summary>I settori delle carte ATC SMAC di AD 2 letti.</summary>
    public int SettoriSmac { get; init; }

    /// <summary>Com'è andata ogni correzione in piedi del file delle correzioni (vuoto se il file non c'è).</summary>
    public IReadOnlyList<CorrectionOutcome> Correzioni { get; init; } = [];

    /// <summary>
    /// Le correzioni in parole: «3 applicate · superate dal PDF: LI R107A · non vanno: 2026-10-19-2 ABRUS (manca la fonte)»;
    /// null se non ce n'erano.
    /// </summary>
    public string? CorrezioniInParole()
    {
        if (Correzioni.Count == 0)
            return null;
        var applicate = Correzioni.Count(c => c.Outcome == CorrectionOutcomeKind.Applied);
        var parti = new List<string> { applicate == 1 ? "1 applicata" : $"{applicate} applicate" };
        var dalPdf = Correzioni.Where(c => c.Outcome == CorrectionOutcomeKind.SupersededByAip).Select(c => c.Key).ToList();
        if (dalPdf.Count > 0)
            parti.Add("superate dal PDF: " + string.Join(", ", dalPdf));
        var daAltre = Correzioni.Where(c => c.Outcome == CorrectionOutcomeKind.SupersededByCorrection).Select(c => $"{c.Id} (dalla {c.SupersededBy})").ToList();
        if (daAltre.Count > 0)
            parti.Add("superate da un'altra correzione: " + string.Join(", ", daAltre));
        var nonVanno = Correzioni.Where(c => c.Outcome == CorrectionOutcomeKind.Invalid).Select(c => $"{c.Id} {c.Key} ({Perche(c.Problem)})").ToList();
        if (nonVanno.Count > 0)
            parti.Add("non vanno: " + string.Join(", ", nonVanno));
        return string.Join(" · ", parti);
    }

    private static string Perche(CorrectionProblem? problema) => problema switch
    {
        CorrectionProblem.MissingReason => "manca il perché",
        CorrectionProblem.MissingSource => "manca la fonte",
        CorrectionProblem.MissingAfter => "manca l'oggetto corretto",
        CorrectionProblem.UnreadableAfter => "l'oggetto corretto non si legge",
        CorrectionProblem.DuplicateKey => "il PDF ha più oggetti con quella chiave",
        CorrectionProblem.NotInAip => "il PDF non ha quell'oggetto",
        CorrectionProblem.DuplicateId => "id ripetuto",
        _ => "?",
    };

    /// <summary>Quel che si è letto, in parole: «870 aree, 29 rotte, 413 attese, 55 radioassistenze…» (le voci a zero no).</summary>
    public string Quanti() => string.Join(", ", new[]
    {
        $"{Aree} aree",
        Rotte > 0 ? $"{Rotte} rotte" : null,
        Attese > 0 ? $"{Attese} attese" : null,
        Radioassistenze > 0 ? $"{Radioassistenze} radioassistenze" : null,
        Punti > 0 ? $"{Punti} punti" : null,
        Luci > 0 ? $"{Luci} luci" : null,
        Scali > 0 ? $"{Scali} scali" : null,
        Pericoli > 0 ? $"{Pericoli} pericoli" : null,
        SettoriSmac > 0 ? $"{SettoriSmac} settori SMAC" : null,
    }.OfType<string>());
}

/// <summary>
/// L'AIP espanso (carta F6 §3, D5): tutto quel che il Lab importa dall'AIP — quel che il sector ha e quel che non ha —
/// nel modello, in una cartella <b>sua</b>, una sottocartella per ciclo AIRAC (<c>2611\li.json</c>). Domani la
/// cartella è un repo a sé (privato, committente), e il Lab ci scrive come nel sector.
/// <para>Si scrive per intero a ogni lettura: il file di un ciclo è quel che i PDF di quel ciclo dicono, più le correzioni
/// in piedi di <c>correzioni\li.json</c> (carta «Le correzioni dell'AIP espanso»): errori dell'AIP e aggiunte da altre
/// fonti, che durano da un ciclo all'altro finché un PDF o un'altra correzione non le supera.</para>
/// </summary>
public static class AipEspanso
{
    /// <summary>Il file del modello di uno Stato dentro la cartella del ciclo.</summary>
    public static string File(string espanso, string ciclo, string stato = ItalianAip.State) =>
        Path.Combine(espanso, ciclo, stato.ToLowerInvariant() + ".json");

    /// <summary>La cartella delle correzioni dentro l'AIP espanso.</summary>
    public const string CartellaDelleCorrezioni = "correzioni";

    /// <summary>Il file delle correzioni di uno Stato: fuori dalle cartelle dei cicli, perché vale per tutti.</summary>
    public static string FileDelleCorrezioni(string espanso, string stato = ItalianAip.State) =>
        Path.Combine(espanso, CartellaDelleCorrezioni, stato.ToLowerInvariant() + ".json");

    /// <summary>Perché quella cartella non può fare da AIP espanso, o null.</summary>
    public static string? PercheNonVa(string? cartella, string? cartellaDeiPdf)
    {
        if (string.IsNullOrWhiteSpace(cartella))
            return "Manca la cartella dell'AIP espanso: sceglila nelle Impostazioni.";
        if (!Path.IsPathFullyQualified(cartella.Trim()))
            return "La cartella dell'AIP espanso va scritta per intero (D:\\…\\aip-espanso).";
        if (cartellaDeiPdf is not null && Path.IsPathFullyQualified(cartellaDeiPdf)
            && string.Equals(Path.GetFullPath(cartella.Trim()).TrimEnd('\\', '/'), Path.GetFullPath(cartellaDeiPdf).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            return "L'AIP espanso non sta nella cartella dei PDF: i PDF non vanno nel repo (peso, copyright ENAV).";
        return null;
    }

    /// <summary>
    /// Legge i PDF, applica le correzioni e scrive il modello del ciclo. Un PDF che non si apre non ferma gli altri: si dice.
    /// 🔴 Un file delle correzioni che non si legge ferma tutto (<see cref="InvalidDataException"/>): un modello scritto
    /// senza le correzioni le toglierebbe in silenzio alla registrazione dopo.
    /// </summary>
    public static EsitoDellAipEspanso Leggi(IReadOnlyList<string> pdf, string espanso, string ciclo)
    {
        ArgumentException.ThrowIfNullOrEmpty(espanso);
        if (!CicloAirac.Valido(ciclo))
            throw new ArgumentException($"Ciclo AIRAC non valido: «{ciclo}».", nameof(ciclo));

        var aree = new List<Airspace>();
        var rotte = new List<Route>();
        var attese = new List<Holding>();
        var radioassistenze = new List<Navaid>();
        var punti = new List<SignificantPoint>();
        var luci = new List<GroundLight>();
        var scali = new List<Aerodrome>();
        var pericoli = new List<Hazard>();
        var smac = new List<MinimumAltitudeArea>();
        var letti = new List<string>();
        var nonAncora = new List<string>();
        var illeggibili = new List<string>();
        var problemi = new List<ProblemaDellImport>();
        var pagine = new List<ImportPage>();

        // Si legge in ordine di nome — ENR 4 prima di ENR 5, che ne usa i punti (le AAR di ENR 5.2.2.1) —, ma il modello si
        // compone nell'ordine dei PDF di sempre, così le aree di un file nuovo si aggiungono dove sono sempre state e il resto
        // dell'AIP espanso in git non si sposta.
        var giaLette = new List<Navaid>();
        var puntiLetti = new List<SignificantPoint>();
        var risolvi = ItalianEnr5Zone.Risolutore(giaLette, puntiLetti);
        var perFile = new Dictionary<string, ImportResult>(StringComparer.Ordinal);
        foreach (var file in pdf.OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            try
            {
                var letto = ItalianAip.ReadFile(file, risolvi);
                perFile[file] = letto;
                giaLette.AddRange(letto.Navaids);
                puntiLetti.AddRange(letto.Points);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                illeggibili.Add($"{Path.GetFileName(file)}: {e.Message}");
            }
        }

        foreach (var file in pdf)
        {
            var nome = Path.GetFileName(file);
            if (!perFile.TryGetValue(file, out var letto)) continue;
            if (letto.Issues.Any(p => p.Kind == ImportIssueKind.Unsupported))
            {
                nonAncora.Add(nome);
                continue;
            }

            letti.Add(nome);
            aree.AddRange(letto.Airspaces);
            rotte.AddRange(letto.Routes);
            attese.AddRange(letto.Holdings);
            radioassistenze.AddRange(letto.Navaids);
            punti.AddRange(letto.Points);
            luci.AddRange(letto.Lights);
            scali.AddRange(letto.Aerodromes);
            pericoli.AddRange(letto.Hazards);
            smac.AddRange(letto.MinimumAltitudes);
            pagine.AddRange(letto.Pages);
            problemi.AddRange(letto.Issues.Select(p => new ProblemaDellImport(nome, p)));
        }

        var documento = new AipDocument
        {
            State = ItalianAip.State,
            Airac = ciclo,
            Effective = CicloInVigore(ciclo),
            Airspaces = aree,
            Routes = rotte.Count == 0 ? null : rotte,
            Holdings = attese.Count == 0 ? null : attese,
            Navaids = radioassistenze.Count == 0 ? null : radioassistenze,
            Points = punti.Count == 0 ? null : punti,
            Lights = luci.Count == 0 ? null : luci,
            Aerodromes = scali.Count == 0 ? null : scali,
            Hazards = pericoli.Count == 0 ? null : pericoli,
            MinimumAltitudes = smac.Count == 0 ? null : smac,
        };

        IReadOnlyList<CorrectionOutcome> correzioni = [];
        var fileDelleCorrezioni = FileDelleCorrezioni(espanso);
        if (System.IO.File.Exists(fileDelleCorrezioni))
        {
            AipCorrectionFile lette;
            try
            {
                lette = AipCorrections.Deserialize(System.IO.File.ReadAllText(fileDelleCorrezioni));
            }
            catch (AipJsonException e)
            {
                throw new InvalidDataException($"Il file delle correzioni ({fileDelleCorrezioni}) non si legge ({e.Message}): l'AIP espanso non è stato scritto.", e);
            }

            if (!string.Equals(lette.State, documento.State, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Il file delle correzioni ({fileDelleCorrezioni}) è dello Stato «{lette.State}», non «{documento.State}»: l'AIP espanso non è stato scritto.");

            var applicate = AipCorrections.Apply(documento, lette);
            documento = applicate.Document;
            correzioni = applicate.Outcomes;
            if (applicate.FileChanged)
                ScriviAlPosto(fileDelleCorrezioni, flusso => flusso.Write(System.Text.Encoding.UTF8.GetBytes(AipCorrections.Serialize(applicate.File))));
        }

        var scritto = File(espanso, ciclo);
        ScriviAlPosto(scritto, flusso => AipJson.Serialize(flusso, documento));

        var date = pagine.GroupBy(p => p.Effective).OrderBy(g => g.Key).Select(g => (g.Key, g.Count())).ToList();
        return new EsitoDellAipEspanso(ciclo, scritto, pdf.Count, letti, nonAncora, aree.Count, date, problemi, illeggibili)
        {
            Rotte = rotte.Count,
            Attese = attese.Count,
            Radioassistenze = radioassistenze.Count,
            Punti = punti.Count,
            Luci = luci.Count,
            Scali = scali.Count,
            Pericoli = pericoli.Count,
            SettoriSmac = smac.Count,
            Correzioni = correzioni,
        };
    }

    // Prima in un file accanto, poi al posto del vecchio: un'interruzione a metà non lascia un file rotto.
    internal static void ScriviAlPosto(string percorso, Action<Stream> scrivi)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
        var provvisorio = percorso + ".scrivendo";
        using (var flusso = System.IO.File.Create(provvisorio))
            scrivi(flusso);
        System.IO.File.Move(provvisorio, percorso, overwrite: true);
    }

    /// <summary>La data di entrata in vigore del ciclo <c>AACC</c>.</summary>
    public static DateOnly CicloInVigore(string ciclo)
    {
        var anno = 2000 + int.Parse(ciclo.AsSpan(0, 2), System.Globalization.CultureInfo.InvariantCulture);
        var numero = int.Parse(ciclo.AsSpan(2), System.Globalization.CultureInfo.InvariantCulture);
        var primo = CicloAirac.Di(new DateOnly(anno, 1, 1));
        var inVigore = primo.InVigore.Year < anno ? primo.InVigore.AddDays(28) : primo.InVigore;
        return inVigore.AddDays(28 * (numero - 1));
    }
}
