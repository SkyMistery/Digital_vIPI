using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Carta F3-bis, slice 3: il nome fra virgolette nei tag <c>//@</c> (D4), la chiave <c>composta</c> col suo elenco (D5),
/// e il <c>&lt;br&gt;</c> delle righe per nome, che il modello perdeva. La forma è quella di <c>lime.str</c>
/// (<c>STAR RNAV(ALL)</c>, misurata nella slice 0). Sull'albero intero la prova la fa <c>tools/Vipi.SectorfileProva</c>.
/// </summary>
public sealed class ComposteNeiTagTests
{
    private readonly CollectingWarnings _warnings = new();

    private ParseResult<StrRecord> Str(string testo)
        => new StrParser(_warnings).Parse(ParserTestHelpers.Read(testo), "t.str");

    private static string Righe(params string[] righe) => string.Join("\r\n", righe) + "\r\n";

    private static readonly string[] Mappa =
    [
        "LIME;MAPS;STAR RNAV(ALL);;;;;1;",
        "ODINA;ODINA;<br>",
        "ODINA;ODINA;4E;",
        "OBFUL;OBFUL;",
        "",
        "EKLIB;EKLIB;<br>",
        "EKLIB;EKLIB;4E;",
        "OBFUL;OBFUL;",
        "",
        "N045.29.09.347;E010.01.56.131;<br>",
        "N045.28.41.379;E010.01.41.829;",
    ];

    [Fact]
    public void IlNomeFraVirgoletteHaGliSpaziELeChiaviDopo()
    {
        var letto = Str(Righe([
            "//@\"STAR RNAV(ALL)\" composta=ODIN4E,EKLI4E",
            "//@START",
            .. Mappa,
            "//@END \"STAR RNAV(ALL)\"",
        ]));

        var metadati = Metadati.Leggi(letto, Metadati.NomeStr);

        Assert.Empty(metadati.Problemi);
        var della = metadati.Di(letto.Records[0])!;
        Assert.Equal(("STAR RNAV(ALL)", true), (della.Nome, della.Delimitato));
        Assert.Equal("ODIN4E,EKLI4E", della.Chiavi["composta"]);
    }

    [Fact]
    public void SiScriveFraVirgoletteESiRilegge()
    {
        var letto = Str(Righe(Mappa));

        var scritto = Metadati.Scrivi(letto, letto.Records[0], Metadati.NomeStr,
            new Dictionary<string, string> { ["composta"] = "ODIN4E,25:NENI5A" });
        string[] dopo = Salva(scritto);

        Assert.Equal("//@\"STAR RNAV(ALL)\" composta=ODIN4E,25:NENI5A", dopo[0]);
        Assert.Equal("//@START", dopo[1]);
        Assert.Contains("//@END \"STAR RNAV(ALL)\"", dopo);
        Assert.Equal(Mappa, dopo.Where(r => !Metadati.EUnTag(r)));

        var riletto = Str(string.Join("\r\n", dopo) + "\r\n");
        var metadati = Metadati.Leggi(riletto, Metadati.NomeStr);
        Assert.Empty(metadati.Problemi);
        Assert.Equal("ODIN4E,25:NENI5A", metadati.Di(riletto.Records[0])!.Chiavi["composta"]);
    }

    // Senza virgolette si legge ancora, come lo scriveva F2: il nome arriva fino alla prima parola con «=».
    [Fact]
    public void LaFormaSenzaVirgoletteSiLeggeAncora()
    {
        var letto = Str(Righe(["//@STAR RNAV(ALL) composta=ODIN4E", "//@START", .. Mappa, "//@END STAR RNAV(ALL)"]));

        var metadati = Metadati.Leggi(letto, Metadati.NomeStr);

        Assert.Empty(metadati.Problemi);
        Assert.Equal("ODIN4E", metadati.Di(letto.Records[0])!.Chiavi["composta"]);
    }

    [Theory]
    [InlineData("//@\"STAR RNAV(ALL) composta=ODIN4E")]     // la virgoletta non chiude
    [InlineData("//@\"STAR RNAV(ALL)\"composta=ODIN4E")]    // attaccata alla chiave
    [InlineData("//@\"STAR RNAV(ALL)\" ODIN4E")]            // dopo il nome, una parola che non è una chiave
    [InlineData("//@\"\" composta=ODIN4E")]                 // nome vuoto
    public void LeVirgoletteRotteSonoUnaRigaIllegibile(string dichiarazione)
    {
        var metadati = Metadati.Leggi(Str(Righe([dichiarazione, .. Mappa])), Metadati.NomeStr);

        Assert.Contains(metadati.Problemi, p => p.Tipo == TipoDiProblemaDeiMetadati.RigaIllegibile);
        Assert.Empty(metadati.Record);
    }

