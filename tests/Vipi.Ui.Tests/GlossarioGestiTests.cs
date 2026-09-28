using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Translation;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// I gesti del glossario di fraseologia (<c>/services/vsop/admin/translations</c>).
/// <list type="bullet">
///   <item>🔴 U-179 (revisione totale 3): il cestino cancellava la voce al primo clic, senza conferma.</item>
///   <item>🔴 U-190: i gesti erano in fila ma con un <c>finally</c> e basta, e un guasto del database usciva dal
///     gestore: circuito giù.</item>
/// </list>
/// </summary>
public class GlossarioGestiTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class Editor : IEditAuthorizationService
    {
        public VipiRole Role => VipiRole.Editor;
        public bool IsAdmin => false;
        public int? CurrentUserId => 704798;
        public string? CurrentName => "Chi cura";
        public void EnsureAdmin() { }
    }

    private sealed class GlossarioFinto : IGlossaryStore
    {
        public List<int> Tolte { get; } = new();
        public Exception? Lancia { get; set; }

        public Task<IReadOnlyList<GlossaryTerm>> ListAsync(string sourceLang, string targetLang, string? cerca = null,
            bool alfabetico = false, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GlossaryTerm>>(new[]
            {
                new GlossaryTerm
                {
                    Id = 7, SourceLang = "it", TargetLang = "en", SourceText = "Mantieni la posizione",
                    SourceKey = "mantieni la posizione", TargetText = "Hold position", CreatedUtc = DateTime.UtcNow,
                    UpdatedByUserId = 704798,
                },
            });

        public Task<bool> UpsertAsync(string sourceLang, string targetLang, string sourceText, string targetText,
            int? userId, CancellationToken ct = default) =>
            Lancia is null ? Task.FromResult(true) : Task.FromException<bool>(Lancia);

        public Task<bool> SeminaVoceAsync(string sourceLang, string targetLang, string sourceText, string targetText,
            CancellationToken ct = default) => throw new NotSupportedException();

        public Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            Tolte.Add(id);
            return Task.FromResult(true);
        }

        public Task<int> ContaAsync(string sourceLang, string targetLang, CancellationToken ct = default) => Task.FromResult(1);
    }

    private readonly GlossarioFinto _glossario = new();

    private IRenderedComponent<GlossarioPage> Apri()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddSingleton<IEditAuthorizationService>(new Editor());
        Services.AddSingleton<IGlossaryStore>(_glossario);
        Services.AddSingleton(ServizioVuoto.Di<ITranslationMemory>());
        Services.AddSingleton(ServizioVuoto.Di<Vipi.Application.Content.IDocumentAdminService>());
        Services.AddSingleton(ServizioVuoto.Di<IStatoTraduzione>());
        Services.AddSingleton(ServizioVuoto.Di<IAttesaTraduzione>());
        Services.AddSingleton(ServizioVuoto.Di<Vipi.Application.Routing.IDocRoutesRegistry>());
        var cut = RenderComponent<GlossarioPage>();
        cut.WaitForAssertion(() => Assert.Contains("Mantieni la posizione", cut.Markup));
        return cut;
    }

    [Fact]
    public void Il_cestino_chiede_conferma_e_solo_dopo_toglie()
    {
        var cut = Apri();

        cut.Find("button[title=Common_Delete]").Click();
        Assert.Empty(_glossario.Tolte);
        Assert.Contains("Gl_DeletePrompt", cut.Markup);

        cut.Find("span.inline-confirm.is-open button.danger").Click();
        Assert.Equal(new[] { 7 }, _glossario.Tolte);
    }

    [Fact]
    public async Task Un_guasto_del_database_al_salvataggio_resta_un_messaggio()
    {
        _glossario.Lancia = new InvalidOperationException("database giù");
        var cut = Apri();
        cut.FindAll("input.app-in").First(i => i.GetAttribute("placeholder") == "Gl_PhSource").Input("Riporta in finale");
        cut.FindAll("input.app-in").First(i => i.GetAttribute("placeholder") == "Gl_PhTarget").Input("Report on final");

        await cut.FindAll("button").First(b => b.TextContent.Contains("Gl_Add")).ClickAsync(new());

        var caduta = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(300));
        if (caduta == Renderer.UnhandledException) Assert.Fail("Circuito caduto: " + await Renderer.UnhandledException);
        cut.WaitForAssertion(() => Assert.Contains("database giù", cut.Markup));
    }
}
