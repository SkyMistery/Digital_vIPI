namespace Aip.Pdf.Tests;

/// <summary>Il lettore delle geometrie, sul testo: le forme della grammatica di carta madre §5, e la guardia.</summary>
public sealed class GeometryTextTests
{
    private static Position P(double lat, double lon) => new(lat, lon);

    private static void Vicino(Position atteso, Position letto)
    {
        Assert.Equal(atteso.Lat, letto.Lat, 9);
        Assert.Equal(atteso.Lon, letto.Lon, 9);
    }

    [Fact]
    public void UnPoligonoAlPuntoDiOrigine()
    {
        var letto = GeometryText.Read("""
            44°50'06"N 007°09'12"E;
            44°48'45"N 007°33'42"E;
            44°37'02.5"N 007°49'57"E;
            to point of origin.
            """);

        Assert.Empty(letto.Issues);
        var anello = Assert.IsType<RingGeometry>(Assert.Single(letto.Geometries));
        Assert.Equal(3, anello.Segments.Count);
        Assert.All(anello.Segments, s => Assert.IsType<LineSegment>(s));
        Vicino(P(44 + 50 / 60.0 + 6 / 3600.0, 7 + 9 / 60.0 + 12 / 3600.0), anello.Segments[0].From);
        Vicino(P(44 + 37 / 60.0 + 2.5 / 3600.0, 7 + 49 / 60.0 + 57 / 3600.0), anello.Segments[2].From);
    }

    [Theory]
    [InlineData("Circular area centered on 42°00'45\"N 013°36'00\"E within a 1.0 NM radius.", 1.0, DistanceUnit.Nm)]
    [InlineData("Circular area centered on 42°00'45\"N 013°36'00\"E within a 300.0 M radius.", 300.0, DistanceUnit.M)]
    [InlineData("Circular area centered on\n42°00'45\"N 013°36'00\"E within a\n5.0 KM radius.", 5.0, DistanceUnit.Km)]
    public void UnCerchioColRaggioComeStampato(string testo, double raggio, DistanceUnit unita)
    {
        var letto = GeometryText.Read(testo);

        Assert.Empty(letto.Issues);
        var cerchio = Assert.IsType<CircleGeometry>(Assert.Single(letto.Geometries));
        Assert.Equal(new Distance(raggio, unita), cerchio.Radius);
        Vicino(P(42 + 45 / 3600.0, 13.6), cerchio.Center);
    }

    [Theory]
    [InlineData("43°57’17’’N 008°10’22’’E")]
    [InlineData("43°57'17''N 008°10'22''E")]
    [InlineData("43°57'17\"N 008°10'22\"E")]
    public void UnPuntoConGliApiciDiOgniGenere(string testo)
    {
        // ENR 4.5 chiude i secondi con due apici tipografici (’’), ENR 4.1 con due dritti, ENR 5.1 con le virgolette.
        var p = GeometryText.ReadPoint(testo);
        Assert.NotNull(p);
        Assert.Equal(43 + 57 / 60.0 + 17 / 3600.0, p!.Value.Lat, 9);
        Assert.Equal(8 + 10 / 60.0 + 22 / 3600.0, p.Value.Lon, 9);
    }

    [Fact]
    public void DueCerchiDiSeguitoSonoDueGeometrie()
    {
        var letto = GeometryText.Read(
            "Circular area centered on 42°00'00\"N 013°00'00\"E within a 1.0 NM radius. " +
            "Circular area centered on 43°00'00\"N 013°00'00\"E within a 2.0 NM radius.");

        Assert.Empty(letto.Issues);
        Assert.Equal([1.0, 2.0], letto.Geometries.Cast<CircleGeometry>().Select(c => c.Radius.Value));
    }

    [Fact]
    public void UnArcoOrarioFinoAUnPunto_ColRaggioACapo()
    {
        var letto = GeometryText.Read("""
            40°54'00"N 014°06'00"E
            40°54'00"N 014°18'00"E then arc of circle in
            clockwise direction radius
            7.5 NM centred on 40°48'00"N 014°12'00"E till point
            40°42'00"N 014°18'00"E
            to point of origin.
            """);

        Assert.Empty(letto.Issues);
        var tratti = Assert.IsType<RingGeometry>(Assert.Single(letto.Geometries)).Segments;
        Assert.Equal(3, tratti.Count);
        Assert.IsType<LineSegment>(tratti[0]);
        var arco = Assert.IsType<ArcSegment>(tratti[1]);
        Assert.Equal(ArcDirection.Clockwise, arco.Direction);
        Assert.Equal(new Distance(7.5, DistanceUnit.Nm), arco.Radius);
        Vicino(P(40.8, 14.2), arco.Center);
        Vicino(P(40.9, 14.3), arco.From);
        Vicino(P(40.7, 14.3), tratti[2].From);
    }

