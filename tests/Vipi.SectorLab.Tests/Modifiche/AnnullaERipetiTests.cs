using Vipi.SectorLab.Tests.Ui;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Modifiche;

/// <summary>
/// Annulla e ripeti l'ultimo gesto (chiesti dal committente il 23 settembre). Annullare rimette tutto com'era
/// all'apertura e rigioca i gesti tranne l'ultimo: si prova che i valori tornino, che i diff spariscano, e che un gesto
/// nuovo dopo un annulla butti via quello da ripetere — come in ogni programma.
/// </summary>
public sealed class AnnullaERipetiTests : IDisposable
{
    private const string Settore = "SectorFiles/Include/IT/DYNAMIC_SEC/libb_es_ctr.tfl";

    private readonly AlberoDiProva _albero = new();
    private readonly SessioneDelLab _lab;

    public AnnullaERipetiTests() => _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));

    public void Dispose() => _albero.Dispose();

    private async Task<(string File, int Record)> IlFixBc404()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var forma = _lab.Strati.Single(s => s.Id == "punti").Forme.First(f => f.Etichetta == "BC404");
        _lab.Scegli(forma.File, forma.Record);
        return (forma.File, forma.Record);
    }

    private string Posizione((string File, int Record) fix)
        => _lab.Scheda()!.Campi.Single(c => c.Nome == "Position").Valore;

    [Fact]
    public async Task AnnullaRimetteIlValoreERipetiLoRifà()
    {
        var fix = await IlFixBc404();
        string prima = Posizione(fix);
        Assert.False(_lab.SiPuoAnnullare);

        Assert.True(_lab.CambiaCampo(fix.File, fix.Record, "Position", "N041.00.00.000 E012.00.00.000"));
        string dopo = Posizione(fix);
        Assert.True(_lab.SiPuoAnnullare);
        Assert.Contains("BC404", _lab.DaAnnullare);

        _lab.Annulla();

        Assert.Equal(prima, Posizione(fix));
        Assert.Equal(0, _lab.Modifiche.Quante);
        Assert.Empty(_lab.DiffDi(fix.File).Pezzi);
        Assert.True(_lab.SiPuoRipetere);

        _lab.Ripeti();

        Assert.Equal(dopo, Posizione(fix));
        Assert.Equal(1, _lab.Modifiche.Quante);
        Assert.False(_lab.SiPuoRipetere);
    }

    [Fact]
    public async Task SiAnnullaUnGestoAllaVoltaDallUltimo()
    {
        var fix = await IlFixBc404();
        string primo = "N041.00.00.000 E012.00.00.000";
        Assert.True(_lab.CambiaCampo(fix.File, fix.Record, "Position", primo));
        Assert.True(_lab.CambiaCampo(fix.File, fix.Record, "Position", "N042.00.00.000 E013.00.00.000"));

        _lab.Annulla();

        // Torna al valore del PRIMO gesto, non a quello dell'apertura: è la differenza con «annulla» del pannello.
        Assert.Equal(1, _lab.Modifiche.Quante);
        Assert.Contains("41", Posizione(fix));
        Assert.Contains("012", Posizione(fix));
    }

    [Fact]
    public async Task UnGestoNuovoDopoUnAnnullaNonLasciaPiùNienteDaRipetere()
    {
        var fix = await IlFixBc404();
        Assert.True(_lab.CambiaCampo(fix.File, fix.Record, "Position", "N041.00.00.000 E012.00.00.000"));
        _lab.Annulla();
        Assert.True(_lab.SiPuoRipetere);

        Assert.True(_lab.CambiaCampo(fix.File, fix.Record, "Position", "N042.00.00.000 E013.00.00.000"));

        Assert.False(_lab.SiPuoRipetere);
    }

    [Fact]
    public async Task UnGestoRifiutatoNonEntraNellaStoria()
    {
        var fix = await IlFixBc404();

        Assert.False(_lab.CambiaCampo(fix.File, fix.Record, "Position", "non è una coordinata"));

        Assert.False(_lab.SiPuoAnnullare);
    }

    [Fact]
    public async Task SiAnnullaAncheUnRecordAggiunto()
    {
        var fix = await IlFixBc404();
        int quanti = _lab.Sessione!.File[fix.File].Record;

        Assert.True(_lab.AggiungiRecord(fix.File, fix.Record));
        Assert.Equal(quanti + 1, _lab.Sessione.File[fix.File].Record);

        _lab.Annulla();

        Assert.Equal(quanti, _lab.Sessione.File[fix.File].Record);
        Assert.Equal(0, _lab.Modifiche.Quante);

        _lab.Ripeti();

        Assert.Equal(quanti + 1, _lab.Sessione.File[fix.File].Record);
    }

    [Fact]
    public async Task SiAnnullaAncheLAnnullaDelPannello()
    {
        var fix = await IlFixBc404();
        Assert.True(_lab.CambiaCampo(fix.File, fix.Record, "Position", "N041.00.00.000 E012.00.00.000"));
        string cambiata = Posizione(fix);

        _lab.AnnullaModifica(_lab.Modifiche.Voci.Single());
        Assert.Equal(0, _lab.Modifiche.Quante);

        _lab.Annulla();

        Assert.Equal(1, _lab.Modifiche.Quante);
        Assert.Equal(cambiata, Posizione(fix));
    }

    [Fact]
    public async Task IVerticiIncollatiSiAnnullano()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        int prima = _lab.VerticiDi(Settore, 0, "Vertices").Count;
        const string testo = "N041.00.00.000 E012.00.00.000\nN041.10.00.000 E012.10.00.000\nN041.00.00.000 E012.20.00.000";

        Assert.True(_lab.GestoSuiVertici(Settore, 0, "Vertices", GestoDeiVertici.Incolla, testo: testo, puntiPerGrado: 1));
        Assert.NotEqual(prima, _lab.VerticiDi(Settore, 0, "Vertices").Count);

        _lab.Annulla();

        Assert.Equal(prima, _lab.VerticiDi(Settore, 0, "Vertices").Count);
        Assert.Equal(0, _lab.Modifiche.Quante);
    }

    [Fact]
    public async Task UnGestoCambiaLaVersioneDelSuoStrato()
    {
        // La mappa riprende uno strato quando la SUA versione cambia (prima un numero solo: un gesto su due strati
        // — le copie gemelle — ne faceva riprendere solo l'ultimo).
        var fix = await IlFixBc404();
        int punti = _lab.VersioneDelloStrato("punti");
        int settori = _lab.VersioneDelloStrato("settori");

        Assert.True(_lab.CambiaCampo(fix.File, fix.Record, "Position", "N041.00.00.000 E012.00.00.000"));

        Assert.True(_lab.VersioneDelloStrato("punti") > punti);
        Assert.Equal(settori, _lab.VersioneDelloStrato("settori"));
    }

    [Fact]
    public async Task RiaprireLaCartellaScordaLaStoria()
    {
        var fix = await IlFixBc404();
        Assert.True(_lab.CambiaCampo(fix.File, fix.Record, "Position", "N041.00.00.000 E012.00.00.000"));

        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));

        Assert.False(_lab.SiPuoAnnullare);
        Assert.False(_lab.SiPuoRipetere);
    }
}
