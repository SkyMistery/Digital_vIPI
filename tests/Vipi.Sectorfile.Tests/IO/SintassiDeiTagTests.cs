using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Lotto «Subito», slice 1a: la sintassi dei tag della carta «file per file» §M (27 settembre 2026) — valori fra
/// virgolette quando hanno spazi, nomi con spazi negli elenchi delle composte, un catalogo di chiavi per tipo di file,
/// le chiavi per verso di pista, e le righe <c>//@@</c> dei punti che non sono una dichiarazione.
/// </summary>
public sealed class SintassiDeiTagTests
{
    private readonly CollectingWarnings _warnings = new();

    private ParseResult<SidProcedure> Sid(string testo)
        => new SidParser(_warnings).Parse(ParserTestHelpers.Read(testo), "t.sid", new ColorPalette());

    private ParseResult<StrRecord> Str(string testo)
        => new StrParser(_warnings).Parse(ParserTestHelpers.Read(testo), "t.str");

    private static string Righe(params string[] righe) => string.Join("\r\n", righe) + "\r\n";

    private const string LaSid = "LIRF;25;EKLO8R;;;;;1;";

    private static readonly string[] LaMappa = ["LIME;MAPS;STAR RNAV(ALL);;;;;1;", "ODINA;ODINA;", "OBFUL;OBFUL;"];

    [Fact]
    public void UnValoreConSpaziSiScriveFraVirgoletteESiRilegge()
    {
        var letto = Sid(Righe(LaSid));

        var scritto = Metadati.Scrivi(letto, letto.Records[0], Metadati.NomeSid,
            new Dictionary<string, string> { ["initialclimb"] = Metadati.ValoreDaScrivere("COO APP"), ["fix"] = "EKLOS" });
        string[] righe = Salva(scritto);

        Assert.Equal("//@\"EKLO8R\" fix=EKLOS initialclimb=\"COO APP\"", righe[0]);
        var metadati = Metadati.Leggi(Sid(string.Join("\r\n", righe) + "\r\n"), Metadati.NomeSid);
        Assert.Empty(metadati.Problemi);
        string valore = metadati.Record.Single().Chiavi["initialclimb"];
        Assert.Equal(("\"COO APP\"", "COO APP"), (valore, Metadati.Testo(valore)));
    }

    [Theory]
    [InlineData("COO APP")]         // spazi fuori dalle virgolette
    [InlineData("\"COO APP")]       // la virgoletta non chiude
    [InlineData("")]
    public void UnValoreCheNonSiRileggeNonSiScrive(string valore)
    {
        var letto = Sid(Righe(LaSid));

        Assert.Throws<ArgumentException>(() => Metadati.Scrivi(letto, letto.Records[0], Metadati.NomeSid,
            new Dictionary<string, string> { ["initialclimb"] = valore }));
    }

    [Theory]
    [InlineData("6000ft", "6000ft")]
    [InlineData("COO APP", "\"COO APP\"")]
    public void ValoreDaScrivereMetteLeVirgoletteSoloSeServono(string testo, string scritto)
        => Assert.Equal(scritto, Metadati.ValoreDaScrivere(testo));

    [Theory]
    [InlineData("")]
    [InlineData("A \"B\"")]
    public void ValoreDaScrivereRifiutaIlVuotoELeVirgolette(string testo)
        => Assert.Throws<ArgumentException>(() => Metadati.ValoreDaScrivere(testo));

    [Fact]
    public void LElencoHaNomiFraVirgoletteConSpaziVirgoleEDuePunti()
    {
        var elenco = Metadati.ElencoDellaComposta("ODIN4E,25:\"RNP10 UPETI\",\"A,B:C\"");

        Assert.Equal(
            [new ProceduraDellaComposta(null, "ODIN4E"), new ProceduraDellaComposta("25", "RNP10 UPETI"), new ProceduraDellaComposta(null, "A,B:C")],
            elenco);
        Assert.Equal("ODIN4E,25:\"RNP10 UPETI\",\"A,B:C\"", Metadati.ScriviLElenco(elenco!));
    }

