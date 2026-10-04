using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Tests.Ispezione;

/// <summary>
/// I valori dei metadati di SID e STAR come li vuole il committente (prova 68, 28 settembre): fix e transizione punti del
/// sector, salita iniziale in ft/FL o COO APP, categorie di scia e Vref come tasti col doppio clic.
/// </summary>
public sealed class ValoriDeiMetadatiTests
{
    private static bool Conosce(string nome) => nome is "EKLOS" or "ESINO";

    private static (bool Ok, string? Scritto, string? Perche) Normalizza(string chiave, string? valore)
        => (ValoriDeiMetadati.Normalizza(chiave, valore, Conosce, out string? scritto, out string? perche), scritto, perche);

    [Theory]
    [InlineData("fix", EditorDelMetadato.Punto)]
    [InlineData("trans", EditorDelMetadato.Punto)]
    [InlineData("initialclimb", EditorDelMetadato.SalitaIniziale)]
    [InlineData("wtc", EditorDelMetadato.Lettere)]
    [InlineData("cat", EditorDelMetadato.Lettere)]
    [InlineData("nav", EditorDelMetadato.Scelta)]
    [InlineData("note", EditorDelMetadato.Testo)]
    public void OgniChiaveHaIlSuoEditor(string chiave, EditorDelMetadato editor)
        => Assert.Equal(editor, ValoriDeiMetadati.EditorDi(chiave, siNo: false));

    [Theory]
    [InlineData("eklos", "EKLOS")]
    [InlineData(" ESINO ", "ESINO")]
    public void FixETransizioneSonoPuntiDelSector(string scritto, string atteso)
    {
        Assert.Equal((true, atteso, null), Normalizza("fix", scritto));
        Assert.Equal((true, atteso, null), Normalizza("trans", scritto));
    }

