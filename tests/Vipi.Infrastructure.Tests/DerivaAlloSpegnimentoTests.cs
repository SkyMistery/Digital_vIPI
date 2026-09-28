using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Vipi.Application.Content;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// U-236 (revisione 3, S39): la finestra di modifiche vive solo in memoria. Se il processo si spegneva prima dei
/// due minuti, la causa delle righe «da ripubblicare» spariva con lui. Ora lo spegnimento consuma la finestra.
/// </summary>
public class DerivaAlloSpegnimentoTests
{
    private sealed class GiroCheAnnota : IImpactDriftUseCase
    {
        public List<FinestraDiModifiche> Finestre { get; } = new();

        public Task<ImpactDriftResult> RunAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ImpactDriftResult> RunForDocumentAsync(int documentId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<ImpactDriftResult> RunAfterChangesAsync(FinestraDiModifiche finestra, CancellationToken ct = default)
        {
            Finestre.Add(finestra);
            return Task.FromResult(new ImpactDriftResult(1, 1, 0, 0));
        }
    }

    private static (DerivaDopoLeModificheHostedService Servizio, GiroCheAnnota Giro) Costruisci(ModificheInAttesa attesa)
    {
        var giro = new GiroCheAnnota();
        var sp = new ServiceCollection().AddSingleton<IImpactDriftUseCase>(giro).BuildServiceProvider();
        return (new DerivaDopoLeModificheHostedService(attesa, sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DerivaDopoLeModificheHostedService>.Instance), giro);
    }

    [Fact]
    public async Task Spegnersi_con_una_finestra_aperta_fa_il_giro_con_la_sua_causa()
    {
        var attesa = new ModificheInAttesa();
        var (servizio, giro) = Costruisci(attesa);
        var quando = DateTime.UtcNow;

        await servizio.StartAsync(CancellationToken.None);
        attesa.Segnala(new[] { FamiglieDiModifica.Coordinamenti }, quando);   // e la scheda si chiude subito
        using var tempo = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await servizio.StopAsync(tempo.Token);

        var finestra = Assert.Single(giro.Finestre);
        Assert.Equal(quando, finestra.DaUtc);
        Assert.Equal(new[] { FamiglieDiModifica.Coordinamenti }, finestra.Famiglie);
        servizio.Dispose();
    }

    [Fact]
    public async Task Spegnersi_senza_modifiche_non_fa_nessun_giro()
    {
        var (servizio, giro) = Costruisci(new ModificheInAttesa());

        await servizio.StartAsync(CancellationToken.None);
        await servizio.StopAsync(CancellationToken.None);

        Assert.Empty(giro.Finestre);
        servizio.Dispose();
    }
}
