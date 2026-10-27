using Aip;
using Vipi.SectorLab.Core.PdfAip;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Carta «Gli aeroporti nel Lab», S2: le IAP per carta — le procedure lette dalle pagine di una carta di avvicinamento ne sono
/// le parti, le «DESCRIPTION TABLES» stanno con la loro carta, lo stato. Dati inventati, sulla forma del 2611 (LIRN, LIRF).
/// </summary>
public sealed class CarteDiAvvicinamentoTests
{
    private static readonly Position A = new(40.88, 14.29);

    private static Procedure Avvicinamento(string nome, string[] piste, string pagina, int tratti = 3, string? segmento = null) => new()
    {
        Aerodrome = "LIXX", Kind = ProcedureKind.Approach, Designator = nome, Runways = piste, Page = new AipPage(pagina), Segment = segmento,
        Legs = [.. Enumerable.Range(0, tratti).Select(i => new ProcedureLeg { PathTerminator = i == 0 ? "IF" : "TF", Fix = "UNO" })],
    };

    private static AipDocument Modello() => new()
    {
        State = "LI",
        Airac = "2611",
        Airspaces = [],
        Points = [new SignificantPoint { Section = "AD 2 LIXX", Designator = "UNO", Position = A }],
        Aerodromes =
        [
            new Aerodrome
            {
                Icao = "LIXX", Name = "PROVA", Arp = A,
                Runways = [new Runway { Designator = "24" }, new Runway { Designator = "06" }],
                Charts =
                [
                    new AerodromeChart("ICAO VISUAL APPROACH CHART", "AD 2 LIXX 5-1"),
                    new AerodromeChart("ICAO INSTRUMENT APPROACH CHART ILS­Z RWY 24", "AD 2 LIXX 5-3"),
                    new AerodromeChart("ICAO INSTRUMENT APPROACH CHART ILS OR LOC Y RWY 24", "AD 2 LIXX 5 - 5"),
                    new AerodromeChart("ICAO INSTRUMENT APPROACH CHART RNP Z RWY 24", "AD 2 LIXX 5-7"),
                    new AerodromeChart("ICAO INSTRUMENT APPROACH CHART RNP Z RWY 24 DESCRIPTION TABLES", "AD 2 LIXX 5-9"),
                    new AerodromeChart("ICAO INSTRUMENT APPROACH CHART VOR RWY 06", "AD 2 LIXX 5-11"),
                ],
            },
        ],
        Procedures =
        [
            // ILS Z: la tabella FIX/POINT, una transizione e il mancato, sulla seconda pagina della carta (5-4).
            Avvicinamento("ILS Z RWY24", ["24"], "AD 2 LIXX 5-4", tratti: 0) with { Fixes = [new ApproachFix("FAF D6.0 IZZ", A)] },
            Avvicinamento("ILS Z RWY24", ["24"], "AD 2 LIXX 5-4", segmento: "RNAV to ILS TRANSITION - APPROACH FROM BENTO"),
            Avvicinamento("ILS Z RWY24", ["24"], "AD 2 LIXX 5-4", segmento: "ILS to RNAV TRANSITION - MISSED APPROACH"),
            // LOC Y: solo i punti, e la procedura non dice la pista (la dice il titolo della carta).
            Avvicinamento("LOC Y RWY24", [], "AD 2 LIXX 5-6", tratti: 0) with { Fixes = [new ApproachFix("IF", A)] },
            // RNP Z: dalle DESCRIPTION TABLES, il finale, una seconda tabella e il blocco SBAS sulla pagina dopo.
            Avvicinamento("RNP Z RWY24", ["24"], "AD 2 LIXX 5-9"),
            Avvicinamento("RNP Z RWY24", ["24"], "AD 2 LIXX 5-9", segmento: "(2)"),
            Avvicinamento("RNP Z RWY24", ["24"], "AD 2 LIXX 5-10", tratti: 0) with { DataBlock = ["Operation Type 0"] },
            // Sulle pagine della carta a vista: non è di una carta di avvicinamento, resta fuori.
            Avvicinamento("VISUAL RWY24", ["24"], "AD 2 LIXX 5-2"),
        ],
    };

    private static PaginaDelloScalo Pagina(AipDocument modello) => PagineDegliScali.Di(AipSullaMappa.Di(modello, "2611", _ => null), modello, "LIXX")!;

    [Fact]
    public void LeParti_StannoSullePagineDellaCarta_ConIlLoroNome()
    {
        var pagina = Pagina(Modello());

        Assert.Equal(["ILS Z RWY 24", "ILS OR LOC Y RWY 24", "RNP Z RWY 24", "VOR RWY 06"], pagina.Avvicinamenti.Select(c => c.Titolo));
        var ils = pagina.Avvicinamenti[0];
        Assert.Equal(["FIX/POINT", "RNAV to ILS TRANSITION - APPROACH FROM BENTO", "ILS to RNAV TRANSITION - MISSED APPROACH"], ils.Parti.Select(x => x.Nome));
        Assert.Equal([true, false, false], ils.Parti.Select(x => x.Tabella));
        Assert.Equal((StatoDellaCarta.Letta, "letta · 3 parti, 6 tratti"), (ils.Stato, ils.StatoInParole));
        Assert.Equal(["24"], ils.Piste);
    }

