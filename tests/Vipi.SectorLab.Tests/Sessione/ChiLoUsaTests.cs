using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Tests.Sessione;

/// <summary>
/// «Chi lo usa» (lotto «Subito» slice 7, «file per file» L2): dove un punto è citato per nome, e perché una citazione
/// dello stesso nome può essere di un altro punto (un'altra FIR, un NDB col nome del VOR).
/// </summary>
public sealed class ChiLoUsaTests : IDisposable
{
    private const string Prova = "SectorFiles/Include/IT/NAVAIDS/prova.fix";
    private const string Aerovia = "SectorFiles/Include/IT/AIRWAY/prova.lairway";
    private const string Comune = "SectorFiles/Include/IT/AIRWAY/comune.lairway";
    private const string Attese = "SectorFiles/Include/IT/HOLDENR.hold";

    private readonly AlberoDiProva _albero = new();

    public ChiLoUsaTests()
    {
        // ITALY.isc carica il LUSIL di prova.fix (e una sua copia a pochi metri, copia.fix); LIRR.isc un altro LUSIL,
        // lontano, in altro.fix. comune.lairway la caricano tutti e due.
        _albero.Scrivi("SectorFiles/ITALY.isc", Isc("""
            [AIRPORT]
            F;OTHER\itap.ap

            [HOLDENR]
            F;HOLDENR.hold

            [FIXES]
            F;NAVAIDS\copia.fix
            F;NAVAIDS\prova.fix

            [LOW AIRWAY]
            F;AIRWAY\prova.lairway
            F;AIRWAY\comune.lairway
            """));
        _albero.Scrivi("SectorFiles/LIRR.isc", Isc("""
            [FIXES]
            F;NAVAIDS\altro.fix

            [LOW AIRWAY]
            F;AIRWAY\comune.lairway
            """));
        _albero.Scrivi(Prova, "LUSIL;N046.02.35.000;E010.07.00.000;1;0;HLD-LUSIL;\r\nTOP;N045.00.00.000;E010.00.00.000;1;0;\r\n");
        _albero.Scrivi("SectorFiles/Include/IT/NAVAIDS/copia.fix", "LUSIL;N046.02.35.100;E010.07.00.000;1;0;\r\n");
        _albero.Scrivi("SectorFiles/Include/IT/NAVAIDS/altro.fix", "LUSIL;N040.00.00.000;E015.00.00.000;1;0;\r\n");
        _albero.Scrivi(Aerovia, "T;M984;TOP;TOP;\r\nT;M984;LUSIL;LUSIL;\r\n//T;M999;LUSIL;LUSIL;\r\n");
        _albero.Scrivi(Comune, "T;Z1;TOP;TOP;\r\nT;Z1;lusil;lusil;\r\n");
        _albero.Scrivi(Attese, "HLD-LUSIL;N046.02.35.000;E010.07.00.000;LUSIL/225R-9000;\r\n");
        _albero.Scrivi("SectorFiles/Include/IT/lirf.sid", "//@\"LUS1A\" fix=LUSIL\r\nLIRF;07;LUS1A;;;;;1;\r\n");
    }

    public void Dispose() => _albero.Dispose();

    private static string Isc(string sezioni)
        => "[INFO]\r\nN041.48.01.000\r\nE012.14.20.000\r\n60\r\n45\r\n+4.0\r\nIT\r\n\r\n" + sezioni.ReplaceLineEndings("\r\n") + "\r\n";

