using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// «La sorgente lo mette sotto un altro centro.» L'assegnazione degli aeroporti è <b>additiva</b> — l'ACC di
/// uno già in archivio non lo tocca nessuno — quindi una riassegnazione fatta da IVAO non produce nessun
/// effetto e, senza questo confronto, nessuna traccia: resterebbe muta per sempre.
///
/// <para>Il confronto sta in un posto solo: lo fa il giro notturno (che ne scrive una riga nel registro) e lo
/// fa la pagina di gestione aeroporti (che lo mostra a chi entra).</para>
/// </summary>
public class AirportAccDivergencesTests
{
    private static SourceAirport Fonte(string icao, string? acc) => new(icao, icao + " Airport", acc, City: null);

    [Fact]
    public void Dice_chi_sta_sotto_un_centro_diverso()
    {
        var nostri = new[] { ("LIBD", "Bari", "LIBB"), ("LIRF", "Fiumicino", "LIRR") };

        var d = AirportAccDivergences.Trova(nostri, new[] { Fonte("LIBD", "LIRR"), Fonte("LIRF", "LIRR") });

        var uno = Assert.Single(d);
        Assert.Equal("LIBD", uno.Icao);
        Assert.Equal("LIBB", uno.Nostro);
        Assert.Equal("LIRR", uno.Sorgente);
        Assert.Equal("Bari", uno.Nome);          // il nome è NOSTRO: è quello che si legge negli elenchi
    }

    [Fact]
    public void Il_confronto_non_bada_alle_maiuscole()
    {
        var d = AirportAccDivergences.Trova(new[] { ("LIBD", "Bari", "LIBB") }, new[] { Fonte("libd", "libb") });

        Assert.Empty(d);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_ACC_assente_nella_sorgente_non_e_un_disaccordo(string? accSorgente)
    {
        // Un dato che non c'è non dice «sta altrove»: trattarlo da divergenza riempirebbe la pagina di
        // segnalazioni su scali di cui non si sa niente.
        Assert.Empty(AirportAccDivergences.Trova(new[] { ("LIBD", "Bari", "LIBB") }, new[] { Fonte("LIBD", accSorgente) }));
    }

    [Fact]
    public void Un_aeroporto_che_la_sorgente_non_nomina_si_lascia_stare()
    {
        // Fuori dal paese configurato, o tolto dall'anagrafica: non si sa dove lo metterebbe, quindi si tace.
        Assert.Empty(AirportAccDivergences.Trova(new[] { ("LIBD", "Bari", "LIBB") }, new[] { Fonte("LIRF", "LIRR") }));
    }

    [Fact]
    public void L_elenco_esce_in_ordine_di_ICAO()
    {
        var nostri = new[] { ("LIRN", "Napoli", "LIRR"), ("LIBD", "Bari", "LIBB"), ("LIMC", "Malpensa", "LIMM") };
        var fonte = new[] { Fonte("LIRN", "LIBB"), Fonte("LIBD", "LIRR"), Fonte("LIMC", "LIPP") };

        Assert.Equal(new[] { "LIBD", "LIMC", "LIRN" }, AirportAccDivergences.Trova(nostri, fonte).Select(x => x.Icao));
    }
}
