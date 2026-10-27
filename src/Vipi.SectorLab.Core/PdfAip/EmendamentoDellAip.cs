using System.Globalization;
using Aip.Pdf;
using Aip.Pdf.Italia;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Com'è andato «Applica l'emendamento». <c>Bloccanti</c> non vuoto = non si è scritto niente, e dice perché. Le pagine
/// rispetto al GEN 0.4 dell'emendamento (le parti che teniamo): <c>Mancano</c> nella cartella nuova, con una
/// <c>DataDiversa</c>, <c>InPiu</c>.
/// </summary>
public sealed record EsitoDellEmendamento(
    string? Numero,
    DateOnly? InVigore,
    string? CicloPrima,
    string? CicloNuovo,
    string? Cartella,
    IReadOnlyList<string> Bloccanti)
{
    public bool Fatto => Bloccanti.Count == 0;

    public int FileRiscritti { get; init; }

    public int FileCopiati { get; init; }

    public IReadOnlyList<string> FileNuovi { get; init; } = [];

    public IReadOnlyList<string> FileTolti { get; init; } = [];

    public int PagineTolte { get; init; }

    public int PagineMesse { get; init; }

    /// <summary>Le pagine dell'emendamento delle parti che non teniamo (GEN, ENR 0, ENR 1, AD 0, AD 1): non si toccano.</summary>
    public int PagineFuoriDalleParti { get; init; }

    /// <summary>Le pagine da togliere di sezioni che nella cartella non ci sono (mai scaricate).</summary>
    public IReadOnlyList<string> SezioniNonScaricate { get; init; } = [];

    /// <summary>
    /// Le sezioni che la cartella non ha e di cui l'emendamento porta solo alcune pagine: non si scrivono (un file a buchi
    /// ingannerebbe); le loro pagine restano fra quelle che mancano.
    /// </summary>
    public IReadOnlyList<string> SezioniNuoveNonScritte { get; init; } = [];

    /// <summary>Vero se l'emendamento aveva il GEN 0.4 e il controllo si è fatto.</summary>
    public bool Controllato { get; init; }

    public IReadOnlyList<string> Mancano { get; init; } = [];

    public IReadOnlyList<string> DataDiversa { get; init; } = [];

    public IReadOnlyList<string> InPiu { get; init; } = [];

    /// <summary>Il PDF dell'emendamento copiato nella cartella del ciclo nuovo (D2).</summary>
    public string? CopiaDellEmendamento { get; init; }
}

/// <summary>Le fasi di «Applica l'emendamento», nell'ordine.</summary>
public enum FaseDellEmendamento
{
    LeggeLEmendamento,
    LeggeIlCicloPrima,
    ScriveIlCicloNuovo,
}

/// <summary>A che punto è l'emendamento: la fase, quanti file fatti su quanti nella fase, quello in mano.</summary>
public sealed record AvanzamentoDellEmendamento(FaseDellEmendamento Fase, int Fatti, int Tutti, string File)
{
    /// <summary>La fase in parole, per la pagina: «Leggo i PDF del ciclo prima».</summary>
    public string Cosa => Fase switch
    {
        FaseDellEmendamento.LeggeLEmendamento => "Leggo l'emendamento",
        FaseDellEmendamento.LeggeIlCicloPrima => "Leggo i PDF del ciclo prima",
        _ => "Scrivo i PDF del ciclo nuovo",
    };
}

/// <summary>
/// L'emendamento AIRAC applicato alla cartella dei PDF (carta «L'AIP intero e gli emendamenti», passo 7). L'AOD scarica
/// solo il PDF dell'AMDT; il Lab crea <c>AIP &lt;ciclo nuovo&gt;</c> accanto a <c>AIP &lt;ciclo prima&gt;</c>, che non tocca:
/// toglie le pagine da distruggere, mette le nuove al loro posto, crea i file delle sezioni nuove, toglie quelli rimasti
/// vuoti, copia l'emendamento in <c>AMDT\</c> e controlla la cartella col GEN 0.4.
/// <para>Si ferma prima di scrivere se l'emendamento non si legge per intero o se le pagine da togliere non sono quelle che
/// la cartella ha (un emendamento saltato, o la cartella sbagliata).</para>
/// </summary>
public static class EmendamentoDellAip
{
    /// <summary>Le parti dell'AIP che il Lab tiene (carta §2): il resto dell'emendamento non si tocca.</summary>
    public static readonly IReadOnlyList<string> PartiTenute = ["ENR 2", "ENR 3", "ENR 4", "ENR 5", "ENR 6", "AD 2", "AD 3"];

