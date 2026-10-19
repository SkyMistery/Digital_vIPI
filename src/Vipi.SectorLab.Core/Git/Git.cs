using System.Diagnostics;
using System.Text;

namespace Vipi.SectorLab.Core.Git;

/// <summary>Come è finito un comando di git: il codice d'uscita, quel che ha scritto e quel che ha detto di sbagliato.</summary>
public sealed record EsitoDiGit(int Codice, string Uscita, string Errore)
{
    public bool Riuscito => Codice == 0;

    /// <summary>Le righe dell'uscita, senza quelle vuote.</summary>
    public IReadOnlyList<string> Righe => Uscita.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>
/// Il Lab parla con git chiamando <c>git</c> (Git for Windows sul PC dell'AOD), con gli argomenti uno per uno e mai una
/// riga di shell (carta F4 §3, decisione 4 della carta madre: git locale, le credenziali le tiene il Credential
/// Manager di Windows). Nessuna libreria in più: il git che l'AOD usa già.
/// <para>🔴 Il Lab non chiama mai i comandi che buttano via lavoro o riscrivono la storia: <c>reset --hard</c>,
/// <c>clean</c>, <c>stash</c>, <c>push --force</c> (§3). <see cref="Vietato"/> li ferma anche se qualcuno li scrivesse.</para>
/// </summary>
public sealed class Git
{
    private readonly string _eseguibile;

    /// <param name="eseguibile">Il comando di git: di base <c>git</c>, cercato nel PATH.</param>
    public Git(string eseguibile = "git") => _eseguibile = eseguibile;

    /// <summary>Esegue git nella cartella data. Un git che non c'è torna un esito col codice -1 e il perché.</summary>
    public async Task<EsitoDiGit> EseguiAsync(string cartella, IReadOnlyList<string> argomenti, CancellationToken annulla = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(cartella);
        ArgumentNullException.ThrowIfNull(argomenti);
        if (Vietato(argomenti) is { } perche)
            throw new InvalidOperationException(perche);

        var avvio = new ProcessStartInfo(_eseguibile)
        {
            WorkingDirectory = cartella,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string argomento in argomenti)
            avvio.ArgumentList.Add(argomento);
        // Mai una domanda nella console (non c'è): le credenziali le chiede il Credential Manager nella sua finestra.
        avvio.Environment["GIT_TERMINAL_PROMPT"] = "0";
        // I nomi dei file come sono, senza le virgolette e gli \ottali di core.quotePath.
        avvio.Environment["GIT_CONFIG_COUNT"] = "1";
        avvio.Environment["GIT_CONFIG_KEY_0"] = "core.quotePath";
        avvio.Environment["GIT_CONFIG_VALUE_0"] = "false";
        // Un `git status` di sola lettura non prende index.lock per rinfrescare l'indice: quello in sottofondo dopo «Salva»
        // faceva fallire il commit di «Registra» («index.lock: File exists»), tre volte nella CI del 19 ottobre.
        avvio.Environment["GIT_OPTIONAL_LOCKS"] = "0";

        Process processo;
        try
        {
            processo = Process.Start(avvio) ?? throw new InvalidOperationException("git non è partito.");
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            return new EsitoDiGit(-1, "", $"git non c'è su questo PC ({e.Message}): serve Git for Windows, https://git-scm.com/download/win");
        }

        using (processo)
        {
            var uscita = processo.StandardOutput.ReadToEndAsync(annulla);
            var errore = processo.StandardError.ReadToEndAsync(annulla);
            await processo.WaitForExitAsync(annulla).ConfigureAwait(false);
            return new EsitoDiGit(processo.ExitCode, await uscita.ConfigureAwait(false), (await errore.ConfigureAwait(false)).Trim());
        }
    }

    /// <summary>Il perché un comando non si può dare, o null. I comandi che buttano via lavoro o riscrivono la storia.</summary>
    public static string? Vietato(IReadOnlyList<string> argomenti)
    {
        ArgumentNullException.ThrowIfNull(argomenti);
        string? comando = argomenti.FirstOrDefault(a => !a.StartsWith('-'));
        if (argomenti.Any(a => a is "--force" or "-f" or "--force-with-lease" || a.StartsWith("--force-with-lease=", StringComparison.Ordinal)) && comando is "push")
            return "Il Lab non fa mai push --force.";
        if (comando is "reset" && argomenti.Contains("--hard"))
            return "Il Lab non fa mai reset --hard: butta via lavoro.";
        if (comando is "clean" or "stash")
            return $"Il Lab non usa git {comando}: butta via o nasconde lavoro.";
        return null;
    }
}