    [Fact]
    public void UnFixCheIlMasterNonConosceSiRifiutaColPerche()
    {
        var (ok, _, perche) = Normalizza("fix", "EKLOSS");

        Assert.False(ok);
        Assert.Contains("non è un punto del sector", perche, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("6000", "6000ft")]
    [InlineData("6000ft", "6000ft")]
    [InlineData("6000 FT", "6000ft")]
    [InlineData("2500'", "2500ft")]
    [InlineData("FL80", "FL80")]
    [InlineData("fl 100", "FL100")]
    [InlineData("coo app", "COO APP")]
    public void LaSalitaInizialeEInPiediOFlOCooApp(string scritto, string atteso)
        => Assert.Equal((true, atteso, null), Normalizza("initialclimb", scritto));

    // Slice 13c (D9, J7, Q8; §M regola 8): i limiti verticali «come nel PDF, in piedi» — SFC, GND, 1500ft, FL195, UNL.
    [Theory]
    [InlineData("lower", "sfc", "SFC")]
    [InlineData("lower", "gnd", "GND")]
    [InlineData("lower", "1500", "1500ft")]
    [InlineData("lower", "1500 FT", "1500ft")]
    [InlineData("upper", "fl 195", "FL195")]
    [InlineData("upper", "FL095", "FL95")]
    [InlineData("upper", "unl", "UNL")]
    [InlineData("upper", "2000ft", "2000ft")]
    public void ILimitiVerticaliSiScrivonoComeNelPdf(string chiave, string scritto, string atteso)
        => Assert.Equal((true, atteso, null), Normalizza(chiave, scritto));

    [Theory]
    [InlineData("lower", "basso")]
    [InlineData("upper", "FL700")]
    [InlineData("upper", "600m")]
    [InlineData("lower", "UNL")]
    [InlineData("upper", "SFC")]
    public void UnLimiteCheNonEUnaQuotaSiRifiutaColPerche(string chiave, string scritto)
    {
        var (ok, _, perche) = Normalizza(chiave, scritto);

        Assert.False(ok);
        Assert.Contains("SFC", perche, StringComparison.Ordinal);
    }

    [Fact]
    public void LaClasseDelloSpazioAereoEUnaSceltaDaAaG()
    {
        Assert.Equal(EditorDelMetadato.Scelta, ValoriDeiMetadati.EditorDi("class", siNo: false));
        Assert.Equal((true, "D", null), Normalizza("class", "d"));
        Assert.False(Normalizza("class", "H").Ok);
        Assert.Equal(["A", "B", "C", "D", "E", "F", "G"], ValoriDeiMetadati.Scelte["class"]);
    }

    [Theory]
    [InlineData("seimila")]
    [InlineData("FL700")]
    [InlineData("COO")]
    [InlineData("6000m")]
    public void UnaSalitaCheNonEUnaQuotaSiRifiuta(string scritto)
    {
        var (ok, _, perche) = Normalizza("initialclimb", scritto);

        Assert.False(ok);
        Assert.NotNull(perche);
    }

    [Theory]
    [InlineData("wtc", "hml", "LMH")]
    [InlineData("wtc", "L, M, S", "LMS")]
    [InlineData("cat", "dcba", "ABCD")]
    public void LeLettereSiScrivonoInOrdine(string chiave, string scritto, string atteso)
        => Assert.Equal((true, atteso, null), Normalizza(chiave, scritto));

    [Fact]
    public void UnaLetteraFuoriInsiemeSiRifiutaEIlVuotoToglie()
    {
        Assert.False(Normalizza("cat", "ABF").Ok);
        Assert.Equal((true, (string?)null, (string?)null), Normalizza("wtc", " "));
    }

    [Theory]
    // Un clic: accende o spegne quella lettera sola.
    [InlineData("wtc", "", 'H', false, "H")]
    [InlineData("wtc", "LMH", 'M', false, "LH")]
    // Doppio clic su H: il primo clic l'ha accesa, il secondo accende anche L e M (S resta com'era).
    [InlineData("wtc", "H", 'H', true, "LMH")]
    [InlineData("wtc", "HS", 'H', true, "LMHS")]
    // Doppio clic su H quando il primo clic l'ha spenta: si spengono anche L e M.
    [InlineData("wtc", "LMS", 'H', true, "S")]
    [InlineData("cat", "D", 'D', true, "ABCD")]
    [InlineData("cat", "ABCDE", 'A', true, "ABCDE")]   // A non ha precedenti
    public void IlDoppioClicPortaLePrecedentiAlloStatoDiQuellaCliccata(string chiave, string attuale, char lettera, bool doppio, string atteso)
        => Assert.Equal(atteso, ValoriDeiMetadati.Clic(chiave, attuale, lettera, doppio));

    [Fact]
    public void LaRicercaDeiFileGuardaIlNomePoiIlPercorso()
    {
        string[] file =
        [
            "SectorFiles/Include/IT/lirf.sid", "SectorFiles/Include/IT/lirf.str", "SectorFiles/Include/IT/GEO/lirf.geo",
            "SectorFiles/Include/IT/GND_LAYOUT/rf_ad_gnd.pol", "SectorFiles/Include/IT/limf.sid", "SectorFiles/ITALY.isc",
        ];

        Assert.Equal(["SectorFiles/Include/IT/GEO/lirf.geo", "SectorFiles/Include/IT/lirf.sid", "SectorFiles/Include/IT/lirf.str"],
            Ricerca.CercaFile(file, "LIRF"));
        Assert.Equal(["SectorFiles/Include/IT/lirf.sid"], Ricerca.CercaFile(file, "lirf.sid"));
        Assert.Equal(["SectorFiles/Include/IT/GND_LAYOUT/rf_ad_gnd.pol"], Ricerca.CercaFile(file, @"GND_LAYOUT\rf"));
        Assert.Equal(["SectorFiles/ITALY.isc"], Ricerca.CercaFile(file, "italy"));
        Assert.Empty(Ricerca.CercaFile(file, "x"));
    }
}
