using Vipi.SectorLab.Core.Git;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Il repo dell'AIP espanso (carta F6 §3, D9): la cartella dell'AIP espanso è un clone, e il Lab ci scrive come nel
/// sector — un ramo, la registrazione, l'invio, la PR che fonde il committente. Così tutti gli AOD hanno lo stesso AIP
/// espanso, con la storia di ogni ciclo.
/// <para>🔴 Le regole sono quelle del sector (carta F4 §3), con confini suoi: il Lab lavora sul ramo del ciclo
/// <c>aip/&lt;ciclo&gt;</c>, mai su <c>main</c>; registra solo il modello (<c>&lt;ciclo&gt;/*.json</c>) e il
/// <c>.gitignore</c> — mai un PDF (peso, copyright ENAV), che il <c>.gitignore</c> tiene fuori anche per chi lavora a
/// mano; niente <c>--force</c>, niente <c>reset --hard</c> (<see cref="Git.Git.Vietato"/>).</para>
/// </summary>
public static class RepoDellAip
{
    /// <summary>Il repo di prova sull'account del committente (D9), finché non si passa a quello di <c>ivao-italy</c>.</summary>
    public const string IndirizzoDiBase = "https://github.com/SkyMistery/it-aip-espanso-test.git";

    /// <summary>Il prefisso dei rami del Lab nel repo dell'AIP: uno per ciclo.</summary>
    public const string PrefissoDelRamo = "aip/";

    /// <summary>Il <c>.gitignore</c> che il Lab mette nel repo: i PDF e i file a metà scrittura non entrano mai.</summary>
    public const string GitIgnore = "# Messo da Aurora Sector Lab (carta F6): nel repo dell'AIP espanso vanno i dati, mai i PDF di ENAV.\n*.pdf\n*.PDF\n*.scrivendo\n";

    public static string RamoDelCiclo(string ciclo) => PrefissoDelRamo + ciclo;

