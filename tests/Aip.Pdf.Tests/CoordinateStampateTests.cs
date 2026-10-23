using Aip.Pdf.Italia;

namespace Aip.Pdf.Tests;

/// <summary>
/// Segnalazioni 2611, revisione 5 (committente, 24 ottobre): le coordinate dei punti dell'AD 2 stampate male, coi testi del
/// 2611, si leggono e si dice come.
/// </summary>
public sealed class CoordinateStampateTests
{
    [Theory]
    [InlineData("36°40’23.6”N 011°42’07. 6”E", "36°40'23.6\"N 011°42'07.6\"E")]                 // CG753: lo spazio nei decimali
    [InlineData("38°13°23.05”N 013°07’03.72”E", "38°13'23.05\"N 013°07'03.72\"E")]              // CJ516: «°» al posto di «’»
    [InlineData("38°07’20.79” 015°33’31.70”E", "38°07'20.79\"N 015°33'31.70\"E")]               // CR401: senza N
    [InlineData("38°14’14.79”N 015°42’’4.55”E", "38°14'14.79\"N 015°42'04.55\"E")]              // CR402: «’’»
    [InlineData("45°44’42.3” 3N 008°42’06.13” E", "45°44'42.33\"N 008°42'06.13\"E")]            // MC722: la cifra dopo le virgolette
    [InlineData("45°39’43.72” N 008°35’:28.77” E", "45°39'43.72\"N 008°35'28.77\"E")]           // MC724: «’:»
    [InlineData("45°31’40.95” N 008°45’42.02”", "45°31'40.95\"N 008°45'42.02\"E")]              // MC906: senza E
    [InlineData("44°02’26.2” N E 008°54’48.0” E", "44°02'26.2\"N 008°54'48.0\"E")]               // MJ601: una E di troppo
    [InlineData("44°33'40.7'' N 011°12'60.0'' E", "44°33'40.7\"N 011°13'00.0\"E")]              // PE502: 60 secondi
    [InlineData("41°52’'19.26” N 012°32’40.72” E", "41°52'19.26\"N 012°32'40.72\"E")]            // RA761: «’'»
    [InlineData("42°32’38”6N 011°31’00”3E", "42°32'38.6\"N 011°31'00.3\"E")]                     // RF407: il punto dopo le virgolette
    [InlineData("40°32’01.0359”N 013:51:24.4115”E", "40°32'01.0359\"N 013°51'24.4115\"E")]       // RN534: i due punti
    [InlineData("40°56’21.13”N 014°024’13.45”E", "40°56'21.13\"N 014°24'13.45\"E")]              // FAF LIRN: «024’»
    public void LeCoordinateStampateMale_SiLeggono(string stampata, string letta)
    {
        var esito = ItalianCoordinateStampate.Leggi(stampata);

        Assert.NotNull(esito);
        Assert.Equal(letta, esito.Value.Letto);
    }

    [Fact]
    public void Mc722_ComeNellAltraPagina()
    {
        // MC722 stampato giusto altrove nel 2611: 45.745091666, 8.701702777.
        var p = ItalianCoordinateStampate.Leggi("45°44’42.3” 3N 008°42’06.13” E")!.Value.Posizione;

        Assert.Equal(45.7450917, p.Lat, 6);
        Assert.Equal(8.7017028, p.Lon, 6);
    }

    [Theory]
    [InlineData("45°44’42.3”N")]                          // una coordinata sola
    [InlineData("12°44’42.3”N 008°42’06.13”E")]           // fuori dall'Italia
    [InlineData("45°74’42.3”N 008°42’06.13”E")]           // 74 primi
    public void SeNonTorna_NonSiLegge(string stampata) => Assert.Null(ItalianCoordinateStampate.Leggi(stampata));
}
