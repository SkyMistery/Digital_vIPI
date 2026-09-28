using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.Sessione;
using Vipi.SectorLab.Ui.Servizi;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Tests.Mappa;

/// <summary>
/// I simboli dei punti dal <c>.sym</c> del master (lotto «Subito» slice 4d): il committente li vuole dal sector. Quale
/// simbolo per quale tipo è una proposta, per nome del simbolo (<see cref="SimboliDellaMappa.Abbinamenti"/>).
/// </summary>
public sealed class SimboliDellaMappaTests : IDisposable
{
    private const string Pixel = "0000000100000;0000001100000;0000010100000;0000100100000;0001000100000;0010000100000;0100000100000;0010000100000;0001000100000;0000100100000;0000010100000;0000001100000;0000000100000;";

    private readonly AlberoDiProva _albero = new();

    public void Dispose() => _albero.Dispose();

    private static SimboloDelSector Simbolo(int numero, string? nome)
        => new(numero, nome, Pixel.Split(';', StringSplitOptions.RemoveEmptyEntries), numero * 2);

    private static SimboliDellaMappa Simboli()
        => new([Simbolo(1, "APT"), Simbolo(2, "FIX vuoto"), Simbolo(3, "FIX pieno"), Simbolo(4, "TERM"), Simbolo(5, "VOR"), Simbolo(6, "NDB")]);

    private static FormaDellaMappa Punto(string? tipo) => new("x", 0, TipoDiForma.Punto, "P", [], [], Punto: tipo);

    [Theory]
    [InlineData("FIX:0", 1)]
    [InlineData("FIX:1", 3)]      // terminale → TERM
    [InlineData("FIX:2", 2)]      // in rotta e terminale → FIX pieno
    [InlineData("FIX", 1)]        // tipo non scritto
    [InlineData("FIX:7", 1)]      // tipo che la tabella non ha: il simbolo della famiglia
    [InlineData("VOR:0", 4)]
    [InlineData("NDB", 5)]
    [InlineData("APT", 0)]
    public void OgniTipoHaIlSuoSimboloPerNome(string tipo, int indice)
        => Assert.Equal(indice, Simboli().Di(Punto(tipo)));

    [Theory]
    [InlineData("FIX:3")]   // «FIX vuoto piccolo» non c'è in questo .sym
    [InlineData("VFR")]
    [InlineData(null)]      // una forma che non è un punto
    [InlineData("GATE")]
    public void SenzaIlSimboloNelFileResta_unCerchio(string? tipo)
        => Assert.Null(Simboli().Di(Punto(tipo)));

    [Fact]
    public void INomiSiConfrontanoSenzaMaiuscole()
        => Assert.Equal(0, new SimboliDellaMappa([Simbolo(1, " fix VUOTO ")]).Di(Punto("FIX:0")));

    [Fact]
    public void IlTipoDelFixViaggiaConLaForma()
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        var catalogo = CatalogoDeiPunti.PerOgniIsc(sessione)["ITALY.isc"];

        var bc404 = Geometria.DelFile(sessione.File["SectorFiles/Include/IT/NAVAIDS/APT.fix"], catalogo).First(f => f.Etichetta == "BC404");

        Assert.Equal("FIX:3", bc404.Punto);   // BC404;…;3; (nascosto)
    }

    [Fact]
    public async Task ISimboliSonoQuelliDelSymDelMaster()
    {
        string isc = File.ReadAllText(_albero.Percorso("SectorFiles/ITALY.isc"));
        _albero.Scrivi("SectorFiles/ITALY.isc", isc + "\r\n[SYMBOLS]\r\nF;symbols.sym\r\n");
        _albero.Scrivi("SectorFiles/Include/IT/symbols.sym", "//APT\r\n" + Pixel + "\r\n\r\n//FIX vuoto piccolo\r\n" + Pixel + "\r\n");

        var lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        Assert.True(await lab.ApriAsync(_albero.Radice));
        await lab.ScegliIscAsync("ITALY.isc");

        Assert.NotNull(lab.Simboli);
        Assert.Equal(["APT", "FIX vuoto piccolo"], lab.Simboli!.Simboli.Select(s => s.Nome));
        var bc404 = lab.Strati.Single(s => s.Id == "punti").Forme.First(f => f.Etichetta == "BC404");
        Assert.Equal(1, lab.Simboli.Di(bc404));
    }

    [Fact]
    public async Task UnMasterSenzaSymNonHaSimboli()
    {
        var lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        Assert.True(await lab.ApriAsync(_albero.Radice));

        Assert.Null(lab.Simboli);
    }
}
