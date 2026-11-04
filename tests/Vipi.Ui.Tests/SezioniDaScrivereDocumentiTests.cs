using System.Reflection;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Application.Routing;
using Vipi.Domain;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// S134: nella pagina «Sezioni da scrivere», sopra le tabelle delle facoltative, l'elenco dei documenti che hanno
/// sezioni da scrivere ancora vuote. Il nome apre l'editor, la riga si apre e dice quali; spegnere una sezione
/// (facoltativa) rifà l'elenco subito.
/// </summary>
public class SezioniDaScrivereDocumentiTests : TestContext
{
    private sealed class ChiaveComeValore : IStringLocalizer<SharedResource>
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
        public string? CurrentName => "Prova";
    }

    private sealed class Facoltative : ISezioniFacoltativeService
    {
        public readonly HashSet<(SectionProfile, string)> Spente = new();

        public Task<IReadOnlyList<SezioneDelCatalogo>> ElencoAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SezioneDelCatalogo>>(new[]
            {
                new SezioneDelCatalogo(SectionProfile.Airport, "vfr", "Gestione del traffico › VFR",
                    Spente.Contains((SectionProfile.Airport, "vfr"))),
            });

        public Task ImpostaAsync(SectionProfile profilo, string chiave, bool facoltativa, CancellationToken ct = default)
        {
            if (facoltativa) Spente.Add((profilo, chiave)); else Spente.Remove((profilo, chiave));
            return Task.CompletedTask;
        }
    }

    /// <summary>Il conto: LIML ha due vuote (una è «vfr»), la vIPI di LIMM una nel blocco APP, LIRF niente.</summary>
    private sealed class Conto : ISezioniDaScrivereService
    {
        private readonly Facoltative _f;
        public Conto(Facoltative f) => _f = f;

        public Task<IReadOnlyDictionary<int, StatoSezioni>> QuadroAsync(CancellationToken ct = default)
        {
            var liml = new List<SezioneVuota> { new(21, "atis", "ATIS") };
            if (!_f.Spente.Contains((SectionProfile.Airport, "vfr")))
                liml.Add(new SezioneVuota(22, "vfr", "Gestione del traffico › VFR"));
            return Task.FromResult<IReadOnlyDictionary<int, StatoSezioni>>(new Dictionary<int, StatoSezioni>
            {
                [1] = new(1, new[] { new ContoSezioni(null, 5, liml) }),
                [2] = new(2, new[]
                {
                    new ContoSezioni("Settori di aerovia", 3, Array.Empty<SezioneVuota>()),
                    new ContoSezioni("Milano APP", 2, new[] { new SezioneVuota(31, "regulated", "Aree regolamentate") }),
                }),
                [3] = new(3, new[] { new ContoSezioni(null, 7, Array.Empty<SezioneVuota>()) }),
            });
        }

        public Task<StatoSezioni?> DocumentoAsync(int documentId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    /// <summary>L'elenco dei documenti: risponde solo a <c>ListAsync</c>.</summary>
    public class Documenti : DispatchProxy
    {
        public static readonly IReadOnlyList<ManagedDoc> Elenco = new[]
        {
            new ManagedDoc(ReleaseTargetType.Airport, "vIPI — LIML Milano Linate", "LIML", "LIMM", true, false, false,
                ReleaseTargetType.Airport, "LIML", 1),
            new ManagedDoc(ReleaseTargetType.AccVipi, "vIPI — Milano ACC", "LIMM_CTR", "LIMM", true, false, false,
                ReleaseTargetType.AccVipi, "LIMM", 2),
            new ManagedDoc(ReleaseTargetType.Airport, "vIPI — LIRF Roma Fiumicino", "LIRF", "LIRR", true, false, false,
                ReleaseTargetType.Airport, "LIRF", 3),
        };

        protected override object? Invoke(MethodInfo? m, object?[]? a) => m!.Name == nameof(IDocumentAdminService.ListAsync)
            ? Task.FromResult(Elenco)
            : throw new NotSupportedException($"{m.Name} non doveva essere chiamato.");
    }

    private sealed class Rotte : IDocKindRoutes
    {
        public Rotte(ReleaseTargetType t) => Target = t;
        public ReleaseTargetType Target { get; }
        public string? ViewerUrl(string acc, string key, string? neighbourCode, int releaseId) => null;
        public string? PublicUrl(string acc, string key, string? neighbourCode) => null;
        public string? EditorUrl(string acc, string key, string? neighbourCode, int? documentId) => $"/ed/{Target}/{acc}/{key}";
        public string? DraftUrl(string acc, string key, string? neighbourCode) => null;
    }

    private readonly Facoltative _facoltative = new();

    public SezioniDaScrivereDocumentiTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeValore());
        Services.AddSingleton<StringheDelSito>();
        Services.AddSingleton<IEditAuthorizationService>(new Admin());
        Services.AddSingleton<ISezioniFacoltativeService>(_facoltative);
        Services.AddSingleton<ISezioniDaScrivereService>(new Conto(_facoltative));
        Services.AddSingleton(DispatchProxy.Create<IDocumentAdminService, Documenti>());
        Services.AddSingleton<IDocRoutesRegistry>(new DocRoutesRegistry(
            Enum.GetValues<ReleaseTargetType>().Select(t => (IDocKindRoutes)new Rotte(t))));
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Elenca_solo_i_documenti_con_sezioni_vuote_e_il_nome_apre_l_editor()
    {
        var cut = RenderComponent<SezioniFacoltativePage>();
        cut.WaitForElement("[data-docs-vuote]");

        var righe = cut.FindAll("[data-docs-vuote] details[data-doc]");
        Assert.Equal(new[] { "1", "2" }, righe.Select(r => r.GetAttribute("data-doc")));

        var nome = cut.Find("details[data-doc='1'] summary a");
        Assert.Equal("/ed/Airport/limm/LIML", nome.GetAttribute("href"));
        Assert.Contains("vIPI — LIML Milano Linate", nome.TextContent);
        // L'elenco sta SOPRA le tabelle delle facoltative.
        Assert.True(cut.Markup.IndexOf("data-docs-vuote", StringComparison.Ordinal)
                    < cut.Markup.IndexOf("data-profilo", StringComparison.Ordinal));
    }

    [Fact]
    public void Aperta_la_riga_dice_quali_sezioni_e_porta_a_ciascuna()
    {
        var cut = RenderComponent<SezioniFacoltativePage>();
        cut.WaitForElement("[data-docs-vuote]");

        var liml = cut.Find("details[data-doc='1']");
        Assert.Equal(new[] { "ATIS", "Gestione del traffico › VFR" },
            liml.QuerySelectorAll("li [data-percorso]").Select(s => s.TextContent.Trim()));
        Assert.Equal("/ed/Airport/limm/LIML#s-22", liml.QuerySelectorAll("li a").Last().GetAttribute("href"));

        // La vIPI di ACC: la parte (il blocco) prima delle sue vuote, e le parti senza vuote non si dicono.
        var limm = cut.Find("details[data-doc='2']");
        Assert.Contains("Milano APP", limm.TextContent);
        Assert.DoesNotContain("Settori di aerovia", limm.TextContent);
    }

    [Fact]
    public void Una_sezione_resa_facoltativa_esce_subito_dall_elenco()
    {
        var cut = RenderComponent<SezioniFacoltativePage>();
        cut.WaitForElement("[data-docs-vuote]");

        cut.Find("table[data-profilo] input[type=checkbox]").Change(true);

        cut.WaitForAssertion(() => Assert.Equal(new[] { "ATIS" },
            cut.Find("details[data-doc='1']").QuerySelectorAll("li [data-percorso]").Select(s => s.TextContent.Trim())));
    }
}
