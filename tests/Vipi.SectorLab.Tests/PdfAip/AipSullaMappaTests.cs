using System.Text;
using Aip;
using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.PdfAip;
using Vipi.SectorLab.Ui.Server;
using Vipi.Sectorfile.Shared;
using Route = Aip.Route;
using RouteDirection = Aip.RouteDirection;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// L'AIP espanso sulla mappa (carta F6, prima della slice 2b): le aree, le rotte e le attese del modello in uno strato suo,
/// un genere per tipo, e per ogni forma la voce della scheda. Dati inventati.
/// </summary>
public sealed class AipSullaMappaTests
{
    private static readonly Position A = new(45.0, 9.0), B = new(45.0, 9.3), C = new(44.8, 9.3);

    private static AipDocument Modello() => new()
    {
        State = "LI",
        Airac = "2611",
        Airspaces =
        [
            new Airspace
            {
                Section = "ENR 2.1", Type = AirspaceType.Ctr, Designator = "Prova CTR", Class = "D",
                Upper = new VerticalLimit { Text = "2500ft AMSL" }, Lower = new VerticalLimit { Text = "SFC" },
                Geometry = new RingGeometry([new LineSegment(A), new BorderSegment(B, BorderKind.State, "confine"), new LineSegment(C)]),
                Page = new AipPage("ENR 2.1.2.1-1", new DateOnly(2026, 9, 3)),
            },
            new Airspace
            {
                Section = "ENR 5.1", Type = AirspaceType.Restricted, Designator = "LI R901",
                Upper = new VerticalLimit { Text = "FL95" }, Lower = new VerticalLimit { Text = "GND" },
                Geometry = new CircleGeometry(A, new Distance(5, DistanceUnit.Nm)),
            },
        ],
        Routes =
        [
            new Route
            {
                Section = "ENR 3.2", Designator = "Z99",
                Points = [new RoutePoint("UNO", "UNO", A, true), new RoutePoint("DUE", "DUE", B, false), new RoutePoint("TRE", "TRE", C, true)],
                Segments =
                [
                    new RouteSegment { Track = "090° / 270°", DistanceNm = 12.7, Upper = new VerticalLimit { Text = "FL195" }, Lower = new VerticalLimit { Text = "FL95" },
                                       Odd = RouteDirection.Forward, MinimumOdd = "110" },
                    new RouteSegment { Discontinuity = true },
                ],
            },
        ],
        Holdings =
        [
            new Holding { Section = "ENR 3.4.1", Fix = "UNO", Minimum = new VerticalLimit { Text = "3000" }, Turn = TurnDirection.Left, InboundTrack = 250 },
            new Holding { Section = "ENR 3.4.1", Fix = "NESSUNO", Minimum = new VerticalLimit { Text = "FL100" }, Turn = TurnDirection.Right, InboundTrack = 90 },
        ],
    };

    private static StratoDellAip Strato() =>
        AipSullaMappa.Di(Modello(), "2611", nome => nome == "UNO" ? new Coordinate(A.Lat, A.Lon) : null);

    [Fact]
    public void LeForme_UnGenerePerTipo_IlFileDellAip_LeVociConLoStessoIndice()
    {
        var s = Strato();

        Assert.Equal("aip", s.Strato.Id);
        Assert.Equal("aip:2611", s.File);
        Assert.Equal(["CTR", "R", "Rotte", "Attese", "Attese"], s.Strato.Forme.Select(f => f.Genere));
        Assert.All(s.Strato.Forme, f => Assert.Equal("aip:2611", f.File));
        Assert.Equal([0, 1, 2, 3, 4], s.Strato.Forme.Select(f => f.Record));
        Assert.Equal(s.Strato.Forme.Select(f => f.Etichetta), s.Voci.Select(v => v.Titolo));
        Assert.Equal(AipSullaMappa.Colori["CTR"], s.Strato.Forme[0].Tratto);
        Assert.True(AipSullaMappa.EDellAip(s.File));
        Assert.False(AipSullaMappa.EDellAip("SectorFiles/Include/IT/lirf.str"));
    }

    [Fact]
    public void LAreaEUnAnello_ColConfineDrittoDetto()
    {
        var s = Strato();

        Assert.Equal(TipoDiForma.Area, s.Strato.Forme[0].Tipo);
        Assert.Equal(3, Assert.Single(s.Strato.Forme[0].Tratti).Count);
        Assert.Contains(("Limiti", "2500ft AMSL – SFC"), s.Voci[0].Campi);
        Assert.Contains(("Pagina", "ENR 2.1.2.1-1 (03/09/2026)"), s.Voci[0].Campi);
        Assert.Contains(s.Voci[0].Campi, c => c.Nome == "⚠" && c.Valore.Contains("retta", StringComparison.Ordinal));
        // Il cerchio a un punto per grado.
        Assert.Equal(360, Assert.Single(s.Strato.Forme[1].Tratti).Count);
    }

