using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Tests.Sessione;

/// <summary>La cartella del sector riconosciuta da quella scelta, e i confini di scrittura (carta F3 §2.2, §2.4).</summary>
public sealed class CartellaEConfiniTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();

    public void Dispose() => _albero.Dispose();

    private CartellaDelSector Cartella()
        => CartellaDelSector.Riconosci(_albero.Radice, out _) ?? throw new InvalidOperationException("albero di prova non riconosciuto");

    [Theory]
    [InlineData("")]
    [InlineData("SectorFiles")]
    [InlineData("SectorFiles/Include/IT")]
    [InlineData("SectorFiles/Include/IT/GEO")]
    public void LaRadiceSiTrovaDaQualunqueCartellaDentro(string scelta)
    {
        var cartella = CartellaDelSector.Riconosci(_albero.Percorso(scelta), out string? motivo);

        Assert.Null(motivo);
        Assert.NotNull(cartella);
        Assert.Equal(Path.GetFullPath(_albero.Radice), cartella.Radice);
        Assert.Equal(Path.GetFullPath(_albero.CartellaIt), cartella.CartellaIt);
    }

    [Fact]
    public void UnaCartellaCheNonEDiUnSectorDiceIlPerche()
    {
        string altrove = Path.Combine(Path.GetTempPath(), "non-sector-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(altrove);
        try
        {
            Assert.Null(CartellaDelSector.Riconosci(altrove, out string? motivo));
            Assert.Contains("SectorFiles", motivo, StringComparison.Ordinal);
            Assert.Null(CartellaDelSector.Riconosci(Path.Combine(altrove, "non-esiste"), out motivo));
            Assert.Contains("non esiste", motivo, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(altrove, recursive: true);
        }
    }

    [Fact]
    public void SenzaIscONiDatiNonESector()
    {
        foreach (string isc in Directory.GetFiles(Path.Combine(_albero.Radice, "SectorFiles"), "*.isc"))
            File.Delete(isc);
        Assert.Null(CartellaDelSector.Riconosci(_albero.Radice, out string? motivo));
        Assert.Contains(".isc", motivo, StringComparison.Ordinal);

        _albero.Scrivi("SectorFiles/ITALY.isc", "[INFO]");
        Directory.Delete(_albero.CartellaIt, recursive: true);
        Assert.Null(CartellaDelSector.Riconosci(_albero.Radice, out motivo));
        Assert.Contains("Include", motivo, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SectorFiles/Include/IT/GEO/liap.geo")]
    [InlineData("SectorFiles/Include/IT/nuova/cartella/nuovo.fix")]
    [InlineData("SectorFiles/update.ini")]
    [InlineData("changelog.md")]
    public void DentroIConfini(string relativo)
    {
        Assert.True(Confini.Scrivibile(Cartella(), relativo));
        Assert.True(Confini.Scrivibile(Cartella(), _albero.Percorso(relativo)));
    }

    [Theory]
    [InlineData("Aurora.exe")]
    [InlineData("SectorFiles/ITALY.isc")]
    [InlineData("SectorFiles/delete.upd")]
    [InlineData("SectorFiles/Include/IT")]
    [InlineData("SectorFiles/Include/altro.txt")]
    [InlineData("README.md")]
    [InlineData("SectorFiles/Include/IT/../../../Aurora.exe")]
    [InlineData("SectorFiles/Include/IT/../../ITALY.isc")]
    [InlineData("../fuori.txt")]
    public void FuoriDaiConfini(string relativo)
    {
        Assert.False(Confini.Scrivibile(Cartella(), relativo));
        Assert.Throws<UnauthorizedAccessException>(() => Confini.Pretendi(Cartella(), relativo));
    }

    [Fact]
    public void UnaRisalitaScrittaCoiSeparatoriDelSistemaEFuori()
    {
        // Il caso che conta: comincia con la cartella IT, separatori veri, e risale fino ad Aurora.exe. Coi casi scritti
        // con «/» il test passava anche togliendo la normalizzazione (su Windows «/» e «\» non combaciavano) — la
        // controprova della slice 2 l'ha mostrato.
        string risalita = Path.Combine(_albero.CartellaIt, "..", "..", "..", "Aurora.exe");
        Assert.False(Confini.Scrivibile(Cartella(), risalita));
        Assert.False(Confini.Scrivibile(Cartella(), Path.Combine("SectorFiles", "Include", "IT", "..", "..", "ITALY.isc")));
    }

    [Fact]
    public void UnPercorsoAssolutoFuoriDalCloneEFuori()
        => Assert.False(Confini.Scrivibile(Cartella(), Path.Combine(Path.GetTempPath(), "altrove", "x.fix")));

    [Fact]
    public void UnaCartellaCheCominciaComeITNonEIT()
    {
        // «IT-vecchio» comincia con «IT» come stringa, ma non sta DENTRO IT.
        Assert.False(Confini.Scrivibile(Cartella(), "SectorFiles/Include/IT-vecchio/x.fix"));
    }
}
