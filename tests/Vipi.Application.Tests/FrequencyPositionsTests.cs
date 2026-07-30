using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Fissa il vocabolario condiviso delle posizioni-frequenza, prima triplicato nei repository di derivazione
/// (ACC, APP, aeroporto). Il caso che conta è <see cref="Posizione_Di_Soli_Spazi_Rende_Il_Trattino"/>: era
/// esattamente il punto su cui le tre copie erano divergute, con l'aeroporto che rendeva una cella bianca.
/// </summary>
public class FrequencyPositionsTests
{
    [Theory]
    [InlineData("ATIS", "ATIS")]
    [InlineData("DEL", "Delivery")]
    [InlineData("GND", "Ground")]
    [InlineData("TWR", "Tower")]
    [InlineData("APP", "Approach")]
    [InlineData("DEP", "Departure")]
    [InlineData("CTR", "Control")]
    [InlineData("FSS", "Information")]
    public void Nomi_Delle_Posizioni_Note(string position, string expected) =>
        Assert.Equal(expected, FrequencyPositions.NameOf(position));

    [Theory]
    [InlineData("twr")]
    [InlineData("  TWR  ")]
    [InlineData("Twr")]
    public void Il_Nome_Ignora_Case_E_Spazi(string position) =>
        Assert.Equal("Tower", FrequencyPositions.NameOf(position));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Posizione_Di_Soli_Spazi_Rende_Il_Trattino(string? position) =>
        Assert.Equal("—", FrequencyPositions.NameOf(position));

    [Fact]
    public void Una_Posizione_Ignota_Viene_Resa_Cosi_Come_E()
    {
        // Non riconosciuta ma valorizzata: si mostra il valore grezzo, non un trattino (l'informazione c'è).
        Assert.Equal("RADAR", FrequencyPositions.NameOf("RADAR"));
    }

    [Fact]
    public void Ordine_Di_Presentazione_Canonico()
    {
        var positions = new[] { "CTR", "TWR", "ATIS", "DEP", "GND", "APP", "DEL" };

        var sorted = positions.OrderBy(FrequencyPositions.OrderOf).ToArray();

        Assert.Equal(new[] { "ATIS", "DEL", "GND", "TWR", "APP", "DEP", "CTR" }, sorted);
    }

    [Fact]
    public void Le_Posizioni_Ignote_Vanno_In_Coda_Dopo_Il_Ctr()
    {
        Assert.Equal(99, FrequencyPositions.OrderOf("RADAR"));
        Assert.Equal(99, FrequencyPositions.OrderOf(null));
        Assert.True(FrequencyPositions.OrderOf("CTR") < FrequencyPositions.OrderOf("RADAR"));
    }

    [Theory]
    [InlineData(SectorType.Del, "DEL")]
    [InlineData(SectorType.Gnd, "GND")]
    [InlineData(SectorType.Twr, "TWR")]
    [InlineData(SectorType.ITwr, "TWR")]
    [InlineData(SectorType.App, "APP")]
    [InlineData(SectorType.Ctr, "CTR")]
    public void Sigla_Dal_Tipo_Di_Settore(SectorType type, string expected) =>
        Assert.Equal(expected, FrequencyPositions.FromSectorType(type));

    [Fact]
    public void Le_Due_Varianti_Di_Torre_Collassano_Su_Twr() =>
        Assert.Equal(FrequencyPositions.FromSectorType(SectorType.Twr),
                     FrequencyPositions.FromSectorType(SectorType.ITwr));

    [Fact]
    public void Ogni_Sigla_Da_Tipo_Ha_Un_Nome_Leggibile()
    {
        // Invariante di coerenza fra le due tabelle: nessun tipo di settore deve produrre una sigla che il
        // dizionario dei nomi non conosce (era il tipo di scollamento che la triplicazione rendeva possibile).
        foreach (var type in Enum.GetValues<SectorType>())
        {
            var code = FrequencyPositions.FromSectorType(type);
            Assert.False(string.IsNullOrWhiteSpace(FrequencyPositions.NameOf(code)));
            Assert.NotEqual("—", FrequencyPositions.NameOf(code));
        }
    }
}
