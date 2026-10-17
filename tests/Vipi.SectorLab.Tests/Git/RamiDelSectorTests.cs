using Vipi.SectorLab.Core.Git;

namespace Vipi.SectorLab.Tests.Git;

/// <summary>
/// Carta F4, slice 3: i rami — il nome di un lavoro, il ciclo AIRAC dopo, il lavoro nuovo da un ramo del server, il
/// passaggio, il ramo del ciclo che nasce quando manca.
/// </summary>
public sealed class RamiDelSectorTests : IDisposable
{
    private readonly RepoDiProva _repo = new();

    public void Dispose() => _repo.Dispose();

    [Theory]
    [InlineData("Correzioni R-10", "aod/704798/correzioni-r-10")]
    [InlineData("  LIRF: SID nuove (A2611)!  ", "aod/704798/lirf-sid-nuove-a2611")]
    [InlineData("Città d'Aosta", "aod/704798/citta-d-aosta")]
    [InlineData("--- ", null)]
    [InlineData("", null)]
    public void IlNomeDelLavoroEPulito(string argomento, string? atteso)
        => Assert.Equal(atteso, RamiDelSector.NomeDiLavoro("704798", argomento));

    [Fact]
    public void UnArgomentoLungoSiTaglia()
        => Assert.Equal("aod/1/" + new string('a', 40), RamiDelSector.NomeDiLavoro("1", new string('a', 60)));

    [Theory]
    [InlineData(2026, 10, 17, "2610", "2611", 2026, 10, 29)]
    [InlineData(2026, 10, 28, "2610", "2611", 2026, 10, 29)]
    [InlineData(2026, 10, 29, "2611", "2612", 2026, 11, 26)]
    public void IlCicloDopoVieneDalCalendarioDiVipi(int a, int m, int g, string inVigore, string dopo, int da, int dm, int dg)
    {
        var cicli = RamiDelSector.Cicli(new DateTime(a, m, g, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal((inVigore, dopo, new DateOnly(da, dm, dg)), cicli);
        Assert.Equal("airac/" + dopo, RamiDelSector.RamoDelCicloDopo(new DateTime(a, m, g, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task UnLavoroNuovoNasceDalServer_ESiRicordaDaDove()
    {
        Assert.Equal("master", await RamiDelSector.PredefinitoAsync(_repo.Git, _repo.Lavoro));

        Assert.Null(await RamiDelSector.NuovoLavoroAsync(_repo.Git, _repo.Lavoro, "aod/704798/prova", "master"));

        var (stato, _) = await RepoDelSector.LeggiAsync(_repo.Git, _repo.Lavoro);
        Assert.Equal("aod/704798/prova", stato!.Ramo);
        Assert.Null(stato.Monte);   // non segue origin/master: si manderà col suo nome
        Assert.Equal("master", await RamiDelSector.BaseDiAsync(_repo.Git, _repo.Lavoro, "aod/704798/prova"));
        Assert.Equal(["aod/704798/prova", "master"], await RamiDelSector.LocaliAsync(_repo.Git, _repo.Lavoro));
        // Lo stesso nome due volte no; un nome che non è un lavoro no; un ramo che il server non ha no.
        Assert.Contains("c'è già", await RamiDelSector.NuovoLavoroAsync(_repo.Git, _repo.Lavoro, "aod/704798/prova", "master"), StringComparison.Ordinal);
        Assert.Contains("aod/", await RamiDelSector.NuovoLavoroAsync(_repo.Git, _repo.Lavoro, "master2", "master"), StringComparison.Ordinal);
        Assert.Contains("non c'è il ramo airac/2611", await RamiDelSector.NuovoLavoroAsync(_repo.Git, _repo.Lavoro, "aod/704798/altro", "airac/2611"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task IlRamoDelCicloNasceUnaVoltaSola_EUnLavoroCiParte()
    {
        Assert.Null(await RamiDelSector.FaiNascereIlCicloAsync(_repo.Git, _repo.Lavoro, "airac/2611"));

        Assert.True(await RamiDelSector.SulServerAsync(_repo.Git, _repo.Lavoro, "airac/2611"));
        Assert.Contains("c'è già", await RamiDelSector.FaiNascereIlCicloAsync(_repo.Git, _repo.Lavoro, "airac/2611"), StringComparison.Ordinal);
        Assert.Contains("airac/", await RamiDelSector.FaiNascereIlCicloAsync(_repo.Git, _repo.Lavoro, "master"), StringComparison.Ordinal);
        Assert.Null(await RamiDelSector.NuovoLavoroAsync(_repo.Git, _repo.Lavoro, "aod/704798/ciclo", "airac/2611"));
        Assert.Equal("airac/2611", await RamiDelSector.BaseDiAsync(_repo.Git, _repo.Lavoro, "aod/704798/ciclo"));
    }

    [Fact]
    public async Task SiPassaDaUnRamoAllAltro_MaGitNonPerdeLavoro()
    {
        Assert.Null(await RamiDelSector.NuovoLavoroAsync(_repo.Git, _repo.Lavoro, "aod/704798/a", "master"));
        _repo.Scrivi(RepoDiProva.FileDiProva, "ABBOZ;N046.02.40.000;E011.07.48.000;0;\r\n");
        _repo.Esegui(_repo.Lavoro, "commit", "--quiet", "-am", "su a");

        Assert.Null(await RamiDelSector.PassaAsync(_repo.Git, _repo.Lavoro, "master"));
        Assert.Contains("N046.02.37.000", File.ReadAllText(Path.Combine(_repo.Lavoro, RepoDiProva.FileDiProva)), StringComparison.Ordinal);

        // Un file cambiato e non registrato che l'altro ramo ha diverso: git rifiuta, e il file resta com'era.
        _repo.Scrivi(RepoDiProva.FileDiProva, "ABBOZ;N046.02.41.000;E011.07.48.000;0;\r\n");
        Assert.NotNull(await RamiDelSector.PassaAsync(_repo.Git, _repo.Lavoro, "aod/704798/a"));
        Assert.Contains("N046.02.41.000", File.ReadAllText(Path.Combine(_repo.Lavoro, RepoDiProva.FileDiProva)), StringComparison.Ordinal);
    }
}
