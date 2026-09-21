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

    /// <summary>
    /// ENR 2.1.1.1 (Milano, frequenza VFR), alla lettera come esce dal PDF: italiano e inglese mescolati, la
    /// barra fra le lingue, coordinate compatte, il trattino fra i vertici e la virgola dopo il punto d'arrivo.
    /// Si ferma prima del tratto «lungo il fiume Po», che è la slice 5.
    /// </summary>
    private const string MilanoBilingue =
        "455000N\n0091500E -\n454500N\n0091500E -\n453636N\n0091303E -\n453450N\n0091241E -\n453450N\n" +
        "0091308E -\n453115N\n0091308E -\n453030N\n0091225E quindi\narco di cerchio in\nsenso antiorario\n" +
        "di raggio/then\narc of circle\nin anti-clockwise\ndirection radius\n5.0 NM centrato\nin/centered on\n" +
        "452630N\n0091640E fino\nal punto/till\npoint 452335N\n0091054E,\nquindi linea\ncongiungente i\n" +
        "punti/then line\njoining points\n451529N\n0091132E -\n450743N\n0090939E";

    [Fact]
    public void Il_Bilingue_Di_ENR_2_1_1_1_Alla_Lettera()
    {
        var esito = AipGeometryReader.Leggi(MilanoBilingue);

        Assert.Empty(esito.Segnalazioni);
        var area = Assert.Single(esito.Aree);
        var centro = Dms(45, 26, 30, 9, 16, 40);
        Assert.Equal(-1, IndiceDi(area.Punti, centro));

        var inizio = IndiceDi(area.Punti, Dms(45, 30, 30, 9, 12, 25));
        var fine = IndiceDi(area.Punti, Dms(45, 23, 35, 9, 10, 54));
        Assert.Equal(6, inizio);                                 // sette vertici prima dell'arco
        Assert.True(fine - inizio > 10);
        for (var k = inizio; k <= fine; k++)
            Assert.InRange(ArcGeometry.DistanzaNm(centro, area.Punti[k]), 4.9, 5.1);

        // Antiorario: da nord-ovest del centro a sud-ovest passando per OVEST, l'arco corto.
        Assert.InRange(fine - inizio, 60, 120);
        Assert.Equal(fine + 3, area.Punti.Count);                // poi i due vertici della linea
        Assert.Equal(Dms(45, 7, 43, 9, 9, 39).Item1, area.Punti[^1].Lat, 9);
    }

    [Fact]
    public void L_Italiano_Da_Solo()
    {
        const string testo =
            "453030N 0091225E quindi arco di cerchio in senso antiorario di raggio 5.0 NM centrato in " +
            "452630N 0091640E fino al punto 452335N 0091054E; 451529N 0091132E fino al punto di origine.";

        var esito = AipGeometryReader.Leggi(testo);

        Assert.Empty(esito.Segnalazioni);
        var area = Assert.Single(esito.Aree);
        Assert.True(area.AnelloChiuso);
        Assert.Equal(-1, IndiceDi(area.Punti, Dms(45, 26, 30, 9, 16, 40)));
        Assert.InRange(area.Punti.Count, 60, 125);
    }

    /// <summary>«di raggio di 60 NM»: la forma di ENR 2.1.1.1 per i cerchi radar.</summary>
    [Fact]
    public void Il_Raggio_Italiano_Col_Di()
    {
        var esito = AipGeometryReader.Leggi(
            "area circolare centrata su 453714N 0084348E di raggio di 60 NM.");

        Assert.Empty(esito.Segnalazioni);
        var area = Assert.Single(esito.Aree);
        Assert.InRange(ArcGeometry.DistanzaNm(Dms(45, 37, 14, 8, 43, 48), area.Punti[0]), 59.99, 60.01);
    }

    /// <summary>I separatori fra i vertici: trattino corto e lungo, virgola, punto e virgola.</summary>
    [Fact]
    public void I_Separatori_Fra_I_Vertici()
    {
        var esito = AipGeometryReader.Leggi(
            "455000N 0091500E - 454500N 0091500E – 453636N 0091303E, 453450N 0091241E; to point of origin.");

        Assert.Empty(esito.Segnalazioni);
        Assert.Equal(4, Assert.Single(esito.Aree).Punti.Count);
    }

    [Theory]
    [InlineData(EsempioDelCommittente, true)]
    [InlineData(MilanoBilingue, true)]
    [InlineData("453030N 0091225E; 452335N 0091054E; 451529N 0091132E fino al punto di origine", true)]
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

    /// <summary>
    /// 🔴 Fra due cerchi, «within a 1.0 NM radius. Circular area centered on» è UNA frase: chiude il primo e
    /// apre il secondo. Prima il secondo usciva come un'area di un punto; e il raggio del primo non deve
    /// passare al secondo.
    /// </summary>
    [Fact]
    public void Due_Cerchi_Di_Seguito_Ognuno_Col_Suo_Raggio()
    {
        var esito = AipGeometryReader.Leggi(
            "Circular area centered on 45°00'00\"N 009°00'00\"E within a 1.0 NM radius.\n" +
            "Circular area centered on 46°00'00\"N 010°00'00\"E within a 2.0 NM radius.");

        Assert.Empty(esito.Segnalazioni);
        Assert.Equal(2, esito.Aree.Count);
        Assert.InRange(ArcGeometry.DistanzaNm((45, 9), esito.Aree[0].Punti[0]), 0.999, 1.001);
        Assert.InRange(ArcGeometry.DistanzaNm((46, 10), esito.Aree[1].Punti[0]), 1.999, 2.001);
    }

    /// <summary>Lo stesso con un arco: «till point of origin. Circular area centred on» chiude e apre.</summary>
    [Fact]
    public void Un_Arco_Che_Chiude_Sull_Origine_E_Un_Cerchio_Nella_Stessa_Frase()
    {
        var esito = AipGeometryReader.Leggi(
            CagliariZona3 + " Circular area centered on 45°00'00\"N 009°00'00\"E within a 1.0 NM radius.");

        Assert.Empty(esito.Segnalazioni);
        Assert.Equal(2, esito.Aree.Count);
        Assert.Equal(360, esito.Aree[1].Punti.Count);
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

    // ---- Slice 5: la guardia, i tratti non disegnabili, gli archi incompleti ----

    /// <summary>Zone '18' Monte Bianco (AIP ENR 2.1.1.4.1): quattro tratti di confine di stato.</summary>
    private const string MonteBianco =
        "45°53'00\"N 007°05'46\"E\nItalian northern geographical border till point\n45°56'19\"N 007°26'30\"E;\n" +
        "45°56'41\"N 007°28'03\"E;\n45°51'37\"N 007°23'47\"E;\n45°47'47\"N 007°20'45\"E;\n45°39'10\"N 007°12'29\"E;\n" +
        "45°28'37\"N 007°02'49\"E;\n45°28'41\"N 007°02'47\"E\nItalian northern geographical border till point\n" +
        "45°48'22\"N 006°48'46\"E;\n45°48'29\"N 006°48'43\"E\nItalian northern geographical border till point\n" +
        "45°55'20\"N 007°02'41\"E\nItalian northern geographical border till point\n45°53'08\"N 007°05'34\"E;\n" +
        "to point of origin.";

    /// <summary>
    /// Il confine si unisce con una retta e si DICE, una segnalazione per tratto, con la frase. I vertici ci sono
    /// tutti: il tratto non ne mangia nessuno.
    /// </summary>
    [Fact]
    public void Monte_Bianco_Quattro_Tratti_Di_Confine()
    {
        var esito = AipGeometryReader.Leggi(MonteBianco);

        var tratti = esito.Segnalazioni.Where(s => s.Kind == CoordinateIssueKind.TrattoNonDisegnabile).ToList();
        Assert.Equal(4, tratti.Count);
        Assert.Equal(esito.Segnalazioni.Count, tratti.Count);
        Assert.Equal([2, 10, 13, 15], tratti.Select(s => s.Riga));
        Assert.All(tratti, s => Assert.Contains("BORDER", s.Dettaglio));

        var area = Assert.Single(esito.Aree);
        Assert.True(area.AnelloChiuso);
        Assert.Equal(12, area.Punti.Count);                     // i 12 vertici dichiarati, né uno di più né uno di meno
    }

    /// <summary>
    /// LI R12: «line at 500 m from coast to point of origin». Il 500 NON è un angolo (non dichiara l'emisfero),
    /// la costa è un tratto, e il punto di origine chiude.
    /// </summary>
    [Fact]
    public void La_Costa_E_Un_Tratto_E_Il_500_Non_E_Un_Angolo()
    {
        var esito = AipGeometryReader.Leggi(
            "38°06'21\"N 013°24'12\"E;\n38°05'30\"N 013°23'34\"E;\n38°05'00\"N 013°19'41\"E;\n38°09'00\"N 013°19'41\"E;\n" +
            "38°12'47\"N 013°16'51\"E\nline at 500 m from coast\nto point of origin.");

        var s = Assert.Single(esito.Segnalazioni);
        Assert.Equal(CoordinateIssueKind.TrattoNonDisegnabile, s.Kind);
        Assert.Equal(6, s.Riga);
        var area = Assert.Single(esito.Aree);
        Assert.True(area.AnelloChiuso);
        Assert.Equal(5, area.Punti.Count);
    }

    /// <summary>
    /// 🔴 La guardia di F0: gli identificativi fra un'area e l'altra. Cagliari CTR intera, con «Zona/Zone '2'»
    /// davanti a ogni zona: il 2 letto come angolo SALDAVA la zona alla precedente senza errore. Ora tre aree
    /// chiuse, e le etichette dette.
    /// </summary>
    [Fact]
    public void Cagliari_Intera_Le_Etichette_Delle_Zone_Non_Saldano_Le_Aree()
    {
        var esito = AipGeometryReader.Leggi(
            "Zona/Zone '1' 39°30'00\"N 008°46'47\"E; 39°31'30\"N 008°59'00\"E; 39°10'00\"N 009°15'00\"E; " +
            "39°03'00\"N 009°04'10\"E; 39°04'00\"N 008°50'30\"E; to point of origin. Zona/Zone '2' " + CagliariZona2 +
            " Zona/Zone '3' " + CagliariZona3);

        Assert.Equal(3, esito.Aree.Count);
        Assert.All(esito.Aree, a => Assert.True(a.AnelloChiuso));
        Assert.Equal(5, esito.Aree[0].Punti.Count);
        Assert.All(esito.Segnalazioni, s => Assert.Equal(CoordinateIssueKind.FraseNonRiconosciuta, s.Kind));
        Assert.Equal(["ZONA ZONE 1", "ZONA ZONE 2", "ZONA ZONE 3"], esito.Segnalazioni.Select(s => s.Dettaglio));
    }

    [Theory]
    [InlineData("EUC 60 ", "EUC 60")]
    [InlineData("Zona '29' ", "ZONA 29")]
    [InlineData("LI R48 A ", "LI R48 A")]
    public void Un_Identificativo_Davanti_Si_Segnala_E_Non_Diventa_Un_Vertice(string davanti, string avanzo)
    {
        var esito = AipGeometryReader.Leggi(
            davanti + "45°00'00\"N 009°00'00\"E; 45°10'00\"N 009°00'00\"E; 45°10'00\"N 009°10'00\"E; to point of origin.");

        var s = Assert.Single(esito.Segnalazioni);
        Assert.Equal(CoordinateIssueKind.FraseNonRiconosciuta, s.Kind);
        Assert.Equal(avanzo, s.Dettaglio);
        Assert.Equal(3, Assert.Single(esito.Aree).Punti.Count);
    }

    [Theory]
    // Arco senza un vertice prima: non c'è da dove partire.
    [InlineData("then arc of circle in clockwise direction radius 5 NM centred on 45°00'00\"N 009°00'00\"E till point 45°05'00\"N 009°00'00\"E", "inizio")]
    // Arco senza centro.
    [InlineData("45°05'00\"N 009°00'00\"E then arc of circle in clockwise direction radius 5 NM till point 45°00'00\"N 009°07'00\"E", "centro")]
    // Il testo finisce dopo il centro.
    [InlineData("45°05'00\"N 009°00'00\"E then arc of circle in clockwise direction radius 5 NM centred on 45°00'00\"N 009°00'00\"E till point", "fine")]
    // Arco senza raggio: si disegna lo stesso, ma si dice.
    [InlineData("45°05'00\"N 009°00'00\"E then arc of circle in clockwise direction centred on 45°00'00\"N 009°00'00\"E till point 45°00'00\"N 009°07'04\"E", "raggio")]
    // Cerchio senza raggio.
    [InlineData("Circular area centered on 45°00'00\"N 009°00'00\"E.", "raggio")]
    public void L_Arco_Incompleto_Dice_Che_Cosa_Manca(string testo, string manca)
    {
        var esito = AipGeometryReader.Leggi(testo);

        Assert.Contains(esito.Segnalazioni, s => s.Kind == CoordinateIssueKind.ArcoIncompleto && s.Dettaglio == manca);
    }

    [Fact]
    public void L_Arco_Senza_Raggio_Si_Disegna_Lo_Stesso()
    {
        var esito = AipGeometryReader.Leggi(
            "45°05'00\"N 009°00'00\"E then arc of circle in clockwise direction centred on 45°00'00\"N 009°00'00\"E " +
            "till point 45°00'00\"N 009°07'04\"E");

        Assert.True(Assert.Single(esito.Aree).Punti.Count > 80);
    }

    [Theory]
    [InlineData("THEN ARC OF CIRCLE IN ANTI-CLOCKWISE DIRECTION CENTRED ON", "")]
    [InlineData("TILL POINT OF ORIGIN", "")]
    [InlineData("TILL POINT", "")]
    [InlineData("ARCO DI CERCHIO IN SENSO ANTIORARIO DI RAGGIO THEN ARC OF CIRCLE IN ANTI-CLOCKWISE DIRECTION CENTRATO IN CENTERED ON", "")]
    [InlineData("FINO AL PUNTO TILL POINT", "")]
    [InlineData("QUINDI LINEA CONGIUNGENTE I PUNTI THEN LINE JOINING POINTS", "")]
    [InlineData("- –", "")]
    [InlineData("ZONA ZONE", "ZONA ZONE")]
    public void Il_Vocabolario_Toglie_Quello_Che_Riconosce(string frase, string avanzo) =>
        Assert.Equal(avanzo, AipGeometryReader.Classifica(frase).Avanzo);

    [Theory]
    [InlineData("ARC OF CIRCLE IN ANTI-CLOCKWISE DIRECTION")]
    [InlineData("ARCO DI CERCHIO IN SENSO ANTIORARIO")]
    [InlineData("ARCO DI CERCHIO ANTIORARIO")]
    public void L_Antiorario_Non_Lascia_Dietro_Un_Orario(string frase)
    {
        var (sensi, _) = AipGeometryReader.Classifica(frase);

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
