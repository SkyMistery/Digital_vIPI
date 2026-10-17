using Vipi.SectorLab.Core.Git;

namespace Vipi.SectorLab.Tests.Git;

/// <summary>
/// Carta F4, slice 5: «Manda» — il push del solo ramo di lavoro — e la pagina di GitHub della PR già compilata.
/// </summary>
public sealed class InvioTests : IDisposable
{
    private readonly RepoDiProva _repo = new();

    public void Dispose() => _repo.Dispose();

    private async Task<StatoDelRamo> Stato() => (await RepoDelSector.LeggiAsync(_repo.Git, _repo.Lavoro)).Stato!;

    private async Task Registra(string coordinate, string messaggio)
    {
        _repo.Scrivi(RepoDiProva.FileDiProva, $"ABBOZ;{coordinate};E011.07.48.000;0;\r\n");
        Assert.Null(await Registrazione.RegistraAsync(_repo.Git, await Stato(), "master", messaggio));
    }

    [Fact]
    public async Task IlRamoDiLavoroArrivaSulServer_EPoiNonCeNienteDaMandare()
    {
        Assert.Null(await RamiDelSector.NuovoLavoroAsync(_repo.Git, _repo.Lavoro, "aod/704798/prova", "master"));
        await Registra("N046.02.38.000", "ABBOZ spostato");

        Assert.Null(await Invio.MandaAsync(_repo.Git, await Stato()));

        var dopo = await Stato();
        Assert.Equal(("origin/aod/704798/prova", 0), (dopo.Monte, dopo.Avanti));
        Assert.Contains("refs/heads/aod/704798/prova", _repo.Esegui(_repo.Server, "for-each-ref", "--format=%(refname)"), StringComparison.Ordinal);
        Assert.Contains("niente di nuovo", Invio.PercheNonSiManda(dopo), StringComparison.Ordinal);
        // Il master del server non è stato toccato.
        Assert.Equal(_repo.Esegui(_repo.Lavoro, "rev-parse", "origin/master"), _repo.Esegui(_repo.Server, "rev-parse", "master"));
    }

    [Fact]
    public async Task MasterNonSiManda()
    {
        await RamiDelSector.PassaAsync(_repo.Git, _repo.Lavoro, "master");
        _repo.Scrivi(RepoDiProva.FileDiProva, "ABBOZ;N046.02.39.000;E011.07.48.000;0;\r\n");
        _repo.Esegui(_repo.Lavoro, "commit", "--quiet", "-am", "su master a mano");

        Assert.Contains("mai master", await Invio.MandaAsync(_repo.Git, await Stato()), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://github.com/SkyMistery/it-aurora-sector-test.git", "SkyMistery/it-aurora-sector-test")]
    [InlineData("https://github.com/ivao-italy/it-aurora-sector", "ivao-italy/it-aurora-sector")]
    [InlineData("git@github.com:ivao-italy/it-aurora-sector.git", "ivao-italy/it-aurora-sector")]
    [InlineData("D:/server.git", null)]
    [InlineData(null, null)]
    public void IlRepoGitHubDallIndirizzo(string? remoto, string? atteso)
        => Assert.Equal(atteso, Invio.RepoGitHub(remoto));

    [Fact]
    public void LaPaginaDellaPrEGiaCompilata()
    {
        var pagina = Invio.PaginaDellaPr("https://github.com/SkyMistery/it-aurora-sector-test.git", "airac/2611", "aod/704798/correzioni-r10",
            "Correzioni R-10", "- AC_comb SEL col //\n");

        Assert.Equal("https://github.com/SkyMistery/it-aurora-sector-test/compare/airac%2F2611...aod%2F704798%2Fcorrezioni-r10"
                     + "?expand=1&title=Correzioni%20R-10&body=-%20AC_comb%20SEL%20col%20%2F%2F%0A", pagina!.AbsoluteUri);
        Assert.Null(Invio.PaginaDellaPr("D:/server.git", "master", "aod/1/x", "t", "b"));
    }

    [Fact]
    public async Task TitoloETestoDalleRegistrazioniDelRamo()
    {
        Assert.Null(await RamiDelSector.NuovoLavoroAsync(_repo.Git, _repo.Lavoro, "aod/704798/prova", "master"));
        await Registra("N046.02.38.000", "ABBOZ spostato");
        Assert.Equal("ABBOZ spostato", (await Invio.TitoloETestoAsync(_repo.Git, _repo.Lavoro, "master", "aod/704798/prova")).Titolo);

        await Registra("N046.02.40.000", "ABBOZ di nuovo");
        var (titolo, testo) = await Invio.TitoloETestoAsync(_repo.Git, _repo.Lavoro, "master", "aod/704798/prova");

        Assert.Equal("2 registrazioni: ABBOZ spostato…", titolo);
        Assert.StartsWith("- ABBOZ spostato\n- ABBOZ di nuovo\n", testo, StringComparison.Ordinal);
    }
}
