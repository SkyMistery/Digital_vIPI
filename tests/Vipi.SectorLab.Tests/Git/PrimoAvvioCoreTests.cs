using Vipi.SectorLab.Core.Git;
using Vipi.SectorLab.Core.PdfAip;

namespace Vipi.SectorLab.Tests.Git;

/// <summary>
/// Il primo avvio (20 ottobre): una cartella di lavoro col sector, l'AIP espanso e i PDF; un passo fallito si riprova
/// senza ricominciare. (Per l'AIP espanso basta un repo qualunque: si usa lo stesso server del sector.)
/// </summary>
public sealed class PrimoAvvioCoreTests : IDisposable
{
    private readonly RepoDiProva _repo = new();

    public void Dispose() => _repo.Dispose();

    private string Lavoro => Path.Combine(_repo.Radice, "IVAO");

    [Fact]
    public async Task PreparaLeTreCartelle()
    {
        var passi = new List<string>();

        var (cartelle, perche) = await PrimoAvvio.PreparaAsync(_repo.Git, Lavoro, _repo.Server, _repo.Server, passi.Add);

        Assert.Null(perche);
        Assert.Equal(Path.Combine(Lavoro, "sector"), cartelle!.Sector);
        Assert.True(PrimoAvvio.SectorGiaPreso(cartelle.Sector));
        Assert.True(RepoDellAip.EUnRepo(cartelle.AipEspanso));
        Assert.True(Directory.Exists(cartelle.Pdf));
        Assert.Equal(3, passi.Count);
    }

    [Fact]
    public async Task UnPassoFallitoSiRiprovaSenzaRicominciare()
    {
        string sbagliato = Path.Combine(_repo.Radice, "non-ce.git");

        var (prima, perche) = await PrimoAvvio.PreparaAsync(_repo.Git, Lavoro, _repo.Server, sbagliato);
        Assert.Null(prima);
        Assert.StartsWith("AIP espanso:", perche, StringComparison.Ordinal);
        Assert.True(PrimoAvvio.SectorGiaPreso(Path.Combine(Lavoro, "sector")));

        var passi = new List<string>();
        var (dopo, ancora) = await PrimoAvvio.PreparaAsync(_repo.Git, Lavoro, _repo.Server, _repo.Server, passi.Add);
        Assert.Null(ancora);
        Assert.NotNull(dopo);
        Assert.DoesNotContain(passi, p => p.Contains("sector", StringComparison.Ordinal));
    }

    [Fact]
    public void LeCartelleOccupateORelativeSiRifiutano()
    {
        Assert.Contains("per intero", PrimoAvvio.PercheNonVa("IVAO", _repo.Server, _repo.Server), StringComparison.Ordinal);
        Assert.Contains("cartella di lavoro", PrimoAvvio.PercheNonVa(" ", _repo.Server, _repo.Server), StringComparison.Ordinal);
        Assert.StartsWith("AIP espanso:", PrimoAvvio.PercheNonVa(Lavoro, _repo.Server, "--upload-pack=calc"), StringComparison.Ordinal);

        Directory.CreateDirectory(Path.Combine(Lavoro, "sector"));
        File.WriteAllText(Path.Combine(Lavoro, "sector", "altro.txt"), "non è un sector");
        Assert.Contains("non è il sector", PrimoAvvio.PercheNonVa(Lavoro, _repo.Server, _repo.Server), StringComparison.Ordinal);
    }
}
