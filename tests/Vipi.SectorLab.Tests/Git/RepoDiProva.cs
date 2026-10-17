using Vipi.SectorLab.Core.Git;

namespace Vipi.SectorLab.Tests.Git;

/// <summary>
/// Un repo finto con la forma del sector (carta F4, slice 0): un «server» (<c>git init --bare</c>, ramo predefinito
/// <c>master</c> come il repo vero) e un clone di lavoro con <c>SectorFiles/Include/IT</c>, tutto nel temporaneo.
/// Così i test del giro git non vanno in rete e girano uguali in CI (Ubuntu) e sul PC. Nome e mail dell'autore sono
/// scritti nel clone, non nella configurazione di chi lancia i test.
/// </summary>
public sealed class RepoDiProva : IDisposable
{
    public const string FileDiProva = "SectorFiles/Include/IT/prova.fix";

    public RepoDiProva()
    {
        Radice = Path.Combine(Path.GetTempPath(), "repo-" + Guid.NewGuid().ToString("N"));
        Server = Path.Combine(Radice, "server.git");
        Lavoro = Path.Combine(Radice, "lavoro");
        Directory.CreateDirectory(Radice);

        Esegui(Radice, "init", "--quiet", "--bare", "--initial-branch=master", Server);
        Esegui(Radice, "clone", "--quiet", Server, Lavoro);
        Configura(Lavoro);
        Scrivi(FileDiProva, "ABBOZ;N046.02.37.000;E011.07.48.000;0;\r\n");
        Esegui(Lavoro, "add", "--", FileDiProva);
        Esegui(Lavoro, "commit", "--quiet", "-m", "primo");
        Esegui(Lavoro, "push", "--quiet", "-u", "origin", "master");
    }

    public string Radice { get; }

    /// <summary>Il «server», un repo nudo.</summary>
    public string Server { get; }

    /// <summary>Il clone dell'AOD.</summary>
    public string Lavoro { get; }

    public Core.Git.Git Git { get; } = new();

    /// <summary>Un altro clone, come un secondo AOD che manda le sue registrazioni.</summary>
    public string UnAltroClone()
    {
        string altro = Path.Combine(Radice, "altro-" + Guid.NewGuid().ToString("N")[..6]);
        Esegui(Radice, "clone", "--quiet", Server, altro);
        Configura(altro);
        return altro;
    }

    public void Scrivi(string relativo, string testo, string? clone = null)
    {
        string percorso = Path.Combine(clone ?? Lavoro, relativo.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
        File.WriteAllText(percorso, testo);
    }

    /// <summary>Un comando di git per preparare la prova (fuori dal Lab): fallisce il test se git dice di no.</summary>
    public string Esegui(string cartella, params string[] argomenti)
    {
        var esito = Git.EseguiAsync(cartella, argomenti).GetAwaiter().GetResult();
        Assert.True(esito.Riuscito, $"git {string.Join(' ', argomenti)}: {esito.Errore}");
        return esito.Uscita;
    }

    private void Configura(string clone)
    {
        Esegui(clone, "config", "user.name", "Prova del Lab");
        Esegui(clone, "config", "user.email", "prova@example.invalid");
        Esegui(clone, "config", "core.autocrlf", "false");
    }

    public void Dispose()
    {
        if (!Directory.Exists(Radice))
            return;
        // git lascia i suoi oggetti in sola lettura: senza toglierla, su Windows la cancellazione fallisce.
        foreach (string file in Directory.EnumerateFiles(Radice, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(Radice, recursive: true);
    }
}