    [Fact]
    public void UnNomeConLeVirgoletteNonSiDichiara()
    {
        var letto = Str(Righe("LIME;MAPS;STAR \"A\";;;;;1;", "ODINA;ODINA;"));

        Assert.Throws<InvalidOperationException>(() => Metadati.Scrivi(letto, letto.Records[0], Metadati.NomeStr,
            new Dictionary<string, string> { ["composta"] = "ODIN4E" }));
    }

    [Fact]
    public void LElencoDellaCompostaHaNomiEPiste()
    {
        var elenco = Metadati.ElencoDellaComposta("ODIN4E,25:NENI5A");

        Assert.Equal([new ProceduraDellaComposta(null, "ODIN4E"), new ProceduraDellaComposta("25", "NENI5A")], elenco);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ODIN4E,,EKLI4E")]
    [InlineData(":NENI5A")]
    [InlineData("25:")]
    [InlineData("25:10:NENI5A")]
    public void UnElencoCheNonSiLeggeENull(string valore) => Assert.Null(Metadati.ElencoDellaComposta(valore));

    // Il <br> sulle righe per nome (e sulle coordinate di un record misto) arriva nel modello, e lo scrittore lo rimette.
    [Fact]
    public void IlBrDelleRighePerNomeStaNelModello()
    {
        var mista = Assert.IsType<HoldingStrRecord>(Str(Righe(Mappa)).Records[0]);

        Assert.Equal([true, false, false, true, false, false, true, false], mista.Points.Select(p => p switch
        {
            HoldingFixPoint f => f.IniziaUnTratto,
            HoldingCoordPoint c => c.IniziaUnTratto,
            _ => false,
        }));
        Assert.Equal(
            ["ODINA;ODINA;<br>", "ODINA;ODINA;4E;", "OBFUL;OBFUL;", "EKLIB;EKLIB;<br>", "EKLIB;EKLIB;4E;", "OBFUL;OBFUL;",
             "N045.29.09.347;E010.01.56.131;<br>", "N045.28.41.379;E010.01.41.829;"],
            new StrSaver().Serialize(mista).Skip(1));

        // Una mappa di sole procedure per nome (come STAR VOR(ALL)) è un ProcedureStrRecord: anche lì.
        var perNome = Assert.IsType<ProcedureStrRecord>(Str(Righe(Mappa[..8])).Records[0]);
        Assert.Equal(2, perNome.Waypoints.Count(w => w.IniziaUnTratto));
        Assert.Equal(2, new StrSaver().Serialize(perNome).Count(r => r.EndsWith("<br>", StringComparison.Ordinal)));
    }

    // Slice 5: una mappa che non è più composta perde il tag, e il file torna com'era byte per byte — anche con la
    // procedura dopo e le righe vuote che il record teneva fino all'intestazione successiva.
    [Fact]
    public void TogliereIlTagRimetteIlFileComEra()
    {
        string[] righe = [.. Mappa, "", "LIME;28:10;ODIN4E;;;;;1;", "ODINA;ODINA;4E;", "OBFUL;OBFUL;"];
        var letto = Str(Righe(righe));

        var scritto = Metadati.Scrivi(letto, letto.Records[0], Metadati.NomeStr, new Dictionary<string, string> { ["composta"] = "ODIN4E" });
        var riletto = Str(string.Join("\r\n", Salva(scritto)) + "\r\n");
        var tolto = Metadati.Togli(riletto, riletto.Records[0], Metadati.NomeStr);

        Assert.Equal(righe, Salva(tolto));
        Assert.Empty(Metadati.Leggi(tolto, Metadati.NomeStr).Record);
        // Anche sul file appena scritto, senza rileggerlo: i pezzi sono quelli di Scrivi.
        Assert.Equal(righe, Salva(Metadati.Togli(scritto, letto.Records[0], Metadati.NomeStr)));
    }

    [Fact]
    public void TogliereDaUnRecordSenzaTagNonCambiaNiente()
    {
        var letto = Str(Righe(Mappa));

        Assert.Same(letto, Metadati.Togli(letto, letto.Records[0], Metadati.NomeStr));
    }

    private static string[] Salva<T>(ParseResult<T> letto)
    {
        string temporaneo = Path.Combine(Path.GetTempPath(), "composte-" + Guid.NewGuid().ToString("N") + ".tmp");
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
