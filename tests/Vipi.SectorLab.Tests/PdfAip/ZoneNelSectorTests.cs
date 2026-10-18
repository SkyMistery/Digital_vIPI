using Aip;
using Aip.Aurora;
using Vipi.SectorLab.Core.PdfAip;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Carta F6, slice 1c: un CTR dell'AIP scritto come lo tiene il sector — le righe dei punti di una voce del <c>MAPS</c>,
/// una zona dopo l'altra, la penna alzata fra le zone, i tratti di confine presi dalla linea che il sector ha già (Ronchi,
/// l'unico CTR del 2610 con un tratto di confine). Dati inventati.
/// </summary>
public sealed class CtrNelSectorTests
{
    private static readonly Position A = new(45.0, 9.0), B = new(45.0, 9.3), C = new(44.8, 9.3), D = new(44.8, 9.0);

    private static Airspace Zona(string designatore, Geometry geometria) => new()
    {
        Section = "ENR 2.1",
        Type = AirspaceType.Ctr,
        Designator = designatore,
        Upper = new VerticalLimit { Text = "2000ft AMSL" },
        Lower = new VerticalLimit { Text = "SFC" },
        Geometry = geometria,
    };

    private static RingGeometry Anello(params Position[] p) => new([.. p.Select(x => (Segment)new LineSegment(x))]);

    private static IReadOnlyList<Coordinate> Linea(params Position[] p) => [.. p.Select(x => new Coordinate(x.Lat, x.Lon))];

    [Fact]
    public void DueZone_ChiuseUnaDopoLAltra_ColCommentoSuUnaRigaSuaELaPennaAlzata()
    {
        var zone = new[] { Zona("Prova CTR Zona/Zone '1'", Anello(A, B, C)), Zona("Prova CTR Zona/Zone '2'", Anello(A, C, D)) };

        var corpo = CtrNelSector.Corpo(zone, [], 1, out var perche);

        Assert.Null(perche);
        Assert.Equal(
        [
            "//zona 1",
            "N045.00.00.000;E009.00.00.000;",
            "N045.00.00.000;E009.18.00.000;",
            "N044.48.00.000;E009.18.00.000;",
            "N045.00.00.000;E009.00.00.000;",
            "//zona 2",
            "N045.00.00.000;E009.00.00.000;<br>",
            "N044.48.00.000;E009.18.00.000;",
            "N044.48.00.000;E009.00.00.000;",
            "N045.00.00.000;E009.00.00.000;",
        ], corpo);
    }

    [Fact]
    public void UnaZonaSola_SenzaCommento_ElCerchioAllaDensitaData()
    {
        var corpo = CtrNelSector.Corpo([Zona("Prova CTR", new CircleGeometry(A, new Distance(5, DistanceUnit.Nm)))], [], 0.2, out _);

        // 72 punti (uno ogni 5 gradi) e il primo ripetuto per chiudere; nessun commento in coda.
        Assert.Equal(73, corpo!.Count);
        Assert.Equal(corpo[0], corpo[^1]);
        Assert.DoesNotContain(corpo, r => r.Contains("//", StringComparison.Ordinal));
    }

    // Ronchi (LIPQ): «Italian northern geographical border» fra due vertici; il sector lo disegna coi suoi punti.
    [Fact]
    public void IlTrattoDiConfine_SiPrendeDallaLineaDelSector()
    {
        var mezzo1 = new Position(45.0, 9.35);
        var mezzo2 = new Position(44.9, 9.36);
        var zona = Zona("Prova CTR", new RingGeometry([new LineSegment(A), new BorderSegment(B, BorderKind.State, "Italian border"), new LineSegment(C), new LineSegment(D)]));
        // La linea del sector va al contrario (D, C, …, B, A): il tracciato si prende nel verso dell'AIP.
        var vecchia = Linea(D, new Position(44.8, 9.30001), mezzo2, mezzo1, B, A, D);

        var corpo = CtrNelSector.Corpo([zona], [vecchia], 1, out var perche);

        Assert.Null(perche);
        Assert.Equal(
        [
            AuroraText.Coordinates(A) + ";",
            AuroraText.Coordinates(B) + ";",
            AuroraText.Coordinates(mezzo1) + ";",
            AuroraText.Coordinates(mezzo2) + ";",
            AuroraText.Coordinates(C) + ";",
            AuroraText.Coordinates(D) + ";",
            AuroraText.Coordinates(A) + ";",
        ], corpo);
    }

    [Fact]
    public void IlTrattoDiConfine_SenzaUnaLineaDaCuiPrenderlo_NonSiScrive()
    {
        var zona = Zona("Prova CTR", new RingGeometry([new LineSegment(A), new BorderSegment(B, BorderKind.State, "Italian border"), new LineSegment(C)]));

        Assert.Null(CtrNelSector.Corpo([zona], [], 1, out var perche));
        Assert.Contains("il sector non ha una linea", perche, StringComparison.Ordinal);

        // Una linea c'è, ma lontana dai capi del tratto: nemmeno.
        Assert.Null(CtrNelSector.Corpo([zona], [Linea(new Position(40, 9), new Position(40, 9.3))], 1, out perche));
        Assert.Contains("non si ritrova sulla linea del sector", perche, StringComparison.Ordinal);
    }

    [Fact]
    public void AlPostoDelCorpo_LaTestataResta()
    {
        string[] righe = ["//MENU MAPS", "LIXX;MAPS;LIXX CTR; ; ;1;", "N045.00.00.000;E009.00.00.000;", "N044.00.00.000;E009.00.00.000;", "", "LIXX;MAPS;LIXX ATZ; ; ;5;"];

        var sostituzioni = CtrNelSector.AlPostoDelCorpo(righe, 1, 3, ["uno", "due"]);

        Assert.Equal(["LIXX;MAPS;LIXX CTR; ; ;1;", "uno", "due"], sostituzioni[2]);
        Assert.Empty(sostituzioni[3]);
        Assert.Empty(sostituzioni[4]);
        Assert.False(sostituzioni.ContainsKey(5));
    }

    [Fact]
    public void LeZoneDiUnCtr_InOrdine_ESoloQuelle()
    {
        Airspace[] aip =
        [
            Zona("Prova CTR Zona/Zone '1'", Anello(A, B, C)),
            Zona("Altra CTR", Anello(A, B, C)),
            Zona("Prova CTR Zona/Zone '2'", Anello(A, C, D)),
            Zona("Prova CTR Zona/Zone '3'", Anello(A, C, D)) with { Type = AirspaceType.Atz },
        ];

        Assert.Equal(["Prova CTR Zona/Zone '1'", "Prova CTR Zona/Zone '2'"], CtrNelSector.ZoneDi(aip, "Prova CTR").Select(z => z.Designator));
        Assert.Equal("LIXX;MAPS;LIXX CTR; ; ;1;", CtrNelSector.Testata("LIXX"));
    }
}