    /// <summary>Vero se la pagina (o la sezione) è di una parte che il Lab tiene.</summary>
    public static bool Tenuta(string riferimento) =>
        PartiTenute.Any(p => riferimento.StartsWith(p, StringComparison.Ordinal)
                             && (riferimento.Length == p.Length || riferimento[p.Length] is '.' or ' ' or '-'));

    /// <summary>La cartella dei PDF di un ciclo: <c>&lt;cartella dei PDF&gt;\AIP 2611</c>.</summary>
    public static string CartellaDelCiclo(string cartellaDeiPdf, string ciclo) => Path.Combine(cartellaDeiPdf, "AIP " + ciclo);

    /// <summary>
    /// Dove leggere i PDF di un ciclo: la sua cartella se c'è; se la cartella dei PDF non ha cartelle di ciclo, tutta la
    /// cartella (come prima dei cicli); se ne ha altre ma non questa, null (si leggerebbero i PDF di un altro ciclo).
    /// </summary>
    public static string? CartellaDaLeggere(string cartellaDeiPdf, string ciclo)
    {
        var delCiclo = CartellaDelCiclo(cartellaDeiPdf, ciclo);
        if (Directory.Exists(delCiclo))
            return delCiclo;
        return CicliNellaCartella(cartellaDeiPdf).Count == 0 ? cartellaDeiPdf : null;
    }

    /// <summary>I cicli che hanno la loro cartella dentro la cartella dei PDF.</summary>
    public static IReadOnlyList<string> CicliNellaCartella(string cartellaDeiPdf) =>
        Directory.Exists(cartellaDeiPdf)
            ? Directory.EnumerateDirectories(cartellaDeiPdf, "AIP *").Select(d => Path.GetFileName(d)[4..])
                .Where(CicloAirac.Valido).Order(StringComparer.Ordinal).ToList()
            : [];

