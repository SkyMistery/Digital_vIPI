using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vipi.Application.Content;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// Dal 7 settembre 2026 la pubblicazione rivaluta <b>subito</b> la deriva del documento appena scritto, così
/// chi edita non deve aspettare il giro delle 24 ore per sapere se è andata. Per farlo,
/// <c>ReleaseService</c> chiede <c>IImpactDriftUseCase</c>, che a sua volta chiede <c>IReleaseService</c>.
///
/// <para>⚠️ <b>Questo è un ciclo</b>, e un ciclo nel contenitore non si vede compilando: esplode alla prima
/// <i>risoluzione</i> — cioè in produzione, all'apertura della prima pagina che pubblica, con
/// <c>InvalidOperationException: A circular dependency was detected</c>. È rotto nel punto in cui non è un
/// ciclo (un <c>Lazy</c>: la deriva serve DOPO la scrittura, mai per costruire il servizio), e questo banco
/// esiste per accorgersi se qualcuno lo togliesse credendolo un vezzo.</para>
/// </summary>
public sealed class DerivaAllaPubblicazioneTests : IClassFixture<DerivaAllaPubblicazioneTests.Fabbrica>
{
    private readonly Fabbrica _fabbrica;
    public DerivaAllaPubblicazioneTests(Fabbrica fabbrica) => _fabbrica = fabbrica;

    [Fact]
    public void Il_contenitore_risolve_release_e_deriva_senza_ciclo()
    {
        using var scope = _fabbrica.Services.CreateScope();

        var release = scope.ServiceProvider.GetRequiredService<IReleaseService>();
        var deriva = scope.ServiceProvider.GetRequiredService<IImpactDriftUseCase>();

        Assert.NotNull(release);
        Assert.NotNull(deriva);
    }

    /// <summary>
    /// Il pigro dev'essere registrato: senza, <c>ReleaseService</c> nasce col parametro opzionale a
    /// <c>null</c> — compila, gira, e non riconcilia niente. È il modo esatto in cui questa riparazione
    /// tornerebbe a essere il difetto che era, in silenzio.
    /// </summary>
    [Fact]
    public void Il_pigro_della_deriva_e_registrato_e_si_apre()
    {
        using var scope = _fabbrica.Services.CreateScope();

        var pigro = scope.ServiceProvider.GetRequiredService<Lazy<IImpactDriftUseCase>>();

        Assert.False(pigro.IsValueCreated);
        Assert.NotNull(pigro.Value);
    }

    /// <summary>
    /// Stessa trappola del pigro, sull'altro parametro opzionale: se il contenitore non passa
    /// <c>IImportStateStore</c>, <c>ReleaseService</c> nasce con <c>_stati = null</c> — compila, gira,
    /// pubblica — e il guasto della riconciliazione torna a essere <b>muto</b>, che è esattamente il difetto
    /// che quella nota è andata a togliere (8 settembre 2026). Si guarda il campo perché è l'unica cosa che
    /// distingue «c'è» da «c'è e non gliel'hanno dato».
    /// </summary>
    [Fact]
    public void Il_registro_degli_stati_arriva_davvero_al_servizio_delle_release()
    {
        using var scope = _fabbrica.Services.CreateScope();

        var release = scope.ServiceProvider.GetRequiredService<IReleaseService>();

        var campo = typeof(ReleaseService).GetField("_stati",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(campo);
        Assert.NotNull(campo!.GetValue(release));
    }

    public sealed class Fabbrica : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"vipi-deriva-{Guid.NewGuid():N}.db");

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureHostConfiguration(cfg => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Vipi"] = $"Data Source={_dbPath}",
            }));
            Environment.SetEnvironmentVariable("VipiAuth__Enabled", "false");
            return base.CreateHost(builder);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* best-effort */ }
        }
    }
}
