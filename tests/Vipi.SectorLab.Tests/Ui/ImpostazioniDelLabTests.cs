using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Ui.Servizi;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Le impostazioni dell'app (lotto «Subito» slice 13f): il modello dei nomi delle configurazioni si cambia dall'app, si
/// ricorda nei dati del Lab e gli avvisi lo seguono (committente, 5 ottobre: «così se cambia non devo ricorrere al codice»).
/// </summary>
public sealed class ImpostazioniDelLabTests : IDisposable
{
    private const string Bassa = "SectorFiles/Include/IT/LOW_AIRSPACE/prova13_tma.lartcc";

    private readonly AlberoDiProva _albero = new();

    public ImpostazioniDelLabTests()
        => _albero.Scrivi(Bassa, "T;ZZ CNF1;N041.00.00.000;E012.00.00.000;\r\nT;ZZ CNF1;N041.10.00.000;E012.00.00.000;\r\n"
                                 + "T;ZZ CNF1;N041.10.00.000;E012.10.00.000;\r\n");

    public void Dispose() => _albero.Dispose();

    private string Dati => Path.Combine(_albero.Radice, "dati-del-lab");

    private static List<string> Avvisi(SessioneDelLab lab)
        => [.. lab.ProblemiDellAlbero.Where(p => p.Problema.Regola == Regola.NomeDellaConfigurazione && p.File == Bassa).Select(p => p.Problema.Dettaglio)];

    [Fact]
    public async Task IlModelloSiCambiaSiRicordaEGliAvvisiLoSeguono()
    {
        var lab = new SessioneDelLab(Dati);
        Assert.Equal(NomiDelleConfigurazioni.DiBase, lab.ModelloDelleConfigurazioni);
        Assert.True(await lab.ApriEValidaAsync(_albero.Radice));
        Assert.Contains("«ZZ CONF1»", Assert.Single(Avvisi(lab)), StringComparison.Ordinal);

        // Col modello che il file usa già, l'avviso sparisce: senza toccare il codice.
        Assert.True(lab.CambiaIlModelloDelleConfigurazioni("{acc} cnf{n}"));
        await lab.Validazione;
        Assert.Equal("{ACC} CNF{N}", lab.ModelloDelleConfigurazioni);
        Assert.Empty(Avvisi(lab));

        // Un'altra sessione (un altro avvio) lo ritrova.
        Assert.Equal("{ACC} CNF{N}", new SessioneDelLab(Dati).ModelloDelleConfigurazioni);
    }

    [Fact]
    public void UnModelloCheNonVaSiRifiutaColPercheEVuotoTornaQuelloDiBase()
    {
        var lab = new SessioneDelLab(Dati);

        Assert.False(lab.CambiaIlModelloDelleConfigurazioni("CONF{N}"));
        Assert.Contains("{ACC}", lab.Rifiuto, StringComparison.Ordinal);
        Assert.Equal(NomiDelleConfigurazioni.DiBase, lab.ModelloDelleConfigurazioni);

        Assert.True(lab.CambiaIlModelloDelleConfigurazioni("{ACC} CFG {N}"));
        Assert.True(lab.CambiaIlModelloDelleConfigurazioni("  "));
        Assert.Equal(NomiDelleConfigurazioni.DiBase, new SessioneDelLab(Dati).ModelloDelleConfigurazioni);
    }
}
