using Vipi.Application.Coordinates;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il lettore dei testi AIP (carta <c>docs/feature/2026-09-18-f1-archi-convertitore.md</c>, slice 3). ⚠️ Solo i
/// pochi esempi della carta: i testi AIP interi non entrano nel repo, che è pubblico (copyright ENAV).
/// </summary>
public class AipGeometryReaderTests
{
    private static (double Lat, double Lon) Dms(int g, int m, int s, int gl, int ml, int sl) =>
        (g + m / 60.0 + s / 3600.0, gl + ml / 60.0 + sl / 3600.0);

    /// <summary>L'esempio del committente, 17 settembre 2026 (carta F1 §0).</summary>
    private const string EsempioDelCommittente =
        "44°51'24\" N 008°14'57\" E\n" +
        "then arc of circle in clockwise direction radius 17 NM centred on\n" +
        "44°55'29\" N 007°51'43\" E\n" +
        "till point\n" +
        "44°41'08\" N 008°04'34\" E";

    /// <summary>Cagliari CTR, zona 2 (AIP ENR 2.1.2.9): due archi, da 17 e da 25 NM.</summary>
    private const string CagliariZona2 =
        "39°49'41\"N 009°03'00\"E; 39°31'00\"N 009°26'00\"E; 39°19'43\"N 009°44'22\"E then arc of circle in " +
        "clockwise direction radius 17.0 NM centred on 39°06'16\"N 009°30'58\"E till point 38°55'50\"N 009°48'12\"E; " +
        "38°45'13\"N 009°37'39\"E; 38°47'50\"N 009°05'22\"E then arc of circle in clockwise direction radius 25.0 NM " +
        "centred on 39°12'51\"N 009°05'49\"E till point 39°07'20\"N 008°34'28\"E; 39°11'00\"N 008°33'20\"E; " +
        "39°11'00\"N 009°03'00\"E; to point of origin.";

    /// <summary>Cagliari CTR, zona 3: l'arco finisce SUL punto di origine.</summary>
    private const string CagliariZona3 =
        "39°48'15\"N 008°36'38\"E; 39°52'30\"N 008°59'30\"E; 39°49'41\"N 009°03'00\"E; 39°11'00\"N 009°03'00\"E; " +
        "39°11'00\"N 008°33'20\"E; 39°24'48\"N 008°29'17\"E then arc of circle in clockwise direction radius 27.0 NM " +
        "centred on 39°30'46\"N 009°03'17\"E till point of origin.";

    /// <summary>LI R503/A (AIP ENR 5.1.2), a righe come esce dal PDF: la frase va a capo fra «radius» e il numero.</summary>
    private const string LiR503A =
        "37°43'04\"N 013°04'40\"E;\n37°27'17\"N 013°04'27\"E\nthen arc of circle in clockwise direction radius\n" +
        "16.0 NM centred on\n37°34'41\"N 012°46'38\"E till point\n37°24'35\"N 012°31'02\"E;\n38°04'12\"N 011°48'55\"E\n" +
        "then arc of circle in clockwise direction radius\n18.5 NM centred on\n38°16'00\"N 012°07'00\"E till point\n" +
        "38°27'56\"N 012°25'00\"E;\nto point of origin.";

    [Theory]
    [InlineData(EsempioDelCommittente, true)]
    [InlineData("Circular area centered on 45°00'00\"N 009°00'00\"E within a 1.0 NM radius.", true)]
    [InlineData("44°51'24\"N 008°14'57\"E; 44°41'08\"N 008°04'34\"E; to point\nof origin.", true)]
    [InlineData("N042.00.28.000;E011.58.06.000;\nN041.59.26.000;E011.59.00.000;", false)]
    // ⚠️ Le parole comuni non accendono il lettore: un sectorfile commentato resta al parser a righe.
    [InlineData("//then point from arc\nN042.00.28.000;E011.58.06.000;", false)]
    [InlineData("   ", false)]
    public void Si_Accende_Solo_Con_Una_Frase_Lunga(string testo, bool atteso) =>
        Assert.Equal(atteso, AipGeometryReader.Riconosce(testo));

    /// <summary>
    /// 🔴 Il difetto di partenza, ribaltato: il CENTRO non è più un vertice, il 17 del raggio non è un angolo, e
    /// l'arco passa per i due punti dichiarati.
    /// </summary>
    [Fact]
    public void L_Esempio_Del_Committente_Diventa_Un_Arco()
    {
        var esito = AipGeometryReader.Leggi(EsempioDelCommittente);

        Assert.Empty(esito.Segnalazioni);
        var area = Assert.Single(esito.Aree);
        var inizio = Dms(44, 51, 24, 8, 14, 57);
        var centro = Dms(44, 55, 29, 7, 51, 43);
        var fine = Dms(44, 41, 8, 8, 4, 34);

        Assert.Equal(inizio.Lat, area.Punti[0].Lat, 9);
        Assert.Equal(fine.Lat, area.Punti[^1].Lat, 9);
        Assert.Equal(fine.Lon, area.Punti[^1].Lon, 9);
        Assert.True(area.Punti.Count > 10);
        Assert.All(area.Punti, p => Assert.InRange(ArcGeometry.DistanzaNm(centro, p), 16.9, 17.1));
        Assert.False(area.AnelloChiuso);        // il testo non dice «point of origin»
    }

