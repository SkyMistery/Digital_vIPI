using Vipi.SectorLab.Core.Git;

namespace Vipi.SectorLab.Tests.Git;

/// <summary>L'indirizzo del repo come lo incolla l'AOD (20 ottobre, primo avvio): il link del browser diventa il repo.</summary>
public sealed class IndirizzoDelRepoTests
{
    [Theory]
    [InlineData("https://github.com/SkyMistery/it-aurora-sector-test", "https://github.com/SkyMistery/it-aurora-sector-test.git")]
    [InlineData("https://github.com/SkyMistery/it-aurora-sector-test/", "https://github.com/SkyMistery/it-aurora-sector-test.git")]
    [InlineData("https://github.com/SkyMistery/it-aurora-sector-test.git", "https://github.com/SkyMistery/it-aurora-sector-test.git")]
    [InlineData("  https://github.com/SkyMistery/it-aip-espanso-test/tree/aip/2610  ", "https://github.com/SkyMistery/it-aip-espanso-test.git")]
    [InlineData("\"https://github.com/ivao-italy/it-aurora-sector/pulls\"", "https://github.com/ivao-italy/it-aurora-sector.git")]
    public void IlLinkDiGitHubDiventaIlRepo(string incollato, string atteso) =>
        Assert.Equal(atteso, IndirizzoDelRepo.Pulito(incollato));

    [Theory]
    [InlineData("git@github.com:ivao-italy/it-aurora-sector.git")]
    [InlineData("https://gitlab.example.org/aod/sector")]
    [InlineData(@"D:\repo\sector.git")]
    public void GliAltriIndirizziPassanoComeSono(string indirizzo) =>
        Assert.Equal(indirizzo, IndirizzoDelRepo.Pulito(indirizzo));

    [Fact]
    public void VuotoENulloEUnOpzioneNo()
    {
        Assert.Null(IndirizzoDelRepo.Pulito("   "));
        Assert.Null(IndirizzoDelRepo.Pulito("\"\""));
        Assert.Contains("«-»", IndirizzoDelRepo.PercheNonVa(" --upload-pack=calc"), StringComparison.Ordinal);
        Assert.Null(IndirizzoDelRepo.PercheNonVa("https://github.com/SkyMistery/it-aurora-sector-test.git"));
    }
}
