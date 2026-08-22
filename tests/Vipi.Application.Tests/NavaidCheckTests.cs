using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il controllo dei punti scritti a mano. Metà di questi test difendono i casi in cui il controllo deve
/// <b>tacere</b>: un avviso che grida su dati corretti si impara a ignorare, e allora non serve più nemmeno
/// quando ha ragione.
/// </summary>
public class NavaidCheckTests
{
    private static readonly NavaidCatalog Catalog = new(new[]
    {
        new NavaidName("BEKIV", NavaidKind.Fix),
        new NavaidName("TOPNO", NavaidKind.Fix),
        new NavaidName("VALMA", NavaidKind.Fix),
        new NavaidName("ELB", NavaidKind.Vor),
    });

    [Theory]
    [InlineData("BESIV")]            // il typo vero trovato in archivio: BEKIV con una lettera sbagliata
    [InlineData("VALMAA")]
    public void Un_nome_che_somiglia_a_un_punto_e_non_esiste_viene_segnalato(string token)
    {
        // VALMAA ha 6 lettere: fuori forma, quindi NON verificabile. Il caso resta qui per dire che la forma
        // conta prima dell'esistenza — e infatti solo il primo dei due si segnala.
        if (token.Length <= 5) Assert.True(NavaidCheck.IsUnknown(token, Catalog));
        else Assert.False(NavaidCheck.IsUnknown(token, Catalog));
    }

    [Theory]
    [InlineData("BEKIV")]
    [InlineData("bekiv")]            // il confronto ignora le maiuscole: la grafia è cosmesi, l'esistenza no
    [InlineData("ELB")]
    public void Un_punto_del_catalogo_non_si_segnala(string token) =>
        Assert.False(NavaidCheck.IsUnknown(token, Catalog));

    [Theory]
    [InlineData("Y01-Y12")]          // intervallo di aerovie
    [InlineData("TOPNO 3A")]         // STAR
    [InlineData("ALL")]              // il quantificatore dei sorvoli
    [InlineData("ALL to GR")]
    [InlineData("A")]                // troppo corto per essere un nome
    [InlineData("")]
    [InlineData(null)]
    public void Cio_che_non_e_un_nome_di_punto_non_si_giudica(string? token)
    {
        Assert.False(NavaidCheck.IsCheckable(token));
        Assert.False(NavaidCheck.IsUnknown(token, Catalog));
    }

    [Fact]
    public void Con_la_sorgente_muta_non_e_sconosciuto_niente()
    {
        // GitHub irraggiungibile => catalogo vuoto. Segnare tutto trasformerebbe un disservizio della sorgente
        // in una pagina intera di avvisi falsi.
        Assert.False(NavaidCheck.IsUnknown("QUALSIASI", NavaidCatalog.Empty));
        Assert.False(NavaidCheck.IsUnknown("BESIV", NavaidCatalog.Empty));
        Assert.False(NavaidCheck.IsUnknown("BESIV", null));
        Assert.Empty(NavaidCheck.UnknownCops("BESIV, ZZZZZ", NavaidCatalog.Empty));
    }

    [Fact]
    public void In_un_elenco_si_segnalano_solo_i_token_sbagliati()
    {
        var unknown = NavaidCheck.UnknownCops("VALMA, BESIV, ELB, Y01-Y12, ALL", Catalog);
        Assert.Equal(new[] { "BESIV" }, unknown);
    }

    [Fact]
    public void Un_elenco_ripete_un_errore_una_volta_sola()
    {
        // L'avviso nomina i punti: ripetere due volte lo stesso nome allunga la riga senza aggiungere niente.
        Assert.Equal(new[] { "BESIV" }, NavaidCheck.UnknownCops("BESIV, VALMA, BESIV", Catalog));
    }

    [Fact]
    public void Un_elenco_tutto_giusto_non_dice_niente()
    {
        Assert.Empty(NavaidCheck.UnknownCops("VALMA, TOPNO, ELB", Catalog));
        Assert.Empty(NavaidCheck.UnknownCops("", Catalog));
        Assert.Empty(NavaidCheck.UnknownCops(null, Catalog));
    }
}