    private (SessioneAperta Sessione, IReadOnlyDictionary<string, CatalogoDeiPunti> Cataloghi, ChiLoUsa Indice) Apri()
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        return (sessione, CatalogoDeiPunti.PerOgniIsc(sessione), ChiLoUsa.Di(sessione));
    }

    [Fact]
    public void UnFixSiTrovaInAerovieAtteseETagConLaRigaGiusta()
    {
        var (sessione, cataloghi, indice) = Apri();

        var usi = indice.Di(sessione, cataloghi, Prova, 0, _ => []);

        Assert.NotNull(usi);
        Assert.Equal("fix", usi.Catalogo);
        Assert.Equal(["LUSIL"], usi.Nomi);
        // La riga commentata dell'aerovia no: Aurora non la legge.
        Assert.Contains(usi.Citazioni, c => c.File == Aerovia && c.Riga == 2 && c.Come == "punto");
        Assert.DoesNotContain(usi.Citazioni, c => c.File == Aerovia && c.Riga == 3);
        Assert.Contains(usi.Citazioni, c => c.File == Attese && c.Riga == 1 && c.Come == "attesa");
        Assert.Contains(usi.Citazioni, c => c.File.EndsWith("lirf.sid", StringComparison.Ordinal) && c.Riga == 1 && c.Come == "fix=");
    }

    [Fact]
    public void LeMaiuscoleNonContanoEUnMasterCheRisolveAltroloDice()
    {
        var (sessione, cataloghi, indice) = Apri();

        var usi = indice.Di(sessione, cataloghi, Prova, 0, _ => [])!;

        // comune.lairway la carica anche LIRR.isc, dove LUSIL è quello lontano di altro.fix: è nostra solo in ITALY.isc.
        var comune = Assert.Single(usi.Citazioni, c => c.File == Comune);
        Assert.Equal(2, comune.Riga);
        Assert.Equal(["ITALY.isc"], comune.SoloIn);
    }

    [Fact]
    public void UnaCopiaAPochiMetriEloStessoPuntoUnoLontanoNo()
    {
        var (sessione, cataloghi, indice) = Apri();

        // ITALY.isc risolve LUSIL nella copia (copia.fix viene prima): a 3 m, è lo stesso punto — le citazioni sono sue.
        Assert.Contains(indice.Di(sessione, cataloghi, Prova, 0, _ => [])!.Citazioni, c => c.File == Aerovia);

        // Il LUSIL di altro.fix: in comune.lairway LIRR.isc lo risolve in lui, ITALY.isc no.
        var lontano = indice.Di(sessione, cataloghi, "SectorFiles/Include/IT/NAVAIDS/altro.fix", 0, _ => [])!;
        var suo = Assert.Single(lontano.Citazioni);
        Assert.Equal(["LIRR.isc"], suo.SoloIn);
        Assert.DoesNotContain(lontano.Citazioni, c => c.File == Aerovia);
    }

    [Fact]
    public void UnaCitazioneDiUnAltroPuntoDiceDoveVa()
    {
        _albero.Scrivi("SectorFiles/LIRR.isc", Isc("""
            [FIXES]
            F;NAVAIDS\altro.fix

            [LOW AIRWAY]
            F;AIRWAY\prova.lairway
            """));
        var (sessione, cataloghi, indice) = Apri();

        var usi = indice.Di(sessione, cataloghi, "SectorFiles/Include/IT/NAVAIDS/altro.fix", 0, _ => [])!;

        // prova.lairway la carica ora anche LIRR.isc: lì va ad altro.fix; comune.lairway solo ITALY.isc, dove va a prova.fix.
        Assert.Contains(usi.Citazioni, c => c.File == Aerovia && c.SoloIn.SequenceEqual(["LIRR.isc"]));
        // Quel che carica solo ITALY.isc (l'aerovia comune, l'attesa, la SID) va al LUSIL di là: non è suo, e lo dice.
        Assert.Equal([Comune, Attese, "SectorFiles/Include/IT/lirf.sid"], usi.AltroPunto.Select(c => c.File));
        Assert.All(usi.AltroPunto, c => Assert.Equal("fix, NAVAIDS/copia.fix", c.VaA));
    }

    [Fact]
    public void UnAttesaLaCitanoIFixCheCiRimandano()
    {
        var (sessione, cataloghi, indice) = Apri();

        var usi = indice.Di(sessione, cataloghi, Attese, 0, _ => [])!;

        Assert.Equal("attesa", usi.Catalogo);
        var fix = Assert.Single(usi.Citazioni);
        Assert.Equal((Prova, 1, "rimanda all'attesa"), (fix.File, fix.Riga, fix.Come));
    }

    [Fact]
    public void UnRecordCheNonSiCitaPerNomeNonHaUsi()
    {
        var (sessione, cataloghi, indice) = Apri();

        Assert.Null(indice.Di(sessione, cataloghi, Aerovia, 0, _ => []));
    }

    [Fact]
    public void UnFileCambiatoSiRifaESiLeggeComEAdesso()
    {
        var (sessione, cataloghi, indice) = Apri();
        var modifiche = new ModificheInSospeso();
        var aerovia = sessione.File[Aerovia];

        // La riga che citava LUSIL ora cita TOP: rifatto il file, LUSIL lì non c'è più, e TOP ha una riga in più.
        Assert.IsType<ModificaDelTesto>(modifiche.CambiaRiga(aerovia, 2, "T;M984;TOP;TOP;"));
        indice.RifaiIlFile(aerovia);

        Assert.DoesNotContain(indice.Di(sessione, cataloghi, Prova, 0, modifiche.SporchiDi)!.Citazioni, c => c.File == Aerovia);
        Assert.Equal(2, indice.Di(sessione, cataloghi, Prova, 1, modifiche.SporchiDi)!.Citazioni.Count(c => c.File == Aerovia));
    }
}