    [Theory]
    [InlineData("ODIN4E,\"RNP10 UPETI")]    // la virgoletta non chiude
    [InlineData("\"\"")]                    // nome vuoto fra virgolette
    [InlineData("25:\"A\"B")]               // qualcosa dopo la virgoletta che chiude
    [InlineData("2\"5\":A")]                // virgolette nella pista
    public void UnElencoConVirgoletteRotteENull(string valore) => Assert.Null(Metadati.ElencoDellaComposta(valore));

    [Fact]
    public void UnaCompostaConUnNomeConSpaziSiScriveESiRilegge()
    {
        var letto = Str(Righe(LaMappa));
        string valore = Metadati.ScriviLElenco([new ProceduraDellaComposta("10", "RNP10 UPETI"), new ProceduraDellaComposta(null, "ODIN4E")]);

        var scritto = Metadati.Scrivi(letto, letto.Records[0], Metadati.NomeStr,
            new Dictionary<string, string> { [Metadati.Compose] = valore, [Metadati.Whole] = "si" });
        var riletto = Str(string.Join("\r\n", Salva(scritto)) + "\r\n");

        var composta = Assert.Single(MappeComposte.Di(riletto));
        Assert.Equal([new ProceduraDellaComposta("10", "RNP10 UPETI"), new ProceduraDellaComposta(null, "ODIN4E")], composta.Elenco);
        Assert.True(composta.Intere);
        Assert.Empty(Metadati.Leggi(riletto, Metadati.NomeStr).Problemi);
    }

    // Il catalogo dipende dal file: una composta sta in un .str, non in un .sid.
    [Fact]
    public void UnaChiaveFuoriDalCatalogoDelSuoFileEUnAvviso()
    {
        var sid = Metadati.Leggi(Sid(Righe("//@\"EKLO8R\" compose=A", LaSid)), Metadati.NomeSid);
        var str = Metadati.Leggi(Str(Righe(["//@\"STAR RNAV(ALL)\" compose=ODIN4E", .. LaMappa])), Metadati.NomeStr);

        var avviso = Assert.Single(sid.Problemi);
        Assert.Equal((TipoDiProblemaDeiMetadati.ChiaveSconosciuta, false), (avviso.Tipo, avviso.EUnErrore));
        Assert.Single(sid.Record);   // si legge lo stesso: un refuso non rompe il file
        Assert.Empty(str.Problemi);
    }

    // Le chiavi di prima del 27 settembre non ci sono più: nel sector vero erano zero.
    [Fact]
    public void LeChiaviVecchieDelleCompostaNonSonoNelCatalogo()
    {
        var metadati = Metadati.Leggi(Str(Righe(["//@\"STAR RNAV(ALL)\" composta=ODIN4E intere=si", .. LaMappa])), Metadati.NomeStr);

        Assert.Equal(TipoDiProblemaDeiMetadati.ChiaveSconosciuta, Assert.Single(metadati.Problemi).Tipo);
        Assert.Empty(MappeComposte.Di(Str(Righe(["//@\"STAR RNAV(ALL)\" composta=ODIN4E", .. LaMappa]))));
    }

    [Fact]
    public void LeChiaviComuniValgonoInOgniFile()
    {
        var metadati = Metadati.Leggi(Sid(Righe("//@\"EKLO8R\" locked=si note=\"dal PDF di giugno\" gen=labels", LaSid)), Metadati.NomeSid);

        Assert.Empty(metadati.Problemi);
        Assert.Equal("dal PDF di giugno", Metadati.Testo(metadati.Record.Single().Chiavi["note"]));
    }

