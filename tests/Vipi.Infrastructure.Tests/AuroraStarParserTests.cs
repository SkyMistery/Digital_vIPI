using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Sectorfile;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Parser delle STAR dai file <c>&lt;icao&gt;.str</c>: che cosa è una STAR e che cosa è una voce del menu mappe.
/// Le righe sono estratti REALI dei <c>.str</c> della divisione (lirf, lipa, libd, lizz), con l'ICAO tenuto.
/// </summary>
public class AuroraStarParserTests
{
    // Estratti reali del catalogo punti (formato itfix / itvor).
    private const string Fix = """
        ELKAN;N041.00.00.000;E012.00.00.000;0;1;
        ELKAP;N041.10.00.000;E012.10.00.000;0;1;
        GILIO;N041.20.00.000;E012.20.00.000;0;1;
        XIBIL;N041.30.00.000;E012.30.00.000;0;1;
        ROSKO;N045.30.00.000;E012.30.00.000;0;1;
        """;
    private const string Vor = """
        OST;114.90;N041.48.13.600;E012.14.15.100;;;;HLD-OST;
        """;

    private static IReadOnlyList<SourceProcedure> Star(string str, string icao = "LIRF")
    {
        var nav = AuroraSectorfileParser.ParseNavaids(Fix, Vor);
        return AuroraSectorfileParser.ParseStars(icao, str, nav.Names, new Dictionary<string, string>());
    }

    [Fact]
    public void Estrae_La_Star_Su_Ogni_Pista_Col_Punto_Risolto()
    {
        var rows = Star("LIRF;16L:16R;GILI3A;;;;;1;");

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Runway == "16L");
        Assert.Contains(rows, r => r.Runway == "16R");
        Assert.All(rows, r =>
        {
            Assert.Equal("GILIO", r.Fix);            // prefisso troncato GILI completato dal catalogo
            Assert.Equal("GILI3A", r.Name);
            Assert.Equal("RNAV", r.Type);            // campo 8 = 1
            Assert.Equal(ProcedureKind.Star, r.Kind);
            Assert.False(r.NeedsFixReview);
        });
        Assert.NotEqual(rows[0].StableKey, rows[1].StableKey);   // la pista fa parte dell'identità
    }

    [Fact]
    public void Le_Voci_Del_Menu_Mappe_Non_Sono_Star()
    {
        // Estratto di lirf.str: shape del CTR (tipo 1), dell'ATZ (tipo 5), FAP di pista (tipo 4) e il
        // raggruppamento «tutte le STAR per la 16» — che disegna, ma non è una procedura.
        var rows = Star("""
            LIRF;MAPS;LIRF CTR; ; ;1;
            LIRF;MAPS;LIRF ATZ; ; ;5;
            LIRF;MAPS:07;RWY07;;;4;
            LIRF;MAPS;STAR 16 (ALL);;;;;1;
            LIRF;16L:16R;XIBI3A;;;;;1;
            """);

        Assert.Equal(2, rows.Count);                 // solo XIBI3A, sulle sue due piste
        Assert.All(rows, r => Assert.Equal("XIBI3A", r.Name));
    }

    [Fact]
    public void Attese_E_Avvicinamenti_Hanno_La_Pista_Ma_Non_Sono_Star()
    {
        // Righe reali con una pista VERA nel campo 2: solo il tipo le distingue da una STAR.
        var rows = Star("""
            LIRF;25:MAPS;RNP25;;;3;;1;
            LIRF;07;HLD-EFZIL; ; ;2;
            LIRF;16L:16R;ELKA3A;;;;;1;
            """);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal("ELKA3A", r.Name));
    }

    [Fact]
    public void Maps_Dentro_L_Elenco_Toglie_Il_Gettone_Non_La_Star()
    {
        // lipa.str: una STAR vera, per la 05, che è anche voce del menu mappe.
        var rows = Star("LIPA;05:MAPS;ROSK1E; ; ; ;", "LIPA");

        var r = Assert.Single(rows);
        Assert.Equal("05", r.Runway);
        Assert.Equal("ROSK1E", r.Name);
        Assert.Equal("ROSKO", r.Fix);
        Assert.Equal("CONV", r.Type);                // campo 8 vuoto = non RNAV
    }

    [Fact]
    public void Le_Piste_Finte_Di_Lizz_Non_Fanno_Passare_Niente()
    {
        // lizz.str, ICAO finto del militare: nel campo pista ci sono nomi di raccolta (BULL, AAR, AEW).
        var rows = Star("""
            LIZZ;BULL;LEVIS; ; ;3;
            LIZZ;AAR;AAR AURORA; ; ;2;
            LIZZ;AEW;AEW AURORA; ; ; ;
            """, "LIZZ");

        Assert.Empty(rows);
    }

    [Fact]
    public void La_Chiave_Stabile_Distingue_Una_Star_Da_Una_Sid()
    {
        var nav = AuroraSectorfileParser.ParseNavaids(Fix, Vor);
        var vuoto = new Dictionary<string, string>();
        var sid = AuroraSectorfileParser.ParseSids("LIRF", "LIRF;16L;OST1E;;;;;1;", nav.Names, vuoto).Single();
        var star = AuroraSectorfileParser.ParseStars("LIRF", "LIRF;16L;OST1E;;;;;1;", nav.Names, vuoto).Single();

        Assert.Equal("LIRF|OST|E||16L", sid.StableKey);          // invariata: sta scritta nel database
        Assert.Equal("STAR|LIRF|OST|E||16L", star.StableKey);
        Assert.Equal(ProcedureKind.Sid, sid.Kind);
    }

    [Fact]
    public void I_Nomi_Militari_Passano_Come_Sono()
    {
        // lipc.str / licz.str: nomi che non hanno la forma «punto + revisione». Non è il nome a dire se una
        // riga è una STAR — è la pista più il tipo — e il punto irrisolto si segnala, non si indovina.
        var rows = Star("""
            LIPC;11;HITACX14L(ATC); ; ; ;
            LIPC;29;TANGO REC; ; ; ;
            """, "LIPC");

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Name == "HITACX14L(ATC)");
        Assert.Contains(rows, r => r.Name == "TANGO REC");
        Assert.All(rows, r => Assert.True(r.NeedsFixReview));
    }

    [Fact]
    public void Solo_Le_Righe_Con_L_Icao_Del_File()
    {
        // I .str sono per tre quarti vertici, e fra i vertici ci sono etichette di quattro lettere (KILO, LIMA).
        var rows = Star("""
            //MENU MAPS
            N042.02.22.000;E012.21.18.000;
            LIMA;LIMA;
            //LIRF;16L:16R;GILI3A;;;;;1;
            LIRF;16L;ELKA3A;;;;;1;
            """);

        var r = Assert.Single(rows);
        Assert.Equal("ELKA3A", r.Name);
        Assert.True(r.NeedsFixReview);               // ELKA è ambiguo: ELKAN e ELKAP
        Assert.Equal("ELKA", r.Fix);                 // prefisso grezzo, da risolvere con un alias
    }
}
