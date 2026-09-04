using Vipi.Application.Abstractions;
using Vipi.Application.Import;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Quando una cella porta uno scalo che <b>non abbiamo in archivio</b> — gli alternati esteri, LGKR o LDDU —
/// il risolutore va a chiederlo alla sorgente. Ogni domanda è una chiamata di rete, e il tetto
/// <see cref="RisolutoreCelle.MaxAllaSorgente"/> esiste per non farne una per riga.
///
/// <para>⚠️ Il conto si tiene <b>per CODICE, non per cella</b>. «LGKR» e «LGKR Kerkyra» sono due celle
/// diverse con lo stesso scalo: chiedere due volte spendeva due chiamate di rete e <b>due colpi del tetto</b>
/// — su una tabella di alternati, dove lo stesso campo compare più volte, si arrivava a «troppi scali da
/// verificare» avendone verificati molti meno di venticinque.</para>
/// </summary>
public class RisolutoreScaliAllaSorgenteTests
{
    /// <summary>Archivio vuoto: così ogni codice finisce alla sorgente, che è il caso da misurare.</summary>
    private sealed class CatalogoFinto : IAirportNameLookup
    {
        public List<string> Chiesti { get; } = new();

        public Task<IReadOnlyDictionary<string, string>> NamesAsync(
            IReadOnlyList<string> icaos, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

        public Task<AirportName?> FindAsync(string icao, CancellationToken ct = default)
        {
            Chiesti.Add(icao);
            return Task.FromResult<AirportName?>(new AirportName(icao, "Nome di " + icao, InArchivio: false));
        }

        public Task<IReadOnlyList<AirportName>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AirportName>>(Array.Empty<AirportName>());
    }

    /// <summary>⚠️ L'anagrafica delle radioassistenze si passa nulla di proposito: la strada degli scali non
    /// la tocca, e montarne una finta vorrebbe dire implementare quattordici metodi che il test non usa —
    /// una finta che invecchia a ogni firma nuova, per niente.</summary>
    private static (RisolutoreCelle Risolutore, CatalogoFinto Catalogo) Banco()
    {
        var catalogo = new CatalogoFinto();
        return (new RisolutoreCelle(catalogo, null!), catalogo);
    }

    /// <summary>⚠️ La prova che conta: lo stesso scalo scritto in tre modi costa <b>una</b> domanda.</summary>
    [Fact]
    public async Task Lo_stesso_scalo_scritto_in_piu_modi_si_chiede_una_volta_sola()
    {
        var (risolutore, catalogo) = Banco();

        var esiti = await risolutore.RisolviAsync(
            TipoCella.Aeroporto, new[] { "LGKR", "LGKR Kerkyra", "(LGKR)" });

        Assert.Equal(new[] { "LGKR" }, catalogo.Chiesti);
        Assert.Equal(3, esiti.Count);
        Assert.All(esiti.Values, e => Assert.Equal(EsitoCella.Risolta, e.Esito));
    }

    /// <summary>E scali diversi restano domande diverse: la deduplica non deve confondere due codici.</summary>
    [Fact]
    public async Task Scali_diversi_restano_domande_diverse()
    {
        var (risolutore, catalogo) = Banco();

        await risolutore.RisolviAsync(TipoCella.Aeroporto, new[] { "LGKR", "LDDU", "LGKR Kerkyra" });

        Assert.Equal(new[] { "LDDU", "LGKR" }, catalogo.Chiesti.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// Il tetto conta i <b>codici</b>. Con 25 scali distinti ripetuti due volte ciascuno, prima si
    /// spendevano 25 colpi sulla prima metà e la seconda restava fuori con «troppi scali da verificare»;
    /// ora passano tutti.
    /// </summary>
    [Fact]
    public async Task Il_tetto_conta_i_codici_non_le_celle()
    {
        var (risolutore, catalogo) = Banco();
        var codici = Enumerable.Range(0, RisolutoreCelle.MaxAllaSorgente)
            .Select(i => $"LX{i:00}").ToList();
        var celle = codici.Concat(codici.Select(c => c + " ripetuto")).ToList();

        var esiti = await risolutore.RisolviAsync(TipoCella.Aeroporto, celle);

        Assert.Equal(RisolutoreCelle.MaxAllaSorgente, catalogo.Chiesti.Count);
        Assert.All(esiti.Values, e => Assert.Equal(EsitoCella.Risolta, e.Esito));
    }

    /// <summary>⚠️ Ma il tetto resta un tetto: oltre i 25 codici distinti, gli altri restano fuori — e con
    /// scritto perché, non in silenzio.</summary>
    [Fact]
    public async Task Oltre_il_tetto_gli_altri_restano_fuori_e_lo_dicono()
    {
        var (risolutore, catalogo) = Banco();
        var celle = Enumerable.Range(0, RisolutoreCelle.MaxAllaSorgente + 5)
            .Select(i => $"LX{i:00}").ToList();

        var esiti = await risolutore.RisolviAsync(TipoCella.Aeroporto, celle);

        Assert.Equal(RisolutoreCelle.MaxAllaSorgente, catalogo.Chiesti.Count);
        var fuori = esiti.Values.Where(e => e.Esito == EsitoCella.NonLetta).ToList();
        Assert.Equal(5, fuori.Count);
        Assert.All(fuori, e => Assert.Equal("troppi scali da verificare", e.Nota));
    }
}
