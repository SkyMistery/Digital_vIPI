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
/// «Fine modifica» salva invece di buttare via (<c>EditLockBar.BeforeRelease</c>).
///
/// <para>⚠️ Non è una comodità: prima di questo aggancio, chi scriveva e premeva «Fine modifica» — che è la
/// strada naturale per «ho finito» — perdeva tutto <b>in silenzio</b>, perché il rilascio del lock faceva
/// rileggere da archivio. Il difetto non dava nessun errore: dava una pagina che tornava com'era.</para>
/// </summary>
public class FineModificaSalvaTests : TestContext
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

    /// <summary>Un lock che è già nostro, e che tiene il conto di quando viene lasciato.</summary>
    private sealed class LockFinto : IResourceLockService
    {
        public readonly List<string> Ordine = new();

        private static LockInfo Mio => new() { Locked = true, IsMine = true, ByUserId = 1, ByName = "test" };

        public Task<LockInfo> InspectAsync(string k, CancellationToken ct = default) => Task.FromResult(Mio);
        public Task<LockInfo> AcquireAsync(string k, CancellationToken ct = default) => Task.FromResult(Mio);
        public Task<LockInfo> HeartbeatAsync(string k, CancellationToken ct = default) => Task.FromResult(Mio);
        public Task ForceUnlockAsync(string k, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureHeldAsync(string k, CancellationToken ct = default) => Task.CompletedTask;

        public Task ReleaseAsync(string k, CancellationToken ct = default)
        {
            Ordine.Add("rilascio");
            return Task.CompletedTask;
        }
    }

    private LockFinto Monta()
    {
        var locks = new LockFinto();
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<IEditAuthorizationService>(new Editore());
        Services.AddScoped<IResourceLockService>(_ => locks);
        return locks;
    }

    private static void FineModifica(IRenderedComponent<EditLockBar> cut) =>
        cut.FindAll("button").First(b => b.TextContent.Contains("Lock_FinishEdit")).Click();

    /// <summary>⚠️ L'ordine è la cosa che conta: chi salva deve poterlo fare <b>mentre il lock è ancora
    /// suo</b>. Salvare dopo il rilascio vuol dire scrivere senza permesso.</summary>
    [Fact]
    public void Il_salvataggio_avviene_PRIMA_del_rilascio()
    {
        var locks = Monta();
        var cut = RenderComponent<EditLockBar>(p => p
            .Add(x => x.ResourceKey, "editor:page-intro:mil")
            .Add(x => x.BeforeRelease, () => { locks.Ordine.Add("salvataggio"); return Task.CompletedTask; }));

        FineModifica(cut);

        Assert.Equal(new[] { "salvataggio", "rilascio" }, locks.Ordine);
    }

    /// <summary>⚠️ Se il salvataggio fallisce il lock <b>resta</b>: lasciarlo andare è il modo più rapido di
    /// perdere il lavoro appena scritto, perché chi ha scritto non ha più il permesso per riprovare.</summary>
    [Fact]
    public void Se_il_salvataggio_fallisce_il_lock_non_si_rilascia()
    {
        var locks = Monta();
        var cut = RenderComponent<EditLockBar>(p => p
            .Add(x => x.ResourceKey, "editor:page-intro:mil")
            .Add(x => x.BeforeRelease, () => throw new InvalidOperationException("archivio irraggiungibile")));

        Assert.Throws<InvalidOperationException>(() => FineModifica(cut));
        Assert.Empty(locks.Ordine);
    }

    /// <summary>Chi non aggancia niente non cambia di una virgola: è la prova che il parametro è additivo.</summary>
    [Fact]
    public void Senza_aggancio_il_tasto_rilascia_e_basta()
    {
        var locks = Monta();
        var cut = RenderComponent<EditLockBar>(p => p.Add(x => x.ResourceKey, "editor:page-intro:mil"));

        FineModifica(cut);

        Assert.Equal(new[] { "rilascio" }, locks.Ordine);
    }
}
