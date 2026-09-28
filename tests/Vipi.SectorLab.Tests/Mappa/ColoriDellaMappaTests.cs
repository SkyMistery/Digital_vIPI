using System.Drawing;
using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.Sessione;
using Vipi.SectorLab.Ui.Servizi;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Tests.Mappa;

/// <summary>
/// I colori di Aurora sulla mappa (lotto «Subito» slice 4, D3): lo schema scelto, i nomi di <c>colors.def</c> e chi
/// vince dove.
/// </summary>
public sealed class ColoriDellaMappaTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();

    public void Dispose() => _albero.Dispose();

    // Righe vere di LIRR_RDR_V1.0.clr; TAXIWAY è anche in colors.def (grigio), ed è il caso che conta.
    private const string Schema = """
        RADARBACK=$00040404
        ARTCCHIGH=$00F06E90
        ACC_HIGH_SOLID=3
        FIX=$007D716D
        IAP=$007D716D
        ATCPOSITION=clNone
        TAXIWAY=$FFFFFF00
        COAST=$FF906EF0
        """;

    private static readonly Color Giallo = Color.FromArgb(0xFF, 0xFF, 0x00);
    private static readonly Color Grigio = Color.FromArgb(0x76, 0x75, 0x87);

    private static ColoriDellaMappa Colori(string? schema = null)
    {
        var avvisi = new RaccoltaDiAvvisi();
        var letto = new ClrParser(avvisi).Parse(SectorFileReader.Decode(System.Text.Encoding.UTF8.GetBytes(schema ?? Schema)), "prova.clr");
        var definiti = new DefParser(avvisi).Parse(SectorFileReader.Decode("TAXIWAY;#767587;\r\nGRASS;#406230;\r\nTWR;#D3D3D3;\r\n"u8.ToArray()), "colors.def");
        Assert.Equal(0, avvisi.Count);
        return new ColoriDellaMappa(letto, definiti);
    }

    private static FormaDellaMappa Forma(string file, string? tratto = null, string? riempimento = null, bool soloBordo = false, string? chiave = null)
        => new(file, 0, TipoDiForma.Linea, "prova", [], [], tratto, riempimento, soloBordo, chiave);

    private static int? Argb(Color? colore) => colore?.ToArgb();

    [Fact]
    public void NelleLineeDelGeoVinceLoSchemaSuColorsDef()
    {
        // Committente, 28 settembre: i bordi taxiway in Aurora sono gialli (lo schema), non grigi (colors.def).
        var colore = Colori().Di(Forma("SectorFiles/Include/IT/GEO/lirf.geo", "TAXIWAY"));

        Assert.Equal(Argb(Giallo), Argb(colore.Tratto));
        Assert.Equal("schema TAXIWAY", colore.Origine);
        Assert.Null(colore.Riempimento);
    }

    [Theory]
    [InlineData("GRASS", 0x40, 0x62, 0x30, "colors.def GRASS")]   // nome di colors.def che lo schema non ha
    [InlineData("#2f2f2f", 0x2F, 0x2F, 0x2F, "#2F2F2F")]           // un valore
    public void NelGeoPoiColorsDefPoiIlValore(string scritto, int r, int g, int b, string origine)
    {
        var colore = Colori().Di(Forma("SectorFiles/Include/IT/GEO/lirf.geo", scritto));

        Assert.Equal(Color.FromArgb(r, g, b).ToArgb(), Argb(colore.Tratto));
        Assert.Equal(origine, colore.Origine);
    }

    [Theory]
    [InlineData("PIPPO", "sconosciuto PIPPO")]
    [InlineData("", "sconosciuto (vuoto)")]    // i 10 tipi vuoti di liap.geo
    public void UnNomeCheNessunoConosceEMagenta(string scritto, string origine)
    {
        var colore = Colori().Di(Forma("SectorFiles/Include/IT/GEO/liap.geo", scritto));

        Assert.Equal(ColoriDellaMappa.Sconosciuto.ToArgb(), Argb(colore.Tratto));
        Assert.Equal(origine, colore.Origine);
    }

    [Fact]
    public void NeiPolVinceColorsDefELoSchemaNonConta()
    {
        var colori = Colori();

        var taxiway = colori.Di(Forma("SectorFiles/Include/IT/GND_LAYOUT/rf_ad_gnd.pol", "TAXIWAY", "TAXIWAY"));
        Assert.Equal(Argb(Grigio), Argb(taxiway.Tratto));
        Assert.Equal(Argb(Grigio), Argb(taxiway.Riempimento));

        // L'orfano limw.pol: COAST lo colora lo schema nei .geo, ma nella testa di un .pol non è un nome di colors.def.
        var costa = colori.Di(Forma("SectorFiles/Include/IT/limw.pol", "COAST", "COAST"));
        Assert.Equal(ColoriDellaMappa.Sconosciuto.ToArgb(), Argb(costa.Tratto));
        Assert.Equal(ColoriDellaMappa.Sconosciuto.ToArgb(), Argb(costa.Riempimento));
    }

    [Fact]
    public void UnSettoreSoloBordoNonHaRiempimento()
    {
        var colori = Colori();

        var dinamico = colori.Di(Forma("SectorFiles/Include/IT/DYNAMIC_SEC/lirrctr.tfl", "TWR", "TWR", soloBordo: true));
        Assert.Equal(Color.FromArgb(0xD3, 0xD3, 0xD3).ToArgb(), Argb(dinamico.Tratto));
        Assert.Null(dinamico.Riempimento);

        var statico = colori.Di(Forma("SectorFiles/Include/IT/OTHER/GCI.tfl", "TWR", "GRASS"));
        Assert.Equal(Color.FromArgb(0x40, 0x62, 0x30).ToArgb(), Argb(statico.Riempimento));
    }

    [Fact]
    public void IRecordSenzaColoreScrittoPrendonoLaChiaveDelFileODellaVoce()
    {
        var colori = Colori();
        var fix = Color.FromArgb(0x6D, 0x71, 0x7D);   // $007D716D, rosso nel byte basso

        var punto = colori.Di(Forma("SectorFiles/Include/IT/NAVAIDS/APT.fix"));
        Assert.Equal(fix.ToArgb(), Argb(punto.Tratto));
        Assert.Equal("schema FIX", punto.Origine);

        var iap = colori.Di(Forma("SectorFiles/Include/IT/lirf.str", chiave: "IAP"));
        Assert.Equal("schema IAP", iap.Origine);

        // Lo stile della linea viene dall'impostazione della sua chiave: ACC_HIGH_SOLID=3.
        var alta = colori.Di(Forma("SectorFiles/Include/IT/HI_AIRSPACE/lirr.hartcc"));
        Assert.Equal(Color.FromArgb(0x90, 0x6E, 0xF0).ToArgb(), Argb(alta.Tratto));
        Assert.Equal(3, alta.Tratteggio);
    }

    [Fact]
    public void UnaChiaveCheLoSchemaNonHaSiVedeMagentaEClNoneNonSiDisegna()
    {
        var colori = Colori();

        var mva = colori.Di(Forma("SectorFiles/Include/IT/ENRMVA/lirr.mva"));   // MRVA non c'è in questo schema
        Assert.Equal(ColoriDellaMappa.Sconosciuto.ToArgb(), Argb(mva.Tratto));
        Assert.Equal("schema senza MRVA", mva.Origine);

        var nessuno = Colori("FIX=clNone\r\n").Di(Forma("SectorFiles/Include/IT/NAVAIDS/APT.fix"));
        Assert.Null(nessuno.Tratto);
    }

    [Fact]
    public void IlFondoEQuelloDelloSchermoRadar()
        => Assert.Equal(Color.FromArgb(4, 4, 4).ToArgb(), Argb(Colori().Sfondo));

    // --- la geometria porta i colori scritti -------------------------------------------------------------------------

    private IReadOnlyList<FormaDellaMappa> Forme(string relativo)
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        return Geometria.DelFile(sessione.File["SectorFiles/Include/IT/" + relativo], CatalogoDeiPunti.PerOgniIsc(sessione)["ITALY.isc"]);
    }

    [Fact]
    public void DueSegmentiAttaccatiDiColoreDiversoSonoDueLinee()
    {
        _albero.Scrivi("SectorFiles/Include/IT/GEO/prova.geo", """
            N041.00.00.000;E012.00.00.000;N041.01.00.000;E012.00.00.000;TAXIWAY;
            N041.01.00.000;E012.00.00.000;N041.02.00.000;E012.00.00.000;TAXIWAY;
            N041.02.00.000;E012.00.00.000;N041.03.00.000;E012.00.00.000;RUNWAY;
            """.ReplaceLineEndings("\r\n"));

        var forme = Forme("GEO/prova.geo");

        Assert.Equal(["TAXIWAY", "RUNWAY"], forme.Select(f => f.Tratto));
        Assert.Equal([3, 2], forme.Select(f => f.Punti));
    }

    [Theory]
    [InlineData("PROVA_CTR;TWR;1;TWR;1;", true)]    // dinamico
    [InlineData("STATIC;GRASS;1;TWR;0;", false)]   // statico, riempito
    [InlineData("STATIC;GRASS;1;TWR;1;", true)]    // statico con l'opacità a 1: riempimento trasparente
    public void IlSettoreSaSeHaIlRiempimento(string testa, bool soloBordo)
    {
        _albero.Scrivi("SectorFiles/Include/IT/DYNAMIC_SEC/prova.tfl",
            testa + "\r\nN041.00.00.000;E012.00.00.000;\r\nN042.00.00.000;E013.00.00.000;\r\nN041.00.00.000;E013.00.00.000;\r\n");

        var forma = Assert.Single(Forme("DYNAMIC_SEC/prova.tfl"));

        Assert.Equal("TWR", forma.Tratto);
        Assert.Equal(testa.Split(';')[1], forma.Riempimento);
        Assert.Equal(soloBordo, forma.SoloBordo);
    }

    [Fact]
    public void LeVociDegliStrPortanoLaLoroChiave()
    {
        _albero.Scrivi("SectorFiles/Include/IT/zzzz.str", "ZZZZ;16;ILS 16;;;3;\r\nBC404;BC404;\r\nBC406;BC406;\r\n");

        var forma = Assert.Single(Forme("zzzz.str"));

        Assert.Equal("IAP", forma.Chiave);
    }

    // --- quale schema ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("ITALY_GND.clr", "ITALY_GND.clr")]         // quello ricordato
    [InlineData("SPARITO.clr", "LIRR_RDR_V1.0.clr")]       // ricordato ma non c'è più: quello di base
    [InlineData(null, "LIRR_RDR_V1.0.clr")]
    public void SiUsaQuelloRicordatoPoiQuelloDiBase(string? ricordato, string scelto)
        => Assert.Equal(scelto, SchemiDiAurora.Scegli(["Default.clr", "ITALY_GND.clr", "LIRR_RDR_V1.0.clr"], ricordato));

    [Fact]
    public void SenzaQuelloDiBaseIlPrimoESenzaNienteNessuno()
    {
        Assert.Equal("Default.clr", SchemiDiAurora.Scegli(["Default.clr", "ITALY_GND.clr"], null));
        Assert.Null(SchemiDiAurora.Scegli([], null));
    }

    [Fact]
    public async Task LaSceltaDelloSchemaEDelModoSiRicordaFraUnAvvioELAltro()
    {
        _albero.Scrivi("ColorSchemes/LIRR_RDR_V1.0.clr", Schema.ReplaceLineEndings("\r\n"));
        _albero.Scrivi("ColorSchemes/ITALY_GND.clr", "RADARBACK=$00040404\r\n");
        string dati = Path.Combine(_albero.Radice, "dati-del-lab");

        var primo = new SessioneDelLab(dati);
        Assert.True(await primo.ApriAsync(_albero.Radice));
        Assert.Equal("LIRR_RDR_V1.0.clr", primo.SchemaScelto);
        Assert.True(primo.ColoriDiAurora);
        Assert.NotNull(primo.Colori);

        primo.ScegliSchema("ITALY_GND.clr");
        primo.UsaIColoriDiAurora(false);

        var secondo = new SessioneDelLab(dati);
        Assert.True(await secondo.ApriAsync(_albero.Radice));
        Assert.Equal("ITALY_GND.clr", secondo.SchemaScelto);
        Assert.False(secondo.ColoriDiAurora);
    }

    [Fact]
    public async Task SenzaSchemiLaMappaResta_coiColoriDelLabEDiceIlPerche()
    {
        var lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        Assert.True(await lab.ApriAsync(_albero.Radice));

        Assert.Null(lab.Colori);
        Assert.Contains("Nessuno schema di Aurora", lab.ColoriMancanti);
    }

    [Fact]
    public async Task IRiempimentiDiUnMasterVengonoDalSuoColorsDef()
    {
        _albero.Scrivi("ColorSchemes/LIRR_RDR_V1.0.clr", Schema.ReplaceLineEndings("\r\n"));
        // ITALY.isc dell'albero di prova carica colors.def dei campioni (F;COLORS\colors.def, sotto [DEFINE]).
        string isc = File.ReadAllText(_albero.Percorso("SectorFiles/ITALY.isc"));
        _albero.Scrivi("SectorFiles/ITALY.isc", isc + "\r\n[DEFINE]\r\nF;COLORS\\colors.def\r\n");
        _albero.Scrivi("SectorFiles/Include/IT/GND_LAYOUT/prova.pol",
            "STATIC;GRASS;1;GRASS;\r\nN041.00.00.000;E012.00.00.000;\r\nN042.00.00.000;E013.00.00.000;\r\nN041.00.00.000;E013.00.00.000;\r\n");

        var lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        Assert.True(await lab.ApriAsync(_albero.Radice));
        await lab.ScegliIscAsync("ITALY.isc");
        var forma = lab.Strati.Single(s => s.Id == "terra").Forme.Single(f => f.File.EndsWith("prova.pol", StringComparison.Ordinal));

        var colore = lab.Colori!.Di(forma);
        Assert.Equal("colors.def GRASS", colore.Origine);
        Assert.Equal(Color.FromArgb(0x40, 0x62, 0x30).ToArgb(), Argb(colore.Riempimento));
    }
}
