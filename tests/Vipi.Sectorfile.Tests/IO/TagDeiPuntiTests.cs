using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Lotto «Subito», slice 1c: il tag di un punto, <c>//@@"PUNTO" chiave=valore</c> (carta «file per file» §M regola 4,
/// Q2 e P11) — sta subito sopra il suo punto, dentro la procedura, e a differenza di un <c>//@</c> non la chiude.
/// Fino al 27 settembre un <c>//@</c> fra i punti di una STAR la spezzava in due.
/// </summary>
public sealed class TagDeiPuntiTests
{
    private readonly CollectingWarnings _warnings = new();

    private ParseResult<SidProcedure> Sid(string testo)
        => new SidParser(_warnings).Parse(ParserTestHelpers.Read(testo), "t.sid", new ColorPalette());

    private ParseResult<StrRecord> Str(string testo)
        => new StrParser(_warnings).Parse(ParserTestHelpers.Read(testo), "t.str");

    private static string Righe(params string[] righe) => string.Join("\r\n", righe) + "\r\n";

    private static readonly string[] LaStar =
    [
        "LIME;28:10;ODIN4E;;;;;1;",
        "ODINA;ODINA;4E;",
        "OBFUL;OBFUL;",
        "N045.29.09.347;E010.01.56.131;",
    ];

    private static readonly string[] LaSid =
    [
        "LIRF;25;EKLO8R;;;;;1;",
        "N041.49.12.000;E012.14.03.000;",
        "N041.52.00.000;E012.10.00.000;",
    ];

    [Fact]
    public void IlTagDiUnPuntoNonSpezzaLaStar()
    {
        var senza = Str(Righe(LaStar));
        var con = Str(Righe(LaStar[0], LaStar[1], "//@@\"OBFUL\" role=IAF alt=+FL80 spd=-210", LaStar[2], LaStar[3]));

        var star = Assert.Single(con.Records);
        Assert.Equal(PuntiDi(senza.Records[0]), PuntiDi(star));
        var metadati = Metadati.Leggi(con);
        Assert.Empty(metadati.Problemi);
        var delPunto = Assert.Single(metadati.PuntiDi(star));
        Assert.Equal(("OBFUL", "IAF", "+FL80", "-210", 3), (delPunto.Punto, delPunto.Chiavi["role"], delPunto.Chiavi["alt"], delPunto.Chiavi["spd"], delPunto.RigaDelPunto));
        Assert.Equal(3, delPunto.Riga);
    }

    [Fact]
    public void IlTagDelPrimoPuntoDiUnaSidStaNellaSid()
    {
        var con = Sid(Righe(LaSid[0], "//@@\"N041.49.12.000;E012.14.03.000\" alt=+3000", LaSid[1], LaSid[2]));

        var sid = Assert.Single(con.Records);
        Assert.Equal(2, sid.Track.Count);
        var metadati = Metadati.Leggi(con);
        Assert.Empty(metadati.Problemi);
        Assert.Equal("N041.49.12.000;E012.14.03.000", Assert.Single(metadati.Punti).Punto);
    }

    // Una riga vuota fra due punti apre un tratto nuovo della SID; il tag del punto che lo apre non la chiude
    // (trovato dalla prova sull'albero intero in lied.sid).
    [Fact]
    public void IlTagDelPuntoCheApreUnTrattoNonChiudeLaSid()
    {
        var con = Sid(Righe(LaSid[0], LaSid[1], "", "//@@\"N041.52.00.000;E012.10.00.000\" alt=+3000", LaSid[2]));

        var sid = Assert.Single(con.Records);
        Assert.Equal([false, true], sid.Track.Select(p => p.NuovoTratto));
        var metadati = Metadati.Leggi(con);
        Assert.Empty(metadati.Problemi);
        Assert.Single(metadati.Punti);
    }

    [Theory]
    [InlineData(TipoDiProblemaDeiMetadati.PuntoNonCombacia, "//@@\"ODINA\" role=IAF", 2)]        // sopra OBFUL
    [InlineData(TipoDiProblemaDeiMetadati.TagDiPuntoOrfano, "//@@\"OBFUL\" role=IAF", 4)]         // in fondo, niente sotto
    [InlineData(TipoDiProblemaDeiMetadati.RigaIllegibile, "//@@\"OBFUL\" role", 2)]               // parola senza =
    [InlineData(TipoDiProblemaDeiMetadati.RigaIllegibile, "//@@START", 2)]                        // non è una dichiarazione
    public void UnTagDiPuntoCheNonSiAggancia(TipoDiProblemaDeiMetadati atteso, string tag, int dopo)
    {
        var righe = LaStar.ToList();
        righe.Insert(dopo, tag);

        var metadati = Metadati.Leggi(Str(Righe([.. righe])));

        var problema = Assert.Single(metadati.Problemi);
        Assert.Equal((atteso, true), (problema.Tipo, problema.EUnErrore));
        Assert.Empty(metadati.Punti);
    }

    [Fact]
    public void UnTagDiPuntoFuoriDallaProceduraEUnErrore()
    {
        var metadati = Metadati.Leggi(Str(Righe(["//@@\"OBFUL\" role=IAF", .. LaStar])));

        Assert.Equal(TipoDiProblemaDeiMetadati.TagDiPuntoFuoriDalRecord, Assert.Single(metadati.Problemi).Tipo);
    }

