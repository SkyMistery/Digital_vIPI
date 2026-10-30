using Vipi.SectorLab.Core.PdfAip;
using Vipi.SectorLab.Tests.Ui;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Carta «Gli aeroporti nel Lab», §2 D, controllo 3: gli stand dei <c>.gts</c> contro le coordinate che la carta dei
/// parcheggi stampa. Le righe hanno le forme del 2611 com'è lette dal disegno (LIPE, LIEE, LIPY, LIRP, LIRI); il sector e i
/// numeri sono inventati.
/// </summary>
public sealed class StandDelloScaloTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();

    public void Dispose() => _albero.Dispose();

    private static PaginaDiNote Pagina(params string[] righe) => new("AIRCRAFT PARKING DOCKING CHART ICAO", "AD 2 LIXX 2-7", null, righe);

    [Fact]
    public void IPunti_LeFormeDelDisegno()
    {
        var punti = StandDelloScalo.Punti([Pagina(
            // LIPE: più punti sulla riga, «-stop1», «-stop 1», secondi con '' o ".
            "121.925 (APRON \"1\") 112-stop2 44° 31' 45.07\" 011° 17' 47.97\" 303-stop1 44° 32' 4.16'' 011° 16' 51.19''",
            "Coordinates WGS84 ATIS 102-stop 1 44° 31' 47.49'' 011° 17' 29.58''",
            // LIRP: « - » fra latitudine e longitudine, e gli stand «L7».
            "CIVIL AVIATION APRON 10 43°41'41.60\" - 010°23'47.71\" L7 43°41'47.11'' - 010°23'48.29''",
            // LIRI: i decimali dopo le virgolette.
            "21 40°37'25'',99 014°54'52'',05",
            // LIPY: la lettera dopo il numero.
            "11A 43°36'24.64\" 013°21'13.46\" P",
            // Una coordinata senza nome davanti (il nome è su un'altra riga del disegno) e l'ARP con la N: fuori.
            "D 011° 17' 50.02\" 44° 31' 55.87'' 011° 17' 19.78''",
            "L I P E 44°31'51\" N 011°17'49\" E")]);

        Assert.Equal(["112-stop2", "303-stop1", "102-stop1", "10", "L7", "21", "11A"], punti.Select(p => p.Nome));
        Assert.Equal(["112", "303", "102", "10", "L7", "21", "11A"], punti.Select(p => p.Stand));
        Assert.Equal(44 + 31 / 60.0 + 45.07 / 3600, punti[0].Posizione.Lat, 9);
        Assert.Equal(14 + 54 / 60.0 + 52.05 / 3600, punti[5].Posizione.Lon, 9);
        Assert.Equal("AD 2 LIXX 2-7", punti[0].Pagina);
    }

    [Theory]
    [InlineData("44° 31' 47.36\"", 44 + 31 / 60.0 + 47.36 / 3600)]
    [InlineData("011°17'44''", 11 + 17 / 60.0 + 44 / 3600.0)]
    [InlineData("40°37'25'',99", 40 + 37 / 60.0 + 25.99 / 3600)]
    [InlineData("44° 75' 10\"", null)]
    [InlineData("niente", null)]
    public void IGradi(string testo, double? gradi)
    {
        if (gradi is null)
            Assert.Null(StandDelloScalo.Gradi(testo));
        else
            Assert.Equal(gradi.Value, StandDelloScalo.Gradi(testo)!.Value, 9);
    }

    [Fact]
    public async Task IlConfronto_InMetri_ColPuntoPiuVicinoDelloStessoStand()
    {
        // 110: due punti d'arresto, il sector sul secondo. 07 nel sector, «7» nell'AIP. 29S1 e 29S2 (LIRP) contro lo stand 29.
        // 211 a una ventina di metri. 999 l'AIP non lo stampa; 401 nessun .gts lo ha. La riga di LIYY non è di LIXX.
        _albero.Scrivi("SectorFiles/Include/IT/lixx.gts",
            "110;LIXX;N044.31.47.160;E011.17.44.290;\r\n"
            + "07;LIXX;N044.31.40.000;E011.17.40.000;\r\n"
            + "29S1;LIXX;N044.31.30.000;E011.17.30.000;\r\n"
            + "29S2;LIXX;N044.31.30.300;E011.17.30.000;\r\n"
            + "211;LIXX;N044.31.58.600;E011.17.11.000;\r\n"
            + "999;LIXX;N044.31.00.000;E011.17.00.000;\r\n"
            + "401;LIYY;N044.31.00.000;E011.17.00.000;\r\n");
        var lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        Assert.True(await lab.ApriEValidaAsync(_albero.Radice));
        var punti = StandDelloScalo.Punti([Pagina(
            "110-stop1 44° 31' 47.36\" 011° 17' 44.44\" 110-stop2 44° 31' 47.16\" 011° 17' 44.29\"",
            "7 44° 31' 40.00\" 011° 17' 40.00\" 29 44° 31' 30.00\" 011° 17' 30.00\"",
            "211-stop1 44° 31' 58.00\" 011° 17' 11.00\" 401 44° 31' 10.00\" 011° 17' 10.00\"")]);

        var c = StandDelloScalo.Confronta(punti, "LIXX", lab.Sessione!);

        Assert.Equal(["110", "07", "29S1", "29S2", "211", "999"], c.Stand.Select(x => x.Stand));
        var s110 = c.Stand[0];
        Assert.Equal("110-stop2", s110.Punto!.Nome);
        Assert.InRange(s110.Metri!.Value, 0, 1);
        Assert.InRange(c.Stand[1].Metri!.Value, 0, 1);
        Assert.InRange(c.Stand[2].Metri!.Value, 0, 1);
        Assert.InRange(c.Stand[3].Metri!.Value, 8, 11);
        Assert.Equal("29", c.Stand[3].Punto!.Nome);
        Assert.InRange(c.Stand[4].Metri!.Value, 17, 20);
        Assert.True(c.Stand[4].Diverso);
        Assert.Null(c.Stand[5].Punto);
        Assert.False(c.Stand[5].Diverso);
        Assert.Equal(["401"], c.MancanoNelSector);
        Assert.Equal(["SectorFiles/Include/IT/lixx.gts"], c.File);
    }
}
