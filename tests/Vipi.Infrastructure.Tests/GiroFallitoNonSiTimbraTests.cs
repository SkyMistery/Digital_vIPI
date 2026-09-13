using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Infrastructure.Ivao;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Un giro d'import che fallisce per un guasto di IVAO non si timbra «riuscito» (T-006, 13 settembre 2026).
///
/// <para>🔴 I giri automatici catturavano <see cref="InvalidOperationException"/> come «credenziali assenti» e
/// rispondevano <c>true</c>, cioè «timbra»: ma la stessa eccezione esce anche per un 503 o un 403. Il giro
/// fallito finiva timbrato, niente retry, Sorgenti verde, e dopo due notti la soglia di eliminazione
/// autorizzava a togliere tutto quel che non era stato riletto.</para>
/// </summary>
public class GiroFallitoNonSiTimbraTests
{
    [Fact]
    public async Task Un_errore_http_di_ivao_risale_e_non_diventa_un_giro_riuscito()
    {
        var giro = Giro(new InvalidOperationException("IVAO 503 ServiceUnavailable su /v2/centers"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => giro());
    }

    [Fact]
    public async Task Senza_credenziali_il_giro_non_timbra()
    {
        var giro = Giro(new SorgenteNonConfigurataException("Credenziali IVAO non configurate"));

        Assert.False(await giro());
    }

    /// <summary>La guardia sulla classe del difetto: nessun giro automatico cattura più l'eccezione generica.</summary>
    [Fact]
    public void Nessun_giro_automatico_cattura_InvalidOperationException()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Vipi.Infrastructure", "Ivao"))) dir = dir.Parent;
        Assert.NotNull(dir);

        var colpevoli = Directory.EnumerateFiles(Path.Combine(dir!.FullName, "src", "Vipi.Infrastructure", "Ivao"), "*HostedService.cs")
            .Where(f => File.ReadAllText(f).Contains("catch (InvalidOperationException", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(colpevoli.Count == 0, "Giri che trattano ogni InvalidOperationException come «non configurato»: " + string.Join(", ", colpevoli));
    }

    private static Func<Task<bool>> Giro(Exception lanciata)
    {
        var servizi = new ServiceCollection()
            .AddSingleton<IAccImportUseCase>(new ImportCheSolleva(lanciata))
            .BuildServiceProvider();
        var servizio = new AccImportHostedService(servizi.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new IvaoOptions()), NullLogger<AccImportHostedService>.Instance);
        var metodo = typeof(AccImportHostedService).GetMethod("ImportOnceAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return () => (Task<bool>)metodo.Invoke(servizio, new object[] { servizi, CancellationToken.None })!;
    }

    private sealed class ImportCheSolleva(Exception ex) : IAccImportUseCase
    {
        public Task<AccImportResult> RunAsync(CancellationToken ct = default) => Task.FromException<AccImportResult>(ex);
    }
}
