using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Tests.Sessione;

/// <summary>L'albero aperto (carta F3 §2.2 passo 1, slice 2): tutto letto, ogni file con la sua impronta.</summary>
public sealed class SessioneApertaTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();

    public void Dispose() => _albero.Dispose();

    private SessioneAperta Apri()
        => SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);

    [Fact]
    public void OgniFileDeiDatiEAperto()
    {
        var sessione = Apri();

        int nelLaCartella = Directory.GetFiles(_albero.CartellaIt, "*", SearchOption.AllDirectories).Length;
        Assert.Equal(nelLaCartella, sessione.File.Keys.Count(k => k.StartsWith("SectorFiles/Include/IT/", StringComparison.Ordinal)));
        Assert.True(sessione.RecordTotali > 1000, $"record {sessione.RecordTotali}");
    }

    [Fact]
    public void IFormatiCheIlMotoreConosceSonoLettiGliAltriNo()
    {
        var sessione = Apri();

        var sid = Assert.IsType<FileLetto<Vipi.Sectorfile.Models.SidProcedure>>(sessione.File["SectorFiles/Include/IT/lirf.sid"]);
        Assert.True(sid.Record > 0);
        Assert.IsType<FileNonInterpretato>(sessione.File["SectorFiles/Include/IT/LEGGIMI.md"]);
    }

    [Fact]
    public void LaChiaveNonGuardaLeMaiuscole()
        => Assert.True(Apri().File.ContainsKey("sectorfiles/include/it/navaids/ITVOR.VOR"));

    [Theory]
    [InlineData("SectorFiles/ITALY.isc")]
    [InlineData("SectorFiles/LIRR.isc")]
    [InlineData("SectorFiles/update.ini")]
    [InlineData("changelog.md")]
    public void GliIscEIFileDiConsegnaSonoApertiComeTesto(string relativo)
        => Assert.IsType<FileNonInterpretato>(Apri().File[relativo]);

    [Fact]
    public void QuelCheStaFuoriNonSiApre()
        => Assert.False(Apri().File.ContainsKey("Aurora.exe"));

    [Fact]
    public void LImprontaEQuellaDeiByteDelFile()
    {
        var sessione = Apri();
        const string relativo = "SectorFiles/Include/IT/NAVAIDS/itvor.vor";

        Assert.Equal(Impronta.Di(File.ReadAllBytes(_albero.Percorso(relativo))), sessione.File[relativo].Impronta);
    }

    [Fact]
    public void LeRigheSonoQuelleDelFile()
    {
        var sessione = Apri();
        const string relativo = "SectorFiles/Include/IT/lirf.str";

        string testo = File.ReadAllText(_albero.Percorso(relativo));
        int righe = testo.Split('\n').Length - (testo.EndsWith('\n') ? 1 : 0);
        Assert.Equal(righe, sessione.File[relativo].Righe);
    }

    [Fact]
    public void AppenaApertoNienteECambiato()
        => Assert.Empty(Apri().CambiatiSulDisco());

    [Fact]
    public void UnFileRiscrittoDaAltriSiVede()
    {
        var sessione = Apri();
        const string relativo = "SectorFiles/Include/IT/GEO/liap.geo";

        File.AppendAllText(_albero.Percorso(relativo), "\r\n// riga aggiunta da un altro editor");

        Assert.Equal([relativo], sessione.CambiatiSulDisco());
    }

    [Fact]
    public void UnFileSparitoSiVede()
    {
        var sessione = Apri();
        const string relativo = "SectorFiles/Include/IT/lirh.vrt";

        File.Delete(_albero.Percorso(relativo));

        Assert.Equal([relativo], sessione.CambiatiSulDisco());
    }

    [Fact]
    public void UnFileRiscrittoUgualeNonECambiato()
    {
        // Git che riscrive un file con gli stessi byte (un checkout, un rebase) non è un conflitto.
        var sessione = Apri();
        string percorso = _albero.Percorso("SectorFiles/Include/IT/lirf.gts");
        File.WriteAllBytes(percorso, File.ReadAllBytes(percorso));

        Assert.Empty(sessione.CambiatiSulDisco());
    }

    [Fact]
    public void ITmpRimastiSiSegnalanoENonSiAprono()
    {
        _albero.Scrivi("SectorFiles/Include/IT/GEO/liap.geo.tmp", "mezzo salvataggio");

        var sessione = Apri();

        Assert.Equal(["SectorFiles/Include/IT/GEO/liap.geo.tmp"], sessione.TmpOrfani);
        Assert.False(sessione.File.ContainsKey("SectorFiles/Include/IT/GEO/liap.geo.tmp"));
    }

    [Fact]
    public async Task SiApreAncheFuoriDalFiloChiamante()
    {
        var sessione = await SessioneAperta.ApriAsync(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        Assert.NotEmpty(sessione.File);
    }
}
