using Vipi.Application.Airspace;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>
/// Le quote come le scrive il file dell'AIP: quattro forme sole, contate su tutte e 3 072 le quote del file
/// del 15 luglio 2026.
/// </summary>
public class AirspaceLevelParserTests
{
    [Theory]
    [InlineData("GND", AirspaceDatum.Gnd, 0)]
    [InlineData("2500 FT AMSL", AirspaceDatum.Amsl, 2500)]
    [InlineData("2491 FT AMSL", AirspaceDatum.Amsl, 2491)]     // ROMA CTR Z6: esiste davvero
    [InlineData("1500 FT AGL", AirspaceDatum.Agl, 1500)]
    [InlineData("FL105", AirspaceDatum.FlightLevel, 10500)]
    [InlineData("FL85", AirspaceDatum.FlightLevel, 8500)]
    public void Le_Quattro_Forme_Del_File(string testo, AirspaceDatum datum, int piedi)
    {
        var quota = AirspaceLevelParser.Parse(testo);

        Assert.NotNull(quota);
        Assert.Equal(datum, quota.Datum);
        Assert.Equal(piedi, quota.Feet);
        Assert.Equal(testo, quota.Raw);   // il testo di partenza è quel che il documento stampa
    }

    [Theory]
    [InlineData("FL999")]     // 16 quote nel file
    [InlineData("FL980")]     // 1
    [InlineData("FL2000")]    // 1
    [InlineData("UNL")]
    public void Sopra_Lunl_Convenzionale_Ce_Lillimitato_Non_Un_Livello(string testo)
    {
        var quota = AirspaceLevelParser.Parse(testo);

        Assert.NotNull(quota);
        Assert.Equal(AirspaceDatum.Unlimited, quota.Datum);
        Assert.Null(quota.Feet);   // ⚠️ FL2000 preso alla lettera sarebbe un'area alta 200 000 piedi
    }

    [Fact]
    public void Il_Livello_Piu_Alto_Vero_Del_File_Resta_Un_Livello()
    {
        // FL600 è il massimo che nel file è un limite vero (sei quote): la soglia dell'illimitato gli sta sopra.
        var quota = AirspaceLevelParser.Parse("FL600");

        Assert.NotNull(quota);
        Assert.Equal(AirspaceDatum.FlightLevel, quota.Datum);
        Assert.Equal(60000, quota.Feet);
    }

    [Theory]
    [InlineData("SFC")]
    [InlineData("0 FT AGL")]
    [InlineData("GROUND")]
    public void Il_Suolo_Si_Scrive_In_Piu_Modi_Ed_E_Sempre_Il_Suolo(string testo)
    {
        var quota = AirspaceLevelParser.Parse(testo);

        Assert.NotNull(quota);
        Assert.Equal(AirspaceDatum.Gnd, quota.Datum);
        Assert.Equal(0, quota.Feet);
    }

    [Fact]
    public void Senza_Riferimento_Sono_Piedi_Sul_Mare()
    {
        var quota = AirspaceLevelParser.Parse("3000 FT");

        Assert.NotNull(quota);
        Assert.Equal(AirspaceDatum.Amsl, quota.Datum);
        Assert.Equal(3000, quota.Feet);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("quota da confermare")]
    [InlineData("FL")]
    public void Quel_Che_Non_Si_Riconosce_Torna_Nullo_Invece_Di_Diventare_Zero(string? testo)
    {
        // ⚠️ Un numero inventato qui diventa un'area alta zero piedi in un documento pubblicato: chi chiama
        // deve poter distinguere «non l'ho capita» da «vale zero», e segnalarla.
        Assert.Null(AirspaceLevelParser.Parse(testo));
    }
}
