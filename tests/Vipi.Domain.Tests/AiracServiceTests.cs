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
}
