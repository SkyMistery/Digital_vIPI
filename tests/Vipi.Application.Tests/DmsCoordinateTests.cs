using Vipi.Application.Coordinates;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il DMS Aurora nei due versi. La lettura è quella che stava in <c>AuroraSectorfileParser</c> (i cui test
/// restano dove sono e provano la delega); la scrittura nasce col convertitore di coordinate.
/// </summary>
public class DmsCoordinateTests
{
    // Tolleranza: mezzo millisecondo d'arco, che è la risoluzione del formato.
    private const double Mezzo = 0.5 / 3_600_000.0;

    [Theory]
    [InlineData("N041.37.28.965", 41.62471250)]
    [InlineData("E015.43.18.960", 15.72193333)]
    [InlineData("S041.37.28.965", -41.62471250)]
    [InlineData("W015.43.18.960", -15.72193333)]
    [InlineData("N0413728965", 41.62471250)]     // forma compatta
    [InlineData("E0154318960", 15.72193333)]
    [InlineData("N042.00.28.000", 42.00777778)]  // l'esempio del committente (R14A, primo vertice)
    [InlineData("E011.58.06.000", 11.96833333)]
    public void Legge_Le_Due_Forme(string token, double atteso)
    {
        Assert.True(DmsCoordinate.TryParse(token, out var gradi));
        Assert.Equal(atteso, gradi, 6);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("X041.37.28.965")]   // emisfero inesistente
    [InlineData("41.37.28.965")]     // emisfero assente
    [InlineData("N041.37")]          // pezzi insufficienti
    [InlineData("N04137")]           // compatta troppo corta
    [InlineData("N041372896X")]      // compatta non numerica
    public void Rifiuta_Il_Malformato(string? token) =>
        Assert.False(DmsCoordinate.TryParse(token, out _));

    [Theory]
    [InlineData(42.00777778, true, "N042.00.28.000")]
    [InlineData(11.96833333, false, "E011.58.06.000")]
    [InlineData(-42.00777778, true, "S042.00.28.000")]
    [InlineData(-11.96833333, false, "W011.58.06.000")]
    [InlineData(41.975, true, "N041.58.30.000")]
    [InlineData(11.92, false, "E011.55.12.000")]
    public void Scrive_La_Forma_Puntata(double gradi, bool latitudine, string atteso) =>
        Assert.Equal(atteso, DmsCoordinate.Format(gradi, latitudine));

    [Theory]
    [InlineData(41.62471250, true, "N0413728965")]
    [InlineData(15.72193333, false, "E0154318960")]
    public void Scrive_La_Forma_Compatta(double gradi, bool latitudine, string atteso) =>
        Assert.Equal(atteso, DmsCoordinate.Format(gradi, latitudine, DmsCoordinate.Forma.Compatta));

    [Fact]
    public void I_Gradi_Sono_Sempre_Tre_Cifre_Anche_In_Latitudine()
    {
        // I file veri scrivono N042, non N42: chi rilegge conta i caratteri.
        Assert.Equal("N005.00.00.000", DmsCoordinate.Format(5.0, isLatitudine: true));
        Assert.Equal("E005.00.00.000", DmsCoordinate.Format(5.0, isLatitudine: false));
    }

    [Fact]
    public void L_Arrotondamento_Riporta_Su_Primi_E_Gradi()
    {
        // ⚠️ 41.9999999° = 41°59'59.99964": arrotondando i millisecondi si sfora il minuto, e senza riporto
        // uscirebbe "N041.59.60.000", che è un DMS inesistente. Deve diventare il grado successivo.
        Assert.Equal("N042.00.00.000", DmsCoordinate.Format(41.9999999, isLatitudine: true));

        // Stesso caso un passo prima: sfora solo il secondo.
        Assert.Equal("N041.58.30.000", DmsCoordinate.Format(41.9749999999, isLatitudine: true));
    }

    [Fact]
    public void Lo_Zero_E_Nord_Ed_Est()
    {
        // Il segno decide l'emisfero, e lo zero non è negativo: N/E, non S/W.
        Assert.Equal("N000.00.00.000", DmsCoordinate.Format(0.0, isLatitudine: true));
        Assert.Equal("E000.00.00.000", DmsCoordinate.Format(0.0, isLatitudine: false));
    }

    [Theory]
    [InlineData(42.00777778)]
    [InlineData(-11.96833333)]
    [InlineData(0.0)]
    [InlineData(89.99999)]
    public void Andata_E_Ritorno_Entro_Mezzo_Millisecondo(double gradi)
    {
        var token = DmsCoordinate.Format(gradi, isLatitudine: true);
        Assert.True(DmsCoordinate.TryParse(token, out var tornato));
        Assert.True(Math.Abs(gradi - tornato) <= Mezzo, $"{gradi} → {token} → {tornato}");
    }

    [Theory]
    [InlineData(42.00777778)]
    [InlineData(-11.96833333)]
    public void Anche_La_Forma_Compatta_Torna_Indietro(double gradi)
    {
        var token = DmsCoordinate.Format(gradi, isLatitudine: false, DmsCoordinate.Forma.Compatta);
        Assert.True(DmsCoordinate.TryParse(token, out var tornato));
        Assert.True(Math.Abs(gradi - tornato) <= Mezzo, $"{gradi} → {token} → {tornato}");
    }
}
