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
/// 🔴 U-114/U-181 (revisione totale 3): la pagina dei permessi (<c>/services/vsop/admin/permissions</c>). La lettura
/// passava dalla fila della pagina, i due gesti no: un doppio clic su «Salva» apriva due scritture sullo stesso
/// DbContext, e un guasto che il servizio non traduce (una nota oltre la colonna) usciva dal gestore e il circuito
/// cadeva.
/// </summary>
public class PaginaPermessiTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class Admin : IEditAuthorizationService
    {
        public VipiRole Role => VipiRole.Admin;
        public bool IsAdmin => true;
        public int? CurrentUserId => 704798;
        public string? CurrentName => "Chi decide";
        public void EnsureAdmin() { }
    }

    private sealed class LivelliFinti : IRoleAdminService
    {
        public int Scritture { get; private set; }
        public TaskCompletionSource? Trattieni { get; set; }
        public Exception? Lancia { get; set; }

        public Task<IReadOnlyList<RoleRow>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RoleRow>>(new[]
            {
                new RoleRow(201143, "Persona di prova", Array.Empty<string>(), VipiRole.User, VipiRole.Editor,
                    VipiRole.Editor, DateTime.UtcNow, null, null),
            });

        public async Task SetAsync(int userId, VipiRole level, string? note, CancellationToken ct = default)
        {
            Scritture++;
            if (Trattieni is not null) await Trattieni.Task;
            if (Lancia is not null) throw Lancia;
        }

        public Task RemoveAsync(int userId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class ElencoFinto : IStaffRosterRepository
    {
        public Task UpsertLoginAsync(int UserId, string? displayName, IReadOnlyList<string> positions, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<StaffRosterEntry>> ListActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StaffRosterEntry>>(Array.Empty<StaffRosterEntry>());
        public Task<IReadOnlyList<int>> ListAllUserIdsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());
        public Task UpdateVerifiedAsync(int UserId, string? displayName, string? atcRating, IReadOnlyList<string> positions, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeactivateAsync(int UserId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyDictionary<int, string>> GetDisplayNamesAsync(IReadOnlyCollection<int> userIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string>());
        public Task<StaffRosterEntry?> FindAsync(int userId, CancellationToken ct = default) =>
            Task.FromResult<StaffRosterEntry?>(null);
    }

    private readonly LivelliFinti _livelli = new();

    private IRenderedComponent<AdminRolesPage> Apri()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddSingleton<IEditAuthorizationService>(new Admin());
        Services.AddSingleton<IRoleAdminService>(_livelli);
        Services.AddSingleton<IStaffRosterRepository>(new ElencoFinto());
        var cut = RenderComponent<AdminRolesPage>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[role=button].acc-pick")));
        cut.InvokeAsync(() => cut.Find("[role=button].acc-pick").Click());
        return cut;
    }

    [Fact]
    public async Task Il_doppio_clic_su_Salva_scrive_una_volta_e_non_fa_cadere_il_circuito()
    {
        _livelli.Trattieni = new TaskCompletionSource();
        var cut = Apri();
        var tasto = cut.Find("button.perm-go");

        var primo = tasto.ClickAsync(new());
        var secondo = tasto.ClickAsync(new());   // lo stesso elemento: il clic arriva prima che il tasto si spenga
        _livelli.Trattieni.SetResult();
        await Task.WhenAll(primo, secondo);

        var caduta = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(300));
        if (caduta == Renderer.UnhandledException) Assert.Fail("Circuito caduto: " + await Renderer.UnhandledException);
        Assert.Equal(1, _livelli.Scritture);
    }

    [Fact]
    public async Task Un_guasto_imprevisto_al_salvataggio_resta_un_messaggio()
    {
        _livelli.Lancia = new InvalidOperationException("colonna Note troppo corta");
        var cut = Apri();

        await cut.Find("button.perm-go").ClickAsync(new());

        var caduta = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(300));
        if (caduta == Renderer.UnhandledException) Assert.Fail("Circuito caduto: " + await Renderer.UnhandledException);
        cut.WaitForAssertion(() => Assert.Contains("colonna Note troppo corta", cut.Markup));
    }

    /// <summary>La casella della nota non accetta più di quel che la colonna tiene.</summary>
    [Fact]
    public void La_nota_ha_il_tetto_della_colonna()
    {
        var cut = Apri();
        var nota = cut.FindAll("input.app-in").First(i => i.GetAttribute("placeholder") == "Roles_NotePh");
        Assert.Equal(RoleAdminService.NotaMassima.ToString(), nota.GetAttribute("maxlength"));
    }
}
