using Vipi.Domain.Services;
using Xunit;

namespace Vipi.Domain.Tests;

public class AiracServiceTests
{
    private readonly AiracService _sut = new();

    [Theory]
    [InlineData("2020-01-02", "2001")] // ancora: AIRAC 2001
    [InlineData("2020-01-30", "2002")] // +28 giorni (inizio ciclo 2002)
    [InlineData("2026-06-18", "2606")] // ciclo di riferimento usato nei documenti
    public void GetCycle_KnownDates(string date, string expected)
    {
        var utc = DateTime.Parse(date, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
        Assert.Equal(expected, _sut.GetCycle(utc));
    }

    [Fact]
    public void GetCycle_IsStableWithinCycle()
    {
        var a = _sut.GetCycle(new DateTime(2026, 6, 18, 0, 0, 0, DateTimeKind.Utc));
        var b = _sut.GetCycle(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)); // stesso ciclo
        Assert.Equal(a, b);
    }

    [Fact]
    public void GetCycle_FirstCycleOfYear_IsNumber01()
    {
        // Il primo ciclo di un anno deve terminare con "01".
        var cycle = _sut.GetCycle(new DateTime(2025, 1, 25, 0, 0, 0, DateTimeKind.Utc));
        Assert.EndsWith("01", cycle);
        Assert.StartsWith("25", cycle);
    }

    [Theory]
    [InlineData("2001", "2020-01-02")]   // epoch = inizio certo
    [InlineData("2002", "2020-01-30")]   // epoch +28 = inizio certo
    public void EffectiveUtcForCycle_KnownStarts(string cycle, string expectedDate)
    {
        var eff = _sut.EffectiveUtcForCycle(cycle);
        Assert.Equal(DateTime.Parse(expectedDate + "T00:00:00Z", null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal), eff);
    }

    [Theory]
    [InlineData("2001")]
    [InlineData("2606")]
    [InlineData("2513")]
    public void EffectiveUtcForCycle_RoundTripsThroughGetCycle(string cycle)
    {
        // La data efficace di un ciclo, ridata a GetCycle, riproduce lo stesso ciclo.
        Assert.Equal(cycle, _sut.GetCycle(_sut.EffectiveUtcForCycle(cycle)));
    }

    [Fact]
    public void EffectiveUtcForCycle_Rejects_Malformed()
    {
        Assert.Throws<ArgumentException>(() => _sut.EffectiveUtcForCycle("abc"));
        Assert.Throws<ArgumentException>(() => _sut.EffectiveUtcForCycle("26"));
    }

    /// <summary>
    /// ⚠️ <b>Il segno passava, e il sollevamento è usato come validatore</b>: `SidStampCycle` chiede proprio
    /// a questo metodo se il ciclo dichiarato dalla sorgente sia buono. Con `Length == 4 && int.TryParse`,
    /// «+261» dava l'anno 2002 e «−261» il 1998 — nessun errore, una data sbagliata, e il ciclo di un
    /// sectorfile datato a ventotto anni fa (revisione del 6 settembre 2026, R-019).
    /// </summary>
    [Theory]
    [InlineData("+261")]
    [InlineData("-261")]
    [InlineData("26 6")]
    [InlineData("2600")]   // il numero del ciclo dentro l'anno parte da 1
    [InlineData("2615")]   // e non arriva a quindici: in un anno ce ne stanno al massimo quattordici
    public void EffectiveUtcForCycle_Rejects_SignsAndOutOfRange(string cycle) =>
        Assert.Throws<ArgumentException>(() => _sut.EffectiveUtcForCycle(cycle));

    /// <summary>La controprova: i cicli veri continuano a passare, quattordicesimo compreso.</summary>
    [Theory]
    [InlineData("2601")]
    [InlineData("2614")]
    [InlineData(" 2606 ")]
    public void EffectiveUtcForCycle_Accepts_RealCycles(string cycle) =>
        Assert.True(_sut.EffectiveUtcForCycle(cycle) > DateTime.MinValue);

    [Fact]
    public void NextCycles_AreConsecutive_28DaysApart()
    {
        var from = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc);
        var cycles = _sut.NextCycles(from, 3);
        Assert.Equal(3, cycles.Count);
        Assert.Equal("2606", cycles[0].Cycle);   // ciclo corrente incluso
        Assert.Equal(28, (cycles[1].EffectiveUtc - cycles[0].EffectiveUtc).Days);
        Assert.Equal(28, (cycles[2].EffectiveUtc - cycles[1].EffectiveUtc).Days);
    }
}