    [Fact]
    public void Cagliari_Zona_2_Due_Archi_E_L_Anello_Chiuso()
    {
        var esito = AipGeometryReader.Leggi(CagliariZona2);

        Assert.Empty(esito.Segnalazioni);
        var area = Assert.Single(esito.Aree);
        Assert.True(area.AnelloChiuso);

        // I vertici dichiarati ci sono tutti, nell'ordine; i due centri no.
        (double, double)[] dichiarati =
        [
            Dms(39, 49, 41, 9, 3, 0), Dms(39, 31, 0, 9, 26, 0), Dms(39, 19, 43, 9, 44, 22), Dms(38, 55, 50, 9, 48, 12),
            Dms(38, 45, 13, 9, 37, 39), Dms(38, 47, 50, 9, 5, 22), Dms(39, 7, 20, 8, 34, 28), Dms(39, 11, 0, 8, 33, 20),
            Dms(39, 11, 0, 9, 3, 0),
        ];
        var dove = -1;
        foreach (var v in dichiarati)
        {
            var i = IndiceDi(area.Punti, v);
            Assert.True(i > dove, $"vertice {v} mancante o fuori ordine");
            dove = i;
        }
        Assert.Equal(-1, IndiceDi(area.Punti, Dms(39, 6, 16, 9, 30, 58)));
        Assert.Equal(-1, IndiceDi(area.Punti, Dms(39, 12, 51, 9, 5, 49)));

        // Fra il terzo e il quarto vertice c'è l'arco da 17 NM.
        var a = IndiceDi(area.Punti, dichiarati[2]);
        var b = IndiceDi(area.Punti, dichiarati[3]);
        Assert.True(b - a > 10);
        for (var k = a; k <= b; k++)
            Assert.InRange(ArcGeometry.DistanzaNm(Dms(39, 6, 16, 9, 30, 58), area.Punti[k]), 16.9, 17.1);
    }

    /// <summary>«till point of origin»: l'arco finisce sul primo vertice, che è la chiusura e non si ripete.</summary>
    [Fact]
    public void Cagliari_Zona_3_L_Arco_Chiude_Sul_Punto_Di_Origine()
    {
        var esito = AipGeometryReader.Leggi(CagliariZona3);

        Assert.Empty(esito.Segnalazioni);
        var area = Assert.Single(esito.Aree);
        Assert.True(area.AnelloChiuso);
        var origine = Dms(39, 48, 15, 8, 36, 38);
        Assert.Equal(0, IndiceDi(area.Punti, origine));
        Assert.NotEqual(origine, area.Punti[^1]);
        Assert.InRange(ArcGeometry.DistanzaNm(Dms(39, 30, 46, 9, 3, 17), area.Punti[^1]), 26.9, 27.1);
    }

    [Fact]
    public void LI_R503_A_Con_La_Frase_Che_Va_A_Capo()
    {
        var esito = AipGeometryReader.Leggi(LiR503A);

        Assert.Empty(esito.Segnalazioni);                     // niente «angolo spaiato» dai raggi
        var area = Assert.Single(esito.Aree);
        Assert.True(area.AnelloChiuso);
        Assert.Equal(-1, IndiceDi(area.Punti, Dms(37, 34, 41, 12, 46, 38)));
        Assert.Equal(-1, IndiceDi(area.Punti, Dms(38, 16, 0, 12, 7, 0)));
        Assert.True(IndiceDi(area.Punti, Dms(38, 27, 56, 12, 25, 0)) > 0);
        Assert.Equal(12, esito.RigheTotali);
    }

    /// <summary>
    /// Il raggio si toglie dal testo intero, attraverso gli a capo: i numeri di riga di quello che segue devono
    /// restare quelli veri. Il secondo centro, spostato, sta alla riga 10.
    /// </summary>
    [Fact]
    public void Dopo_Un_Raggio_Andato_A_Capo_Le_Righe_Restano_Vere()
    {
        var esito = AipGeometryReader.Leggi(LiR503A.Replace("38°16'00\"", "38°20'00\""));

        var s = Assert.Single(esito.Segnalazioni);
        Assert.Equal(CoordinateIssueKind.RaggioIncoerente, s.Kind);
        Assert.Equal(10, s.Riga);
        Assert.StartsWith("38°20'00\"N", s.Testo);
    }

