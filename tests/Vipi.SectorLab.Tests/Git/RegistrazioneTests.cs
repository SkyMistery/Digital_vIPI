using Vipi.SectorLab.Core.Git;

namespace Vipi.SectorLab.Tests.Git;

/// <summary>
/// Carta F4, slice 4: «Registra» — il commit dei file del sector salvati, col messaggio scelto dall'AOD; mai su
/// <c>master</c> né su un <c>airac/</c>; i file fuori dai confini restano fuori; un binario ferma tutto.
/// </summary>
public sealed class RegistrazioneTests : IDisposable
{
    private readonly RepoDiProva _repo = new();

    public RegistrazioneTests()
        => Assert.Null(RamiDelSector.NuovoLavoroAsync(_repo.Git, _repo.Lavoro, "aod/704798/prova", "master").GetAwaiter().GetResult());

    public void Dispose() => _repo.Dispose();

    private async Task<StatoDelRamo> Stato() => (await RepoDelSector.LeggiAsync(_repo.Git, _repo.Lavoro)).Stato!;

    [Fact]
    public async Task SiRegistranoSoloIFileDelSector_EGliAltriRestanoFuori()
    {
        _repo.Scrivi(RepoDiProva.FileDiProva, "ABBOZ;N046.02.38.000;E011.07.48.000;0;\r\n");
        _repo.Scrivi("SectorFiles/Include/IT/nuovo.fix", "X;N046.00.00.000;E011.00.00.000;0;\r\n");
        _repo.Scrivi("Profiles/mio.cpr", "[SETTINGS]\r\n");
        var prima = await Stato();
        Assert.Equal(["Profiles/mio.cpr"], Registrazione.Fuori(prima).Select(c => c.Percorso));

        Assert.Null(await Registrazione.RegistraAsync(_repo.Git, prima, "master", "ABBOZ spostato, X nuovo"));

        var dopo = await Stato();
        Assert.Equal(["Profiles/mio.cpr"], dopo.Cambiati.Select(c => c.Percorso));   // resta fuori, non registrato
        Assert.Equal(1, dopo.Avanti + (dopo.Monte is null ? 1 : 0));
        string ultimo = _repo.Esegui(_repo.Lavoro, "log", "-1", "--format=%s", "--name-only");
        Assert.Contains("ABBOZ spostato, X nuovo", ultimo, StringComparison.Ordinal);
        Assert.Contains("SectorFiles/Include/IT/nuovo.fix", ultimo, StringComparison.Ordinal);
        Assert.DoesNotContain("mio.cpr", ultimo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnFileToltoSiRegistraComeTolto()
    {
        File.Delete(Path.Combine(_repo.Lavoro, RepoDiProva.FileDiProva.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Null(await Registrazione.RegistraAsync(_repo.Git, await Stato(), "master", "via prova.fix"));

        Assert.Contains("D\tSectorFiles/Include/IT/prova.fix", _repo.Esegui(_repo.Lavoro, "show", "--name-status", "--format=", "HEAD"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("master")]
    [InlineData("airac/2611")]
    public async Task SuMasterESuUnCicloNo(string ramo)
    {
        _repo.Esegui(_repo.Lavoro, "switch", "--quiet", "-c", ramo == "master" ? "x" : ramo);
        if (ramo == "master")
            _repo.Esegui(_repo.Lavoro, "switch", "--quiet", "master");
        _repo.Scrivi(RepoDiProva.FileDiProva, "ABBOZ;N046.02.39.000;E011.07.48.000;0;\r\n");

        string? perche = await Registrazione.RegistraAsync(_repo.Git, await Stato(), "master", "no");

        Assert.Contains($"non registra su {ramo}", perche, StringComparison.Ordinal);
        Assert.Single((await Stato()).Cambiati);   // il file resta salvato e non registrato
    }

    [Fact]
    public async Task UnBinarioFermaTutto_EIlMessaggioVuotoAnche()
    {
        File.WriteAllBytes(Path.Combine(_repo.Lavoro, "SectorFiles", "Include", "IT", "suono.wav"), [0x52, 0x49, 0x46, 0x46, 0x00, 0x01]);
        Assert.Contains("suono.wav", await Registrazione.RegistraAsync(_repo.Git, await Stato(), "master", "con un suono"), StringComparison.Ordinal);

        File.Delete(Path.Combine(_repo.Lavoro, "SectorFiles", "Include", "IT", "suono.wav"));
        _repo.Scrivi(RepoDiProva.FileDiProva, "ABBOZ;N046.02.39.000;E011.07.48.000;0;\r\n");
        Assert.Contains("messaggio", await Registrazione.RegistraAsync(_repo.Git, await Stato(), "master", "  "), StringComparison.Ordinal);
        Assert.Contains("niente da registrare", Registrazione.PercheNonSiRegistra(await StatoPulito(), "master"), StringComparison.Ordinal);
    }

    private async Task<StatoDelRamo> StatoPulito()
    {
        Assert.Null(await Registrazione.RegistraAsync(_repo.Git, await Stato(), "master", "un passo"));
        return await Stato();
    }

    [Fact]
    public void IlMessaggioPropostoDiceIGestiEIFile()
    {
        var file = new[] { new FileCambiato("SectorFiles/Include/IT/lirf.sid", "cambiato"), new FileCambiato("SectorFiles/Include/IT/NAVAIDS/APT.fix", "cambiato") };

        Assert.Equal("Nome di ABBOZ\n\nFile: lirf.sid, APT.fix", Registrazione.MessaggioProposto(["Nome di ABBOZ"], file));
        Assert.Equal("2 modifiche in lirf.sid, APT.fix\n\n- Nome di ABBOZ\n- EKLO8R aggiunta\n\nFile: lirf.sid, APT.fix",
            Registrazione.MessaggioProposto(["Nome di ABBOZ", "EKLO8R aggiunta"], file));
        Assert.Equal("Modifiche in lirf.sid, APT.fix\n\nFile: lirf.sid, APT.fix", Registrazione.MessaggioProposto([], file));
    }
}