    [Fact]
    public void LaRotta_SpezzataDoveSiInterrompe_EITrattiNellaScheda()
    {
        var s = Strato();

        var rotta = s.Strato.Forme[2];
        Assert.Equal(TipoDiForma.Linea, rotta.Tipo);
        // UNO-DUE disegnato, DUE-TRE interrotto: un pezzo solo, di due punti.
        Assert.Equal(2, Assert.Single(rotta.Tratti).Count);
        Assert.Equal(["▲ UNO", "    090° / 270° 12.7 NM · FL195 – FL95 · ↓ solo avanti · MNM 110/—", "∆ DUE", "    — interruzione —", "▲ TRE"], s.Voci[2].Righe);
    }

    [Fact]
    public void LAttesa_DoveIlSectorHaIlPunto_AltrimentiSenzaDisegnoESiDice()
    {
        var s = Strato();

        Assert.Equal((TipoDiForma.Punto, 1), (s.Strato.Forme[3].Tipo, Assert.Single(s.Strato.Forme[3].Tratti).Count));
        Assert.Equal("UNO 3000 L 250°", s.Voci[3].Titolo);
        Assert.Empty(s.Strato.Forme[4].Tratti);
        Assert.Equal(["NESSUNO"], s.Strato.Forme[4].NomiNonRisolti);
        Assert.Contains(s.Voci[4].Campi, c => c.Nome == "⚠" && c.Valore.Contains("NESSUNO", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NelJsonDellaMappa_IlColoreDelGenere()
    {
        using var flusso = new MemoryStream();

        await MappaDelLab.Scrivi(flusso, "aip", Strato().Strato.Forme, coloreDellaForma: true);

        string json = Encoding.UTF8.GetString(flusso.ToArray());
        Assert.Contains("\"lc\":\"" + AipSullaMappa.Colori["Rotte"] + "\"", json, StringComparison.Ordinal);
        Assert.Contains("\"h\":\"Attese\"", json, StringComparison.Ordinal);
        Assert.Contains("\"p\":\"aip:2611\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void UnCorridoioMilitare_IlSuoContorno_IlGenereEIlCampoDellaScheda()
    {
        var aip = new AipDocument
        {
            State = "LI",
            Airspaces =
            [
                new Airspace
                {
                    Section = "ENR 5.2", Type = AirspaceType.Military, Designator = "ALBA Corridor",
                    Upper = new VerticalLimit { Text = "FL200" }, Lower = new VerticalLimit { Text = "FL150" }, Hours = "H24", Unit = "Brindisi SCCAM",
                    Geometry = new CorridorGeometry(new Distance(10, DistanceUnit.Nm), [A, B]),
                },
            ],
        };

        var s = AipSullaMappa.Di(aip, "2611", _ => null);

        var forma = Assert.Single(s.Strato.Forme);
        Assert.Equal("Militari", forma.Genere);
        Assert.Equal(AipSullaMappa.Colori["Militari"], forma.Tratto);
        // Il contorno: due punti per lato, chiuso.
        Assert.True(forma.Tratti[0].Count >= 4);
        Assert.Contains(("Corridoio", "10 NM di ampiezza, 2 punti"), s.Voci[0].Campi);
        Assert.Contains(("Ente", "Brindisi SCCAM"), s.Voci[0].Campi);
    }
    // Carta F7, slice 7c: le procedure di AD 2 sullo strato, un genere per tipo; i punti dall'AIP espanso stesso (ENR 4.4, punti
    // di scalo, radioassistenze) e, se mancano, dal sector; un CA non ha posto; un punto che non si trova stacca la linea.
    [Fact]
    public void LeProcedure_UnaLineaPerIPuntiDellAip()
    {
        var sid = new Procedure
        {
            Aerodrome = "LIXX", Kind = ProcedureKind.Sid, Designator = "PROVA 1A", Runways = ["24"],
            Legs =
            [
                new ProcedureLeg { PathTerminator = "CA", Altitude = "+410" },
                new ProcedureLeg { PathTerminator = "CF", Fix = "XX601" },
                new ProcedureLeg { PathTerminator = "TF", Fix = "PRV" },
                new ProcedureLeg { PathTerminator = "TF", Fix = "SECTO" },
                new ProcedureLeg { PathTerminator = "TF", Fix = "NESSUNO" },
                new ProcedureLeg { PathTerminator = "TF", Fix = "XX601" },
                new ProcedureLeg { PathTerminator = "TF", Fix = "PRV" },
            ],
        };
        var prosa = sid with { Kind = ProcedureKind.Star, Designator = "PROSA 1A", Legs = [], Description = "Proceed via …" };
        var aip = new AipDocument
        {
            State = "LI", Airspaces = [],
            Points = [new SignificantPoint { Section = "AD 2 LIXX", Designator = "XX601", Position = A }],
            Navaids = [new Navaid { Section = "ENR 4.1", Designator = "PRV", Name = "PROVA", Type = "VOR", Position = B, Components = [] }],
            Procedures = [sid, prosa],
        };

        var strato = AipSullaMappa.Di(aip, "2611", nome => nome == "SECTO" ? new Coordinate(44.9, 9.4) : null);

        // Le procedure stanno in fondo allo strato, dopo il punto e la radioassistenza.
        var forma = strato.Strato.Forme[2];
        Assert.Equal((AipSullaMappa.Sid, TipoDiForma.Linea), (forma.Genere, forma.Tipo));
        Assert.Equal([3, 2], forma.Tratti.Select(t => t.Count));
        Assert.Equal(new Coordinate(44.9, 9.4), forma.Tratti[0][2]);
        Assert.Equal(["NESSUNO"], forma.NomiNonRisolti);
        var voce = strato.Voci[2];
        Assert.Equal("LIXX PROVA 1A RWY 24", voce.Titolo);
        Assert.Equal("CA — +410", voce.Righe[0]);
        Assert.Contains(voce.Campi, c => c.Valore.Contains("NESSUNO", StringComparison.Ordinal));
        // La STAR in sola prosa (che la 7b non ha letto): la scheda c'è, la linea no.
        Assert.Equal(AipSullaMappa.Star, strato.Voci[3].Genere);
        Assert.Empty(strato.Strato.Forme[3].Tratti);
        Assert.Contains(strato.Voci[3].Campi, c => c.Valore.Contains("non si è letta", StringComparison.Ordinal));
    }

    // Slice 7b: i tratti dalla prosa — il punto calcolato con la sua posizione, l'arco DME attorno alla radioassistenza; la
    // scheda dice da dove vengono.
    [Fact]
    public void UnaStarDallaProsa_IPuntiCalcolati_ELArcoDme()
    {
        var centro = B;
        var inizio = new Position(B.Lat + 15 / 60.0, B.Lon);                                     // 15 NM a nord
        var fine = new Position(B.Lat, B.Lon + 15 / 60.0 / Math.Cos(B.Lat * Math.PI / 180));     // 15 NM a est
        var star = new Procedure
        {
            Aerodrome = "LIXX", Kind = ProcedureKind.Star, Designator = "PROSA 1H", Runways = ["24"], LegsFromDescription = true,
            Description = "XX601 – TR 180° …",
            Legs =
            [
                new ProcedureLeg { PathTerminator = "IF", Fix = "XX601" },
                new ProcedureLeg { PathTerminator = "CF", Computed = "RDL 000/15 NM PRV", Position = inizio },
                new ProcedureLeg { PathTerminator = "AF", Computed = "RDL 090/15 NM PRV", Position = fine, ArcCenter = "PRV", ArcRadiusNm = 15, Turn = "R" },
            ],
        };
        var aip = new AipDocument
        {
            State = "LI", Airspaces = [],
            Points = [new SignificantPoint { Section = "AD 2 LIXX", Designator = "XX601", Position = A }],
            Navaids = [new Navaid { Section = "ENR 4.1", Designator = "PRV", Name = "PROVA", Type = "VOR", Position = centro, Components = [] }],
            Procedures = [star],
        };

        var strato = AipSullaMappa.Di(aip, "2611", _ => null);

        var tratto = Assert.Single(strato.Strato.Forme[2].Tratti);
        Assert.Equal(new Coordinate(inizio.Lat, inizio.Lon), tratto[1]);
        Assert.Equal(new Coordinate(fine.Lat, fine.Lon), tratto[^1]);
        // Un quarto di giro in senso orario, un punto ogni 5°: 18 dopo l'inizio.
        Assert.Equal(2 + 18, tratto.Count);
        Assert.Contains(strato.Voci[2].Campi, c => c.Valore.Contains("dalla prosa", StringComparison.Ordinal));
        Assert.Contains("RDL 000/15 NM PRV (calcolato)", strato.Voci[2].Righe[1], StringComparison.Ordinal);
    }

    // Un RF è un arco attorno al suo centro, nel verso del tratto, e finisce esattamente sul punto.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UnArcoRF_NelVersoGiusto(bool orario)
    {
        var centro = new Coordinate(45.0, 9.0);
        var da = new Coordinate(45.0 + 2 / 60.0, 9.0);   // 2 NM a nord
        var a = new Coordinate(45.0, 9.0 + 2 / 60.0 / Math.Cos(45 * Math.PI / 180)); // 2 NM a est

        var punti = AipSullaMappa.Arco(da, a, centro, orario).ToList();

        Assert.Equal(a, punti[^1]);
        // Da nord a est in senso orario è un quarto di giro (18 punti ogni 5°); in senso antiorario tre quarti (54).
        Assert.Equal(orario ? 18 : 54, punti.Count);
        var k = Math.Cos(45 * Math.PI / 180);
        Assert.All(punti, p => Assert.Equal(2 / 60.0, Math.Sqrt(Math.Pow((p.LongitudeDeg - 9.0) * k, 2) + Math.Pow(p.LatitudeDeg - 45.0, 2)), 6));
        // A metà strada: in senso orario si passa a nord-est, in senso antiorario a sud-ovest.
        var meta = punti[punti.Count / 2];
        Assert.Equal(orario, meta.LatitudeDeg > 45.0 && meta.LongitudeDeg > 9.0);
    }
}