    [Theory]
    [InlineData("Circular area centered on 45°00'00\"N 009°00'00\"E within a 1.0 NM radius.", 1.0)]
    [InlineData("Circular area centred on\n45°00'00\"N 009°00'00\"E within a 300.0 M radius.", 300.0 / 1852)]
    [InlineData("Circular area centered on 45°00'00\"N 009°00'00\"E within a 5.0 KM radius.", 5000.0 / 1852)]
    public void Il_Cerchio_Legge_Il_Raggio_Nelle_Tre_Unita(string testo, double raggioNm)
    {
        var esito = AipGeometryReader.Leggi(testo);

        Assert.Empty(esito.Segnalazioni);
        var area = Assert.Single(esito.Aree);
        Assert.True(area.AnelloChiuso);
        Assert.Equal(360, area.Punti.Count);
        Assert.All(area.Punti, p =>
            Assert.InRange(ArcGeometry.DistanzaNm((45, 9), p), raggioNm * 0.9999, raggioNm * 1.0001));
    }

    /// <summary>Stessi estremi, verso opposto: i due archi insieme fanno il giro.</summary>
    [Fact]
    public void L_Antiorario_Fa_L_Altro_Giro()
    {
        var orario = Assert.Single(AipGeometryReader.Leggi(EsempioDelCommittente).Aree).Punti.Count;
        var antiorario = Assert.Single(AipGeometryReader.Leggi(
            EsempioDelCommittente.Replace("in clockwise", "in anti-clockwise")).Aree).Punti.Count;

        Assert.InRange(orario + antiorario, 361, 363);
    }

    [Fact]
    public void La_Densita_Moltiplica_I_Punti_Dell_Arco()
    {
        var uno = Assert.Single(AipGeometryReader.Leggi(EsempioDelCommittente).Aree).Punti.Count;
        var due = Assert.Single(AipGeometryReader.Leggi(EsempioDelCommittente, puntiPerGrado: 2).Aree).Punti.Count;

        Assert.InRange(due, 2 * uno - 2, 2 * uno);
    }

    /// <summary>Un centro spostato di miglia si segnala, ancorato alla riga del CENTRO, con lo scarto.</summary>
    [Fact]
    public void Un_Centro_Sbagliato_Si_Segnala_Sulla_Sua_Riga()
    {
        var esito = AipGeometryReader.Leggi(EsempioDelCommittente.Replace("44°55'29\"", "44°58'29\""));

        var s = Assert.Single(esito.Segnalazioni);
        Assert.Equal(CoordinateIssueKind.RaggioIncoerente, s.Kind);
        Assert.Equal(3, s.Riga);
        Assert.NotNull(s.Dettaglio);
        Assert.Single(esito.Aree);                            // l'area si disegna lo stesso
    }

    /// <summary>Due aree nello stesso testo: il punto di origine chiude la prima, la coordinata dopo apre la seconda.</summary>
    [Fact]
    public void Due_Aree_Di_Seguito()
    {
        var esito = AipGeometryReader.Leggi(CagliariZona3 + "\n" + CagliariZona2);

        Assert.Empty(esito.Segnalazioni);
        Assert.Equal(2, esito.Aree.Count);
        Assert.All(esito.Aree, a => Assert.True(a.AnelloChiuso));
    }

    [Theory]
    [InlineData("THEN ARC OF CIRCLE IN ANTI-CLOCKWISE DIRECTION CENTRED ON", "")]
    [InlineData("TILL POINT OF ORIGIN", "")]
    [InlineData("TILL POINT", "")]
    [InlineData("ZONA ZONE", "ZONA ZONE")]
    public void Il_Vocabolario_Toglie_Quello_Che_Riconosce(string frase, string avanzo) =>
        Assert.Equal(avanzo, AipGeometryReader.Classifica(frase).Avanzo);

    [Fact]
    public void Anti_Clockwise_Non_Lascia_Dietro_Un_Clockwise()
    {
        var (sensi, _) = AipGeometryReader.Classifica("ARC OF CIRCLE IN ANTI-CLOCKWISE DIRECTION");

        Assert.Contains(AipGeometryReader.Senso.Antiorario, sensi);
        Assert.DoesNotContain(AipGeometryReader.Senso.Orario, sensi);
    }

    private static int IndiceDi(IReadOnlyList<(double Lat, double Lon)> punti, (double Lat, double Lon) v)
    {
        for (var i = 0; i < punti.Count; i++)
            if (Math.Abs(punti[i].Lat - v.Lat) < 1e-6 && Math.Abs(punti[i].Lon - v.Lon) < 1e-6) return i;
        return -1;
    }
}
