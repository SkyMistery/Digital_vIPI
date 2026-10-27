using Aip;
using Vipi.SectorLab.Core.PdfAip;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Carta «Gli aeroporti nel Lab», S1: la pagina dello scalo — le piste coi due lati, le procedure per pista e per genere,
/// le carte di AD 2.24 con quel che il Lab ne legge. Dati inventati.
/// </summary>
public sealed class PaginaDelloScaloTests
{
    private static readonly Position A = new(41.85, 12.26), B = new(41.81, 12.28), C = new(41.80, 12.23), D = new(41.81, 12.27);

    private static Procedure Procedura(ProcedureKind tipo, string nome, string[] piste, string? pagina = null, int tratti = 2) => new()
    {
        Aerodrome = "LIXX", Kind = tipo, Designator = nome, Runways = piste, Page = pagina is null ? null : new AipPage(pagina),
        Legs = [.. Enumerable.Range(0, tratti).Select(i => new ProcedureLeg { PathTerminator = i == 0 ? "IF" : "TF", Fix = i % 2 == 0 ? "UNO" : "DUE" })],
    };

    private static AipDocument Modello() => new()
    {
        State = "LI",
        Airac = "2611",
        Airspaces = [],
        Points = [new SignificantPoint { Section = "AD 2 LIXX", Designator = "UNO", Position = A }, new SignificantPoint { Section = "AD 2 LIXX", Designator = "DUE", Position = D }],
        Aerodromes =
        [
            new Aerodrome
            {
                Icao = "LIXX", Name = "PROVA Aeroporto", Arp = B, Elevation = "14 FT",
                // 16L e 34R con le coordinate (la fine dell'una è la soglia dell'altra); 07 e 25 senza: si accoppiano dal numero.
                Runways =
                [
                    new Runway { Designator = "07", Dimensions = "3190 x 45" }, new Runway { Designator = "16L", Threshold = A, End = B, Dimensions = "3902 x 60" },
                    new Runway { Designator = "25" }, new Runway { Designator = "34R", Threshold = B, End = A },
                ],
                DeclaredDistances = [new DeclaredDistance("16L", null, "3902", "3962", "3902", "3902")],
                Ils = [new Ils { Runway = "16L", Designator = "IFLL", Category = "CAT IIIB", Components = [] }],
                Charts =
                [
                    new AerodromeChart("AERODROME CHART ICAO", "AD 2 LIXX 2-1"),
                    new AerodromeChart("ICAO INSTRUMENT APPROACH CHART ILS Z RWY 16L", "AD 2 LIXX 5-3"),
                    new AerodromeChart("ICAO INSTRUMENT APPROACH CHART RNP RWY 16L DESCRIPTION TABLES", "AD 2 LIXX 5 - 5"),
                    new AerodromeChart("ICAO VISUAL APPROACH CHART", "AD 2 LIXX 5-1"),
                ],
            },
            new Aerodrome { Icao = "LIYY", Name = "ALTRO", Arp = C, Charts = [new AerodromeChart("ICAO INSTRUMENT APPROACH CHART VOR RWY 09", "AD 2 LIYY 5-1")] },
        ],
        Procedures =
        [
            Procedura(ProcedureKind.Approach, "RNP RWY16L", ["16L"], "AD 2 LIXX 5-5"),
            Procedura(ProcedureKind.Approach, "RNP RWY16L via SUVOK", ["16L"], "AD 2 LIXX 5-5"),
            Procedura(ProcedureKind.Star, "ELKAP 1A", [], tratti: 1),
            Procedura(ProcedureKind.Sid, "ABC 1A", ["16L", "34R"], tratti: 0),
            Procedura(ProcedureKind.Sid, "ABC 1B", ["07"]) with { Aerodrome = "LIYY" },
        ],
    };

    [Fact]
    public void LePiste_IDueLati_LeDistanzeEIlsDelLato()
    {
        var modello = Modello();
        var pagina = PagineDegliScali.Di(AipSullaMappa.Di(modello, "2611", _ => null), modello, "lixx")!;

        Assert.Equal("LIXX", pagina.Scalo.Icao);
        Assert.Equal(["07/25", "16L/34R"], pagina.Piste.Select(p => p.Nome));
        var lato = pagina.Piste[1].Lati[0];
        Assert.Equal(("16L", "3902", "IFLL"), (lato.Pista.Designator, Assert.Single(lato.Distanze).Tora, Assert.Single(lato.Ils).Designator));
        Assert.Empty(pagina.Piste[1].Lati[1].Ils);
        Assert.Equal(("34R", "18", "25", null), (PagineDegliScali.Opposto("16L"), PagineDegliScali.Opposto("36"), PagineDegliScali.Opposto("07"), PagineDegliScali.Opposto("H1")));
    }

    [Fact]
    public void LeProcedure_PerPistaEPerGenere_ConLoStato()
    {
        var modello = Modello();
        var strato = AipSullaMappa.Di(modello, "2611", _ => null);
        var pagina = PagineDegliScali.Di(strato, modello, "LIXX", new HashSet<string> { "LIXX Approach RNP RWY16L RWY 16L" })!;

        // La SID di due piste sta sotto tutte e due; la STAR senza pista in fondo; la SID di un altro scalo no.
        Assert.Equal([("16L", "SID"), ("16L", "Avvicinamenti"), ("34R", "SID"), ("", "STAR")], pagina.Procedure.Select(g => (g.Pista, g.Genere)));
        Assert.Equal(["16L", "34R", ""], pagina.PisteDelleProcedure);
        var avvicinamenti = pagina.Procedure[1].Procedure;
        Assert.Equal(["LIXX RNP RWY16L", "LIXX RNP RWY16L via SUVOK"], avvicinamenti.Select(p => strato.Voci[p.Indice].Titolo));
        Assert.Equal([true, false], avvicinamenti.Select(p => p.ConSegnalazione));
        Assert.Equal("2 tratti", avvicinamenti[0].Stato);
        Assert.Equal("senza tratti", pagina.Procedure[0].Procedure[0].Stato);
        Assert.StartsWith("solo un punto", pagina.Procedure[3].Procedure[0].Stato, StringComparison.Ordinal);
        // Le voci dello scalo per «Solo lo scalo sulla mappa»: lo scalo, i suoi due punti, le sue quattro procedure.
        Assert.Contains(pagina.Indice, pagina.Voci);
        Assert.Equal(7, pagina.Voci.Count);
    }

    [Fact]
    public void LeCarte_DiAvvicinamentoLette_SuTutte()
    {
        var modello = Modello();
        var pagina = PagineDegliScali.Di(AipSullaMappa.Di(modello, "2611", _ => null), modello, "LIXX")!;

        Assert.Equal([false, true, true, false], pagina.Carte.Select(c => c.Avvicinamento));
        Assert.Equal([0, 0, 2, 0], pagina.Carte.Select(c => c.Procedure));
        Assert.Equal((1, 2), pagina.CarteIap);
        Assert.Equal(new Dictionary<string, (int, int)> { ["LIXX"] = (1, 2), ["LIYY"] = (0, 1) }, PagineDegliScali.CopertureIap(modello));
        Assert.Null(PagineDegliScali.Di(AipSullaMappa.Di(modello, "2611", _ => null), modello, "LIZZ"));
    }
}
