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

    /// <summary>Quel che si è letto, in parole: «870 aree, 29 rotte, 413 attese, 55 radioassistenze…» (le voci a zero no).</summary>
    public string Quanti() => string.Join(", ", new[]
    {
        $"{Aree} aree",
        Rotte > 0 ? $"{Rotte} rotte" : null,
        Attese > 0 ? $"{Attese} attese" : null,
        Radioassistenze > 0 ? $"{Radioassistenze} radioassistenze" : null,
        Punti > 0 ? $"{Punti} punti" : null,
        Luci > 0 ? $"{Luci} luci" : null,
    }.OfType<string>());
}

/// <summary>
/// L'AIP espanso (carta F6 §3, D5): tutto quel che il Lab importa dall'AIP — quel che il sector ha e quel che non ha —
/// nel modello, in una cartella <b>sua</b>, una sottocartella per ciclo AIRAC (<c>2611\li.json</c>). Domani la
/// cartella è un repo a sé (privato, committente), e il Lab ci scrive come nel sector.
/// <para>Si scrive per intero a ogni lettura: il file di un ciclo è quel che i PDF di quel ciclo dicono, niente di più.
/// Le modifiche dell'AOD alle note sono «da vedere dopo» (carta F6 §6).</para>
/// </summary>
public static class AipEspanso
{
    /// <summary>Il file del modello di uno Stato dentro la cartella del ciclo.</summary>
    public static string File(string espanso, string ciclo, string stato = ItalianAip.State) =>
        Path.Combine(espanso, ciclo, stato.ToLowerInvariant() + ".json");

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

    /// <summary>Legge i PDF e scrive il modello del ciclo. Un PDF che non si apre non ferma gli altri: si dice.</summary>
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
        var letti = new List<string>();
        var nonAncora = new List<string>();
        var illeggibili = new List<string>();
        var problemi = new List<ProblemaDellImport>();
        var pagine = new List<ImportPage>();

        foreach (var file in pdf)
        {
            var nome = Path.GetFileName(file);
            ImportResult letto;
            try
            {
                letto = ItalianAip.ReadFile(file);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                illeggibili.Add($"{nome}: {e.Message}");
                continue;
            }

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
        };

        var scritto = File(espanso, ciclo);
        Directory.CreateDirectory(Path.GetDirectoryName(scritto)!);
        // Prima in un file accanto, poi al posto del vecchio: un'interruzione a metà non lascia un modello rotto.
        var provvisorio = scritto + ".scrivendo";
        using (var flusso = System.IO.File.Create(provvisorio))
            AipJson.Serialize(flusso, documento);
        System.IO.File.Move(provvisorio, scritto, overwrite: true);

        var date = pagine.GroupBy(p => p.Effective).OrderBy(g => g.Key).Select(g => (g.Key, g.Count())).ToList();
        return new EsitoDellAipEspanso(ciclo, scritto, pdf.Count, letti, nonAncora, aree.Count, date, problemi, illeggibili)
        {
            Rotte = rotte.Count,
            Attese = attese.Count,
            Radioassistenze = radioassistenze.Count,
            Punti = punti.Count,
            Luci = luci.Count,
        };
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
