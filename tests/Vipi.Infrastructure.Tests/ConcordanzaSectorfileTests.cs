using Vipi.Application.Abstractions;
using Vipi.SectorfileProva;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il motore del sector (<c>Vipi.Sectorfile</c>) e il lettore dell'import di produzione
/// (<c>AuroraSectorfileParser</c>) devono dire la stessa cosa sugli stessi file: stessi punti con le stesse
/// coordinate, stesse SID e STAR per pista (carta F2 §2.4, slice 9). La regola sta in <see cref="Concordanza"/>,
/// condivisa con lo strumento che la misura sull'albero intero; qui gira sui campioni veri del motore, in CI.
/// </summary>
public sealed class ConcordanzaSectorfileTests : IDisposable
{
    private readonly string _cartella = Path.Combine(Path.GetTempPath(), "concordanza-" + Guid.NewGuid().ToString("N"));

    public ConcordanzaSectorfileTests() => Directory.CreateDirectory(_cartella);

    public void Dispose() => Directory.Delete(_cartella, recursive: true);

    [Theory]
    [InlineData("NAVAIDS/itvor.vor", NavaidKind.Vor, "KPT")]      // minuti 75 e secondi 99: nessuno dei due lo legge
    [InlineData("NAVAIDS/itndb.ndb", NavaidKind.Ndb, null)]
    [InlineData("NAVAIDS/APT.fix", NavaidKind.Fix, "MG763")]      // `E008-11.31.443`: il trattino al posto del punto
    [InlineData("NAVAIDS/VFR_NASCOSTI.fix", NavaidKind.Fix, null)]
    public void IPuntiDeiCampioniConcordano(string campione, NavaidKind natura, string? rifiutato)
    {
        var esito = Concordanza.DeiPunti(Campione(campione), natura);

        Assert.True(esito.Pulito, Descrivi(esito));
        Assert.True(esito.Concordi > 0);
        Assert.Equal(rifiutato is null ? Array.Empty<string>() : new[] { rifiutato }, esito.RifiutatiDaEntrambi);
    }

    // Il TACAN di Grosseto (`GRO;;…;35Y`) è la differenza che la concordanza ha trovato: vIPI lo leggeva, il motore lo
    // chiamava malformato. Ora lo leggono tutti e due, accanto al VOR omonimo.
    [Fact]
    public void IlTacanDiGrossetoLoLegganoTuttiEDue()
    {
        var esito = Concordanza.DeiPunti(Scrivi("itvor.vor",
            "GRO;109.85;N042.45.39.200;E011.04.38.300;;;;",
            "GRO;;N042.45.37.200;E011.04.38.600;0;3;35Y"), NavaidKind.Vor);

        Assert.True(esito.Pulito, Descrivi(esito));
        Assert.Equal(2, esito.Concordi);
    }

    // 🔴 Difetto noto di vIPI, NON corretto in F2 (l'import di produzione non cambia, carta §4; lavori aperti):
    // la coppia DECIMALE di un fix (`MIL.fix:235` `TAC-06R;40.98618505;13.75008401;3;`) il motore la legge, il DMS
    // di vIPI no, e il punto resta in catalogo senza posizione. Quando vIPI la leggerà, questo test cade: si capovolge.
    [Fact]
    public void LaCoppiaDecimaleDiUnFixLaLeggeSoloIlMotore()
    {
        var esito = Concordanza.DeiPunti(Scrivi("MIL.fix", "TAC-06R;40.98618505;13.75008401;3;"), NavaidKind.Fix);

        Assert.Contains("TAC-06R: vIPI senza coordinate", Assert.Single(esito.SoloVipi));
        Assert.Empty(esito.SoloMotore);
    }

    [Theory]
    [InlineData("lirf.sid", false)]
    [InlineData("lied.sid", false)]   // i blocchi di partenza a vista: vertici con etichetta, non SID
    [InlineData("lirf.str", true)]    // MAPS, attese, IAP e FAP convivono con le STAR
    public void LeProcedureDeiCampioniConcordano(string campione, bool star)
    {
        var esito = Concordanza.DelleProcedure(Campione(campione), star);

        Assert.True(esito.Pulito, Descrivi(esito));
        Assert.True(esito.Concordi > 0);
    }

    // Gli stessi filtri dalle due parti: la riga di un altro ICAO, il tipo 1 (una shape), MAPS dentro l'elenco piste.
    [Fact]
    public void IFiltriDelleStarSonoGliStessi()
    {
        var esito = Concordanza.DelleProcedure(Scrivi("lirf.str",
            "LIRF;16L:16R;ELKAP1A; ; ;",
            "ELKAP;ELKAP;",
            "LIRA;16L;XIBR5A; ; ;",
            "XIBRO;XIBRO;",
            "LIRF;16L;LIRF CTR; ; ;1;",
            "N041.00.00.000;E012.00.00.000;",
            "LIRF;MAPS:25;RITE1A; ; ;0;",
            "RITEM;RITEM;"), star: true);

        Assert.True(esito.Pulito, Descrivi(esito));
        Assert.Equal(3, esito.Concordi);   // ELKAP1A per 16L e 16R, RITE1A per 25
    }

    // La prova distingue: una STAR che uno dei due non vede si elenca. Un'intestazione di tre campi vIPI la prende
    // (le basta l'ICAO, la pista e il nome), il motore no (vuole i cinque campi di Aurora). Nel sector di oggi non ce
    // n'è nessuna; se un giorno comparisse, lo strumento la direbbe.
    [Fact]
    public void UnaStarCheIlMotoreNonVedeSiElenca()
    {
        var esito = Concordanza.DelleProcedure(Scrivi("lirf.str",
            "LIRF;16L;ELKAP1A;",
            "ELKAP;ELKAP;"), star: true);

        Assert.False(esito.Pulito);
        Assert.Equal("ELKAP1A pista 16L", Assert.Single(esito.SoloVipi));
    }

    private string Scrivi(string nome, params string[] righe)
    {
        string percorso = Path.Combine(_cartella, nome);
        File.WriteAllText(percorso, string.Join("\r\n", righe) + "\r\n");
        return percorso;
    }

    private static string Descrivi(Concordanza.Esito esito) =>
        string.Join("\n", esito.Discordi.Select(d => "discorde " + d)
            .Concat(esito.SoloVipi.Select(d => "solo vIPI " + d))
            .Concat(esito.SoloMotore.Select(d => "solo motore " + d)));

    // I campioni stanno col motore (tests/Vipi.Sectorfile.Tests/Campioni): se ne manca uno il test è ROSSO, non verde a
    // vuoto (la lezione della libreria A).
    private static string Campione(string relativo)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidato = Path.Combine(dir.FullName, "Vipi.Sectorfile.Tests", "Campioni", relativo);
            if (File.Exists(candidato))
            {
                return candidato;
            }
        }

        throw new FileNotFoundException($"Campione mancante: {relativo}");
    }
}
