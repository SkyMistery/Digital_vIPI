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
}
