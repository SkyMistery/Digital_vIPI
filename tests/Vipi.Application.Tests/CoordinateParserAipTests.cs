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
    ///
    /// <para>🔴 <b>Secondo difetto, trovato scrivendo questo test</b>: nella forma dell'AIP l'emisfero sta
    /// <b>staccato</b> (<c>24" N</c>). Il pezzo <c>N</c> da solo non è una coordinata, quindi diventa
    /// un'etichetta: la prima fa da <b>tipo</b>, l'ultima da <b>nome</b>, e l'area si chiama «E». I punti
    /// escono giusti solo perché vale «latitudine prima» e l'Italia è N/E: un <c>S</c> o un <c>W</c> staccati
    /// si perderebbero, e il punto cambierebbe emisfero senza segnalazioni. Non dipende dagli archi.</para>
    /// </summary>
    [Fact]
    public void Oggi_Il_Centro_Dell_Arco_Diventa_Un_Vertice()
    {
        var esito = CoordinateParser.Parse(EsempioDelCommittente);

        var area = Assert.Single(esito.Aree);
        Assert.Equal("E", area.Nome);
        Assert.Equal("N", area.Tipo);
        Assert.Equal(3, area.Punti.Count);
        Assert.Equal(44.92472222, area.Punti[1].Lat, 5);    // 44°55'29" N: il CENTRO, messo fra i vertici
        Assert.Equal(7.86194444, area.Punti[1].Lon, 5);

        Assert.Contains(esito.Segnalazioni, x => x.Kind == CoordinateIssueKind.AngoloSpaiato && x.Riga == 2);
        Assert.Contains(esito.Segnalazioni, x => x.Kind == CoordinateIssueKind.RigaNonLetta && x.Riga == 4);
    }
}
