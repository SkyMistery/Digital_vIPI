using Aip;
using Aip.Aurora;
using Vipi.SectorLab.Core.Mappa;
using Vipi.SectorLab.Core.PdfAip;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Carta F6, slice 1b: il confronto delle forme fra i CTR dell'AIP e le voci CTR dei <c>MAPS</c> del sector, coi casi
/// trovati sul sector vero il 18 ottobre (Malpensa senza un vertice, Parma con un arco in più, Bergamo a due zone).
/// Dati inventati.
/// </summary>
public sealed class ConfrontoDelleFormeTests
{
    private static readonly Position A = new(45.0, 9.0), B = new(45.0, 9.3), C = new(44.8, 9.3), D = new(44.8, 9.0);

    private static Airspace Ctr(string designatore, Geometry geometria) => new()
    {
        Section = "ENR 2.1",
        Type = AirspaceType.Ctr,
        Designator = designatore,
        Upper = new VerticalLimit { Text = "2000ft AMSL" },
        Lower = new VerticalLimit { Text = "SFC" },
        Geometry = geometria,
    };

    private static RingGeometry Anello(params Position[] p) => new([.. p.Select(x => (Segment)new LineSegment(x))]);

    /// <summary>Una voce del MAPS di uno .str: una linea per tratto, come la dà la mappa del Lab.</summary>
    private static FormaDellaMappa Voce(string nome, params Position[][] tratti) => new(
        $"SectorFiles/Include/IT/{nome[..4].ToLowerInvariant()}.str", 3, TipoDiForma.Linea, $"{nome[..4]} {nome}",
        [.. tratti.Select(t => (IReadOnlyList<Coordinate>)[.. t.Select(p => new Coordinate(p.Lat, p.Lon))])], []);

    private static Position[] Chiuso(params Position[] p) => [.. p, p[0]];

    [Fact]
    public void LoStessoCtrEUguale_AncheConLArcoDisegnatoAUnAltraDensita()
    {
        var centro = new Position(44.9, 9.15);
        var inizio = Sphere.Destination(centro, 0, 5);
        var aip = Ctr("Prova CTR", new RingGeometry([new ArcSegment(inizio, centro, new Distance(5, DistanceUnit.Nm), ArcDirection.Clockwise)]));
        // Il sector ha il cerchio a un punto ogni 3 gradi: la freccia è sotto il centesimo di miglio.
        var sector = Voce("LIXX CTR", Chiuso([.. Enumerable.Range(0, 120).Select(i => Sphere.Destination(centro, i * 3, 5))]));

        var esito = Assert.Single(ConfrontoDelleForme.Ctr([aip], [sector]));

        Assert.Equal(EsitoDelConfronto.Uguale, esito.Esito);
        Assert.Equal(("Prova CTR", "LIXX LIXX CTR", 3), (esito.Aip, esito.Sector, esito.Record));
        Assert.True(esito.ScartoAip < 0.01 && esito.ScartoSector < 0.01, $"{esito.ScartoAip} {esito.ScartoSector}");
        Assert.Null(esito.Dove);
    }

    // Malpensa, 18 ottobre: il sector andava dritto fra due vertici, l'AIP ne ha uno in mezzo, 5 NM più a est.
    [Fact]
    public void UnVerticeCheManeNelSectorEUnaFormaDiversa_ColPuntoDoveGuardare()
    {
        var est = new Position(44.9, 9.4);
        var aip = Ctr("Prova CTR", Anello(A, B, est, C, D));
        var sector = Voce("LIXX CTR", Chiuso(A, B, C, D));

        var esito = Assert.Single(ConfrontoDelleForme.Ctr([aip], [sector]));

        Assert.Equal(EsitoDelConfronto.FormaDiversa, esito.Esito);
        Assert.InRange(esito.ScartoAip!.Value, 4.0, 4.5);
        // Anche la retta del sector si stacca dai due lati dell'AIP che ne prendono il posto (Malpensa: 3,0 e 2,8 NM).
        Assert.InRange(esito.ScartoSector!.Value, 3.0, 4.0);
        Assert.Equal(44.9, esito.Dove!.Value.Lat, 2);
        Assert.Equal(9.4, esito.Dove!.Value.Lon, 2);
        Assert.Equal("44°54'00\"N 009°24'00\"E", ConfrontoDelleForme.Dms(esito.Dove.Value));
    }

    // Parma, 18 ottobre: il sector disegna un pezzo che l'AIP non ha (un arco dentro la zona 1).
    [Fact]
    public void UnPezzoInPiuNelSectorEUnaFormaDiversa()
    {
        var aip = Ctr("Prova CTR", Anello(A, B, C, D));
        var sector = Voce("LIXX CTR", Chiuso(A, B, C, D), [new Position(44.9, 9.0), new Position(44.9, 9.15)]);

        var esito = Assert.Single(ConfrontoDelleForme.Ctr([aip], [sector]));

        Assert.Equal(EsitoDelConfronto.FormaDiversa, esito.Esito);
        Assert.Equal(0, esito.ScartoAip!.Value, 2);
        Assert.InRange(esito.ScartoSector!.Value, 5.5, 6.5);
    }

