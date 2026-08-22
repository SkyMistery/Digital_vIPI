using Vipi.Infrastructure.Sectorfile;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Parser puro dei file <c>.mva</c> di Aurora (minime di vettoramento). Gli estratti sono presi dai file reali del
/// sectorfile italiano: il formato è più libero di quanto sembri e ogni caso qui sotto esiste davvero.
/// </summary>
public class AuroraMvaParserTests
{
    // lipe.mva: L (etichetta) seguita dai suoi vertici, blocchi separati da riga vuota. Il caso "regolare".
    private const string Interleaved = """
        L;110;N044.13.15.000;E010.53.34.000;110;7;
        T;110;N044.19.36.000;E010.47.48.000; //FL110
        T;110;N044.13.47.000;E010.59.59.000;
        T;110;N044.06.39.000;E010.59.59.000;
        T;110;N044.19.36.000;E010.47.48.000; //FL110

        L;100;N043.58.18.000;E011.42.32.000;100;7;
        T;100;N044.00.24.000;E011.32.03.000;
        T;100;N044.00.50.000;E011.41.08.000;
        T;100;N043.52.24.000;E012.01.14.000;
        """;

    [Fact]
    public void Parses_Labels_And_Shapes_Of_Interleaved_File()
    {
        var chart = AuroraSectorfileParser.ParseMva(Interleaved);

        Assert.Equal(2, chart.Labels.Count);
        Assert.Equal(2, chart.Shapes.Count);
        Assert.Equal("110", chart.Labels[0].Text);
        Assert.Equal("7", chart.Labels[0].Color);
        Assert.Equal(44.220833, chart.Labels[0].Lat, 5);
        Assert.Equal(10.892778, chart.Labels[0].Lon, 5);
        Assert.False(chart.IsEmpty);
    }

    [Fact]
    public void Closed_And_Open_Shapes_Are_Distinguished_Not_Fixed()
    {
        var chart = AuroraSectorfileParser.ParseMva(Interleaved);

        // Primo tracciato: ultimo vertice == primo → area.
        Assert.True(chart.Shapes[0].IsClosed);
        Assert.Equal(4, chart.Shapes[0].Points.Count);

        // Secondo: non torna al punto di partenza. Resta aperto — nessun vertice aggiunto d'ufficio.
        Assert.False(chart.Shapes[1].IsClosed);
        Assert.Equal(3, chart.Shapes[1].Points.Count);
    }

    [Fact]
    public void Labels_Are_Independent_From_Shapes()
    {
        // liph.mva: TUTTE le L in cima al file, i vertici dopo. Il formato non lega etichetta e area — se il
        // parser pretendesse la L come intestazione di blocco, qui perderebbe entrambe.
        const string labelsFirst = """
            L; ;N0463144000;E0120244000;170;8;
            L; ;N0461410000;E0115205000;140;8;

            T;ZONA1;N0464304000;E0120510000;
            T;ZONA1;N0464057000;E0122758000;
            T;ZONA1;N0463446000;E0124913000;
            """;
        var chart = AuroraSectorfileParser.ParseMva(labelsFirst);

        Assert.Equal(2, chart.Labels.Count);
        Assert.Single(chart.Shapes);
        Assert.Equal("ZONA1", chart.Shapes[0].Name);
        Assert.Equal("170", chart.Labels[0].Text);
    }

    [Fact]
    public void Dummy_Vertex_Closes_The_Block()
    {
        // ENRMVA: "T;DUMMY;N000.00.00.000;E000.00.00.000;" alza la penna. Non è un vertice e non è un tracciato.
        const string withDummy = """
            L;LIRR;N041.35.15.000;E013.10.44.032;90;8;
            T;LIRR;N041.39.35.000;E013.37.06.000;LIRR;
            T;LIRR;N041.22.46.000;E014.00.56.000;LIRR;
            T;LIRR;N041.14.30.000;E013.53.00.000;LIRR;
            T;DUMMY;N000.00.00.000;E000.00.00.000;
            L;LIRR;N041.47.47.272;E013.17.32.073;110;8;
            T;LIRR;N041.59.42.000;E013.08.10.000;LIRR;
            T;LIRR;N041.39.35.000;E013.37.06.000;LIRR;
            T;DUMMY;N000.00.00.000;E000.00.00.000;
            """;
        var chart = AuroraSectorfileParser.ParseMva(withDummy);

        Assert.Equal(2, chart.Shapes.Count);
        Assert.Equal(3, chart.Shapes[0].Points.Count);   // il DUMMY non entra fra i vertici
        Assert.Equal(2, chart.Shapes[1].Points.Count);
        Assert.DoesNotContain(chart.Shapes.SelectMany(s => s.Points), p => p is { Lat: 0, Lon: 0 });
    }

    [Fact]
    public void Name_Change_Starts_A_New_Shape_Without_Separator()
    {
        // lirs.mva/libn.mva: gruppi consecutivi senza riga vuota fra loro, distinti solo dal nome.
        const string byName = """
            T;ZONA1;N043.45.02.485;E010.09.24.813;
            T;ZONA1;N042.57.32.893;E010.23.37.514;
            T;LINEA2;N043.12.58.271;E009.34.01.733;
            T;LINEA2;N042.09.03.534;E009.40.56.766;
            """;
        var chart = AuroraSectorfileParser.ParseMva(byName);

        Assert.Equal(2, chart.Shapes.Count);
        Assert.Equal("ZONA1", chart.Shapes[0].Name);
        Assert.Equal("LINEA2", chart.Shapes[1].Name);
    }

