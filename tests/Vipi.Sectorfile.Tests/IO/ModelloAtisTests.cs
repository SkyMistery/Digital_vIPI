using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Lotto «Subito», slice 18a (carta «file per file» §22, W1 e W4): un modello ATIS è un testo coi segnaposto fra
/// parentesi quadre (<c>[ATIS_LETTER]</c>) e le parti facoltative, anche annidate (<c>[Arrival runway [ARR]]</c>: c'è
/// solo se <c>ARR</c> ha un valore). I <c>.datis</c> hanno la stessa forma; i <c>.fds</c> dichiarano i campi in più.
/// </summary>
public sealed class ModelloAtisTests
{
    private readonly CollectingWarnings _warnings = new();

    [Fact]
    public void UnModelloSiLeggeInTestoSegnapostoEPartiFacoltative()
    {
        var letto = ModelloAtis.Leggi("This is [STATION_NAME] at [ATIS_TIME]. [Arrival runway [ARR]]. [METAR] .");

        Assert.True(letto.Bilanciato);
        Assert.Equal(["STATION_NAME", "ATIS_TIME", "ARR", "METAR"], letto.Segnaposto);
        Assert.Collection(letto.Pezzi,
            p => Assert.Equal("This is ", Assert.IsType<TestoDelModello>(p).Testo),
            p => Assert.Equal("STATION_NAME", Assert.IsType<SegnapostoDelModello>(p).Nome),
            p => Assert.Equal(" at ", Assert.IsType<TestoDelModello>(p).Testo),
            p => Assert.Equal("ATIS_TIME", Assert.IsType<SegnapostoDelModello>(p).Nome),
            p => Assert.Equal(". ", Assert.IsType<TestoDelModello>(p).Testo),
            p =>
            {
                var parte = Assert.IsType<ParteFacoltativa>(p);
                Assert.Equal("Arrival runway ", Assert.IsType<TestoDelModello>(parte.Pezzi[0]).Testo);
                Assert.Equal("ARR", Assert.IsType<SegnapostoDelModello>(parte.Pezzi[1]).Nome);
            },
            p => Assert.Equal(". ", Assert.IsType<TestoDelModello>(p).Testo),
            p => Assert.Equal("METAR", Assert.IsType<SegnapostoDelModello>(p).Nome),
            p => Assert.Equal(" .", Assert.IsType<TestoDelModello>(p).Testo));
    }

    [Fact]
    public void UnaParentesiInPiuOInMenoSiTrovaDoveSta()
    {
        // default.atis del fork: `[Runway in use [ARR]]].` — tre chiuse per due aperte.
        var inPiu = ModelloAtis.Leggi("at [ATIS_TIME]. [Runway in use [ARR]]]. [METAR]");
        Assert.False(inPiu.Bilanciato);
        Assert.Equal([37], inPiu.ChiuseInPiu);
        Assert.Empty(inPiu.MaiChiuse);
        // Il resto si legge lo stesso.
        Assert.Equal(["ATIS_TIME", "ARR", "METAR"], inPiu.Segnaposto);

        var inMeno = ModelloAtis.Leggi("at [ATIS_TIME]. [Runway in use [ARR]. [METAR]");
        Assert.Equal([16], inMeno.MaiChiuse);
        Assert.Empty(inMeno.ChiuseInPiu);
    }

    [Fact]
    public void RiempitoUnaParteFacoltativaCEsoloSeIlSuoSegnapostoHaUnValore()
    {
        var modello = ModelloAtis.Leggi("Information [ATIS_LETTER]. [Arrival runway [ARR]]. [departure runway [DEP]]. [Q F Echo [QFE]] . [NUOVO] fine");

        string testo = ModelloAtis.Riempi(modello, new Dictionary<string, string>
        {
            ["ATIS_LETTER"] = "Alfa", ["ARR"] = "16L", ["DEP"] = "", ["QFE"] = "1009",
        });

        // DEP è vuoto: la sua parte sparisce. NUOVO non lo conosce nessuno: resta scritto com'è.
        Assert.Equal("Information Alfa. Arrival runway 16L. . Q F Echo 1009 . [NUOVO] fine", testo);
    }

    [Fact]
    public void UnDatisSiLeggeComeUnAtis_EUnoVuotoNonHaModelli()
    {
        Assert.True(Formati.Usa("datis-ad.datis", _warnings, new Conta("This is [STATION_NAME] information [ATIS_LETTER]\r\n"), out int uno));
        Assert.Equal(1, uno);
        Assert.True(Formati.Usa("datis.datis", _warnings, new Conta(""), out int nessuno));
        Assert.Equal(0, nessuno);
        Assert.Empty(_warnings.Snapshot());
    }

    [Fact]
    public void UnFdsDichiaraICampiInPiuDellaFinestraAtis()
    {
        var letto = new FdsParser(_warnings).Parse(ParserTestHelpers.Read("//campi in più\r\nType of Approach;[ARR_TYPE];\r\nPista chiusa;[RWY_CLOSED];\r\n"), "atisextra.fds", new ColorPalette());

        Assert.Empty(_warnings.Snapshot());
        Assert.Equal([("Type of Approach", "ARR_TYPE"), ("Pista chiusa", "RWY_CLOSED")], letto.Records.Select(c => (c.Etichetta, c.Segnaposto)));
        Assert.Equal(["Type of Approach;[ARR_TYPE];"], new FdsSaver().Serialize(letto.Records[0]));

        // Una riga senza il segnaposto fra parentesi non è un campo: resta com'è, con l'avviso del lettore.
        var rotto = new FdsParser(_warnings).Parse(ParserTestHelpers.Read("Type of Approach;ARR_TYPE;\r\n"), "atisextra.fds", new ColorPalette());
        Assert.Empty(rotto.Records);
        Assert.Single(_warnings.Snapshot());
    }

    // Conta i record che il lettore scelto da Formati trova in quel testo.
    private sealed class Conta(string testo) : IUsoDelFormato<int>
    {
        public int Usa<T>(IFileParser<T> lettore, IFileSaver<T> scrittore)
            where T : class
        {
            var metodo = lettore.GetType().GetMethod("Parse", [typeof(FileReadResult), typeof(string), typeof(ColorPalette)])!;
            dynamic letto = metodo.Invoke(lettore, [ParserTestHelpers.Read(testo), "prova", new ColorPalette()])!;
            return letto.Records.Count;
        }
    }
}
