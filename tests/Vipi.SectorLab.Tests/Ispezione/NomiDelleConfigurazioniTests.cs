using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Tests.Ispezione;

/// <summary>
/// I nomi delle configurazioni (lotto «Subito» slice 13f, K1): la forma giusta è un modello che l'AOD sceglie nelle
/// impostazioni dell'app (committente, 5 ottobre), e l'avviso dice il nome com'è e come dovrebbe essere.
/// </summary>
public sealed class NomiDelleConfigurazioniTests : IDisposable
{
    private const string Alta = "SectorFiles/Include/IT/HI_AIRSPACE/prova.hartcc";
    private const string Bassa = "SectorFiles/Include/IT/LOW_AIRSPACE/prova_tma.lartcc";

    private static readonly string[] Quadrato =
        ["N041.00.00.000;E012.00.00.000;", "N041.10.00.000;E012.00.00.000;", "N041.10.00.000;E012.10.00.000;", "N041.00.00.000;E012.10.00.000;"];

    private readonly AlberoDiProva _albero = new();

    public void Dispose() => _albero.Dispose();

    private void Scrivi(string file, params string[] nomi)
        => _albero.Scrivi(file, string.Join("\r\n\r\n", nomi.Select(n => string.Join("\r\n", Quadrato.Select(v => $"T;{n};{v}")))) + "\r\n");

    [Theory]
    [InlineData("RR CONF1", "RR", "CONF", "1", "")]
    [InlineData("RR CONF1M", "RR", "CONF", "1", "M")]
    [InlineData("RR CNF2.1", "RR", "CNF", "2.1", "")]
    [InlineData("MM CONF 2.2", "MM", "CONF", "2.2", "")]
    [InlineData("mm conf 3", "mm", "CONF", "3", "")]
    public void UnaConfigurazioneSiLegge(string nome, string acc, string parola, string numero, string coda)
        => Assert.Equal(new NomeDiConfigurazione(acc, parola, numero, coda), NomiDelleConfigurazioni.Leggi(nome));

    [Theory]
    [InlineData("RR NE")]
    [InlineData("RR CONF")]
    [InlineData("LIRF 16")]
    [InlineData("LIBB CS0")]
    [InlineData("COOR EDUU")]
    [InlineData("DUMMY")]
    public void UnAltroNomeNonEUnaConfigurazione(string nome) => Assert.Null(NomiDelleConfigurazioni.Leggi(nome));

    [Theory]
    [InlineData("{ACC} CONF{N}", "RR CNF2.1", "RR CONF2.1")]
    [InlineData("{ACC} CONF{N}", "MM CONF 3", "MM CONF3")]
    [InlineData("{ACC} CONF{N}", "RR CONF1M", "RR CONF1M")]
    [InlineData("{ACC} CNF {N}", "RR CONF1", "RR CNF 1")]
    [InlineData("{acc} cfg{n}", "MM CONF 2.2", "MM CFG2.2")]
    public void IlModelloScriveIlNome(string modello, string nome, string atteso)
        => Assert.Equal(atteso, NomiDelleConfigurazioni.Scrivi(NomiDelleConfigurazioni.Leggi(nome, modello)!, modello));

    [Theory]
    [InlineData("CONF{N}")]
    [InlineData("{ACC} CONF")]
    [InlineData("{ACC}{N}")]
    [InlineData("{ACC}CONF{N}")]
    [InlineData("{ACC} CONF-{N}")]
    [InlineData("{ACC} CONF{N} X")]
    [InlineData("")]
    public void UnModelloSenzaSiglaParolaENumeroSiRifiuta(string modello)
    {
        Assert.NotNull(NomiDelleConfigurazioni.PercheNonVa(modello));
        Assert.Equal(NomiDelleConfigurazioni.DiBase, NomiDelleConfigurazioni.Pulito(modello));
    }

    // L'albero di prova ha anche i campioni veri (lirr_tma.lartcc): si guardano i due file scritti qui.
    private static List<ProblemaDelSector> Miei(IEnumerable<ProblemaDelSector> problemi)
        => [.. problemi.Where(p => p.File is Alta or Bassa)];

    [Fact]
    public void LAvvisoSeguIlModelloScelto()
    {
        Scrivi(Alta, "RR CONF1", "RR CONF1M", "RR NE");
        Scrivi(Bassa, "RR CNF1", "MM CONF 2.1");
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);

        var diBase = Miei(NomiDelleConfigurazioni.Problemi(sessione, NomiDelleConfigurazioni.DiBase));
        Assert.Equal([(Bassa, 1), (Bassa, 6)], diBase.Select(p => (p.File, p.Riga)));
        Assert.All(diBase, p => Assert.Equal(Regola.NomeDellaConfigurazione, p.Regola));
        Assert.All(diBase, p => Assert.Equal(Gravita.Avviso, p.Gravita));
        Assert.Contains("«RR CNF1»", diBase[0].Dettaglio, StringComparison.Ordinal);
        Assert.Contains("«RR CONF1»", diBase[0].Dettaglio, StringComparison.Ordinal);
        Assert.Contains("«MM CONF2.1»", diBase[1].Dettaglio, StringComparison.Ordinal);

        // Cambiato il modello, gli stessi file danno altri avvisi: niente è scritto nel codice.
        var conCnf = Miei(NomiDelleConfigurazioni.Problemi(sessione, "{ACC} CNF{N}"));
        Assert.Equal([(Alta, 1), (Alta, 6), (Bassa, 6)], conCnf.Select(p => (p.File, p.Riga)));
        Assert.Contains("«RR CNF1M»", conCnf[1].Dettaglio, StringComparison.Ordinal);
    }
}