    [Fact]
    public void Commented_Out_Polygons_Are_Skipped()
    {
        // ENRMVA/lirr.mva porta interi poligoni commentati con "//" davanti: sono storia, non dati.
        const string commented = """
            L;LIRR;N041.08.58.289;E013.24.48.073;100;8;
            //T;LIRR;N041.25.40.000;E013.11.48.000;LIRR;
            //T;LIRR;N041.02.37.000;E013.07.22.000;LIRR;
            T;LIRR;N041.14.30.000;E013.53.00.000;LIRR;
            T;LIRR;N041.22.30.000;E013.38.00.000;LIRR;
            """;
        var chart = AuroraSectorfileParser.ParseMva(commented);

        Assert.Single(chart.Shapes);
        Assert.Equal(2, chart.Shapes[0].Points.Count);
    }

    [Fact]
    public void Label_Text_Is_Verbatim_Never_Numeric()
    {
        // I valori veri del sectorfile: nessuna conversione, nessuno scarto di ciò che non è un numero.
        const string freeText = """
            L;LIBB;N040.00.00.000;E018.00.00.000;NO MINIMA;8;
            L;ZONA A;N044.30.01.000;E009.16.40.730;80/TRL;8;
            L;LIRR;N041.00.00.000;E013.00.00.000;*30/40;8;
            L;LIBV;N040.00.00.000;E018.00.00.000;FL85;8;
            """;
        var chart = AuroraSectorfileParser.ParseMva(freeText);

        Assert.Equal(new[] { "NO MINIMA", "80/TRL", "*30/40", "FL85" }, chart.Labels.Select(l => l.Text));
        Assert.Empty(chart.Shapes);
    }

    [Fact]
    public void Accepts_Plain_Decimal_Degrees_Without_Hemisphere()
    {
        // lipx.mva riga 14: una riga in tutti i 28 file usa gradi decimali nudi. Scartarla farebbe sparire
        // un'etichetta senza dirlo.
        var chart = AuroraSectorfileParser.ParseMva("L;MM ES0;45.55756591;10.27902575;60;8;\n");

        var label = Assert.Single(chart.Labels);
        Assert.Equal("60", label.Text);
        Assert.Equal(45.557566, label.Lat, 5);
        Assert.Equal(10.279026, label.Lon, 5);
    }

    [Fact]
    public void Without_Hemisphere_The_Sign_Says_The_Hemisphere()
    {
        // Convenzione standard: latitudine + = N / − = S, longitudine + = E / − = W. È la stessa uscita della
        // forma con la lettera, dove S e W sono già negativi — così a valle le tre forme sono indistinguibili.
        var plain = AuroraSectorfileParser.ParseMva("L;X;-45.5;-10.25;60;8;\n");
        var lettered = AuroraSectorfileParser.ParseMva("L;X;S045.30.00.000;W010.15.00.000;60;8;\n");

        Assert.Equal(-45.5, plain.Labels[0].Lat, 5);
        Assert.Equal(-10.25, plain.Labels[0].Lon, 5);
        Assert.Equal(lettered.Labels[0].Lat, plain.Labels[0].Lat, 5);
        Assert.Equal(lettered.Labels[0].Lon, plain.Labels[0].Lon, 5);
    }

    [Fact]
    public void Single_Point_Blocks_Are_Dropped()
    {
        // Un vertice solo non è né linea né area: non c'è niente da disegnare.
        var chart = AuroraSectorfileParser.ParseMva("T;X;N041.00.00.000;E013.00.00.000;\n");
        Assert.True(chart.IsEmpty);
    }

    [Fact]
    public void Empty_Or_Null_Input_Gives_Empty_Chart()
    {
        Assert.True(AuroraSectorfileParser.ParseMva(null).IsEmpty);
        Assert.True(AuroraSectorfileParser.ParseMva("").IsEmpty);
    }

    // --- coordinate compatte (formato usato da liph.mva, itgeo.geo, i .vfi) ---

    [Theory]
    [InlineData("N0463144000", 46.528889)]    // 046°31'44.000"
    [InlineData("E0120244000", 12.045556)]    // 012°02'44.000"
    [InlineData("S0463144000", -46.528889)]
    [InlineData("W0120244000", -12.045556)]
    [InlineData("N041.48.01.000", 41.800278)] // la forma coi punti continua a funzionare
    public void Parses_Both_Coordinate_Formats(string token, double expected)
    {
        Assert.True(AuroraSectorfileParser.TryParseDms(token, out var d));
        Assert.Equal(expected, d, 5);
    }

    [Theory]
    [InlineData("N04631")]        // troppo corto anche per il formato compatto
    [InlineData("N04631440X0")]   // non numerico
    public void Rejects_Malformed_Compact_Coordinates(string token)
    {
        Assert.False(AuroraSectorfileParser.TryParseDms(token, out _));
    }
}
