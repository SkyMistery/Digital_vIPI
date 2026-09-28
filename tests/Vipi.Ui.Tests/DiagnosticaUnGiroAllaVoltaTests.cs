using System.Collections;
using System.Reflection;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Application.Diagnostics;
using Vipi.Domain;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// 🔴 U-188 (revisione totale 3): nella Diagnostica «Aggiorna» restava acceso durante «Rilancia la deriva», e il giro
/// della deriva (con la marcatura del successo) stava fuori dalla fila della pagina. Un «Aggiorna» premuto mentre
/// la deriva girava leggeva lo stesso DbContext nello stesso momento: «A second operation», circuito giù.
/// </summary>
public class DiagnosticaUnGiroAllaVoltaTests : TestContext
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
        public string? CurrentName => "Chi controlla";
        public void EnsureAdmin() { }
    }

    /// <summary>Il DbContext della pagina, ridotto a quel che conta: quante operazioni ci sono dentro insieme.</summary>
    public sealed class Contesto
    {
        private int _dentro;
        public int MassimoInsieme { get; private set; }

        public async Task<T> Dentro<T>(Func<Task<T>> lavoro)
        {
            var ora = Interlocked.Increment(ref _dentro);
            if (ora > MassimoInsieme) MassimoInsieme = ora;
            try { return await lavoro(); }
            finally { Interlocked.Decrement(ref _dentro); }
        }
    }

    /// <summary>Un servizio che risponde vuoto a tutto, e ogni risposta passa dal contesto con una piccola attesa.</summary>
    public class Vuoto : DispatchProxy
    {
        internal Contesto? Db;

        protected override object? Invoke(MethodInfo? m, object?[]? a)
        {
            var tipo = m!.ReturnType;
            if (tipo == typeof(Task)) return Db!.Dentro(async () => { await Task.Delay(20); return 0; });
            if (tipo.IsGenericType && tipo.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var valore = Vuota(tipo.GetGenericArguments()[0]);
                var dentro = typeof(Vuoto).GetMethod(nameof(Attendi), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(tipo.GetGenericArguments()[0]);
                return dentro.Invoke(this, new[] { valore });
            }
            return Vuota(tipo);
        }

        private Task<T> Attendi<T>(object? valore) => Db!.Dentro(async () => { await Task.Delay(20); return (T)valore!; });

        private static object? Vuota(Type t)
        {
            if (t == typeof(string)) return null;
            if (t.IsValueType) return Activator.CreateInstance(t);
            if (t.IsGenericType && typeof(IEnumerable).IsAssignableFrom(t))
            {
                var args = t.GetGenericArguments();
                if (args.Length == 2) return Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(args));
                return Array.CreateInstance(args[0], 0);
            }
            return null;
        }
    }

    /// <summary>La deriva: tenuta in volo finché il test non la lascia andare.</summary>
    private sealed class DerivaTrattenuta(Contesto db) : IImpactDriftUseCase
    {
        public readonly TaskCompletionSource Via = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ImpactDriftResult> RunAsync(CancellationToken ct = default) =>
            db.Dentro(async () => { await Via.Task; return new ImpactDriftResult(1, 0, 0, 0); });

        public Task<ImpactDriftResult> RunAfterChangesAsync(FinestraDiModifiche finestra, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<ImpactDriftResult> RunForDocumentAsync(int documentId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private static T Proxy<T>(Contesto db) where T : class
    {
        var p = DispatchProxy.Create<T, Vuoto>();
        ((Vuoto)(object)p).Db = db;
        return p;
    }

    [Fact]
    public async Task Aggiorna_durante_la_deriva_non_legge_insieme_al_giro()
    {
        var db = new Contesto();
        var deriva = new DerivaTrattenuta(db);
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddSingleton<IEditAuthorizationService>(new Admin());
        Services.AddSingleton(Proxy<IConsistencyReportService>(db));
        Services.AddSingleton(Proxy<IDocumentImpactService>(db));
        Services.AddSingleton(Proxy<IImportStateStore>(db));
        Services.AddSingleton(Proxy<IAdminCoverageService>(db));
        Services.AddSingleton<IImpactDriftUseCase>(deriva);
        Services.AddSingleton(Proxy<Vipi.Application.Diagnostics.IDatabaseBackup>(new Contesto()));
        Services.AddSingleton(Proxy<Vipi.Application.Media.IMediaMaintenance>(new Contesto()));

        var cut = RenderComponent<DiagnosticaPage>();
        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll("button"), b => b.TextContent.Contains("Diag_ImpactsRun")),
            TimeSpan.FromSeconds(3));

        var rilancio = cut.FindAll("button").First(b => b.TextContent.Contains("Diag_ImpactsRun")).ClickAsync(new());
        var aggiorna = cut.FindAll("button").First(b => b.TextContent.Contains("Diag_Refresh")).ClickAsync(new());
        await Task.Delay(60);
        deriva.Via.SetResult();
        await Task.WhenAll(rilancio, aggiorna);

        var caduta = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(300));
        if (caduta == Renderer.UnhandledException) Assert.Fail("Circuito caduto: " + await Renderer.UnhandledException);
        Assert.Equal(1, db.MassimoInsieme);
    }

    [Fact]
    public void Durante_la_deriva_Aggiorna_e_spento()
    {
        var sorgente = File.ReadAllText(Path.Combine(Radice(), "Pages", "DiagnosticaPage.razor"));
        var riga = sorgente.Split('\n').First(r => r.Contains("Diag_RefreshTitle", StringComparison.Ordinal));
        Assert.Contains("_derivaInCorso", riga);
    }

    private static string Radice()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui");
            if (Directory.Exists(Path.Combine(c, "Pages"))) return c;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(AppContext.BaseDirectory);
    }
}
