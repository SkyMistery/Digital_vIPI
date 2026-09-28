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
        public int Creazioni { get; private set; }
        public List<ApiClientRow> Righe { get; } = new();

        /// <summary>Se c'è, la creazione aspetta che il test la lasci andare: così un secondo clic la trova in volo.</summary>
        public TaskCompletionSource? Trattieni { get; set; }

        /// <summary>Se c'è, la creazione lancia questo: un guasto che il servizio non traduce.</summary>
        public Exception? Lancia { get; set; }

        public Task<IReadOnlyList<ApiClientRow>> ListAsync(CancellationToken ct = default)
        {
            Letture++;
            return Task.FromResult<IReadOnlyList<ApiClientRow>>(Righe.ToList());
        }

        public async Task<ChiaveEmessa> CreaAsync(string nome, IReadOnlyCollection<string> endpoint, CancellationToken ct = default)
        {
            Creazioni++;
            if (Trattieni is not null) await Trattieni.Task;
            if (Lancia is not null) throw Lancia;
            var chiave = ChiaveApi.Genera();
            var riga = new ApiClientRow(Righe.Count + 1, nome, ChiaveApi.Prefisso(chiave), endpoint.ToList(), 704798,
                DateTime.UtcNow, null, null, null);
            Righe.Add(riga);
            return new ChiaveEmessa(riga, chiave);
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

    /// <summary>
    /// 🔴 U-114/U-199 (revisione totale 3): un VERO doppio clic su «Crea» apriva due scritture sullo stesso
    /// DbContext e il circuito cadeva. Il tasto si spegne solo dopo un giro di rete, e intanto il secondo clic parte.
    /// </summary>
    [Fact]
    public async Task Il_doppio_clic_su_Crea_crea_una_chiave_e_non_fa_cadere_il_circuito()
    {
        var servizio = new ServizioFinto { Trattieni = new TaskCompletionSource() };
        var cut = Render(puoEmettere: true, servizio);
        cut.Find("#api-key-nome").Change("Validatore tour IT");
        cut.Find("#api-key-ep-archivio").Change(true);
        var tasto = cut.Find("button.perm-go");

        var primo = tasto.ClickAsync(new());
        var secondo = tasto.ClickAsync(new());   // lo stesso elemento: il clic arriva prima che il tasto si spenga
        servizio.Trattieni.SetResult();
        await Task.WhenAll(primo, secondo);

        var caduta = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(300));
        if (caduta == Renderer.UnhandledException) Assert.Fail("Circuito caduto: " + await Renderer.UnhandledException);
        Assert.Equal(1, servizio.Creazioni);
        Assert.Single(servizio.Righe);
    }

    /// <summary>U-114: un guasto che il servizio non traduce resta un messaggio, non un circuito caduto.</summary>
    [Fact]
    public async Task Un_guasto_imprevisto_alla_creazione_resta_un_messaggio()
    {
        var servizio = new ServizioFinto { Lancia = new InvalidOperationException("database giù") };
        var cut = Render(puoEmettere: true, servizio);
        cut.Find("#api-key-nome").Change("Validatore tour IT");
        cut.Find("#api-key-ep-archivio").Change(true);

        await cut.Find("button.perm-go").ClickAsync(new());

        var caduta = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(300));
        if (caduta == Renderer.UnhandledException) Assert.Fail("Circuito caduto: " + await Renderer.UnhandledException);
        cut.WaitForAssertion(() => Assert.Contains("database giù", cut.Markup));
    }
}
