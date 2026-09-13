using System.Reflection;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// 🔴 T-040 (revisione del 13 settembre 2026): il registro audit caricava a ogni cambio di periodo e a ogni
/// «Aggiorna» sul DbContext del circuito, senza porta: due cambi ravvicinati erano due caricamenti sovrapposti
/// («A second operation was started»). E vince il periodo scelto per ULTIMO.
/// </summary>
public class PaginaAuditUnGiroAllaVoltaTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class Admin : IEditAuthorizationService
    {
        public VipiRole Role => VipiRole.Admin;
        public bool IsAdmin => true;
        public int? CurrentUserId => 704798;
        public string? CurrentName => "Tizio";
        public void EnsureAdmin() { }
    }

    /// <summary>Conta le letture sovrapposte e ricorda il periodo dell'ultima.</summary>
    private sealed class RegistroCheConta : IAuditLogReader
    {
        private int _dentro;
        public int MassimoInsieme;
        public DateTime? UltimoDa;

        public async Task<IReadOnlyList<AuditEntry>> ListRecentAsync(DateTime? sinceUtc = null, int max = 500, CancellationToken ct = default)
        {
            await Dentro();
            UltimoDa = sinceUtc;
            return Array.Empty<AuditEntry>();
        }

        public async Task<int> CountAsync(DateTime? sinceUtc = null, CancellationToken ct = default)
        {
            await Dentro();
            return 0;
        }

        public Task<IReadOnlyList<AuditEntry>> ListForEntityAsync(string entityType, string entityId, int max, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AuditEntry>> ListForEntitiesAsync(string entityType, IReadOnlyList<string> entityIds, int max, CancellationToken ct = default) =>
            throw new NotSupportedException();

        private async Task Dentro()
        {
            var ora = Interlocked.Increment(ref _dentro);
            if (ora > MassimoInsieme) MassimoInsieme = ora;
            try { await Task.Delay(40); }
            finally { Interlocked.Decrement(ref _dentro); }
        }
    }

    /// <summary>Un servizio che nessuno deve chiamare: con righe vuote nomi e titoli non si cercano.</summary>
    public class Nessuno : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? m, object?[]? a) =>
            throw new NotSupportedException($"{m?.Name} non doveva essere chiamato.");
    }

    [Fact]
    public async Task Due_cambi_di_periodo_ravvicinati_non_sovrappongono_i_caricamenti()
    {
        var registro = new RegistroCheConta();
        Services.AddSingleton<IAuditLogReader>(registro);
        Services.AddSingleton(DispatchProxy.Create<IStaffRosterRepository, Nessuno>());
        Services.AddSingleton(DispatchProxy.Create<IDocumentAdminRepository, Nessuno>());
        Services.AddSingleton<IEditAuthorizationService>(new Admin());
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<StringheDelSito>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<AuditPage>();
        var select = cut.WaitForElement("select.htree-select", TimeSpan.FromSeconds(3));

        var primo = select.ChangeAsync(new() { Value = "7" });
        var secondo = cut.Find("select.htree-select").ChangeAsync(new() { Value = "90" });
        var terzo = cut.Find("button.btn.ghost").ClickAsync(new());
        await Task.WhenAll(primo, secondo, terzo);

        cut.WaitForAssertion(() => Assert.NotNull(registro.UltimoDa), TimeSpan.FromSeconds(3));
        await Task.Delay(300);

        Assert.Equal(1, registro.MassimoInsieme);
        Assert.True(registro.UltimoDa < DateTime.UtcNow.AddDays(-80), "l'ultimo caricamento deve essere quello del periodo scelto per ultimo (90 giorni)");
        var caduta = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(200));
        if (caduta == Renderer.UnhandledException) Assert.Fail("Eccezione non gestita: " + await Renderer.UnhandledException);
    }
}
