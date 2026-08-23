using System.Collections.Generic;
using Vipi.Application.Aor;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Colori di default per tipo-ente (suffisso callsign) + risoluzione con override manuale.</summary>
public class AorColorSchemeTests
{
    [Theory]
    [InlineData("LIRP_TWR", "TWR")]
    [InlineData("LIRR_NE_CTR", "CTR")]
    [InlineData("LIRP_US0_APP", "APP")]
    [InlineData("LIML_GND", "GND")]
    [InlineData("bare", "BARE")]
    [InlineData("", "")]
    public void SuffixOf_Takes_Last_Token(string callsign, string expected) =>
        Assert.Equal(expected, AorColorScheme.SuffixOf(callsign));

    [Fact]
    public void DefaultForCallsign_Maps_Type_To_Color()
    {
        Assert.Equal(AorColorScheme.Defaults["TWR"], AorColorScheme.DefaultForCallsign("LIRP_TWR"));
        Assert.Equal(AorColorScheme.Defaults["CTR"], AorColorScheme.DefaultForCallsign("LIRR_NE_CTR"));
        // Tutte le torri condividono lo stesso colore, a prescindere dall'aeroporto.
        Assert.Equal(AorColorScheme.DefaultForCallsign("LIRF_TWR"), AorColorScheme.DefaultForCallsign("LIML_TWR"));
    }

    [Fact]
    public void DefaultForCallsign_Unknown_Suffix_Falls_Back()
    {
        Assert.Equal(AorColorScheme.Fallback, AorColorScheme.DefaultForCallsign("SOMETHING_WEIRD"));
    }

    [Fact]
    public void Resolve_Prefers_Override_Then_Default()
    {
        var overrides = new Dictionary<string, string> { ["LIRP_TWR"] = "#abcdef" };
        Assert.Equal("#abcdef", AorColorScheme.Resolve("LIRP_TWR", overrides));                 // override
        Assert.Equal(AorColorScheme.Defaults["APP"], AorColorScheme.Resolve("LIRP_APP", overrides)); // default
        Assert.Equal(AorColorScheme.Defaults["APP"], AorColorScheme.Resolve("LIRP_APP", null));      // no overrides
    }

    [Fact]
    public void Resolve_Ignores_Blank_Override()
    {
        var overrides = new Dictionary<string, string> { ["LIRP_TWR"] = "  " };
        Assert.Equal(AorColorScheme.Defaults["TWR"], AorColorScheme.Resolve("LIRP_TWR", overrides));
    }

    /// <summary>
    /// L'override che non e' un colore si scarta. Il commento di <c>Resolve</c> prometteva «sse presente e
    /// valido» dal primo giorno, ma il corpo restituiva la stringa verbatim — e quella stringa finisce
    /// dentro un <c>style="background:…"</c> e nel <c>fill</c> di un SVG, dove un <c>;</c> apre una
    /// dichiarazione CSS in piu'. L'unica sorgente di oggi (un <c>&lt;input type="color"&gt;</c>) e' vincolata
    /// dal browser; un import o una riga corretta a mano nel DB no.
    /// </summary>
    [Theory]
    [InlineData("red")]                              // nome CSS: non lo accettiamo, non ci arriva nessuno
    [InlineData("rgb(255,0,0)")]                     // funzione: idem
    [InlineData("#ab")]                              // troppo corto
    [InlineData("#abcdefgh")]                        // cifre non esadecimali
    [InlineData("abcdef")]                           // manca il cancelletto
    [InlineData("#0d2c99;background-image:url(x)")]  // il caso vero: una seconda dichiarazione in coda
    public void Resolve_Scarta_Un_Override_Che_Non_E_Un_Colore(string sporco)
    {
        var overrides = new Dictionary<string, string> { ["LIRP_TWR"] = sporco };
        Assert.Equal(AorColorScheme.Defaults["TWR"], AorColorScheme.Resolve("LIRP_TWR", overrides));
    }

    [Theory]
    [InlineData("#abc")]
    [InlineData("#abcdef")]
    [InlineData("#ABCDEF")]
    [InlineData("#abcd")]        // con alfa, forma corta
    [InlineData("#abcdef80")]    // con alfa, forma lunga
    [InlineData("  #abcdef  ")]  // spazi attorno: il valore e' buono
    public void Resolve_Accetta_Gli_Esadecimali_Veri(string pulito)
    {
        var overrides = new Dictionary<string, string> { ["LIRP_TWR"] = pulito };
        Assert.Equal(pulito, AorColorScheme.Resolve("LIRP_TWR", overrides));
    }
}
