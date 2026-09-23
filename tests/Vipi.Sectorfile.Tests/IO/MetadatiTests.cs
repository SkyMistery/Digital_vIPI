using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Carta F2, slice 7: i tag <c>//@</c> dei <c>.sid</c> e dei <c>.str</c> (carta madre §8.2) — un file senza tag si legge
/// come prima, un file con i tag li porta ai suoi record, e una dichiarazione che non combacia col nome del record
/// sotto è un errore, non un metadato attaccato al vicino. Sull'albero intero la prova «tag su tutto» la fa
/// <c>tools/Vipi.SectorfileProva</c>.
/// </summary>
public sealed class MetadatiTests
{
    private readonly CollectingWarnings _warnings = new();

    private ParseResult<SidProcedure> Sid(string testo)
        => new SidParser(_warnings).Parse(ParserTestHelpers.Read(testo), "t.sid", new ColorPalette());

    private ParseResult<StrRecord> Str(string testo)
        => new StrParser(_warnings).Parse(ParserTestHelpers.Read(testo), "t.str");

    private static string Righe(params string[] righe) => string.Join("\r\n", righe) + "\r\n";

    [Fact]
    public void UnFileSenzaTagSiLeggeComePrima()
    {
        var letto = new SidParser(_warnings).Parse(RealSectorFiles.Path("lirf.sid")!, new ColorPalette());
        var metadati = Metadati.Leggi(letto, Metadati.NomeSid);

        Assert.Empty(metadati.Record);
        Assert.Empty(metadati.Problemi);
        Assert.Empty(metadati.DelFile);
        Assert.All(letto.Records, r => Assert.Null(metadati.Di(r)));
    }

    [Fact]
    public void UnFileConITagLiPortaAiSuoiRecord()
    {
        var letto = Sid(Righe(
            "//@source=AIRAC2610",
            "LIRF;25;XIBR5A;;;;;1;",
            "//@BANA6W fix=BANAV initialclimb=5000",
            "//@START",
            "LIRF;25;BANA6W;;;;;1;",
            "//@END BANA6W",
            "//@SOSA5A initialclimb=FL070",
            "LIRF;25;SOSA5A;;;;;1;"));
        var metadati = Metadati.Leggi(letto, Metadati.NomeSid);

        Assert.Empty(metadati.Problemi);
        Assert.Equal("AIRAC2610", metadati.DelFile["source"]);
        Assert.Null(metadati.Di(letto.Records[0]));

        var bana = metadati.Di(letto.Records[1])!;
        Assert.Equal(("BANA6W", "BANAV", "5000", true, 3), (bana.Nome, bana.Chiavi["fix"], bana.Chiavi["initialclimb"], bana.Delimitato, bana.Riga));

        // La dichiarazione subito sopra il record basta, senza START/END.
        var sosa = metadati.Di(letto.Records[2])!;
        Assert.Equal(("FL070", false), (sosa.Chiavi["initialclimb"], sosa.Delimitato));
    }

    // La guardia del 21 settembre: un AOD cancella a mano la riga di BANA6W, e la dichiarazione resta sopra la SID dopo.
    [Fact]
    public void UnNomeCheNonCombaciaEUnErrore()
    {
        var letto = Sid(Righe(
            "//@BANA6W initialclimb=5000",
            "LIRF;25;SOSA5A;;;;;1;"));
        var metadati = Metadati.Leggi(letto, Metadati.NomeSid);

        var problema = Assert.Single(metadati.Problemi);
        Assert.Equal((TipoDiProblemaDeiMetadati.NomeNonCombacia, 1, true), (problema.Tipo, problema.Riga, problema.EUnErrore));
        Assert.Null(metadati.Di(letto.Records[0]));
    }

    [Theory]
    [InlineData(TipoDiProblemaDeiMetadati.DichiarazioneOrfana, "//@BANA6W", "", "LIRF;25;BANA6W;;;;;1;")]
    [InlineData(TipoDiProblemaDeiMetadati.DichiarazioneOrfana, "//@BANA6W", "//@SOSA5A", "LIRF;25;SOSA5A;;;;;1;")]
    [InlineData(TipoDiProblemaDeiMetadati.StartSenzaDichiarazione, "//@START", "LIRF;25;BANA6W;;;;;1;", "//@END")]
    [InlineData(TipoDiProblemaDeiMetadati.StartSenzaEnd, "//@BANA6W", "//@START", "LIRF;25;BANA6W;;;;;1;")]
    [InlineData(TipoDiProblemaDeiMetadati.EndSenzaStart, "//@BANA6W", "LIRF;25;BANA6W;;;;;1;", "//@END BANA6W")]
    [InlineData(TipoDiProblemaDeiMetadati.EndConAltroNome, "//@BANA6W", "//@START", "LIRF;25;BANA6W;;;;;1;", "//@END SOSA5A")]
    [InlineData(TipoDiProblemaDeiMetadati.ChiaveSconosciuta, "//@BANA6W colore=rosso", "LIRF;25;BANA6W;;;;;1;")]
    [InlineData(TipoDiProblemaDeiMetadati.ChiaveDiFileFuoriPosto, "LIRF;25;BANA6W;;;;;1;", "//@source=AIRAC2610")]
    [InlineData(TipoDiProblemaDeiMetadati.RigaIllegibile, "//@BANA6W fix=", "LIRF;25;BANA6W;;;;;1;")]
    [InlineData(TipoDiProblemaDeiMetadati.RigaIllegibile, "//@BANA6W fix=BANAV fix=BANAX", "LIRF;25;BANA6W;;;;;1;")]
    [InlineData(TipoDiProblemaDeiMetadati.RigaIllegibile, "//@", "LIRF;25;BANA6W;;;;;1;")]
    public void CiòCheNonTornaSiDice(TipoDiProblemaDeiMetadati atteso, params string[] righe)
    {
        var metadati = Metadati.Leggi(Sid(Righe(righe)), Metadati.NomeSid);

        Assert.Contains(metadati.Problemi, p => p.Tipo == atteso);
    }

