using Aip;
using Vipi.SectorLab.Core.PdfAip;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// I tratti di confine e di costa dell'AIP sul tracciato di <c>itgeo.geo</c> (l'eccezione del committente del 19 ottobre),
/// coi casi trovati sul sector vero: il confine delle Alpi e la costa in due pezzi staccati di 3 NM, una costa di un altro
/// Stato che comincia dove finisce la nostra, i vertici radi del confine di terra. Dati inventati.
/// </summary>
public sealed class ConfiniDelSectorTests
{
    private static IReadOnlyList<Coordinate> Linea(params (double Lat, double Lon)[] p) => [.. p.Select(x => new Coordinate(x.Lat, x.Lon))];

    // Una «penisola»: le Alpi da ovest (44.0, 7.0) a est (45.0, 9.0) passando per il nord, e la costa che torna da est a
    // ovest per il sud, molto più lunga. Il pezzo delle Alpi finisce 3 NM prima di dove ricomincia la costa.
    private static readonly IReadOnlyList<Coordinate> Alpi = Linea((44.0, 7.0), (45.5, 7.5), (45.6, 8.5), (45.05, 8.98));
    private static readonly IReadOnlyList<Coordinate> Costa = Linea((45.0, 9.0), (43.0, 9.5), (41.0, 9.0), (42.0, 7.5), (44.0, 7.0));
    private static readonly IReadOnlyList<Coordinate> CostaFrancese = Linea((44.0, 7.0), (43.6, 6.0), (43.3, 5.0));

    [Fact]
    public void LaStradaPiuCorta_PerLeAlpi_AncheSeIPezziSonoStaccati()
    {
        var confini = new ConfiniDelSector([Costa, CostaFrancese, Alpi]);

        var tratto = confini.Traccia(new Position(44.0, 7.0), new Position(45.0, 9.0));

        Assert.NotNull(tratto);
        Assert.Contains(tratto, p => p.Lat > 45.4);
        Assert.DoesNotContain(tratto, p => p.Lat < 43.5);
    }

    [Fact]
    public void UnCapoSulLato_LontanoDaiVertici_EntraNellaRete()
    {
        var confini = new ConfiniDelSector([Alpi]);

        // A metà del lato (45.5, 7.5) – (45.6, 8.5): 21 NM dai suoi vertici, sulla linea.
        var tratto = confini.Traccia(new Position(45.55, 8.0), new Position(45.05, 8.98));

        Assert.Equal([new Position(45.6, 8.5), new Position(45.05, 8.98)], tratto);
    }

    [Fact]
    public void CapiLontaniDalTracciato_NienteStrada()
    {
        var confini = new ConfiniDelSector([Alpi]);

        Assert.Null(confini.Traccia(new Position(40.0, 15.0), new Position(45.05, 8.98)));
        Assert.Equal(0, new ConfiniDelSector([]).Punti);
    }

    [Fact]
    public void NellaMappa_IlConfineSegueIlTracciato_ELaSchedaLoDice()
    {
        var area = new Airspace
        {
            Section = "ENR 2.1", Type = AirspaceType.Fir, Designator = "PROVA FIR",
            Upper = new VerticalLimit { Text = "FL195" }, Lower = new VerticalLimit { Text = "GND" },
            Geometry = new RingGeometry([new LineSegment(new Position(43.0, 8.0)), new BorderSegment(new Position(44.0, 7.0), BorderKind.State, "Italian northern geographical border"),
                new LineSegment(new Position(45.0, 9.0))]),
        };
        var confini = new ConfiniDelSector([Costa, CostaFrancese, Alpi]);

        var s = AipSullaMappa.Di(new AipDocument { State = "LI", Airspaces = [area] }, "2611", _ => null, confini.Tracciatore);

        Assert.Contains(s.Strato.Forme[0].Tratti[0], p => p.LatitudeDeg > 45.4);
        Assert.Contains(s.Voci[0].Campi, c => c.Nome == "Confine" && c.Valore.Contains("itgeo.geo", StringComparison.Ordinal));
    }
}
