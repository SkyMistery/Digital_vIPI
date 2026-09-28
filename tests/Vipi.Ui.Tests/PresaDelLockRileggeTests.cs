using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// U-011 (revisione totale 3): prendere il lock non rileggeva i dati. La pagina aperta in sola lettura teneva la
/// fotografia dell'apertura, e la prima scrittura dopo «Inizia modifica» rimandava la clausola INTERA com'era
/// allora, sopra il lavoro fatto nel frattempo da chi il lock l'aveva tenuto (riprodotto su Trasferimenti,
/// LIBB clausola 2: LevelValue 150 → 140; succede anche a un solo Admin con due schede).
///
/// <para>⚠️ La rilettura non può stare in <c>LockChanged</c>: la barra lo chiama anche all'APERTURA, mentre la
/// pagina sta ancora leggendo — due operazioni sullo stesso DbContext del circuito. Sta in <c>Acquired</c>, che
/// scatta solo sul gesto, e PRIMA che i comandi si riaccendano.</para>
/// </summary>
public class PresaDelLockRileggeTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class Editore : IEditAuthorizationService
    {
        public VipiRole Role => VipiRole.Editor;
        public bool IsAdmin => false;
        public int? CurrentUserId => 1;
        public string? CurrentName => "test";
    }

    /// <summary>Libero all'apertura (o già nostro, a scelta), nostro dopo «Inizia modifica».</summary>
    private sealed class LockFinto(bool giaMio) : IResourceLockService
    {
        private static LockInfo Mio => new() { Locked = true, IsMine = true, ByUserId = 1, ByName = "test" };
        public Task<LockInfo> InspectAsync(string k, CancellationToken ct = default) =>
            Task.FromResult(giaMio ? Mio : LockInfo.Free());
        public Task<LockInfo> AcquireAsync(string k, CancellationToken ct = default) => Task.FromResult(Mio);
        public Task<LockInfo> HeartbeatAsync(string k, CancellationToken ct = default) => Task.FromResult(Mio);
        public Task ForceUnlockAsync(string k, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureHeldAsync(string k, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReleaseAsync(string k, CancellationToken ct = default) => Task.CompletedTask;
    }

    private IRenderedComponent<EditLockBar> Monta(bool giaMio, List<string> eventi)
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddSingleton<IEditAuthorizationService>(new Editore());
        Services.AddScoped<IResourceLockService>(_ => new LockFinto(giaMio));
        return RenderComponent<EditLockBar>(p => p
            .Add(x => x.ResourceKey, "admin:structure")
            .Add(x => x.Acquired, () => eventi.Add("rilettura"))
            .Add(x => x.LockChanged, (bool mio) => eventi.Add(mio ? "mio" : "non mio")));
    }

    [Fact]
    public void Inizia_modifica_rilegge_prima_di_riaccendere_i_comandi()
    {
        var eventi = new List<string>();
        var cut = Monta(giaMio: false, eventi);
        eventi.Clear();   // quel che dice l'apertura non conta qui

        cut.FindAll("button").First(b => b.TextContent.Contains("Lock_StartEdit")).Click();

        Assert.Equal(new[] { "rilettura", "mio" }, eventi);
    }

    [Fact]
    public void All_apertura_non_si_rilegge_anche_se_il_lock_e_gia_nostro()
    {
        var eventi = new List<string>();
        Monta(giaMio: true, eventi);

        Assert.DoesNotContain("rilettura", eventi);
        Assert.Contains("mio", eventi);
    }

    [Theory]
    [InlineData("AdminTrasferimentiPage.razor")]
    [InlineData("StrutturaPage.razor")]
    [InlineData("AccAdminPage.razor")]
    [InlineData("AeroportiPage.razor")]
    [InlineData("ConfinantiAdminPage.razor")]
    public void Le_pagine_col_lock_di_struttura_rileggono_alla_presa(string pagina)
    {
        var s = File.ReadAllText(Path.Combine(Radice(), "Pages", pagina));
        Assert.Contains("Acquired=\"RileggiAllaPresaAsync\"", s);
        Assert.Contains("private async Task RileggiAllaPresaAsync()", s);
    }

    /// <summary>
    /// U-117 (revisione 3): l'intro di pagina caricava le sezioni solo all'apertura. Chi aspettava sulla pagina che il
    /// collega finisse, prendeva il lock e salvava, riscriveva l'intro INTERA letta all'apertura: le modifiche del
    /// collega sparivano senza avvisi, su un testo pubblico. Stessa porta delle pagine di struttura.
    /// </summary>
    [Fact]
    public void L_intro_di_pagina_rilegge_alla_presa()
    {
        var s = File.ReadAllText(Path.Combine(Radice(), "Components", "PageIntroZone.razor"));
        Assert.Contains("Acquired=\"RileggiAllaPresaAsync\"", s);
        Assert.Contains("private async Task RileggiAllaPresaAsync()", s);
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
        throw new DirectoryNotFoundException($"src/Vipi.Ui non trovata risalendo da {AppContext.BaseDirectory}");
    }
}