    /// <summary>Vero se il Lab può registrare quel file del repo (percorso dalla radice, barre in avanti).</summary>
    public static bool DentroIConfini(string percorso)
    {
        ArgumentNullException.ThrowIfNull(percorso);
        if (percorso == ".gitignore")
            return true;
        var parti = percorso.Split('/');
        return parti.Length == 2 && (CicloAirac.Valido(parti[0]) || parti[0] == AipEspanso.CartellaDelleCorrezioni)
               && parti[1].EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Vero se la cartella è la radice di un clone git.</summary>
    public static bool EUnRepo(string cartella) =>
        Directory.Exists(Path.Combine(cartella, ".git")) || File.Exists(Path.Combine(cartella, ".git"));

    /// <summary>
    /// «Prendi l'AIP espanso»: clona il repo nella cartella (nuova o vuota). Torna null se è andata, o il perché. Il repo
    /// è piccolo (JSON): si prende intero.
    /// </summary>
    public static async Task<string?> PrendiAsync(Git.Git git, string indirizzo, string cartella, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        if (string.IsNullOrWhiteSpace(indirizzo))
            return "Manca l'indirizzo del repo dell'AIP espanso.";
        if (IndirizzoDelRepo.PercheNonVa(indirizzo) is { } perche)
            return perche;
        if (string.IsNullOrWhiteSpace(cartella) || !Path.IsPathFullyQualified(cartella.Trim()))
            return "La cartella va scritta per intero (D:\\…\\aip-espanso).";
        var dove = Path.GetFullPath(cartella.Trim());
        if (File.Exists(dove))
            return "Lì c'è un file, non una cartella.";
        if (Directory.Exists(dove) && Directory.EnumerateFileSystemEntries(dove).Any())
            return "La cartella non è vuota: l'AIP espanso si prende in una cartella nuova (o vuota).";

        var sopra = Path.GetDirectoryName(dove) ?? dove;
        Directory.CreateDirectory(sopra);
        var clone = await git.EseguiAsync(sopra, ["clone", "--quiet", "--", indirizzo.Trim(), dove], annulla).ConfigureAwait(false);
        if (!clone.Riuscito)
            return clone.Codice == -1 ? clone.Errore : $"Il repo non si è lasciato prendere: {clone.Errore}";
        return null;
    }

    /// <summary>
    /// Mette la copia di lavoro sul ramo del ciclo prima di scrivere: lo prende dal server se c'è già (un altro AOD l'ha
    /// cominciato), altrimenti lo fa nascere da <c>origin/&lt;predefinito&gt;</c>. Torna null se è andata, o il perché.
    /// 🔴 Con file cambiati e non registrati su un altro ramo non si cambia ramo: si registra prima.
    /// </summary>
    public static async Task<string?> SulRamoDelCicloAsync(Git.Git git, string radice, string ciclo, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        var ramo = RamoDelCiclo(ciclo);
        await git.EseguiAsync(radice, RamiDelSector.ChiediAlServer, annulla).ConfigureAwait(false);

        var (stato, perche) = await RepoDelSector.LeggiAsync(git, radice, prendi: false, annulla).ConfigureAwait(false);
        if (stato is null)
            return perche ?? "La cartella dell'AIP espanso non è un clone git.";
        if (stato.Ramo == ramo)
            return null;
        if (stato.Cambiati.Count > 0)
            return $"Ci sono file cambiati e non registrati su {stato.Ramo ?? "un commit staccato"}: registrali (o mettili a posto) prima di passare a {ramo}.";

        if ((await git.EseguiAsync(radice, ["rev-parse", "--verify", "--quiet", "refs/heads/" + ramo], annulla).ConfigureAwait(false)).Riuscito)
            return Esito(await git.EseguiAsync(radice, ["switch", "--quiet", ramo], annulla).ConfigureAwait(false), ramo);
        if ((await git.EseguiAsync(radice, ["rev-parse", "--verify", "--quiet", "refs/remotes/origin/" + ramo], annulla).ConfigureAwait(false)).Riuscito)
            return Esito(await git.EseguiAsync(radice, ["switch", "--quiet", "--track", "origin/" + ramo], annulla).ConfigureAwait(false), ramo);

        var predefinito = await RamiDelSector.PredefinitoAsync(git, radice, annulla).ConfigureAwait(false);
        if (!(await git.EseguiAsync(radice, ["rev-parse", "--verify", "--quiet", "refs/remotes/origin/" + predefinito], annulla).ConfigureAwait(false)).Riuscito)
            return $"Sul server non c'è {predefinito}: il repo dell'AIP espanso ha bisogno di un primo commit (lo fa il committente).";
        return Esito(await git.EseguiAsync(radice, ["switch", "--quiet", "--no-track", "-c", ramo, "origin/" + predefinito], annulla).ConfigureAwait(false), ramo);

        static string? Esito(EsitoDiGit e, string ramo) => e.Riuscito ? null : $"Non sono riuscito a passare a {ramo}: {e.Errore}";
    }

    /// <summary>Scrive il <c>.gitignore</c> del Lab se manca, o ci aggiunge le righe che mancano. Vero se l'ha cambiato.</summary>
    public static bool MettiIlGitIgnore(string radice)
    {
        var file = Path.Combine(radice, ".gitignore");
        var prima = File.Exists(file) ? File.ReadAllText(file) : "";
        var righe = prima.Split('\n').Select(r => r.Trim()).ToHashSet(StringComparer.Ordinal);
        var mancanti = GitIgnore.Split('\n').Where(r => r.Length > 0 && !r.StartsWith('#') && !righe.Contains(r)).ToList();
        if (mancanti.Count == 0)
            return false;
        File.WriteAllText(file, prima.Length == 0 ? GitIgnore : prima.TrimEnd('\n', '\r') + "\n" + string.Join("\n", mancanti) + "\n");
        return true;
    }

    /// <summary>Perché adesso non si registra, o null.</summary>
    public static string? PercheNonSiRegistra(StatoDelRamo stato)
    {
        ArgumentNullException.ThrowIfNull(stato);
        if (stato.Ramo is null || !stato.Ramo.StartsWith(PrefissoDelRamo, StringComparison.Ordinal))
            return $"Il Lab registra l'AIP espanso solo sul ramo di un ciclo (aip/…), mai su {stato.Ramo ?? "un commit staccato"}.";
        if (stato.Cambiati.FirstOrDefault(c => c.Come == "in conflitto") is { } conflitto)
            return $"{conflitto.Percorso} è in conflitto: va risolto prima.";
        if (!stato.Cambiati.Any(c => DentroIConfini(c.Percorso)))
            return "Non c'è niente da registrare: l'AIP espanso letto è uguale a quello già registrato.";
        return null;
    }

    /// <summary>Il messaggio proposto per la registrazione di un ciclo letto.</summary>
    /// <remarks>Fino a sei PDF li nomina; oltre, le sezioni (con ENR 2 sono 64 file: il messaggio diventava un elenco).</remarks>
    public static string MessaggioProposto(EsitoDellAipEspanso esito)
    {
        var nomi = esito.Letti.Select(Path.GetFileNameWithoutExtension).ToList();
        var cosa = nomi.Count <= 6
            ? string.Join(", ", nomi)
            : string.Join(", ", nomi.Select(n => System.Text.RegularExpressions.Regex.Match(n!, @"^LI-(?<s>[A-Z]+ \d+)").Groups["s"].Value)
                .Where(s => s.Length > 0).Distinct().Order(StringComparer.Ordinal));
        return $"AIP {esito.Ciclo}: {esito.Quanti()} da {esito.Letti.Count} PDF ({cosa})";
    }

    /// <summary>Registra i file dentro i confini, e solo quelli. Torna null se è andata, o il perché.</summary>
    public static async Task<string?> RegistraAsync(Git.Git git, StatoDelRamo stato, string? messaggio, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        if (PercheNonSiRegistra(stato) is { } perche)
            return perche;
        if (string.IsNullOrWhiteSpace(messaggio))
            return "Scrivi il messaggio: dice agli altri AOD che cosa è cambiato.";

        string[] percorsi = [.. stato.Cambiati.Where(c => DentroIConfini(c.Percorso)).Select(c => c.Percorso)];
        var aggiungi = await git.EseguiAsync(stato.Radice, ["add", "--all", "--", .. percorsi], annulla).ConfigureAwait(false);
        if (!aggiungi.Riuscito)
            return $"git non ha preso i file: {aggiungi.Errore}";
        var registra = await git.EseguiAsync(stato.Radice, ["commit", "--quiet", "-m", messaggio.Trim(), "--", .. percorsi], annulla).ConfigureAwait(false);
        if (registra.Riuscito)
            return null;
        return registra.Errore.Contains("user.email", StringComparison.Ordinal) || registra.Errore.Contains("user.name", StringComparison.Ordinal)
            ? "git non sa chi sei: una volta sola, in un terminale, git config --global user.name \"Nome Cognome\" e git config --global user.email \"la tua mail di GitHub\"."
            : $"git non ha registrato: {registra.Errore}";
    }

    /// <summary>Perché adesso non si manda, o null.</summary>
    public static string? PercheNonSiManda(StatoDelRamo stato)
    {
        ArgumentNullException.ThrowIfNull(stato);
        if (stato.Ramo is null || !stato.Ramo.StartsWith(PrefissoDelRamo, StringComparison.Ordinal))
            return $"Il Lab manda solo i rami dei cicli (aip/…), mai {stato.Ramo ?? "un commit staccato"}: a main si arriva con una PR.";
        if (stato.Remoto is null)
            return "Il clone non ha un server (origin): non c'è dove mandare.";
        if (stato.Monte is not null && stato.Avanti == 0)
            return "Non c'è niente di nuovo da mandare: le registrazioni sono già tutte sul server.";
        return null;
    }

    /// <summary>Manda il ramo del ciclo (<c>git push -u origin aip/…</c>, mai <c>--force</c>). Torna null se è andata, o il perché.</summary>
    public static async Task<string?> MandaAsync(Git.Git git, StatoDelRamo stato, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        if (PercheNonSiManda(stato) is { } perche)
            return perche;
        var push = await git.EseguiAsync(stato.Radice, ["push", "--quiet", "--set-upstream", "origin", stato.Ramo!], annulla).ConfigureAwait(false);
        if (push.Riuscito)
            return null;
        if (push.Errore.Contains("Authentication failed", StringComparison.OrdinalIgnoreCase)
            || push.Errore.Contains("could not read Username", StringComparison.OrdinalIgnoreCase))
            return "GitHub non ha riconosciuto l'accesso: riprova e, nella finestra di git, entra col tuo account GitHub.";
        return $"Il ramo non è partito: {push.Errore}";
    }
}