    [Theory]
    [InlineData("06.tora", true)]
    [InlineData("16L.tora", true)]
    [InlineData("34R.int", true)]
    [InlineData("tora", false)]         // la chiave per verso vuole il verso
    [InlineData("6.tora", false)]       // il verso ha due cifre
    [InlineData("06.lda", false)]       // chiave per verso che il catalogo non ha
    [InlineData("06.width", false)]     // chiave del record, non per verso
    [InlineData("width", true)]
    public void LeChiaviPerVersoHannoIlNumeroDellaPistaDavanti(string chiave, bool ammessa)
    {
        var catalogo = new CatalogoDeiTag(".rw di prova", ["width"], ["tora", "int"], []);

        Assert.Equal(ammessa, catalogo.AmmetteDelRecord(chiave));
    }

    // Il tag di un punto (§M regola 4) non è una dichiarazione: non si attacca al record sotto. Fuori da una procedura
    // non ha un punto, ed è un errore (slice 1c: lettura dei punti in TagDeiPuntiTests).
    [Fact]
    public void UnTagDiPuntoNonEUnaDichiarazione()
    {
        var metadati = Metadati.Leggi(Sid(Righe("//@@\"ELVAD\" role=IAF alt=+FL80 spd=-210", LaSid)), Metadati.NomeSid);

        Assert.Equal(TipoDiProblemaDeiMetadati.TagDiPuntoFuoriDalRecord, Assert.Single(metadati.Problemi).Tipo);
        Assert.Empty(metadati.Record);
        Assert.True(Metadati.EUnTag("//@@\"ELVAD\" role=IAF"));
        Assert.True(Metadati.EUnTagDiPunto("//@@\"ELVAD\" role=IAF"));
        Assert.False(Metadati.EUnTagDiPunto("//@\"EKLO8R\" fix=EKLOS"));
    }

    [Theory]
    [InlineData("//@\"EKLO8R\" note=\"non chiude")]         // virgoletta aperta
    [InlineData("//@\"EKLO8R\" \"fix\"=EKLOS")]             // virgolette nella chiave
    [InlineData("//@\"EKLO8R\" note=\"\"")]                 // valore vuoto fra virgolette
    [InlineData("//@\"EKLO8R\" locked")]                    // una parola da sola (§M regola 6: locked=si)
    public void UnTagConVirgoletteRotteOParoleNudeEIllegibile(string dichiarazione)
    {
        var metadati = Metadati.Leggi(Sid(Righe(dichiarazione, LaSid)), Metadati.NomeSid);

        Assert.Equal(TipoDiProblemaDeiMetadati.RigaIllegibile, Assert.Single(metadati.Problemi).Tipo);
        Assert.Empty(metadati.Record);
    }

    // Senza virgolette nel nome (la forma di F2) il valore fra virgolette si legge lo stesso.
    [Fact]
    public void UnValoreFraVirgoletteSiLeggeAncheColNomeSenza()
    {
        var metadati = Metadati.Leggi(Str(Righe("//@LIRF CTR note=\"due parole\"", "LIRF;MAPS;LIRF CTR;;;1;;", "ODINA;ODINA;")), Metadati.NomeStr);

        Assert.Empty(metadati.Problemi);
        var della = metadati.Record.Single();
        Assert.Equal(("LIRF CTR", "\"due parole\""), (della.Nome, della.Chiavi["note"]));
    }

    private static string[] Salva<T>(ParseResult<T> letto)
    {
        string temporaneo = Path.Combine(Path.GetTempPath(), "sintassi-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            new FileSaverOrchestrator().Save(letto, new HashSet<T>(), new NessunoScrive<T>(), temporaneo);
            return File.ReadAllLines(temporaneo);
        }
        finally
        {
            File.Delete(temporaneo);
        }
    }

    private sealed class NessunoScrive<T> : IFileSaver<T>
    {
        public IReadOnlyList<string> Serialize(T record) => throw new InvalidOperationException("Nessun record sporco.");

        public string GetIdentifier(T record) => "x";
    }
}