    [Fact]
    public void UnArcoAntiorarioFinoAlPuntoDiOrigine()
    {
        var letto = GeometryText.Read(
            "41°00'00\"N 012°00'00\"E 41°00'00\"N 012°10'00\"E then arc of circle in anti-clockwise direction " +
            "radius 2.8 NM centred on 40°58'00\"N 012°05'00\"E till point of origin.");

        Assert.Empty(letto.Issues);
        var tratti = Assert.IsType<RingGeometry>(Assert.Single(letto.Geometries)).Segments;
        Assert.Equal(2, tratti.Count);
        Assert.Equal(ArcDirection.CounterClockwise, Assert.IsType<ArcSegment>(tratti[1]).Direction);
    }

    [Fact]
    public void ConfineECostaSonoTrattiConLaLoroFrase()
    {
        var letto = GeometryText.Read("""
            45°54'00"N 006°54'00"E
            45°57'00"N 007°06'00"E Italian northern geographical border till point
            46°12'00"N 007°48'00"E
            45°48'00"N 007°36'00"E line at 500 m from coast
            to point of origin.
            """);

        Assert.Empty(letto.Issues);
        var tratti = Assert.IsType<RingGeometry>(Assert.Single(letto.Geometries)).Segments;
        var confine = Assert.IsType<BorderSegment>(tratti[1]);
        Assert.Equal(BorderKind.State, confine.Border);
        Assert.Equal("Italian northern geographical border", confine.Text);
        var costa = Assert.IsType<BorderSegment>(tratti[3]);
        Assert.Equal(BorderKind.Coast, costa.Border);
        Assert.Equal("line at 500 m from coast", costa.Text);
    }

    [Fact]
    public void LaFormaCompattaSiLeggeAnche()
    {
        var letto = GeometryText.Read("453000N 0091500E - 453000N 0093000E - 451500N 0092230E - to point of origin");

        Assert.Empty(letto.Issues);
        var tratti = Assert.IsType<RingGeometry>(Assert.Single(letto.Geometries)).Segments;
        Vicino(P(45.5, 9.25), tratti[0].From);
    }

    [Fact]
    public void IlPrimoVerticeRipetutoChiudeLAnello()
    {
        var letto = GeometryText.Read("41°00'00\"N 012°00'00\"E 41°00'00\"N 012°10'00\"E 40°50'00\"N 012°05'00\"E 41°00'00\"N 012°00'00\"E");

        Assert.Empty(letto.Issues);
        Assert.Equal(3, Assert.IsType<RingGeometry>(Assert.Single(letto.Geometries)).Segments.Count);
    }

    // 🔴 La guardia della F0: un identificativo fuori standard dentro il testo non si mangia in silenzio.
    [Fact]
    public void LeParoleSconosciuteSiDicono()
    {
        var letto = GeometryText.Read("""
            41°00'00"N 012°00'00"E 41°00'00"N 012°10'00"E 40°50'00"N 012°05'00"E
            to point of origin.
            Zona '29' - Pippo
            42°00'00"N 012°00'00"E
            """);

        Assert.Contains(letto.Issues, p => p is { Kind: GeometryIssueKind.UnknownWords, Detail: "ZONA 29 PIPPO" });
        Assert.Contains(letto.Issues, p => p.Kind == GeometryIssueKind.NotClosed);
        Assert.Equal(2, letto.Geometries.Count);
    }

    [Fact]
    public void UnAnelloSenzaChiusuraSiChiudeESiDice()
    {
        var letto = GeometryText.Read("41°00'00\"N 012°00'00\"E 41°00'00\"N 012°10'00\"E 40°50'00\"N 012°05'00\"E");

        Assert.Equal(GeometryIssueKind.NotClosed, Assert.Single(letto.Issues).Kind);
        Assert.Single(letto.Geometries);
    }

    [Fact]
    public void UnArcoSenzaRaggioNeVersoSiDisegnaESiDice()
    {
        var letto = GeometryText.Read(
            "41°00'00\"N 012°00'00\"E 41°00'00\"N 012°10'00\"E then arc of circle centred on 40°58'00\"N 012°05'00\"E till point of origin.");

        Assert.Equal(["raggio", "verso"], letto.Issues.Select(p => p.Detail).Order());
        var arco = Assert.IsType<ArcSegment>(Assert.IsType<RingGeometry>(Assert.Single(letto.Geometries)).Segments[1]);
        Assert.Null(arco.Radius);
    }

    [Fact]
    public void UnaCoordinataFuoriIntervalloSiDice()
    {
        var letto = GeometryText.Read("41°61'00\"N 012°00'00\"E");

        Assert.Equal(GeometryIssueKind.BadCoordinate, Assert.Single(letto.Issues).Kind);
        Assert.Empty(letto.Geometries);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void IlVuotoNonDaNiente(string? testo)
    {
        var letto = GeometryText.Read(testo);
        Assert.Empty(letto.Geometries);
        Assert.Empty(letto.Issues);
    }
}
