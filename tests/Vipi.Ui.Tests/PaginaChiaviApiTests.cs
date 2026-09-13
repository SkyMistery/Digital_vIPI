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
/// La pagina delle chiavi API (<c>/services/vsop/admin/api-keys</c>), carta
/// <c>docs/feature/2026-09-13-chiavi-api.md</c>: il cancello prima di leggere, la chiave una volta sola.
/// </summary>
public class PaginaChiaviApiTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class FakeAuthz : IEditAuthorizationService
    {
        public VipiRole Role => VipiRole.Admin;
        public bool IsAdmin => true;
        public int? CurrentUserId => 704798;
        public string? CurrentName => "Tizio";
        public void EnsureAdmin() { }
    }

    private sealed class EmittentiFinti : IEmittentiChiaviApi
    {
        public EmittentiFinti(bool puo) => PuoEmettere = puo;
        public bool PuoEmettere { get; }
    }

    private sealed class ServizioFinto : IApiClientService
    {
        public int Letture { get; private set; }
        public List<ApiClientRow> Righe { get; } = new();

        public Task<IReadOnlyList<ApiClientRow>> ListAsync(CancellationToken ct = default)
        {
            Letture++;
            return Task.FromResult<IReadOnlyList<ApiClientRow>>(Righe.ToList());
        }

        public Task<ChiaveEmessa> CreaAsync(string nome, IReadOnlyCollection<string> endpoint, CancellationToken ct = default)
        {
            var chiave = ChiaveApi.Genera();
            var riga = new ApiClientRow(Righe.Count + 1, nome, ChiaveApi.Prefisso(chiave), endpoint.ToList(), 704798,
                DateTime.UtcNow, null, null, null);
            Righe.Add(riga);
            return Task.FromResult(new ChiaveEmessa(riga, chiave));
        }

        public Task<bool> RevocaAsync(int id, CancellationToken ct = default) => Task.FromResult(true);
    }

    private IRenderedComponent<AdminApiKeysPage> Render(bool puoEmettere, ServizioFinto servizio)
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddSingleton<IEditAuthorizationService>(new FakeAuthz());
        Services.AddSingleton<IEmittentiChiaviApi>(new EmittentiFinti(puoEmettere));
        Services.AddSingleton<IApiClientService>(servizio);
        return RenderComponent<AdminApiKeysPage>();
    }

    /// <summary>Un Admin che non è HQ né WD: accesso riservato, e l'elenco non si legge neppure.</summary>
    [Fact]
    public void Chi_non_emette_non_legge_l_elenco()
    {
        var servizio = new ServizioFinto();
        var cut = Render(puoEmettere: false, servizio);

        Assert.Contains("ApiKeys_Reserved", cut.Markup);
        Assert.Empty(cut.FindAll("#api-key-nome"));
        Assert.Equal(0, servizio.Letture);
    }

    [Fact]
    public void La_chiave_si_vede_una_volta_e_poi_resta_solo_il_prefisso()
    {
        var servizio = new ServizioFinto();
        var cut = Render(puoEmettere: true, servizio);
        Assert.Equal(1, servizio.Letture);

        cut.Find("#api-key-nome").Change("Validatore tour IT");
        cut.Find("#api-key-ep-archivio").Change(true);
        cut.Find("button.perm-go").Click();

        var chiave = cut.Find("code.api-key-value").TextContent.Trim();
        Assert.True(ChiaveApi.BenFormata(chiave));
        Assert.Contains(ChiaveApi.Prefisso(chiave) + "…", cut.Markup);

        cut.FindAll("button").First(b => b.TextContent.Contains("ApiKeys_NewDone")).Click();
        Assert.DoesNotContain(chiave, cut.Markup);
        Assert.Contains(ChiaveApi.Prefisso(chiave) + "…", cut.Markup);
    }
}
