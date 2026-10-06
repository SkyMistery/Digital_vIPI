using Vipi.SectorLab.Tests.Ui;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Modifiche;

/// <summary>
/// Un punto VFR nuovo (lotto «Subito» slice 16c, «file per file» F3): il Lab propone il codice — il primo numero
/// libero dopo quello del punto da cui si parte, con le stesse lettere — e il punto nasce al suo posto in ordine di
/// CODICE, com'è ordinato il file (67 <c>.vfi</c> su 77 sul fork), non di nome.
/// </summary>
public sealed class PuntoVfrNuovoTests : IDisposable
{
    private const string Vfi = "SectorFiles/Include/IT/lzzz.vfi";
    private const string Vicino = "SectorFiles/Include/IT/lzzy.vfi";
    private const string Nascosti = "SectorFiles/Include/IT/NAVAIDS/VFR_NASCOSTI.fix";

    private readonly AlberoDiProva _albero = new();
    private readonly SessioneDelLab _lab;

    public PuntoVfrNuovoTests()
    {
        _albero.Scrivi(Vfi, "ZULU;ZZN1;N041.50.00.000;E012.20.00.000;\r\nBRAVO;ZZN2;N041.51.00.000;E012.20.00.000;\r\n"
                            + "COLOMBO;ZZS3;N041.42.47.000;E012.21.56.000;\r\nDELTA;ZZS10;N041.40.00.000;E012.21.00.000;\r\n");
        // ZZN3 è di un punto dello scalo vicino, ZZN4 di un fix nascosto rimasto senza punto: non sono liberi.
        _albero.Scrivi(Vicino, "ALTROVE;ZZN3;N041.55.00.000;E012.25.00.000;\r\nSENZA;ZY;N041.56.00.000;E012.25.00.000;\r\n");
        _albero.Scrivi(Nascosti, "ZZN1;N0415000000;E0122000000;3;\r\nZZN4;N0450000000;E0100000000;3;\r\nZZS3;N0414247000;E0122156000;3;\r\n");
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
    }

    public void Dispose() => _albero.Dispose();

    [Fact]
    public async Task IlCodicePropostoEIlPrimoNumeroLiberoDopoQuelloDelModello()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

        // Da BRAVO (ZZN2): ZZN3 e ZZN4 sono presi, in un altro .vfi e fra i nascosti.
        Assert.Equal("ZZN5", _lab.CodiceProposto(Vfi, 1));
        Assert.Equal("ZZS4", _lab.CodiceProposto(Vfi, 2));
        Assert.Equal("ZZS11", _lab.CodiceProposto(Vfi, 3));
        // Dal file, senza un punto scelto: dall'ultimo che ha un codice.
        Assert.Equal("ZZS11", _lab.CodiceProposto(Vfi, null));
        // Un punto senza codice (il 2° campo è lo scalo, o una quota) non ne propone: resta quello che ha.
        Assert.Equal("ZY", _lab.CodiceProposto(Vicino, 1));
    }

    [Fact]
    public async Task IlPuntoNuovoNasceInOrdineDiCodice_NonDiNome()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

        // Per nome ALFA andrebbe in testa; per codice ZZN5 va dopo ZZN2. E ZZS4 sta fra ZZS3 e ZZS10: il numero conta
        // come numero.
        Assert.True(_lab.AggiungiRecord(Vfi, 0, "ALFA", codice: "ZZN5"));
        Assert.True(_lab.AggiungiRecord(Vfi, 0, "MIKE", codice: "ZZS4"));

        Assert.Equal(["ZULU;ZZN1;", "BRAVO;ZZN2;", "ALFA;ZZN5;", "COLOMBO;ZZS3;", "MIKE;ZZS4;", "DELTA;ZZS10;"],
            _lab.RigheDiAdesso(Vfi).Where(r => r.Length > 0).Select(r => string.Join(';', r.Split(';').Take(2)) + ";"));
        // Il nuovo parte dalla posizione del modello: la cambia l'AOD.
        Assert.Contains("ALFA;ZZN5;N041.50.00.000;E012.20.00.000;", _lab.RigheDiAdesso(Vfi));
    }

    [Fact]
    public async Task SenzaDireIlCodiceIlLabMetteQuelloProposto()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

        Assert.True(_lab.AggiungiRecord(Vfi, 1, "ALFA"));

        Assert.Contains("ALFA;ZZN5;N041.51.00.000;E012.20.00.000;", _lab.RigheDiAdesso(Vfi));
        // E il prossimo non ripropone lo stesso.
        Assert.Equal("ZZN6", _lab.CodiceProposto(Vfi, 1));
    }

    [Theory]
    [InlineData("ZZS3", "COLOMBO")]              // in questo file
    [InlineData("ZZN3", "lzzy.vfi")]             // in un altro .vfi
    [InlineData("ZZN4", "VFR_NASCOSTI.fix")]     // fra i fix nascosti
    public async Task UnCodiceGiaPresoERifiutatoColPerche(string codice, string dove)
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

        Assert.False(_lab.AggiungiRecord(Vfi, 0, "ALFA", codice: codice));

        Assert.Contains(dove, _lab.Rifiuto, StringComparison.Ordinal);
        Assert.False(_lab.Modifiche.CEQualcosa);
    }

    [Fact]
    public async Task DalFileIlModelloEIlVicinoPerCodice()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

        Assert.True(_lab.AggiungiAlFile(Vfi, "ALFA", codice: "ZZN9"));

        var righe = _lab.RigheDiAdesso(Vfi).Where(r => r.Length > 0).ToList();
        Assert.Equal("ALFA;ZZN9;N041.51.00.000;E012.20.00.000;", righe[2]);
    }
}
