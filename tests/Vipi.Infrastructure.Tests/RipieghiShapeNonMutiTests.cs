using Microsoft.Extensions.Logging;
using Vipi.Infrastructure.Ivao;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// §A64.4 (18 settembre 2026): i quattro ripieghi delle shape nel giro d'import (GitHub, sectorfile, ATZ, cerchio)
/// scrivevano i loro guasti a <c>Debug</c>, che in produzione non si legge. Un guasto vero è un avviso; lo
/// spegnimento (annullamento) no.
/// </summary>
public class RipieghiShapeNonMutiTests
{
    [Fact]
    public void Un_guasto_del_ripiego_e_un_avviso_col_suo_testo()
    {
        var log = new Spia();

        AirportSectorImportHostedService.RipiegoFallito(log, new HttpRequestException("503"), "Ripiego shape settori saltato.");

        var (livello, testo, ex) = Assert.Single(log.Righe);
        Assert.Equal(LogLevel.Warning, livello);
        Assert.Equal("Ripiego shape settori saltato.", testo);
        Assert.IsType<HttpRequestException>(ex);
    }

    [Fact]
    public void Lo_spegnimento_resta_a_debug()
    {
        var log = new Spia();

        AirportSectorImportHostedService.RipiegoFallito(log, new TaskCanceledException(), "Shape TWR da GitHub saltate.");

        Assert.Equal(LogLevel.Debug, Assert.Single(log.Righe).Livello);
    }

    private sealed class Spia : ILogger
    {
        public List<(LogLevel Livello, string Testo, Exception? Ex)> Righe { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Righe.Add((logLevel, formatter(state, exception), exception));
    }
}
