using Vipi.SectorLab.Core.Git;

namespace Vipi.SectorLab.Tests.Git;

/// <summary>Carta F4, slice 2: «Prendi il sector» — la copia parziale del repo, con la sola <c>SectorFiles</c>.</summary>
public sealed class PresaDelSectorTests : IDisposable
{
    private readonly RepoDiProva _repo = new();

    public void Dispose() => _repo.Dispose();

    [Fact]
    public async Task IlSectorPresoHaSectorFilesEUnRamoPulito()
    {
        // Sul server c'è anche roba di Aurora fuori da SectorFiles: non deve arrivare. (I file sciolti della radice, come
        // README.md, arrivano sempre con la copia parziale «a cono»: nel repo vero c'è solo quello.)
        _repo.Scrivi("Sounds/aurora.txt", "un suono\r\n");
        _repo.Esegui(_repo.Lavoro, "add", "--", "Sounds/aurora.txt");
        _repo.Esegui(_repo.Lavoro, "commit", "--quiet", "-m", "aurora");
        _repo.Esegui(_repo.Lavoro, "push", "--quiet");
        string dove = Path.Combine(_repo.Radice, "preso", "it-aurora-sector");

        Assert.Null(await PresaDelSector.PrendiAsync(_repo.Git, _repo.Server, dove));

        Assert.True(File.Exists(Path.Combine(dove, RepoDiProva.FileDiProva.Replace('/', Path.DirectorySeparatorChar))));
        Assert.False(Directory.Exists(Path.Combine(dove, "Sounds")));
        var (stato, _) = await RepoDelSector.LeggiAsync(_repo.Git, dove);
        Assert.Equal(("master", true), (stato!.Ramo, stato.Pulito));
    }

    [Fact]
    public async Task ArrivanoAncheGliSchemiDiAurora()
    {
        // ColorSchemes sta accanto a SectorFiles e la mappa ne legge i colori: senza, un sector preso non li ha.
        _repo.Scrivi("ColorSchemes/Default.clr", "schema\r\n");
        _repo.Esegui(_repo.Lavoro, "add", "--", "ColorSchemes/Default.clr");
        _repo.Esegui(_repo.Lavoro, "commit", "--quiet", "-m", "schemi");
        _repo.Esegui(_repo.Lavoro, "push", "--quiet");
        string dove = Path.Combine(_repo.Radice, "preso-con-gli-schemi");

        Assert.Null(await PresaDelSector.PrendiAsync(_repo.Git, _repo.Server, dove));

        Assert.True(File.Exists(Path.Combine(dove, "ColorSchemes", "Default.clr")));
    }

    [Fact]
    public async Task UnIndirizzoCheSembraUnOpzioneSiRifiuta()
    {
        string dove = Path.Combine(_repo.Radice, "mai");

        Assert.Contains("«-»", await PresaDelSector.PrendiAsync(_repo.Git, "--upload-pack=calc", dove), StringComparison.Ordinal);
        Assert.False(Directory.Exists(dove));
    }

    [Fact]
    public void UnaCartellaPienaORelativaSiRifiuta()
    {
        Assert.Contains("non è vuota", PresaDelSector.PercheNonVa(_repo.Server, _repo.Lavoro), StringComparison.Ordinal);
        Assert.Contains("per intero", PresaDelSector.PercheNonVa(_repo.Server, "sector"), StringComparison.Ordinal);
        Assert.Contains("indirizzo", PresaDelSector.PercheNonVa(" ", Path.Combine(_repo.Radice, "x")), StringComparison.Ordinal);
        Assert.Null(PresaDelSector.PercheNonVa(_repo.Server, Path.Combine(_repo.Radice, "nuova")));
    }

    [Fact]
    public async Task UnRepoCheNonCeSiDiceColPerche()
    {
        string dove = Path.Combine(_repo.Radice, "preso-male");

        string? perche = await PresaDelSector.PrendiAsync(_repo.Git, Path.Combine(_repo.Radice, "non-ce.git"), dove);

        Assert.StartsWith("Il repo non si è lasciato prendere", perche, StringComparison.Ordinal);
    }
}
