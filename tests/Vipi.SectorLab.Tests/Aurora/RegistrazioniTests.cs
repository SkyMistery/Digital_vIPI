using Vipi.SectorLab.Core.Aurora;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Aurora;

/// <summary>
/// Carta F5, slice 5: il lotto dalle registrazioni — il lavoro che arriva da git (un altro AOD, un altro PC) entra nel
/// lotto coi gesti dei suoi commit, solo per i file che la vetrina riceve davvero; e i rami di lavoro del server si
/// guardano prima di fonderli.
/// </summary>
public sealed class RegistrazioniTests : IDisposable
{
    private const string Fix = "SectorFiles/Include/IT/NAVAIDS/APT.fix";

    private readonly AlberoDiProva _albero = new();
    private readonly string _fuori = Path.Combine(Path.GetTempPath(), "registrazioni-" + Guid.NewGuid().ToString("N"));
    private readonly DateTimeOffset _ora = new(2026, 10, 18, 11, 0, 0, TimeSpan.Zero);

    public RegistrazioniTests() => Directory.CreateDirectory(_fuori);

    public void Dispose()
    {
        foreach (string cartella in new[] { _albero.Radice, _fuori })
        {
            foreach (string file in Directory.EnumerateFiles(cartella, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
        }

        _albero.Dispose();
        Directory.Delete(_fuori, recursive: true);
    }

    [Fact]
    public void ILog_GestiDalleRigheODalTitolo()
    {
        string uscita =
            "\u001eab12cd3\u001f3 modifiche in APT.fix\u001f\n- Name di BD100\n- Name di BD200\n- note di BC404\n\nFile: APT.fix\n\u001f\n" +
            "SectorFiles/Include/IT/NAVAIDS/APT.fix\nchangelog.md\n" +
            "\u001eef45678\u001fLIRF: aggiunta SID\u001f\u001f\nSectorFiles/Include/IT/lirf.sid\n";

        var lette = Registrazioni.Leggi(uscita);

        Assert.Equal(2, lette.Count);
        Assert.Equal(["Name di BD100", "Name di BD200", "note di BC404"], lette[0].Gesti);
        Assert.Equal([Fix], lette[0].File);
        Assert.Equal(["LIRF: aggiunta SID"], lette[1].Gesti);
        Assert.Equal("ef45678", lette[1].Commit);
    }

    [Fact]
    public void IlLotto_SoloIFileCheLaVetrinaHaCambiato_ESenzaDoppioni()
    {
        var lotto = Lotto.Leggi(Path.Combine(_fuori, "dati"), _albero.Radice);
        lotto.Salvati([("Name di BD100", [Fix])], _ora);
        lotto.Portato(_ora);
        var registrate = new[]
        {
            new RegistrazioneNelLotto("a1", ["Name di BD100", "note di BC404"], [Fix]),
            new RegistrazioneNelLotto("b2", ["LIRF: aggiunta SID"], ["SectorFiles/Include/IT/lirf.sid"]),
        };

        // In vetrina è cambiato solo APT.fix: la SID c'era già (portata prima di registrarla).
        int aggiunte = lotto.Registrate(registrate, [Fix], _ora);

        Assert.Equal(1, aggiunte);
        Assert.Equal(["Name di BD100", "note di BC404"], lotto.Voci.Select(v => v.Cosa));
        Assert.Equal("a1", lotto.Voci[1].Commit);
        Assert.Equal(_ora, lotto.Voci[1].Portata);
    }

    /// <summary>
    /// Il giro della slice: un altro AOD manda un ramo di lavoro; qui lo si vede fra i rami del server, ci si passa, lo si
    /// porta in Aurora e il lotto ha i suoi gesti — non «cambiato ramo: tutto da rivedere».
    /// </summary>
    [Fact]
    public async Task UnRamoDelServer_GuardatoInAurora_ColLottoDaiCommit()
    {
        string server = Path.Combine(_fuori, "server.git");
        var lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"))
        {
            Aurora = (Path.Combine(_fuori, "aurora", "profile.ini"), Path.Combine(_fuori, "aurora", "SectorFiles")),
        };
        Git(lab, _fuori, "init", "--quiet", "--bare", "--initial-branch=master", server);
        Git(lab, _albero.Radice, "init", "--quiet", "--initial-branch=master");
        Configura(lab, _albero.Radice);
        File.WriteAllText(_albero.Percorso(".gitignore"), "dati-del-lab/\n");
        Git(lab, _albero.Radice, "add", "--all");
        Git(lab, _albero.Radice, "commit", "--quiet", "-m", "primo");
        Git(lab, _albero.Radice, "remote", "add", "origin", server);
        Git(lab, _albero.Radice, "push", "--quiet", "-u", "origin", "master");
        // Come il clone del committente (18 ottobre): fatto con un ramo solo, nella configurazione c'è solo master.
        Git(lab, _albero.Radice, "config", "remote.origin.fetch", "+refs/heads/master:refs/remotes/origin/master");

        Assert.True(await lab.ApriAsync(_albero.Radice));
        await lab.RileggiIlRamoAsync();
        Assert.True(lab.CambiaLaVetrina(Path.Combine(_fuori, "vetrina")), lab.Rifiuto);
        Assert.True(await lab.PortaInAuroraAsync(), lab.Rifiuto);
        lab.VistoInAurora();

        // L'altro AOD: due registrazioni sul suo ramo, la seconda su un file che non cambia niente in vetrina.
        string altro = Path.Combine(_fuori, "altro");
        // Gli a capo come sono nel repo: con la conversione di Windows ogni file sembrerebbe cambiato.
        Git(lab, _fuori, "clone", "--quiet", "-c", "core.autocrlf=false", server, altro);
        Configura(lab, altro);
        // Un ramo di lavoro già fuso (una PR chiusa): non c'è niente da guardare, non si mostra.
        Git(lab, altro, "push", "--quiet", "origin", "master:aod/111111/gia-fusa");
        Git(lab, altro, "switch", "--quiet", "-c", "aod/111111/etichette");
        string fix = Path.Combine(altro, Fix.Replace('/', Path.DirectorySeparatorChar));
        File.AppendAllText(fix, "ZZTOP;N045.00.00.000;E010.00.00.000;\r\n");
        Git(lab, altro, "commit", "--quiet", "-am", "2 modifiche in APT.fix", "-m", "- ZZTOP aggiunto in APT.fix\n- Name di BD100\n\nFile: APT.fix");
        File.WriteAllText(Path.Combine(altro, "LEGGIMI.txt"), "fuori dal sector\r\n");
        Git(lab, altro, "add", "LEGGIMI.txt");
        Git(lab, altro, "commit", "--quiet", "-m", "un file fuori dal sector");
        Git(lab, altro, "push", "--quiet", "-u", "origin", "aod/111111/etichette");

        await lab.RileggiIlRamoAsync(prendi: true);
        Assert.Equal(["aod/111111/etichette"], lab.RamiDaGuardare);
        Assert.True(await lab.PassaAlRamoAsync("aod/111111/etichette"), lab.Rifiuto);
        await lab.RileggiIlRamoAsync();
        Assert.Equal("aod/111111/etichette", lab.Ramo!.Ramo);
        Assert.Empty(lab.RamiDaGuardare);

        Assert.True(await lab.PortaInAuroraAsync(), lab.Rifiuto);

        var voci = lab.Lotto!.Voci;
        Assert.Equal(["ZZTOP aggiunto in APT.fix", "Name di BD100"], voci.Select(v => v.Cosa));
        Assert.All(voci, v => Assert.Equal([Fix], v.File));
        Assert.All(voci, v => Assert.NotNull(v.Commit));
        Assert.Equal(2, lab.Lotto.DaVedere);
    }

    [Fact]
    public async Task UnaStoriaCheNonSiRipercorre_TuttoDaRivedere()
    {
        var lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"))
        {
            Aurora = (Path.Combine(_fuori, "aurora", "profile.ini"), Path.Combine(_fuori, "aurora", "SectorFiles")),
        };
        Git(lab, _albero.Radice, "init", "--quiet", "--initial-branch=master");
        Configura(lab, _albero.Radice);
        File.WriteAllText(_albero.Percorso(".gitignore"), "dati-del-lab/\n");
        Git(lab, _albero.Radice, "add", "--all");
        Git(lab, _albero.Radice, "commit", "--quiet", "-m", "primo");
        Git(lab, _albero.Radice, "switch", "--quiet", "-c", "aod/1/a");
        File.AppendAllText(_albero.Percorso(Fix), "ZZA;N045.00.00.000;E010.00.00.000;\r\n");
        Git(lab, _albero.Radice, "commit", "--quiet", "-am", "ZZA");
        Assert.True(await lab.ApriAsync(_albero.Radice));
        await lab.RileggiIlRamoAsync();
        Assert.True(lab.CambiaLaVetrina(Path.Combine(_fuori, "vetrina")), lab.Rifiuto);
        Assert.True(await lab.PortaInAuroraAsync(), lab.Rifiuto);
        lab.VistoInAurora();

        // Un altro ramo da master: il commit in vetrina non sta nella sua storia.
        Git(lab, _albero.Radice, "switch", "--quiet", "-c", "aod/1/b", "master");
        File.AppendAllText(_albero.Percorso(Fix), "ZZB;N045.00.00.000;E010.00.00.000;\r\n");
        Git(lab, _albero.Radice, "commit", "--quiet", "-am", "ZZB");
        Assert.True(await lab.ApriAsync(_albero.Radice));
        await lab.RileggiIlRamoAsync();
        Assert.True(await lab.PortaInAuroraAsync(), lab.Rifiuto);

        Assert.Contains("tutto da rivedere", Assert.Single(lab.Lotto!.Voci).Cosa, StringComparison.Ordinal);
    }

    private static void Configura(SessioneDelLab lab, string clone)
    {
        Git(lab, clone, "config", "user.name", "Prova del Lab");
        Git(lab, clone, "config", "user.email", "prova@example.invalid");
        Git(lab, clone, "config", "core.autocrlf", "false");
    }

    private static string Git(SessioneDelLab lab, string cartella, params string[] argomenti)
    {
        var esito = lab.Git.EseguiAsync(cartella, argomenti).GetAwaiter().GetResult();
        Assert.True(esito.Riuscito, $"git {string.Join(' ', argomenti)}: {esito.Errore}");
        return esito.Uscita;
    }
}