    // Una chiave fuori dal catalogo si legge (è un avviso), ma non si scrive.
    [Fact]
    public void UnaChiaveSconosciutaSiLeggeENonSiScrive()
    {
        var letto = Sid(Righe("//@BANA6W colore=rosso", "LIRF;25;BANA6W;;;;;1;"));
        var metadati = Metadati.Leggi(letto, Metadati.NomeSid);

        Assert.False(Assert.Single(metadati.Problemi).EUnErrore);
        Assert.Equal("rosso", metadati.Di(letto.Records[0])!.Chiavi["colore"]);
        Assert.Throws<ArgumentException>(() => Metadati.Scrivi(letto, letto.Records[0], Metadati.NomeSid,
            new Dictionary<string, string> { ["colore"] = "rosso" }));
    }

    // La scrittura su un .sid vero: tre righe in più attorno al record, e nient'altro.
    [Fact]
    public void ScrivereMetteIlBloccoAttornoAlRecord()
    {
        string percorso = RealSectorFiles.Path("lirf.sid")!;
        var letto = new SidParser(_warnings).Parse(percorso, new ColorPalette());
        var xibr = letto.Records.Single(s => s.Name == "XIBR5A");

        var scritto = Metadati.Scrivi(letto, xibr, Metadati.NomeSid,
            new Dictionary<string, string> { ["initialclimb"] = "5000", ["fix"] = "XIBRU" });
        string[] dopo = Salva(scritto);
        string[] prima = File.ReadAllLines(percorso);

        int i = Array.IndexOf(dopo, "LIRF;25;XIBR5A;;;;;1;");
        Assert.Equal(new[] { "//@\"XIBR5A\" fix=XIBRU initialclimb=5000", "//@START", "LIRF;25;XIBR5A;;;;;1;", "//@END \"XIBR5A\"" }, dopo[(i - 2)..(i + 2)]);
        Assert.Equal(prima, dopo.Where(r => !r.StartsWith("//@", StringComparison.Ordinal)));
        Assert.DoesNotContain("//@", string.Concat(File.ReadAllLines(percorso)));   // il file passato non cambia

        // Riscrivere cambia la sola riga della dichiarazione.
        var riletto = new SidParser(_warnings).Parse(ParserTestHelpers.Read(string.Join("\r\n", dopo) + "\r\n"), "t.sid", new ColorPalette());
        var ancora = Metadati.Scrivi(riletto, riletto.Records.Single(s => s.Name == "XIBR5A"), Metadati.NomeSid,
            new Dictionary<string, string> { ["initialclimb"] = "6000" });
        string[] ultimo = Salva(ancora);
        Assert.Equal("//@\"XIBR5A\" initialclimb=6000", Assert.Single(ultimo.Where((r, k) => r != dopo[k])));
    }

    // .str: il nome con lo spazio (MAPS «LIRF CTR») e il record che tiene le righe vuote fino all'intestazione dopo:
    // `//@END` va subito dopo l'ultima riga di dati, le righe vuote dopo di lui.
    [Fact]
    public void InUnoStrLaFineStaPrimaDelleRigheVuote()
    {
        var letto = Str(Righe(
            "LIRF;MAPS;LIRF CTR; ; ;1;",
            "N042.02.22.000;E012.21.18.000;",
            "N041.56.25.000;E011.52.10.000;",
            "",
            "LIRF;16L;BULL1A; ; ;0;",
            "BULL;BULL;"));

        var scritto = Metadati.Scrivi(letto, letto.Records[0], Metadati.NomeStr, new Dictionary<string, string> { ["fix"] = "X" });
        string[] dopo = Salva(scritto);

        Assert.Equal(new[]
        {
            "//@\"LIRF CTR\" fix=X", "//@START", "LIRF;MAPS;LIRF CTR; ; ;1;", "N042.02.22.000;E012.21.18.000;",
            "N041.56.25.000;E011.52.10.000;", "//@END \"LIRF CTR\"", "", "LIRF;16L;BULL1A; ; ;0;", "BULL;BULL;",
        }, dopo);

        var riletto = Str(string.Join("\r\n", dopo) + "\r\n");
        var metadati = Metadati.Leggi(riletto, Metadati.NomeStr);
        Assert.Empty(metadati.Problemi);
        Assert.Equal(("LIRF CTR", true), (metadati.Di(riletto.Records[0])!.Nome, metadati.Di(riletto.Records[0])!.Delimitato));
        Assert.Null(metadati.Di(riletto.Records[1]));
    }