    [Fact]
    public void UnaChiaveDiRecordSulPuntoEUnAvviso()
    {
        var metadati = Metadati.Leggi(Str(Righe(LaStar[0], LaStar[1], "//@@\"OBFUL\" fix=OBFUL", LaStar[2], LaStar[3])));

        var problema = Assert.Single(metadati.Problemi);
        Assert.Equal((TipoDiProblemaDeiMetadati.ChiaveSconosciuta, false), (problema.Tipo, problema.EUnErrore));
        Assert.Single(metadati.Punti);
    }

    // Si scrive sopra il punto, si cambia al suo posto, si toglie: il file torna quello di prima.
    [Fact]
    public void IlTagDiUnPuntoSiScriveSiCambiaESiToglie()
    {
        var letto = Str(Righe(LaStar));
        var star = letto.Records[0];

        var scritto = Metadati.ScriviIlPunto(letto, star, 2, new Dictionary<string, string> { ["alt"] = "+FL80", ["role"] = "IAF" });
        Assert.Equal([LaStar[0], LaStar[1], "//@@\"OBFUL\" role=IAF alt=+FL80", LaStar[2], LaStar[3]], Salva(scritto));

        var cambiato = Metadati.ScriviIlPunto(scritto, star, 3, new Dictionary<string, string> { ["role"] = "IF" });
        Assert.Equal([LaStar[0], LaStar[1], "//@@\"OBFUL\" role=IF", LaStar[2], LaStar[3]], Salva(cambiato));

        var riletto = Str(Righe(Salva(cambiato)));
        Assert.Equal("IF", Assert.Single(Metadati.Leggi(riletto).Punti).Chiavi["role"]);
        Assert.Equal(LaStar, Salva(Metadati.TogliIlPunto(riletto, riletto.Records[0], 3)));
        Assert.Same(letto, Metadati.TogliIlPunto(letto, star, 2));
    }

    [Fact]
    public void IlTagDiUnPuntoPerCoordinateHaLeDueCoordinate()
    {
        var letto = Str(Righe(LaStar));

        var scritto = Metadati.ScriviIlPunto(letto, letto.Records[0], 3, new Dictionary<string, string> { ["alt"] = "=4000" });

        Assert.Equal("//@@\"N045.29.09.347;E010.01.56.131\" alt==4000", Salva(scritto)[3]);
        Assert.Empty(Metadati.Leggi(Str(Righe(Salva(scritto)))).Problemi);
    }

    [Theory]
    [InlineData(0)]     // l'intestazione non è un punto
    [InlineData(4)]     // oltre l'ultima riga
    public void SoloUnaRigaDiPuntoPrendeIlTag(int riga)
    {
        var letto = Str(Righe(LaStar));

        Assert.Throws<ArgumentException>(() => Metadati.ScriviIlPunto(letto, letto.Records[0], riga, new Dictionary<string, string> { ["role"] = "IAF" }));
    }

    [Fact]
    public void UnaChiaveDiRecordNonSiScriveSulPunto()
    {
        var letto = Str(Righe(LaStar));

        Assert.Throws<ArgumentException>(() => Metadati.ScriviIlPunto(letto, letto.Records[0], 2, new Dictionary<string, string> { ["fix"] = "OBFUL" }));
    }

    [Theory]
    [InlineData("ELVAD;ELVAD;", "ELVAD")]
    [InlineData("ELVAD;ELVAD;1B 6000;", "ELVAD")]
    [InlineData("N045.29.09.347;E010.01.56.131;<br>", "N045.29.09.347;E010.01.56.131")]
    [InlineData("ALPHA SOUTH;ALPHA SUOTH;", "ALPHA SOUTH;ALPHA SUOTH")]
    public void LaChiaveDelPuntoEIlNomeOLeCoordinate(string riga, string chiave)
        => Assert.Equal(chiave, Metadati.ChiaveDelPunto(riga));

    private static string[] PuntiDi(StrRecord record) => new StrSaver().Serialize(record).Skip(1).ToArray();

    private static string[] Salva<T>(ParseResult<T> letto)
    {
        string temporaneo = Path.Combine(Path.GetTempPath(), "punti-" + Guid.NewGuid().ToString("N") + ".tmp");
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

    // Slice 9d: le righe di punto di una procedura, per la scheda — senza intestazione, tag, commenti e righe vuote.
    [Fact]
    public void LeRigheDeiPuntiSaltanoIntestazioneTagECommenti()
    {
        var letto = Str(Righe(LaStar[0], LaStar[1], "//@@\"OBFUL\" role=IAF", LaStar[2], "//un commento", LaStar[3]));

        Assert.Equal(
            [(1, "ODINA"), (3, "OBFUL"), (5, "N045.29.09.347;E010.01.56.131")],
            Metadati.RigheDeiPunti(letto, letto.Records[0]));
    }

    private sealed class NessunoScrive<T> : IFileSaver<T>
    {
        public IReadOnlyList<string> Serialize(T record) => throw new InvalidOperationException("Nessun record sporco.");

        public string GetIdentifier(T record) => "x";
    }
}
