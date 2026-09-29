using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Tests.Ui;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ispezione;

/// <summary>
/// Lotto «Subito» slice 11c («file per file» M5): i messaggi CPDLC e i nomi dei gruppi hanno la loro scheda; risposta e
/// gruppo si scelgono da un elenco, i valori di uLink si leggono e restano come sono.
/// </summary>
public sealed class CpdlcNelLabTests : IDisposable
{
    private const string Messaggi = "SectorFiles/Include/IT/OTHER/ita.cpdlc";
    private const string Nomi = "SectorFiles/Include/IT/OTHER/ita.cpdlcnames";

    private readonly AlberoDiProva _albero = new();
    private readonly SessioneDelLab _lab;

    public CpdlcNelLabTests()
    {
        _albero.Scrivi(Messaggi, "//REVISED PHRASEOLOGY\r\nCLIMB TO [0];WU;0;[0];;;;1;0;0;0;1;0;0;0;1;\r\n");
        _albero.Scrivi(Nomi, "//TEST\r\nGROUP.15;TWR;");
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
    }

    public void Dispose() => _albero.Dispose();

    [Fact]
    public async Task IlMessaggioHaLaSuaSchedaERispostaEGruppoSiScelgono()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.Equal(["CLIMB TO [0]"], _lab.EtichetteDi(Messaggi));

        var scheda = Ispettore.Scheda(_lab.Sessione!.File[Messaggi], 0, null)!;
        Assert.Equal("Messaggio CPDLC", scheda.NomeDelTipo);
        Assert.Equal(Editor.TipoFisso, scheda.Campi.Single(c => c.Nome == "Risposta").Descrizione!.Editor);
        Assert.Contains(scheda.Campi.Single(c => c.Nome == "Gruppo").Descrizione!.Valori, v => v.Voce == "20 · DCL");
        Assert.DoesNotContain(scheda.Campi, c => c.Sconosciuto);

        Assert.True(_lab.CambiaCampo(Messaggi, 0, "Risposta", "R"));
        Assert.Equal("CLIMB TO [0];R;0;[0];;;;1;0;0;0;1;0;0;0;1;", _lab.RigheDiAdesso(Messaggi)[1]);

        Assert.Equal(["TWR"], _lab.EtichetteDi(Nomi));
        Assert.True(_lab.CambiaCampo(Nomi, 0, "Nome", "TORRE"));
        Assert.Equal("GROUP.15;TORRE;", _lab.RigheDiAdesso(Nomi)[1]);
    }
}