    // Bergamo e Bologna: le zone di un CTR dell'AIP sono un CTR solo, e il sector le passa con una linea.
    [Fact]
    public void LeZoneDiUnCtrVannoInsieme()
    {
        var m1 = new Position(45.0, 9.15);
        var m2 = new Position(44.8, 9.15);
        Airspace[] zone = [Ctr("Prova CTR Zona/Zone '1'", Anello(A, m1, m2, D)), Ctr("Prova CTR Zona/Zone \"2\"", Anello(m1, B, C, m2))];
        var sector = Voce("LIXX CTR", Chiuso(A, m1, m2, D), Chiuso(m1, B, C, m2));

        var esito = Assert.Single(ConfrontoDelleForme.Ctr(zone, [sector]));

        Assert.Equal((EsitoDelConfronto.Uguale, "Prova CTR", 2), (esito.Esito, esito.Aip, esito.Zone));
    }

    [Fact]
    public void MancaNelSector_ESoloNelSector_EIlRestoNonConta()
    {
        var lontano = new Position(40.0, 15.0);
        Airspace[] aip =
        [
            Ctr("Prova CTR", Anello(A, B, C, D)),
            Ctr("Lontano CTR", Anello(lontano, new Position(40.0, 15.2), new Position(39.9, 15.1))),
            // Un'ATZ non è un CTR: non entra nel confronto dei CTR.
            Ctr("Prova ATZ", Anello(A, B, C)) with { Type = AirspaceType.Atz },
        ];
        FormaDellaMappa[] sector =
        [
            Voce("LIXX CTR", Chiuso(A, B, C, D)),
            Voce("LIYY CTR", Chiuso(new Position(38, 13), new Position(38, 13.2), new Position(37.9, 13.1))),
            // Le voci che non dicono CTR, e quelle che non stanno in uno .str, non contano.
            Voce("LIXX ATZ", Chiuso(A, B, C)),
            Voce("LIZZ CTR", Chiuso(A, B, C, D)) with { File = "SectorFiles/Include/IT/DYNAMIC_SEC/lizz.tfl" },
        ];

        var esiti = ConfrontoDelleForme.Ctr(aip, sector);

        Assert.Equal(
            [(EsitoDelConfronto.MancaNelSector, "Lontano CTR", (string?)null), (EsitoDelConfronto.SoloNelSector, null, "LIYY LIYY CTR"), (EsitoDelConfronto.Uguale, "Prova CTR", "LIXX LIXX CTR")],
            esiti.Select(e => (e.Esito, e.Aip, e.Sector)));
    }

    // Il confine o la costa: l'AIP lo nomina e non lo disegna. Il sector lo segue col suo tracciato: non è una differenza.
    [Fact]
    public void UnTrattoDiConfineNonSiGiudica()
    {
        var aip = Ctr("Prova CTR", new RingGeometry(
        [
            new LineSegment(A),
            new BorderSegment(B, BorderKind.State, "Italian northern geographical border"),
            new LineSegment(C),
            new LineSegment(D),
        ]));
        // Fra B e C il sector segue il confine, che si piega 2 NM a est della retta.
        var sector = Voce("LIXX CTR", Chiuso(A, B, new Position(44.9, 9.345), C, D));

        var esito = Assert.Single(ConfrontoDelleForme.Ctr([aip], [sector]));

        Assert.Equal(EsitoDelConfronto.Uguale, esito.Esito);
        Assert.True(esito.SenzaTracciato);
    }

    [Theory]
    [InlineData("Bologna CTR Zona/Zone '1'", "Bologna CTR")]
    [InlineData("Alghero CTR", "Alghero CTR")]
    [InlineData("Aviano CTR Zona /Zone '3'", "Aviano CTR")]
    [InlineData("Senza nome", "Senza nome")]
    public void IlNomeDelCtr(string designatore, string ctr) => Assert.Equal(ctr, ConfrontoDelleForme.NomeDelCtr(designatore));

    // --- le ATZ (slice 1c, dopo la risposta del committente: negli .str e in twrs.tfl) ---------------------------------

    private static Airspace Spazio(AirspaceType tipo, string designatore, Geometry geometria) => Ctr(designatore, geometria) with { Type = tipo };

