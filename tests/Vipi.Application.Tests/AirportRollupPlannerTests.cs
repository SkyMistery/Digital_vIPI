using Vipi.Application.Stats;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Che cosa si chiede alla sorgente, e in che ordine.
///
/// <para>⚠️ I due casi che costano soldi veri: il <b>raggruppamento in blocchi</b> (trenta volte meno
/// chiamate dello stesso dato) e l'ordine <b>globale</b> (senza, il tetto per giro si esaurisce sui primi
/// aeroporti in ordine alfabetico e i grandi non arrivano mai).</para>
/// </summary>
public class AirportRollupPlannerTests
{
    private static readonly DateTimeOffset Adesso = new(2026, 8, 25, 3, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset G(int giorno) => new(2026, 8, giorno, 0, 0, 0, TimeSpan.Zero);

    private static KnownAirportDay Preso(string icao, int giorno, int presoIlGiorno, int ora = 12) =>
        new(icao, new DateTime(2026, 8, giorno), new DateTime(2026, 8, presoIlGiorno, ora, 0, 0));

    [Fact]
    public void Un_archivio_vuoto_chiede_tutto_a_blocchi()
    {
        var piano = AirportRollupPlanner.Plan(
            new[] { "LIRF" }, Array.Empty<KnownAirportDay>(),
            G(1), G(24), max: 10, Adesso, giorniPerBlocco: 10);

        // 24 giorni (1..24) in blocchi da dieci: 10 + 10 + 4.
        Assert.Equal(3, piano.Count);
        Assert.Equal(new[] { 10, 10, 4 }, piano.Select(b => b.Days).ToArray());
        Assert.Equal(G(25), piano[0].To);        // il più recente per primo
        Assert.Equal(G(15), piano[0].From);
    }

    [Fact]
    public void I_giorni_gia_definitivi_non_si_richiedono()
    {
        var conosciuti = Enumerable.Range(1, 20)
            .Select(g => Preso("LIRF", g, g + 1))       // presi il giorno dopo: assestati
            .ToList();

        var piano = AirportRollupPlanner.Plan(
            new[] { "LIRF" }, conosciuti, G(1), G(24), max: 10, Adesso, giorniPerBlocco: 30);

        Assert.Single(piano);
        Assert.Equal(G(21), piano[0].From);       // restano il 21, 22, 23 e 24
        Assert.Equal(G(25), piano[0].To);
    }

    /// <summary>
    /// ⚠️ Un giorno preso mentre era ancora in corso vale a metà: manca la sera, che è quando l'Italia vola.
    /// Si ripassa — ma una volta sola, quando il giorno si è assestato.
    /// </summary>
    [Fact]
    public void Un_giorno_preso_mentre_era_in_corso_si_ripassa_una_volta()
    {
        var mattina = new[] { Preso("LIRF", 20, 20, ora: 9) };

        var piano = AirportRollupPlanner.Plan(
            new[] { "LIRF" }, mattina, G(20), G(20), max: 5, Adesso, giorniPerBlocco: 30);
        Assert.Single(piano);

        // Ripassato dopo l'assestamento: non si tocca più.
        var ripassato = new[] { Preso("LIRF", 20, 22) };
        var dopo = AirportRollupPlanner.Plan(
            new[] { "LIRF" }, ripassato, G(20), G(20), max: 5, Adesso, giorniPerBlocco: 30);
        Assert.Empty(dopo);
    }

    /// <summary>
    /// ⚠️ Il giorno in corso si prende, ma non si rifà a ogni giro: altrimenti novantatré aeroporti
    /// mangerebbero il tetto ogni volta e l'arretrato non scenderebbe mai.
    /// </summary>
    [Fact]
    public void Il_giorno_in_corso_si_prende_una_volta_sola()
    {
        var oggi = new[] { Preso("LIRF", 25, 25, ora: 2) };

        var piano = AirportRollupPlanner.Plan(
            new[] { "LIRF" }, oggi, G(25), G(25), max: 5, Adesso, giorniPerBlocco: 30);

        Assert.Empty(piano);
    }

    /// <summary>
    /// ⚠️ L'ordine è globale. Con il tetto a due e tre aeroporti tutti scoperti, devono uscire i due blocchi
    /// PIÙ RECENTI — uno per aeroporto — non i due blocchi del primo aeroporto in ordine alfabetico.
    /// </summary>
    [Fact]
    public void Il_tetto_si_spende_sui_giorni_piu_recenti_di_tutti()
    {
        var piano = AirportRollupPlanner.Plan(
            new[] { "LIBD", "LIML", "LIRF" }, Array.Empty<KnownAirportDay>(),
            G(1), G(24), max: 2, Adesso, giorniPerBlocco: 10);

        Assert.Equal(2, piano.Count);
        Assert.Equal(new[] { "LIBD", "LIML" }, piano.Select(b => b.Icao).ToArray());
        Assert.All(piano, b => Assert.Equal(G(25), b.To));      // tutti e due sul blocco più recente
    }

    [Fact]
    public void I_buchi_in_mezzo_diventano_blocchi_separati()
    {
        // Manca il 10 e manca il 20; il resto è a posto.
        var conosciuti = Enumerable.Range(1, 24)
            .Where(g => g != 10 && g != 20)
            .Select(g => Preso("LIRF", g, g + 1))
            .ToList();

        var piano = AirportRollupPlanner.Plan(
            new[] { "LIRF" }, conosciuti, G(1), G(24), max: 10, Adesso, giorniPerBlocco: 30);

        Assert.Equal(2, piano.Count);
        Assert.Equal(G(20), piano[0].From);
        Assert.Equal(G(10), piano[1].From);
        Assert.All(piano, b => Assert.Equal(1, b.Days));
    }

    [Fact]
    public void Senza_tetto_o_senza_aeroporti_non_si_chiede_niente()
    {
        Assert.Empty(AirportRollupPlanner.Plan(
            new[] { "LIRF" }, Array.Empty<KnownAirportDay>(), G(1), G(24), max: 0, Adesso));
        Assert.Empty(AirportRollupPlanner.Plan(
            Array.Empty<string>(), Array.Empty<KnownAirportDay>(), G(1), G(24), max: 10, Adesso));
    }
}
