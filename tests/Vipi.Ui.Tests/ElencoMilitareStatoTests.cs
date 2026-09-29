using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La colonna «Stato» dell'elenco nazionale dei vSOP militari (<c>/services/vsop/mil</c>).
///
/// <para>🔴 <b>Perché (29 settembre 2026).</b> Il committente: «perché accanto ai documenti pubblicati c'è scritto
/// no document nella versione accessibile agli utenti? Il documento sta lì». L'ultimo ramo della colonna era un
/// <c>else</c> nudo legato alla catena dei tasti dello staff, e per chi non era Editor scattava sempre: accanto a
/// «Pubblicato» usciva «Nessun documento».</para>
/// </summary>
public class ElencoMilitareStatoTests : TestContext
{
    private sealed class AuthzFinto(VipiRole ruolo) : IEditAuthorizationService
    {
        public VipiRole Role => ruolo;
        public bool IsAdmin => ruolo >= VipiRole.Admin;
        public int? CurrentUserId => null;
        public string? CurrentName => null;
        public void EnsureAdmin() { }
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    /// <summary>Solo l'elenco conta: il resto dell'interfaccia (lunga) risponde vuoto, come <see cref="ServizioVuoto"/>.</summary>
    public class MilitariFinti : ServizioVuoto
    {
        public IReadOnlyList<MilAirportRow> Righe { get; set; } = Array.Empty<MilAirportRow>();

        public static IMilitaryDocumentService Con(IReadOnlyList<MilAirportRow> righe)
        {
            var finto = Create<IMilitaryDocumentService, MilitariFinti>();
            ((MilitariFinti)(object)finto).Righe = righe;
            return finto;
        }

        protected override object? Invoke(System.Reflection.MethodInfo? m, object?[]? a) =>
            m!.Name == nameof(IMilitaryDocumentService.ListAsync)
                ? Task.FromResult<IReadOnlyList<MilAirportRow>>((bool)a![0]! ? Righe : Righe.Where(r => r.Pubblicato).ToList())
                : base.Invoke(m, a);
    }

    private IRenderedComponent<MilListPage> Rendi(VipiRole ruolo)
    {
        var righe = new List<MilAirportRow>
        {
            new("LIPL", "Ghedi", "LIPP", AirportCategory.MilitaryOnly, 10, Pubblicato: true),
            new("LIMN", "Cameri", "LIMM", AirportCategory.MilitaryOnly, 11, Pubblicato: false),
            new("LIRS", "Grosseto", "LIRR", AirportCategory.MilitaryOnly, null, Pubblicato: false),
        };
        Services.AddSingleton<IEditAuthorizationService>(new AuthzFinto(ruolo));
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton(new EnglishStrings());
        Services.AddSingleton<IMilitaryDocumentService>(MilitariFinti.Con(righe));
        ComponentFactories.AddStub<PageIntroZone>();
        JSInterop.Mode = JSRuntimeMode.Loose;
        return RenderComponent<MilListPage>();
    }

    [Fact]
    public void Al_pubblico_un_documento_pubblicato_non_dice_nessun_documento()
    {
        var cut = Rendi(VipiRole.User);

        var riga = Assert.Single(cut.FindAll("tbody tr"));
        Assert.Contains("Mil_Published", riga.TextContent);
        Assert.DoesNotContain("Mil_NoDoc", riga.TextContent);
        Assert.DoesNotContain("Ed_Edit", riga.TextContent);
    }

    [Fact]
    public void Allo_staff_ogni_riga_ha_lo_stato_e_il_suo_tasto()
    {
        var cut = Rendi(VipiRole.Editor);

        var righe = cut.FindAll("tbody tr").ToDictionary(r => r.QuerySelector("b")!.TextContent, r => r.TextContent);
        Assert.Contains("Mil_Published", righe["LIPL"]);
        Assert.Contains("Ed_Edit", righe["LIPL"]);
        Assert.Contains("Mil_Draft", righe["LIMN"]);
        Assert.Contains("Ed_Edit", righe["LIMN"]);
        Assert.Contains("Mil_Create", righe["LIRS"]);
        Assert.DoesNotContain(righe.Values, t => t.Contains("Mil_NoDoc"));
    }
}