    /// <summary>Un settore di twrs.tfl: un'area, che si chiude da sé (il primo punto non si ripete).</summary>
    private static FormaDellaMappa Torre(string nome, int record, params Position[] punti) => new(
        "SectorFiles/Include/IT/DYNAMIC_SEC/twrs.tfl", record, TipoDiForma.Area, nome,
        [(IReadOnlyList<Coordinate>)[.. punti.Select(p => new Coordinate(p.Lat, p.Lon))]], []);

    [Fact]
    public void LeAtz_LeMilitariELeAtzRmz_LeZoneDiUnoScaloInsieme()
    {
        Airspace[] aip =
        [
            Spazio(AirspaceType.Atz, "Cameri", Anello(A, B, C)),
            Spazio(AirspaceType.Rmz, "ATZ Torino/Aeritalia LIMA 3 - RMZ", Anello(A, B, C)),
            Spazio(AirspaceType.Rmz, "ATZ Torino/Aeritalia LIMA 4 - RMZ", Anello(A, C, D)),
            Spazio(AirspaceType.Rmz, "ATZ Crotone LIBC - RMZ", Anello(A, C, D)),
            Spazio(AirspaceType.Rmz, "Bari LIYB - RMZ", Anello(A, C, D)),
            Ctr("Prova CTR", Anello(A, B, C)),
        ];

        var gruppi = ConfrontoDelleForme.Gruppi(FamigliaDelConfronto.Atz, aip);

        Assert.Equal(["ATZ Crotone LIBC", "ATZ Torino/Aeritalia LIMA", "Cameri ATZ"], gruppi.Select(g => g.Nome));
        Assert.Equal(2, gruppi[1].Zone.Count);
        Assert.Equal(gruppi.Select(g => g.Nome), ConfrontoDelleForme.Gruppi(FamigliaDelConfronto.AtzDelleTorri, aip).Select(g => g.Nome));
    }

    // Misura del 18 ottobre: Vergiate (LILG) non ha un settore di torre, e prendeva quello di Malpensa, a 3 NM.
    [Fact]
    public void LoScaloComanda_UnaVoceDiUnAltroScaloNonSiPrende()
    {
        var aip = Spazio(AirspaceType.Rmz, "ATZ Vergiate LILG - RMZ", Anello(A, B, C, D));
        var malpensa = Torre("LIMC_TWR", 4, A, B, C, D);

        var esiti = ConfrontoDelleForme.Di(FamigliaDelConfronto.AtzDelleTorri, [aip], [malpensa]);

        Assert.Equal([(EsitoDelConfronto.MancaNelSector, "ATZ Vergiate LILG", (string?)null), (EsitoDelConfronto.SoloNelSector, null, "LIMC_TWR")],
            esiti.Select(e => (e.Esito, e.Aip, e.Sector)));
    }

    // Viterbo, ENR 2.2 del 2610: il centro dell'ATZ stampato a 017°03'44"E (è 012°). Stesso scalo: forma diversa, da guardare.
    [Fact]
    public void LoStessoScaloLontano_EUnaFormaDiversaDaGuardare()
    {
        var aip = Spazio(AirspaceType.Rmz, "ATZ Viterbo LIRV - RMZ", new CircleGeometry(new Position(42.434, 17.062), new Distance(5, DistanceUnit.Nm)));
        var sector = Voce("LIRV ATZ", Chiuso([.. Enumerable.Range(0, 72).Select(i => Sphere.Destination(new Position(42.434, 12.062), i * 5, 5))]));

        var esito = Assert.Single(ConfrontoDelleForme.Di(FamigliaDelConfronto.Atz, [aip], [sector]));

        Assert.Equal((EsitoDelConfronto.FormaDiversa, "LIRV LIRV ATZ"), (esito.Esito, esito.Sector));
        Assert.True(esito.ScartoAip > 200, $"{esito.ScartoAip}");
        Assert.True(ConfrontoDelleForme.DaGuardare(esito));
    }

    // Cameri, 18 ottobre: il settore di twrs.tfl non ripete il primo punto; il lato che lo chiude conta.
    [Fact]
    public void IlSettoreDellaTorreSiChiudeDaSe()
    {
        var aip = Spazio(AirspaceType.Atz, "Cameri", Anello(A, B, C, D));

        var esito = Assert.Single(ConfrontoDelleForme.Di(FamigliaDelConfronto.AtzDelleTorri, [aip], [Torre("LIMN_TWR", 7, A, B, C, D)]));

        Assert.Equal((EsitoDelConfronto.Uguale, "LIMN_TWR", 7), (esito.Esito, esito.Sector, esito.Record));
        // Le voci CTR e ATZ degli .str non sono settori di torre, e viceversa.
        Assert.Equal(EsitoDelConfronto.MancaNelSector, Assert.Single(ConfrontoDelleForme.Di(FamigliaDelConfronto.Atz, [aip], [Torre("LIMN_TWR", 7, A, B, C, D)])).Esito);
    }
}
