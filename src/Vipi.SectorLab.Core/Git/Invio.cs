using System.Text.RegularExpressions;

namespace Vipi.SectorLab.Core.Git;

/// <summary>
/// «Manda» e la PR (carta F4, slice 5): il push del ramo di lavoro (mai <c>--force</c>, mai <c>master</c> né un
/// <c>airac/</c>) e la pagina di GitHub «nuova PR» già compilata, che l'AOD apre nel browser e conferma con «Create»
/// (D2: niente token nel Lab). Le credenziali le chiede git, nella finestra del Credential Manager.
/// </summary>
public static partial class Invio
{
    /// <summary>Perché adesso non si manda, o null.</summary>
    public static string? PercheNonSiManda(StatoDelRamo stato)
    {
        ArgumentNullException.ThrowIfNull(stato);
        if (stato.Ramo is null)
            return "git è su un commit staccato: niente da mandare.";
        if (!stato.Ramo.StartsWith(RamiDelSector.PrefissoDiLavoro, StringComparison.Ordinal))
            return $"Il Lab manda solo i rami di lavoro (aod/…), mai {stato.Ramo}: lì si arriva con una PR.";
        if (stato.Remoto is null)
            return "Il clone non ha un server (origin): non c'è dove mandare.";
        if (stato.Monte is not null && stato.Avanti == 0)
            return "Non c'è niente di nuovo da mandare: le registrazioni sono già tutte sul server.";
        return null;
    }

    /// <summary>
    /// Manda il ramo di lavoro al server (<c>git push -u origin &lt;ramo&gt;</c>). Torna null se è andata, o il perché. I file
    /// salvati e non registrati restano qui: si manda solo quel che è registrato.
    /// </summary>
    public static async Task<string?> MandaAsync(Git git, StatoDelRamo stato, CancellationToken annulla = default)
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
        if (push.Errore.Contains("rejected", StringComparison.OrdinalIgnoreCase))
            return $"Il server ha rifiutato il ramo (qualcuno ci ha mandato altro nel frattempo?): {push.Errore}";
        return $"Il ramo non è partito: {push.Errore}";
    }

    /// <summary>
    /// <c>owner/repo</c> di un indirizzo GitHub (<c>https://github.com/owner/repo.git</c> o <c>git@github.com:owner/repo.git</c>),
    /// o null se non è GitHub.
    /// </summary>
    public static string? RepoGitHub(string? remoto)
    {
        if (string.IsNullOrWhiteSpace(remoto))
            return null;
        var trovato = IndirizzoGitHub().Match(remoto.Trim());
        return trovato.Success ? $"{trovato.Groups["owner"].Value}/{trovato.Groups["repo"].Value}" : null;
    }

    /// <summary>
    /// La pagina di GitHub «nuova PR» già compilata: da <paramref name="ramo"/> verso <paramref name="base_"/>, col titolo e
    /// il testo. Null se il server non è GitHub.
    /// </summary>
    public static Uri? PaginaDellaPr(string? remoto, string base_, string ramo, string titolo, string testo)
    {
        if (RepoGitHub(remoto) is not { } repo)
            return null;
        return new Uri($"https://github.com/{repo}/compare/{Uri.EscapeDataString(base_)}...{Uri.EscapeDataString(ramo)}"
                       + $"?expand=1&title={Uri.EscapeDataString(titolo)}&body={Uri.EscapeDataString(testo)}");
    }

    /// <summary>
    /// Titolo e testo proposti per la PR: le registrazioni del ramo che la base non ha, dalla più vecchia. Il titolo è la
    /// prima riga dell'unica registrazione, o «N registrazioni: …».
    /// </summary>
    public static async Task<(string Titolo, string Testo)> TitoloETestoAsync(Git git, string radice, string base_, string ramo, CancellationToken annulla = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        var log = await git.EseguiAsync(radice, ["log", "--reverse", "--format=%s", $"origin/{base_}..{ramo}"], annulla).ConfigureAwait(false);
        var titoli = log.Riuscito ? log.Righe : [];
        string titolo = titoli.Count switch
        {
            0 => ramo,
            1 => titoli[0],
            _ => $"{titoli.Count} registrazioni: {titoli[0]}…",
        };
        var righe = titoli.Select(t => "- " + t).ToList();
        righe.Add("");
        righe.Add($"Dal ramo `{ramo}` verso `{base_}`, preparata con Aurora Sector Lab.");
        return (titolo, string.Join("\n", righe));
    }

    [GeneratedRegex(@"github\.com[:/](?<owner>[^/]+)/(?<repo>[^/]+?)(\.git)?/?$", RegexOptions.IgnoreCase)]
    private static partial Regex IndirizzoGitHub();
}