    // Un //@ non è mai corpo di un .str: senza riga vuota, `//@END` e la dichiarazione dopo restano fuori dal record.
    [Fact]
    public void UnTagChiudeIlRecordDelloStr()
    {
        var letto = Str(Righe(
            "//@BULL1A",
            "//@START",
            "LIRF;16L;BULL1A; ; ;0;",
            "BULL;BULL;",
            "//@END BULL1A",
            "//@LEVI1A initialclimb=5000",
            "LIRF;16L;LEVI1A; ; ;0;",
            "LEVIS;LEVIS;"));

        var primo = Assert.IsType<RecordChunk<StrRecord>>(letto.Chunks[0]);
        Assert.Equal(new[] { "LIRF;16L;BULL1A; ; ;0;", "BULL;BULL;" }, primo.RawLines);
        var metadati = Metadati.Leggi(letto, Metadati.NomeStr);
        Assert.Empty(metadati.Problemi);
        Assert.True(metadati.Di(letto.Records[0])!.Delimitato);
        Assert.Equal("5000", metadati.Di(letto.Records[1])!.Chiavi["initialclimb"]);
    }

    // .sid col tracciato (lied.sid, le partenze a vista): un //@ chiude il tracciato come in uno .str.
    [Fact]
    public void UnTagChiudeIlTracciatoDellaSid()
    {
        var letto = new SidParser(_warnings).Parse(RealSectorFiles.Path("lied.sid")!, new ColorPalette());
        var conTracciato = letto.Records.Where(s => s.Track.Count > 0).ToList();
        Assert.NotEmpty(conTracciato);

        var scritto = letto;
        foreach (var sid in conTracciato)
        {
            scritto = Metadati.Scrivi(scritto, sid, Metadati.NomeSid, new Dictionary<string, string> { ["initialclimb"] = "3000" });
        }

        var riletto = new SidParser(_warnings).Parse(ParserTestHelpers.Read(string.Join("\r\n", Salva(scritto)) + "\r\n"), "lied.sid", new ColorPalette());
        var metadati = Metadati.Leggi(riletto, Metadati.NomeSid);
        Assert.Empty(metadati.Problemi);
        Assert.Equal(conTracciato.Select(s => s.Track.Count), riletto.Records.Where(s => metadati.Di(s) is { Delimitato: true }).Select(s => s.Track.Count));
    }

    [Fact]
    public void IlCicloDelFileVaInCimaOSiCambia()
    {
        var letto = Sid(Righe("LIRF;25;XIBR5A;;;;;1;"));

        var scritto = Metadati.ScriviSorgente(letto, Metadati.NomeSid, "AIRAC2610");
        Assert.Equal(new[] { "//@source=AIRAC2610", "LIRF;25;XIBR5A;;;;;1;" }, Salva(scritto));

        var cambiato = Metadati.ScriviSorgente(scritto, Metadati.NomeSid, "AIRAC2611");
        Assert.Equal(new[] { "//@source=AIRAC2611", "LIRF;25;XIBR5A;;;;;1;" }, Salva(cambiato));
        Assert.Equal("AIRAC2611", Metadati.Leggi(cambiato, Metadati.NomeSid).DelFile["source"]);
    }

    // Sopra un blocco rotto non si scrive: prima si sistema (il validatore lo mostra).
    [Fact]
    public void SuUnFileConTagRottiNonSiScrive()
    {
        var letto = Sid(Righe("//@BANA6W", "LIRF;25;SOSA5A;;;;;1;"));

        var ex = Assert.Throws<InvalidOperationException>(() => Metadati.Scrivi(letto, letto.Records[0], Metadati.NomeSid,
            new Dictionary<string, string>()));
        Assert.Contains("NomeNonCombacia", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("5000 ft")]
    [InlineData("")]
    public void UnValoreConSpaziOVuotoNonSiScrive(string valore)
    {
        var letto = Sid(Righe("LIRF;25;XIBR5A;;;;;1;"));

        Assert.Throws<ArgumentException>(() => Metadati.Scrivi(letto, letto.Records[0], Metadati.NomeSid,
            new Dictionary<string, string> { ["initialclimb"] = valore }));
    }

    private static string[] Salva<T>(ParseResult<T> letto)
    {
        string temporaneo = Path.Combine(Path.GetTempPath(), "metadati-" + Guid.NewGuid().ToString("N") + ".tmp");
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

    // Nessun record è toccato: lo scrittore serve solo per gli identificatori.
    private sealed class NessunoScrive<T> : IFileSaver<T>
    {
        public IReadOnlyList<string> Serialize(T record) => throw new InvalidOperationException("Nessun record è toccato.");

        public string GetIdentifier(T record) => "x";
    }
}