    [Fact]
    public void LeDescriptionTables_StannoConLaLoroCarta_ConLaPaginaDopo()
    {
        var pagina = Pagina(Modello());

        var rnp = pagina.Avvicinamenti[2];
        Assert.Equal("AD 2 LIXX 5-9", Assert.Single(rnp.Tabelle).Page);
        Assert.Equal(["RNP Z RWY24", "RNP Z RWY24 (2)", "SBAS FAS DATA BLOCK"], rnp.Parti.Select(x => x.Nome));
        Assert.Equal(StatoDellaCarta.Letta, rnp.Stato);
        // Nella scheda Carte la riga delle tabelle porta alla sua carta.
        Assert.Same(rnp, pagina.Carte.Single(c => c.Carta.Page == "AD 2 LIXX 5-9").Iap);
        Assert.Same(rnp, pagina.Carte.Single(c => c.Carta.Page == "AD 2 LIXX 5-7").Iap);
        Assert.Null(pagina.Carte[0].Iap);
    }

    [Fact]
    public void InParteESoloImmagine_LaPistaDalTitolo_EQuelleFuoriDalleCarte()
    {
        var pagina = Pagina(Modello());

        var loc = pagina.Avvicinamenti[1];
        Assert.Equal((StatoDellaCarta.InParte, "in parte · solo FIX/POINT"), (loc.Stato, loc.StatoInParole));
        Assert.Equal(["24"], loc.Piste);
        var vor = pagina.Avvicinamenti[3];
        Assert.Equal((StatoDellaCarta.SoloImmagine, "solo immagine", 0), (vor.Stato, vor.StatoInParole, vor.Parti.Count));
        // La RWY 06 ha solo una carta (solo immagine): c'è lo stesso fra le piste delle procedure.
        Assert.Equal(["06", "24"], pagina.PisteDelleProcedure);
        Assert.Equal(["VOR RWY 06"], pagina.AvvicinamentiDi("06").Select(c => c.Titolo));
        // L'avvicinamento sulle pagine della carta a vista resta nel gruppo, fuori dalle carte.
        var fuori = Assert.Single(pagina.Procedure);
        Assert.Equal(("24", AipSullaMappa.Avvicinamenti, "LIXX VISUAL RWY24"), (fuori.Pista, fuori.Genere, Assert.Single(fuori.Procedure).Titolo));
        Assert.Equal((3, 4), pagina.CarteIap);
        Assert.Equal((3, 4), PagineDegliScali.CopertureIap(Modello())["LIXX"]);
    }

    [Fact]
    public void UnaParteSenzaTratti_LaCartaEInParte()
    {
        var modello = Modello();
        modello = modello with { Procedures = [.. modello.Procedures!, Avvicinamento("RNP Z RWY24", ["24"], "AD 2 LIXX 5-10", tratti: 1, segmento: "RNP APPROACH FROM DIEZO")] };

        var rnp = Pagina(modello).Avvicinamenti[2];
        Assert.Equal((StatoDellaCarta.InParte, "in parte · 4 parti, 7 tratti · 1 parte con un punto solo"), (rnp.Stato, rnp.StatoInParole));
    }

    [Fact]
    public void IlTitoloDellaCarta_SenzaLaTestataICAO()
    {
        Assert.Equal("ILS (Cat.II) or LOC X RWY 16L", PagineDegliScali.TitoloDellaCarta("ICAO INSTRUMENT APPROACH CHART ILS (Cat.II) or  LOC X RWY 16L"));
        Assert.Equal("VOR Y RWY 17 DESCRIPTION TABLES", PagineDegliScali.TitoloDellaCarta("ICAO INSTRUMENT APPROACH CHART VOR­Y RWY 17 DESCRIPTION TABLES.pdf"));
        Assert.Equal("ICAO INSTRUMENT APPROACH CHART", PagineDegliScali.TitoloDellaCarta("ICAO INSTRUMENT APPROACH CHART"));
    }

    [Fact]
    public void LeTabelle_TrovanoLaCarta_ColTrattinoMorbidoEColPdfInCoda()
    {
        // LIMC 5-27/5-29 e LIBC 5-7/5-9 sul 2611: i due titoli differiscono solo per la stampa.
        var scalo = new Aerodrome
        {
            Icao = "LIXX", Name = "PROVA", Arp = A,
            Charts =
            [
                new AerodromeChart("ICAO INSTRUMENT APPROACH CHART ILS OR LOC­Z RWY 17L", "AD 2 LIXX 5-27"),
                new AerodromeChart("ICAO INSTRUMENT APPROACH CHART ILS OR LOC Z RWY 17L DESCRIPTION TABLES", "AD 2 LIXX 5-29"),
                new AerodromeChart("ICAO INSTRUMENT APPROACH CHART VOR­Y RWY 17", "AD 2 LIXX 5-31"),
                new AerodromeChart("ICAO INSTRUMENT APPROACH CHART VOR­Y RWY 17 DESCRIPTION TABLES.pdf", "AD 2 LIXX 5-33"),
            ],
        };

        var carte = PagineDegliScali.CarteDi(scalo, [Avvicinamento("VOR Y RWY17", ["17"], "AD 2 LIXX 5-34")]);

        Assert.Equal([("ILS OR LOC Z RWY 17L", "AD 2 LIXX 5-29"), ("VOR Y RWY 17", "AD 2 LIXX 5-33")], carte.Select(c => (c.Titolo, Assert.Single(c.Tabelle).Page)));
        Assert.Equal([0], carte[1].Parti);
        Assert.Equal(["17L"], carte[0].Piste);
    }
}