    /// <summary>
    /// Applica l'emendamento <paramref name="amdt"/> alla cartella dei PDF. <paramref name="avanzamento"/> (committente,
    /// prova A45: «una barra di progressione anche sull'emendamento, come quando si legge un AIP») riceve la fase e quanti
    /// file ha fatto: legge l'emendamento, legge i PDF del ciclo prima (il grosso del tempo), scrive quelli del ciclo nuovo.
    /// </summary>
    public static EsitoDellEmendamento Applica(string cartellaDeiPdf, string amdt, IProgress<AvanzamentoDellEmendamento>? avanzamento = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(cartellaDeiPdf);
        ArgumentException.ThrowIfNullOrEmpty(amdt);
        avanzamento?.Report(new AvanzamentoDellEmendamento(FaseDellEmendamento.LeggeLEmendamento, 0, 1, Path.GetFileName(amdt)));
        var letto = ItalianAmendment.Read(PdfText.Read(amdt));
        var bloccanti = letto.Issues
            .Where(p => p.Kind != AmendmentIssueKind.NoChecklist)
            .Select(p => $"L'emendamento non si legge per intero: {Problema(p)}")
            .ToList();
        if (letto.Effective is not { } inVigore)
            return new EsitoDellEmendamento(letto.Number, null, null, null, null, bloccanti);

        var (cicloNuovo, dataDelCiclo) = CicloAirac.Di(inVigore);
        if (dataDelCiclo != inVigore)
            bloccanti.Add($"Il {Giorno(inVigore)} non è l'inizio di un ciclo AIRAC: non è un emendamento AIRAC.");
        var cicloPrima = CicloAirac.Di(inVigore.AddDays(-1)).Nome;
        var sorgente = CartellaDelCiclo(cartellaDeiPdf, cicloPrima);
        var destinazione = CartellaDelCiclo(cartellaDeiPdf, cicloNuovo);
        var esito = new EsitoDellEmendamento(letto.Number, inVigore, cicloPrima, cicloNuovo, destinazione, bloccanti);
        if (!Directory.Exists(sorgente))
            bloccanti.Add($"L'emendamento va applicato al {cicloPrima}, e la cartella «AIP {cicloPrima}» non c'è: i PDF del {cicloPrima} vanno lì.");
        if (Directory.Exists(destinazione))
            bloccanti.Add($"La cartella «AIP {cicloNuovo}» c'è già: se va rifatta, toglila prima.");
        if (bloccanti.Count > 0)
            return esito;

        // L'indice della cartella del ciclo prima: ogni pagina col suo file (relativo) e la sua posizione.
        var indice = new List<IndexedPage>();
        var daLeggere = PdfDellAip.Elenco(sorgente).ToList();
        for (var i = 0; i < daLeggere.Count; i++)
        {
            var relativo = Path.GetRelativePath(sorgente, daLeggere[i]);
            avanzamento?.Report(new AvanzamentoDellEmendamento(FaseDellEmendamento.LeggeIlCicloPrima, i, daLeggere.Count, relativo));
            indice.AddRange(PageIndex.Of(relativo, PdfText.Read(daLeggere[i])));
        }
        var perRiferimento = indice.Where(p => p.Reference is not null).GroupBy(p => p.Reference!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var sezioniPresenti = indice.Select(p => p.Section).OfType<string>().ToHashSet(StringComparer.Ordinal);

        // Le pagine da togliere devono essere quelle che abbiamo, con quella data (le pagine senza testata non la dicono).
        var nonScaricate = new SortedSet<string>(StringComparer.Ordinal);
        var daTogliere = new HashSet<string>(StringComparer.Ordinal);
        foreach (var voce in letto.Destroy.Where(v => Tenuta(v.Reference)))
        {
            if (!perRiferimento.TryGetValue(voce.Reference, out var nostra))
            {
                if (sezioniPresenti.Contains(PageIndex.SectionOf(voce.Reference)))
                    bloccanti.Add($"Da togliere {voce.Reference} del {Giorno(voce.Effective)}: nella cartella del {cicloPrima} non c'è.");
                else
                    nonScaricate.Add(PageIndex.SectionOf(voce.Reference));
                continue;
            }
            if (!nostra.ByPosition && nostra.Effective != voce.Effective)
                bloccanti.Add($"Da togliere {voce.Reference} del {Giorno(voce.Effective)}: nella cartella del {cicloPrima} è del {(nostra.Effective is { } d ? Giorno(d) : "?")}.");
            daTogliere.Add(voce.Reference);
        }
        if (bloccanti.Count > 0)
            return esito with { SezioniNonScaricate = [.. nonScaricate] };

        // Le pagine nuove, per sezione.
        var nuove = letto.Pages.Where(p => Tenuta(p.Reference)).ToList();
        var nuovePerSezione = nuove.GroupBy(p => PageIndex.SectionOf(p.Reference), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var dopo = new Dictionary<string, DateOnly?>(StringComparer.Ordinal);
        int riscritti = 0, copiati = 0;
        var tolti = new List<string>();
        var nuoviFile = new List<string>();

        var provvisoria = destinazione + ".scrivendo";
        if (Directory.Exists(provvisoria))
            Directory.Delete(provvisoria, recursive: true);
        Directory.CreateDirectory(provvisoria);
        var perFile = indice.GroupBy(p => p.File, StringComparer.Ordinal).ToList();
        var scritti = 0;
        foreach (var file in perFile)
        {
            avanzamento?.Report(new AvanzamentoDellEmendamento(FaseDellEmendamento.ScriveIlCicloNuovo, scritti++, perFile.Count, file.Key));
            var sorgenteDelFile = Path.Combine(sorgente, file.Key);
            var arrivo = Path.Combine(provvisoria, file.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(arrivo)!);
            var sezione = file.Select(p => p.Section).OfType<string>().Distinct(StringComparer.Ordinal).SingleOrDefault();
            var messe = sezione is not null && nuovePerSezione.Remove(sezione, out var m) ? m : [];
            var rimaste = file.Where(p => p.Reference is null || !daTogliere.Contains(p.Reference)).ToList();
            foreach (var p in rimaste.Where(p => p.Reference is not null))
                dopo[p.Reference!] = p.Effective;
            if (messe.Count == 0 && rimaste.Count == file.Count())
            {
                File.Copy(sorgenteDelFile, arrivo);
                copiati++;
                continue;
            }
            // Una pagina nuova col riferimento di una rimasta la sostituisce (ENAV la toglie sempre prima: non dovrebbe capitare).
            var sostituite = messe.Select(p => p.Reference).ToHashSet(StringComparer.Ordinal);
            var pagine = rimaste.Where(p => p.Reference is null || !sostituite.Contains(p.Reference))
                .Select(p => (Numero: p.Number ?? p.Position, Fonte: sorgenteDelFile, Pagina: p.Position))
                .Concat(messe.Select(p => (Numero: PageIndex.NumberOf(p.Reference) ?? 0, Fonte: amdt, Pagina: p.Position)))
                .OrderBy(p => p.Numero)
                .ToList();
            foreach (var p in messe)
                dopo[p.Reference] = p.Effective;
            if (pagine.Count == 0)
            {
                tolti.Add(file.Key);
                continue;
            }
            PdfPages.Write(arrivo, pagine.Select(p => (p.Fonte, p.Pagina)).ToList());
            riscritti++;
        }

        // Le sezioni nuove: un file «LI-<sezione>.pdf» nella cartella della sezione più vicina (SICILIA CTA, ENR 2.1.1.4.9,
        // accanto alle altre CTA). 🔴 Solo se l'emendamento la porta intera — tutte le pagine che il GEN 0.4 le dà, o senza
        // GEN 0.4 le pagine da 1 a N —: le pagine nuove di una sezione mai scaricata farebbero un file a buchi (provato sul
        // banco il 21 ottobre: «LI-AD 2 LICJ 2.pdf» con le sole pagine cambiate). Quelle non si scrivono e si dicono.
        var nonScritte = new List<string>();
        foreach (var (sezione, pagine) in nuovePerSezione.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var portate = pagine.Select(p => p.Reference).ToHashSet(StringComparer.Ordinal);
            var dovute = letto.Checklist.Keys.Where(k => PageIndex.SectionOf(k) == sezione).ToHashSet(StringComparer.Ordinal);
            var intera = dovute.Count > 0
                ? dovute.SetEquals(portate)
                : pagine.Select(p => PageIndex.NumberOf(p.Reference) ?? 0).Order().SequenceEqual(Enumerable.Range(1, pagine.Count));
            if (!intera)
            {
                nonScritte.Add(sezione);
                continue;
            }
            var vicina = indice.Where(p => p.Section is not null)
                .OrderByDescending(p => PrefissoComune(p.Section!, sezione)).ThenBy(p => p.File, StringComparer.Ordinal)
                .FirstOrDefault();
            var relativo = Path.Combine(vicina is null ? "" : Path.GetDirectoryName(vicina.File) ?? "", $"LI-{sezione}.pdf");
            var arrivo = Path.Combine(provvisoria, relativo);
            Directory.CreateDirectory(Path.GetDirectoryName(arrivo)!);
            PdfPages.Write(arrivo, pagine.OrderBy(p => PageIndex.NumberOf(p.Reference)).Select(p => (amdt, p.Position)).ToList());
            foreach (var p in pagine)
                dopo[p.Reference] = p.Effective;
            nuoviFile.Add(relativo);
        }

        // L'emendamento accanto (D2), e la cartella al suo posto solo alla fine.
        var copia = Path.Combine(provvisoria, "AMDT", (letto.Number ?? "AMDT").Replace('/', '-') + ".pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(copia)!);
        File.Copy(amdt, copia);
        Directory.Move(provvisoria, destinazione);

        var controllo = letto.Checklist.Where(kv => Tenuta(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        return esito with
        {
            FileRiscritti = riscritti,
            FileCopiati = copiati,
            FileNuovi = nuoviFile,
            FileTolti = tolti,
            PagineTolte = daTogliere.Count,
            PagineMesse = nuove.Count(p => !nonScritte.Contains(PageIndex.SectionOf(p.Reference))),
            SezioniNuoveNonScritte = nonScritte,
            PagineFuoriDalleParti = letto.Pages.Count - nuove.Count,
            SezioniNonScaricate = [.. nonScaricate],
            Controllato = controllo.Count > 0,
            Mancano = controllo.Keys.Where(r => !dopo.ContainsKey(r)).Order(StringComparer.Ordinal).ToList(),
            DataDiversa = controllo.Where(kv => dopo.TryGetValue(kv.Key, out var d) && d is not null && d != kv.Value)
                .Select(kv => $"{kv.Key} ({Giorno(dopo[kv.Key]!.Value)}, il GEN 0.4 dice {Giorno(kv.Value)})").Order(StringComparer.Ordinal).ToList(),
            InPiu = controllo.Count == 0 ? [] : dopo.Keys.Where(r => Tenuta(r) && !controllo.ContainsKey(r)).Order(StringComparer.Ordinal).ToList(),
            CopiaDellEmendamento = Path.Combine(destinazione, Path.GetRelativePath(provvisoria, copia)),
        };
    }

    private static int PrefissoComune(string a, string b)
    {
        var n = 0;
        while (n < a.Length && n < b.Length && a[n] == b[n])
            n++;
        return n;
    }

    private static string Giorno(DateOnly d) => d.ToString("d MMM yyyy", CultureInfo.GetCultureInfo("it-IT"));

    private static string Problema(AmendmentIssue p) => p.Kind switch
    {
        AmendmentIssueKind.NoEffectiveDate => "la copertina non dice la data di entrata in vigore",
        AmendmentIssueKind.NoList => "non c'è l'elenco delle pagine da distruggere e da inserire",
        AmendmentIssueKind.ListRowNotRead => $"una riga dell'elenco non si legge (p.{p.Page}: {p.Detail})",
        AmendmentIssueKind.BodyMismatch => $"le pagine nuove non seguono l'elenco (p.{p.Page}: {p.Detail})",
        AmendmentIssueKind.BodyDate => $"una pagina nuova ha un'altra data dall'elenco (p.{p.Page}: {p.Detail})",
        AmendmentIssueKind.BodyCount => $"pagine nuove ed elenco non tornano ({p.Detail})",
        _ => p.Kind.ToString(),
    };
}
