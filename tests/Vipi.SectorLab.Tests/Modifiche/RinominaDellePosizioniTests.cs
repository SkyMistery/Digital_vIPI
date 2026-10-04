using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
using Vipi.SectorLab.Tests.Ui;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Modifiche;

/// <summary>
/// Le posizioni (lotto «Subito» slice 7c, R-1): chi le usa — i trasferimenti dei .frq, anche escluse, e le teste dei
/// settori dinamici — e la rinomina, che cambia la PAROLA e tiene il «-».
/// </summary>
public sealed class RinominaDellePosizioniTests : IDisposable
{
    private const string Prova = "SectorFiles/Include/IT/OTHER/prova.frq";
    private const string Copia = "SectorFiles/Include/IT/OTHER/copia.frq";
    private const string Settore = "SectorFiles/Include/IT/DYNAMIC_SEC/prova.tfl";

    private readonly AlberoDiProva _albero = new();

    public RinominaDellePosizioniTests()
    {
        _albero.Scrivi(Prova, "LXXX_NW_CTR;124.800;LIRJ LIRR -LXXX_NC_CTR;PREFS\\CTR.cpr;;0;;\r\n"
                              + "LXXX_NC_CTR;131.200;LIRR -LXXX_NW_CTR LXXX_NW_CTRX;PREFS\\CTR.cpr;;0;;\r\n");
        _albero.Scrivi(Copia, "LXXX_NW_CTR;124.800;LIRJ LIRR -LXXX_NC_CTR;PREFS\\CTR.cpr;;0;;\r\n");
        _albero.Scrivi(Settore, "LXXX_NC_CTR LXXX_NW_CTR;CTR;1;CTR;1;\r\nN041.00.00.000;E012.00.00.000;\r\n"
                                + "N042.00.00.000;E012.00.00.000;\r\nN042.00.00.000;E013.00.00.000;\r\n");
    }

    public void Dispose() => _albero.Dispose();

    private (SessioneAperta Sessione, IReadOnlyDictionary<string, CatalogoDeiPunti> Cataloghi, ChiLoUsa Indice) Apri()
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        return (sessione, CatalogoDeiPunti.PerOgniIsc(sessione), ChiLoUsa.Di(sessione));
    }

    [Fact]
    public void UnaPosizioneLaCitanoITrasferimentiEITlf()
    {
        var (sessione, cataloghi, indice) = Apri();

        var usi = indice.Di(sessione, cataloghi, Prova, 0, _ => [])!;

        Assert.Equal("posizione", usi.Catalogo);
        // L'esclusa conta (è una citazione anche lei); LXXX_NW_CTRX no, e nemmeno la riga che la dichiara.
        Assert.Equal([(Settore, 1, "settore dinamico"), (Prova, 2, "trasferimento escluso")],
            usi.Citazioni.Select(c => (c.File, c.Riga, c.Come)));
    }

    [Fact]
    public void LaRinominaCambiaLaParolaETieneIlMeno()
    {
        var (sessione, cataloghi, indice) = Apri();
        var modifiche = new ModificheInSospeso();

        var pronta = Assert.IsType<RinominaPronta>(
            Rinomina.Prepara(sessione, indice, cataloghi, Prova, 0, "LXXX_NW_CTR", "LXXX_WN_CTR", null, modifiche.SporchiDi));
        Assert.IsType<ModificaDelTesto>(modifiche.CambiaInPiuFile([.. pronta.PerFile.Select(f => (sessione.File[f.File], f.Righe))], "rinomina"));

        IReadOnlyList<string> Righe(string file) => ((IFileConRecord)sessione.File[file]).RigheDelFile(modifiche.SporchiDi(file));
        Assert.Equal(["LXXX_WN_CTR;124.800;LIRJ LIRR -LXXX_NC_CTR;PREFS\\CTR.cpr;;0;;",
                      "LXXX_NC_CTR;131.200;LIRR -LXXX_WN_CTR LXXX_NW_CTRX;PREFS\\CTR.cpr;;0;;"], Righe(Prova));
        Assert.Equal("LXXX_WN_CTR;124.800;LIRJ LIRR -LXXX_NC_CTR;PREFS\\CTR.cpr;;0;;", Righe(Copia)[0]);
        Assert.Equal("LXXX_NC_CTR LXXX_WN_CTR;CTR;1;CTR;1;", Righe(Settore)[0]);
        Assert.Equal(1, modifiche.Quante);
    }

    // Slice 13a: le posizioni di una testa separate dai due punti (GCI.tfl, i confini di limmctr.tfl) e senza opacità.
    [Fact]
    public void LaTestaCoiDuePuntiSiTrovaESiRinomina()
    {
        const string confini = "SectorFiles/Include/IT/DYNAMIC_SEC/confini.tfl";
        _albero.Scrivi(confini, "LXXX_NC_CTR:LXXX_NW_CTR:LXXX_NW_CTRX;LIMMLIM;1;LIMMLIM;\r\nN041.00.00.000;E012.00.00.000;\r\n"
                                + "N042.00.00.000;E012.00.00.000;\r\nN042.00.00.000;E013.00.00.000;\r\n");
        var (sessione, cataloghi, indice) = Apri();
        var modifiche = new ModificheInSospeso();

        var usi = indice.Di(sessione, cataloghi, Prova, 0, _ => [])!;
        Assert.Contains((confini, 1, "settore dinamico"), usi.Citazioni.Select(c => (c.File, c.Riga, c.Come)));

        var pronta = Assert.IsType<RinominaPronta>(
            Rinomina.Prepara(sessione, indice, cataloghi, Prova, 0, "LXXX_NW_CTR", "LXXX_WN_CTR", null, modifiche.SporchiDi));
        Assert.IsType<ModificaDelTesto>(modifiche.CambiaInPiuFile([.. pronta.PerFile.Select(f => (sessione.File[f.File], f.Righe))], "rinomina"));

        Assert.Equal("LXXX_NC_CTR:LXXX_WN_CTR:LXXX_NW_CTRX;LIMMLIM;1;LIMMLIM;",
            ((IFileConRecord)sessione.File[confini]).RigheDelFile(modifiche.SporchiDi(confini))[0]);
    }

    [Theory]
    [InlineData("LXXX_NC_CTR", "C'è già")]
    [InlineData("LXXX NW", "spazi")]
    [InlineData("-LXXX_NW", "«-»")]
    public void UnNomeCheNonVaSiRifiuta(string nuovo, string perche)
    {
        var (sessione, cataloghi, indice) = Apri();

        var rifiuto = Assert.IsType<ModificaRifiutata>(
            Rinomina.Prepara(sessione, indice, cataloghi, Prova, 0, "LXXX_NW_CTR", nuovo, null, _ => []));

        Assert.Contains(perche, rifiuto.Motivo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnaPosizioneUsataSiTogliesoloSeUnAltroFrqLaDichiaraAncora()
    {
        var lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        Assert.True(await lab.ApriEValidaAsync(_albero.Radice));

        // LXXX_NC_CTR è usata (dal .tfl e da LXXX_NW_CTR) e sta solo in prova.frq: non si toglie.
        Assert.False(lab.TogliRecord(Prova, 1));
        Assert.Contains("È usato", lab.Rifiuto, StringComparison.Ordinal);

        // LXXX_NW_CTR è usata, ma copia.frq la dichiara ancora: si toglie.
        Assert.True(lab.TogliRecord(Prova, 0));
    }
}
