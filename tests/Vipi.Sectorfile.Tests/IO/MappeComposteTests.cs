using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Vipi.Sectorfile.Validazione;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Carta F3-bis, slice 4: la mappa composta rigenerata dalle sue procedure. La forma viene da <c>lime.str</c> del fork
/// (misurata nella slice 0): ODIN4E intera, EKLI4E troncata su OBFUL (D8), l'arco a coordinate in fondo (D9). Sul fork,
/// 21 aggregati su 56 tornano identici dichiarandoli composti con le procedure che disegnano oggi.
/// </summary>
public sealed class MappeComposteTests : IDisposable
{
    private readonly CollectingWarnings _warnings = new();
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "composte-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_radice))
            Directory.Delete(_radice, recursive: true);
    }

    private ParseResult<StrRecord> Str(params string[] righe)
        => new StrParser(_warnings).Parse(ParserTestHelpers.Read(string.Join("\r\n", righe) + "\r\n"), "t.str");

    private static readonly string[] Procedure =
    [
        "LIME;28:10;ODIN4E;;;;;1;", "ODINA;ODINA;4E;", "ME872;ME872;", "OBFUL;OBFUL;", "TIXUM;TIXUM;", "",
        "LIME;28:10;EKLI4E;;;;;1;", "EKLIB;EKLIB;4E;", "ME768;ME768;", "OBFUL;OBFUL;", "TIXUM;TIXUM;",
    ];

    private static readonly string[] Mappa =
    [
        "LIME;MAPS;STAR RNAV(ALL);;;;;1;",
        "ODINA;ODINA;<br>", "ODINA;ODINA;4E;", "ME872;ME872;", "OBFUL;OBFUL;", "TIXUM;TIXUM;", "",
        "EKLIB;EKLIB;<br>", "EKLIB;EKLIB;4E;", "ME768;ME768;", "OBFUL;OBFUL;", "",
        "N045.29.09.347;E010.01.56.131;<br>", "N045.28.41.379;E010.01.41.829;",
    ];

    private static string[] Composta(string chiavi, string[]? mappa = null)
        => ["//@\"STAR RNAV(ALL)\" " + chiavi, "//@START", .. mappa ?? Mappa, "//@END \"STAR RNAV(ALL)\"", "", .. Procedure];

    private static List<string> Chiavi(IEnumerable<PuntoDellaMappa> punti)
        => [.. punti.Select(p => (p.IniziaUnTratto ? "|" : "") + p.Chiave + (p.Suffisso is null ? "" : "/" + p.Suffisso))];

    [Fact]
    public void UnaMappaGiaCosiTornaUgualeEFermaSulPrimoPuntoGiaDisegnato()
    {
        var letto = Str(Composta("composta=ODIN4E,EKLI4E"));
        var composta = Assert.Single(MappeComposte.Di(letto));

        var rigenerata = composta.Componi(letto.Records);

        Assert.True(MappeComposte.Uguale(composta.Mappa, rigenerata.Punti));
        Assert.Empty(rigenerata.Mancanti);
        Assert.Equal(1, rigenerata.TrattiLiberi);   // l'arco a coordinate: resta in fondo
    }

    // D8 rivista: `intere=si` disegna ogni procedura intera, anche dove ripassa su un tratto già disegnato.
    [Fact]
    public void ConIntereLaProceduraSiDisegnaTutta()
    {
        var letto = Str(Composta("composta=ODIN4E,EKLI4E intere=si"));
        var composta = Assert.Single(MappeComposte.Di(letto));

        var rigenerata = composta.Componi(letto.Records);

        Assert.True(composta.Intere);
        Assert.Equal(["|EKLIB", "EKLIB/4E", "ME768", "OBFUL", "TIXUM"], Chiavi(rigenerata.Punti.Skip(5).Take(5)));
    }

    [Fact]
    public void LOrdineELElencoDecidonoIlDisegno()
    {
        var letto = Str(Composta("composta=EKLI4E,ODIN4E"));
        var rigenerata = MappeComposte.Di(letto)[0].Componi(letto.Records);

        // EKLI4E prima, intera; ODIN4E dopo, ferma su OBFUL; poi l'arco.
        Assert.Equal(
            ["|EKLIB", "EKLIB/4E", "ME768", "OBFUL", "TIXUM", "|ODINA", "ODINA/4E", "ME872", "OBFUL"],
            Chiavi(rigenerata.Punti.Take(9)));
        Assert.Equal(2, rigenerata.Punti.Skip(9).Count());
    }

    // La testa del primo tratto tiene la forma che ha oggi: tre modi nei file veri (lime, liea, limj).
    [Theory]
    [InlineData("ODINA;ODINA;<br>", "ODINA;ODINA;4E;")]
    [InlineData("ODINA;ODINA;", "ODINA;ODINA;4E;")]
    [InlineData("ODINA;ODINA;4E;")]
    public void LaTestaDelPrimoTrattoTieneLaSuaForma(params string[] testa)
    {
        string[] mappa = [Mappa[0], .. testa, .. Mappa[3..]];
        var letto = Str(Composta("composta=ODIN4E,EKLI4E", mappa));
        var composta = MappeComposte.Di(letto)[0];

        Assert.True(MappeComposte.Uguale(composta.Mappa, composta.Componi(letto.Records).Punti));
    }

    [Fact]
    public void UnaProceduraCheNonCeSiDiceEIlRestoSiDisegna()
    {
        var letto = Str(Composta("composta=ODIN4E,NENI5A,25:EKLI4E"));
        var rigenerata = MappeComposte.Di(letto)[0].Componi(letto.Records);

        // NENI5A non c'è; EKLI4E c'è, ma per le piste 28:10, non per la 25.
        Assert.Equal([new ProceduraDellaComposta(null, "NENI5A"), new ProceduraDellaComposta("25", "EKLI4E")], rigenerata.Mancanti);
        Assert.Equal(["|ODINA", "ODINA/4E", "ME872", "OBFUL", "TIXUM"], Chiavi(rigenerata.Punti.Take(5)));
    }

    [Fact]
    public void UnaMappaDiSoliNomiNonPrendeUnaCoordinata()
    {
        var soloNomi = Assert.IsType<ProcedureStrRecord>(Str(Mappa[..6]).Records[0]);
        var prima = MappeComposte.PuntiDi(soloNomi);

        Assert.False(MappeComposte.Applica(soloNomi, [new PuntoDellaMappa(null, null, new Coordinate(45, 10), null, true)]));
        Assert.Equal(prima, MappeComposte.PuntiDi(soloNomi));
    }

    [Fact]
    public void IlValidatoreDiceLaProceduraMancanteELaMappaNonAllineata()
    {
        // La mappa ha ancora ME872, ma ODIN4E ora passa da ME999: qualcuno ha cambiato la STAR fuori dal Lab.
        string[] righe = Composta("composta=ODIN4E,EKLI4E,NENI5A");
        righe = [.. righe.Select((r, i) => r == "ME872;ME872;" && i > 10 ? "ME999;ME999;" : r)];
        string percorso = Path.Combine(_radice, "Include", "IT", "lime.str");
        Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
        File.WriteAllText(percorso, string.Join("\r\n", righe) + "\r\n");

        var problemi = Validatore.ValidaLAlbero(_radice).Where(p => p.Regola is Regola.CompostaConProceduraAssente or Regola.CompostaNonAllineata).ToList();

        var assente = Assert.Single(problemi, p => p.Regola == Regola.CompostaConProceduraAssente);
        Assert.Equal((3, Gravita.Errore), (assente.Riga, assente.Gravita));
        Assert.Contains("«NENI5A» non c'è", assente.Dettaglio, StringComparison.Ordinal);
        var disallineata = Assert.Single(problemi, p => p.Regola == Regola.CompostaNonAllineata);
        Assert.Equal((3, Gravita.Avviso), (disallineata.Riga, disallineata.Gravita));
        Assert.StartsWith("LIME;MAPS;STAR RNAV(ALL)", disallineata.Testo, StringComparison.Ordinal);
    }

    [Fact]
    public void UnaMappaAllineataNonDaProblemi()
    {
        string percorso = Path.Combine(_radice, "Include", "IT", "lime.str");
        Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
        File.WriteAllText(percorso, string.Join("\r\n", Composta("composta=ODIN4E,EKLI4E")) + "\r\n");

        Assert.DoesNotContain(Validatore.ValidaLAlbero(_radice),
            p => p.Regola is Regola.CompostaConProceduraAssente or Regola.CompostaNonAllineata or Regola.TagNonValido);
    }
}
