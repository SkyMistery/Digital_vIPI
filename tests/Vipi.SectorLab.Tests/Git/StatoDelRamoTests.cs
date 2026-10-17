using Vipi.SectorLab.Core.Git;

namespace Vipi.SectorLab.Tests.Git;

/// <summary>
/// Carta F4, slice 1: il Lab sa in che ramo è il sector e in che stato — i file salvati e non registrati, le
/// registrazioni da mandare, le novità sul server.
/// </summary>
public sealed class StatoDelRamoTests : IDisposable
{
    private readonly RepoDiProva _repo = new();

    public void Dispose() => _repo.Dispose();

    private async Task<StatoDelRamo> Stato(bool prendi = false)
    {
        var (stato, perche) = await RepoDelSector.LeggiAsync(_repo.Git, _repo.Lavoro, prendi);
        Assert.Null(perche);
        return Assert.IsType<StatoDelRamo>(stato);
    }

    [Fact]
    public async Task UnCloneAppenaPresoEPulito()
    {
        var stato = await Stato();

        Assert.Equal("master", stato.Ramo);
        Assert.Equal("origin/master", stato.Monte);
        Assert.Equal(_repo.Server, stato.Remoto);
        Assert.True(stato.Pulito);
        Assert.Equal("pulito", stato.InParole());
    }

    [Fact]
    public async Task UnFileSalvatoENuovoSonoDaRegistrare()
    {
        _repo.Scrivi(RepoDiProva.FileDiProva, "ABBOZ;N046.02.38.000;E011.07.48.000;0;\r\n");
        _repo.Scrivi("SectorFiles/Include/IT/nuovo file.fix", "X;N046.00.00.000;E011.00.00.000;0;\r\n");

        var stato = await Stato();

        Assert.Equal([("SectorFiles/Include/IT/nuovo file.fix", "nuovo"), (RepoDiProva.FileDiProva, "cambiato")],
            stato.Cambiati.Select(c => (c.Percorso, c.Come)).OrderBy(c => c.Percorso, StringComparer.Ordinal));
        Assert.Equal("2 file da registrare", stato.InParole());
    }

    [Fact]
    public async Task UnaRegistrazioneDaMandareEUnaNovitaSulServer()
    {
        // Registrato qui e non mandato.
        _repo.Scrivi(RepoDiProva.FileDiProva, "ABBOZ;N046.02.39.000;E011.07.48.000;0;\r\n");
        _repo.Esegui(_repo.Lavoro, "commit", "--quiet", "-am", "qui");
        // Un altro AOD manda una sua registrazione su un altro file.
        string altro = _repo.UnAltroClone();
        _repo.Scrivi("SectorFiles/Include/IT/altro.fix", "Y;N046.00.00.000;E011.00.00.000;0;\r\n", altro);
        _repo.Esegui(altro, "add", "--", "SectorFiles/Include/IT/altro.fix");
        _repo.Esegui(altro, "commit", "--quiet", "-m", "là");
        _repo.Esegui(altro, "push", "--quiet");

        Assert.Equal((1, 0), ((await Stato()).Avanti, (await Stato()).Indietro));   // senza fetch il server non si sa
        var dopo = await Stato(prendi: true);

        Assert.Equal((1, 1), (dopo.Avanti, dopo.Indietro));
        Assert.Equal("1 registrazione da mandare · 1 novità sul server", dopo.InParole());
    }

    [Fact]
    public async Task UnRamoMaiMandatoSiDice()
    {
        _repo.Esegui(_repo.Lavoro, "switch", "--quiet", "-c", "aod/704798/prova");

        var stato = await Stato();

        Assert.Equal("aod/704798/prova", stato.Ramo);
        Assert.Null(stato.Monte);
        Assert.Equal("mai mandato", stato.InParole());
    }

    [Fact]
    public async Task UnaCartellaFuoriDaGitNonEUnErrore()
    {
        string fuori = Path.Combine(_repo.Radice, "fuori");
        Directory.CreateDirectory(fuori);

        var (stato, perche) = await RepoDelSector.LeggiAsync(_repo.Git, fuori);

        Assert.Null(stato);
        Assert.Contains("non sta in un clone git", perche, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitCheNonCeSiDiceColPerche()
    {
        var (stato, perche) = await RepoDelSector.LeggiAsync(new Core.Git.Git("git-che-non-esiste-" + Guid.NewGuid().ToString("N")), _repo.Lavoro);

        Assert.Null(stato);
        Assert.Contains("git non c'è", perche, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("push", "--force")]
    [InlineData("push", "-f")]
    [InlineData("push", "--force-with-lease")]
    [InlineData("reset", "--hard")]
    [InlineData("clean", "-fd")]
    [InlineData("stash")]
    public async Task IComandiCheButtanoViaLavoroNonPartono(params string[] argomenti)
    {
        Assert.NotNull(Core.Git.Git.Vietato(argomenti));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.Git.EseguiAsync(_repo.Lavoro, argomenti));
    }
}
