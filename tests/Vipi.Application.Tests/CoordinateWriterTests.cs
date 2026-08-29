using Vipi.Application.Coordinates;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le uscite del convertitore. La prova che conta è quella coi dati del committente: gli stessi cinque vertici
/// devono uscire <b>carattere per carattere</b> come li scrivono il DB IVAO e <c>italy.restrict</c>.
/// </summary>
public class CoordinateWriterTests
{
    /// <summary>L'area R14A come la scrive il DB IVAO.</summary>
    private const string Db =
        "42.00777778:11.96833333\n" +
        "41.99055556:11.98333333\n" +
        "41.94472222:11.98888889\n" +
        "41.91666667:11.95833333\n" +
        "41.975:11.92";

    /// <summary>La stessa area come la scrive <c>italy.restrict</c>, a segmenti.</summary>
    private const string Segmenti =
        "N042.00.28.000;E011.58.06.000;N041.59.26.000;E011.59.00.000;RESTRICT;R14A;\n" +
        "N041.59.26.000;E011.59.00.000;N041.56.41.000;E011.59.20.000;RESTRICT;R14A;\n" +
        "N041.56.41.000;E011.59.20.000;N041.55.00.000;E011.57.30.000;RESTRICT;R14A;\n" +
        "N041.55.00.000;E011.57.30.000;N041.58.30.000;E011.55.12.000;RESTRICT;R14A;\n" +
        "N041.58.30.000;E011.55.12.000;N042.00.28.000;E011.58.06.000;RESTRICT;R14A;";

    /// <summary>E come elenco di punti, che è l'uscita chiesta dal committente.</summary>
    private const string Punti =
        "N042.00.28.000;E011.58.06.000;\n" +
        "N041.59.26.000;E011.59.00.000;\n" +
        "N041.56.41.000;E011.59.20.000;\n" +
        "N041.55.00.000;E011.57.30.000;\n" +
        "N041.58.30.000;E011.55.12.000;";

    private static IReadOnlyList<(double Lat, double Lon)> R14A() =>
        CoordinateParser.Parse(Db).Aree[0].Punti;

    [Fact]
    public void Dal_Db_All_Elenco_Punti_Del_Sectorfile()
    {
        var uscita = CoordinateWriter.Write(R14A(), CoordinateOutput.SectorfilePunti);
        Assert.Equal(Punti, uscita);
    }

    [Fact]
    public void Dal_Db_Ai_Segmenti_Con_L_Anello_Chiuso()
    {
        var opzioni = CoordinateWriteOptions.Default with { Nome = "R14A" };
        var uscita = CoordinateWriter.Write(R14A(), CoordinateOutput.SectorfileSegmenti, opzioni);
        Assert.Equal(Segmenti, uscita);
    }

    [Fact]
    public void Dai_Segmenti_Al_Db()
    {
        var punti = CoordinateParser.Parse(Segmenti).Aree[0].Punti;
        Assert.Equal(Db, CoordinateWriter.Write(punti, CoordinateOutput.DbIvao));
    }

    [Fact]
    public void Dall_Elenco_Punti_Al_Db()
    {
        var punti = CoordinateParser.Parse(Punti).Aree[0].Punti;
        Assert.Equal(Db, CoordinateWriter.Write(punti, CoordinateOutput.DbIvao));
    }

    [Fact]
    public void Il_Db_Taglia_Gli_Zeri_Finali()
    {
        // ⚠️ Il DB scrive `41.975`, non `41.97500000`: lo dice l'esempio del committente, ultima riga.
        var uscita = CoordinateWriter.Write([(41.975, 11.92)], CoordinateOutput.DbIvao);
        Assert.Equal("41.975:11.92", uscita);
    }

    [Fact]
    public void La_Precisione_Si_Puo_Abbassare_A_Sei()
    {
        var opzioni = CoordinateWriteOptions.Default with { Decimali = 6 };
        var uscita = CoordinateWriter.Write([(42.00777778, 11.96833333)], CoordinateOutput.DbIvao, opzioni);
        Assert.Equal("42.007778:11.968333", uscita);
    }

    [Fact]
    public void Senza_Chiudere_L_Anello_I_Lati_Sono_Uno_Di_Meno()
    {
        var opzioni = CoordinateWriteOptions.Default with { Nome = "R14A", ChiudiAnello = false };
        var uscita = CoordinateWriter.Write(R14A(), CoordinateOutput.SectorfileSegmenti, opzioni);

        // Cinque vertici, quattro lati: è il caso della COSTA, che poligono non è.
        Assert.Equal(4, uscita.Split('\n').Length);
        Assert.Equal(string.Join('\n', Segmenti.Split('\n')[..4]), uscita);
    }

    [Fact]
    public void Il_Nome_Vuoto_Non_Si_Scrive()
    {
        // I .geo hanno il tipo e basta: un campo vuoto in più sarebbe una riga che quei file non contengono.
        var uscita = CoordinateWriter.Write(R14A(), CoordinateOutput.SectorfileSegmenti,
            CoordinateWriteOptions.Default with { Tipo = "COAST" });

        Assert.All(uscita.Split('\n'), r => Assert.EndsWith(";COAST;", r));
    }

    [Fact]
    public void La_Forma_Compatta_E_Quella_Di_Itgeo()
    {
        var uscita = CoordinateWriter.Write([(42.00777778, 11.96833333)], CoordinateOutput.SectorfilePunti,
            CoordinateWriteOptions.Default with { Forma = DmsCoordinate.Forma.Compatta });

        Assert.Equal("N0420028000;E0115806000;", uscita);
    }

    [Theory]
    [InlineData(CoordinateOutput.DbIvao)]
    [InlineData(CoordinateOutput.SectorfilePunti)]
    [InlineData(CoordinateOutput.SectorfileSegmenti)]
    public void Andata_E_Ritorno_Senza_Perdere_Un_Vertice(CoordinateOutput formato)
    {
        var partenza = R14A();
        var testo = CoordinateWriter.Write(partenza, formato, CoordinateWriteOptions.Default with { Nome = "R14A" });

        var tornati = CoordinateParser.Parse(testo).Aree[0].Punti;

        Assert.Equal(partenza.Count, tornati.Count);
        for (var i = 0; i < partenza.Count; i++)
        {
            // Tolleranza: mezzo millisecondo d'arco, la risoluzione del DMS. Non è un «circa»: è il formato.
            Assert.True(Math.Abs(partenza[i].Lat - tornati[i].Lat) <= 0.5 / 3_600_000.0);
            Assert.True(Math.Abs(partenza[i].Lon - tornati[i].Lon) <= 0.5 / 3_600_000.0);
        }
    }

    [Fact]
    public void Nessun_Punto_Nessuna_Riga() =>
        Assert.Equal("", CoordinateWriter.Write([], CoordinateOutput.DbIvao));
}
