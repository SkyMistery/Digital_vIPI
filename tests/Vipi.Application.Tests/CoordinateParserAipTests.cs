using Vipi.Application.Coordinates;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// I testi dell'AIP incollati nel convertitore: archi, cerchi e frasi (carta
/// <c>docs/feature/2026-09-18-f1-archi-convertitore.md</c>).
/// </summary>
public class CoordinateParserAipTests
{
    /// <summary>L'esempio del committente, 17 settembre 2026 (carta F1 §0).</summary>
    private const string EsempioDelCommittente =
        "44°51'24\" N 008°14'57\" E\n" +
        "then arc of circle in clockwise direction radius 17 NM centred on\n" +
        "44°55'29\" N 007°51'43\" E\n" +
        "till point\n" +
        "44°41'08\" N 008°04'34\" E";

    /// <summary>
    /// 🔴 <b>Caratterizzazione del difetto</b> (slice 1): fissa quello che il parser a righe fa OGGI con un
    /// arco. Il <b>centro</b> diventa un vertice, il <c>17</c> del raggio è un «angolo spaiato», <c>till
    /// point</c> una riga non letta — e nessuna segnalazione dice che c'era un arco. La slice 3 ribalta questo
    /// test di proposito, e lo scrive nel commit.
    /// </summary>
    [Fact]
    public void Oggi_Il_Centro_Dell_Arco_Diventa_Un_Vertice()
    {
        var esito = CoordinateParser.Parse(EsempioDelCommittente);

        var area = Assert.Single(esito.Aree);
        Assert.Equal(3, area.Punti.Count);
        Assert.Equal(44.92472222, area.Punti[1].Lat, 5);    // 44°55'29" N: il CENTRO, messo fra i vertici
        Assert.Equal(7.86194444, area.Punti[1].Lon, 5);

        Assert.Contains(esito.Segnalazioni, x => x.Kind == CoordinateIssueKind.AngoloSpaiato && x.Riga == 2);
        Assert.Contains(esito.Segnalazioni, x => x.Kind == CoordinateIssueKind.RigaNonLetta && x.Riga == 4);
    }

    /// <summary>
    /// 🔴 <b>L'emisfero staccato</b>, come lo scrive l'AIP (<c>24" N</c>). Prima diventava un'etichetta: il
    /// primo faceva da tipo, l'ultimo da nome, e l'esempio del committente usciva come un'area di tipo «N» e
    /// nome «E». Trovato scrivendo la caratterizzazione qui sopra.
    /// </summary>
    [Fact]
    public void L_Emisfero_Staccato_Non_Diventa_Un_Nome()
    {
        var esito = CoordinateParser.Parse("44°51'24\" N 008°14'57\" E\n44°41'08\" N 008°04'34\" E");

        var area = Assert.Single(esito.Aree);
        Assert.Null(area.Nome);
        Assert.Null(area.Tipo);
        Assert.Equal(2, area.Punti.Count);
        Assert.Equal(44.85666667, area.Punti[0].Lat, 5);
        Assert.Equal(8.24916667, area.Punti[0].Lon, 5);
        Assert.Empty(esito.Segnalazioni);
    }

    /// <summary>
    /// ⚠️ Il caso che rendeva il difetto pericoloso: un <c>S</c> o un <c>W</c> staccati si perdevano, e il
    /// punto cambiava emisfero senza nessuna segnalazione.
    /// </summary>
    [Theory]
    [InlineData("33°52'00\" S 151°12'00\" E", -33.86666667, 151.2)]
    [InlineData("40°38'00\" N 073°47'00\" W", 40.63333333, -73.78333333)]
    [InlineData("040°38'00\" W 40°38'00\" N", 40.63333333, -40.63333333)]   // l'emisfero decide l'asse
    public void L_Emisfero_Staccato_Decide_Segno_E_Asse(string riga, double lat, double lon)
    {
        var esito = CoordinateParser.Parse(riga);

        var p = Assert.Single(Assert.Single(esito.Aree).Punti);
        Assert.Equal(lat, p.Lat, 5);
        Assert.Equal(lon, p.Lon, 5);
        Assert.Empty(esito.Segnalazioni);
    }

    /// <summary>
    /// La lettera sola si attacca SOLO all'angolo appena letto che non l'aveva detta: dopo un angolo che ha
    /// già il suo emisfero, o lontano da un angolo, resta l'etichetta che era.
    /// </summary>
    [Theory]
    [InlineData("N042.00.28.000;E011.58.06.000;N041.59.26.000;E011.59.00.000;RESTRICT;E;", "RESTRICT", "E")]
    [InlineData("42.00777778 11.96833333 AREA E", "AREA", "E")]
    public void La_Lettera_Sola_Altrove_Resta_Un_Etichetta(string riga, string tipo, string nome)
    {
        var area = Assert.Single(CoordinateParser.Parse(riga).Aree);

        Assert.Equal(tipo, area.Tipo);
        Assert.Equal(nome, area.Nome);
    }
}
