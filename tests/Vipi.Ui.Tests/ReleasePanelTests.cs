using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Services;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// <see cref="ReleasePanel"/> è il pannello di pubblicazione AIRAC condiviso dai tre editor (ACC, APP, aeroporto)
/// e non era coperto da test. I parametri opt-in (ShowDiff, AllowCancel, PreviewUrlFactory, BeforePublishAsync)
/// decidono cosa vede l'utente, quindi qui si fissa il comportamento di ognuno.
/// </summary>
public class ReleasePanelTests : TestContext
{
    private static ReleaseInfo Rel(int id, bool effective = false,
        ReleaseStatus status = ReleaseStatus.Scheduled, string cycle = "2608") =>
        new(id, VersionNumber: 3, ReleaseAiracCycle: cycle,
            ReleaseEffectiveUtc: new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc),
            Status: status, CreatedByUserId: 704798,
            CreatedUtc: new DateTime(2026, 7, 30, 9, 0, 0, DateTimeKind.Utc),
            Note: "nota di prova", IsEffectiveNow: effective);

    private sealed class FakeReleases : IReleaseService
    {
        public List<ReleaseInfo> Releases { get; } = new();
        public ReleaseDiff Diff { get; set; } =
            new(true, "2607", new[] { new ReleaseDiffRow("Separazioni", ReleaseChangeKind.Modified, 3, 5) });

        public int Published, PublishedNow, Canceled, DiffCalls;
        public string? LastCycle, LastNote;

        public Task<IReadOnlyList<ReleaseInfo>> ListAsync(ReleaseTargetType type, string key, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ReleaseInfo>>(Releases.ToList());

        public Task PublishAsync(ReleaseTargetType type, string key, string releaseCycle, string? note, CancellationToken ct = default)
        {
            Published++; LastCycle = releaseCycle; LastNote = note;
            return Task.CompletedTask;
        }

        public Task PublishNowAsync(ReleaseTargetType type, string key, string? note, CancellationToken ct = default)
        {
            PublishedNow++; LastNote = note;
            return Task.CompletedTask;
        }

        public Task CancelReleaseAsync(int releaseId, CancellationToken ct = default)
        {
            Canceled++;
            return Task.CompletedTask;
        }

        public Task<ReleaseDiff> DiffAsync(int releaseId, CancellationToken ct = default)
        {
            DiffCalls++;
            return Task.FromResult(Diff);
        }

        public string CurrentCycle() => "2607";

        public IReadOnlyList<AiracCycleInfo> UpcomingCycles(int count) => new[]
        {
            new AiracCycleInfo("2608", new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc)),
            new AiracCycleInfo("2609", new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc)),
        };

        // Non usati dal pannello.
        public Task<int> BackfillMissingReleasesAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<ReleasePreview?> GetPreviewAsync(int releaseId, CancellationToken ct = default) => Task.FromResult<ReleasePreview?>(null);
        public Task<ReleaseLocation?> GetLocationAsync(int releaseId, CancellationToken ct = default) => Task.FromResult<ReleaseLocation?>(null);
        public Task<IReadOnlyDictionary<(ReleaseTargetType Type, string Key), ReleaseSummary>> SummariesAsync(
            IReadOnlyList<(ReleaseTargetType Type, string Key)> targets, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<(ReleaseTargetType, string), ReleaseSummary>>(
                new Dictionary<(ReleaseTargetType, string), ReleaseSummary>());
        public Task<int> PruneAllAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    /// <summary>Localizer che rende la chiave stessa: le asserzioni restano stabili al variare delle traduzioni.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Enumerable.Empty<LocalizedString>();
    }

    private FakeReleases Arrange(params ReleaseInfo[] releases)
    {
        var fake = new FakeReleases();
        fake.Releases.AddRange(releases);
        Services.AddSingleton<IReleaseService>(fake);
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        return fake;
    }

    private IRenderedComponent<ReleasePanel> Render(bool showDiff = false, bool allowCancel = false,
        Func<int, string?>? previewUrl = null, Func<Task<bool>>? beforePublish = null, Action? onPublished = null) =>
        RenderComponent<ReleasePanel>(p =>
        {
            p.Add(x => x.Target, ReleaseTargetType.App);
            p.Add(x => x.Key, "LIRP_APP");
            p.Add(x => x.ShowDiff, showDiff);
            p.Add(x => x.AllowCancel, allowCancel);
            if (previewUrl is not null) p.Add(x => x.PreviewUrlFactory, previewUrl);
            if (beforePublish is not null) p.Add(x => x.BeforePublishAsync, beforePublish);
            if (onPublished is not null) p.Add(x => x.Published, onPublished);
        });

    [Fact]
    public void Senza_Release_Mostra_Solo_I_Comandi_Di_Pubblicazione()
    {
        Arrange();

        var cut = Render();

        Assert.Empty(cut.FindAll(".ver-row"));
        Assert.Equal(2, cut.FindAll("button").Count);   // pubblica ora + programma al ciclo
    }

    [Fact]
    public void Elenca_Le_Release_E_Marca_Quella_Effettiva()
    {
        Arrange(Rel(1, effective: true, status: ReleaseStatus.Effective), Rel(2));

        var cut = Render();

        Assert.Equal(2, cut.FindAll(".ver-row").Count);
        Assert.Single(cut.FindAll(".ver-row.current"));
        Assert.Contains("nota di prova", cut.Markup);
        Assert.Contains("704798", cut.Markup);
    }

    [Fact]
    public void Il_Numero_Di_Versione_Viene_Valutato_Non_Stampato_Come_Testo()
    {
        // Trovato guidando l'app: scritto «v@r.VersionNumber», Razor legge «v@r.…» come indirizzo email (la @ fra
        // due caratteri non-spazio non apre un'espressione) e lo emette LETTERALE — a schermo compariva
        // «rel. v@r.VersionNumber».
        // L'etichetta è passata dal localizer (chiave Rel_VersionLabel, «rilascio #{0}»): con la forma
        // string.Format(L[chiave].Value, n) il numero NON arriverebbe mai al testo, perché quell'indexer non
        // interpola. Serve l'overload L[chiave, n], che è quello che formatta.
        Arrange(Rel(1));

        var cut = Render();

        Assert.DoesNotContain("@r.VersionNumber", cut.Markup);
        Assert.Contains("Rel_VersionLabel 3", cut.Markup);   // KeyLocalizer: chiave + argomenti; VersionNumber = 3 in Rel()
    }

    [Fact]
    public void Diff_E_Annulla_Sono_Opt_In()
    {
        Arrange(Rel(1));

        var senza = Render();
        Assert.DoesNotContain("Rel_Diff", senza.Markup);

        var con = Render(showDiff: true, allowCancel: true);
        // 2 di pubblicazione + differenze + annulla.
        Assert.Equal(4, con.FindAll("button").Count);
    }

    [Fact]
    public void Il_Toggle_Differenze_Carica_E_Rende_Il_Diff_Una_Volta_Sola()
    {
        var fake = Arrange(Rel(7));
        var cut = Render(showDiff: true);

        cut.FindAll("button").First(b => b.TextContent.Contains("Diff")).Click();

        Assert.Equal(1, fake.DiffCalls);
        Assert.Contains("Separazioni", cut.Markup);
        Assert.Contains("Rel_BaselineCycle", cut.Markup);      // etichetta della baseline, composta dalla UI

        // Chiudi e riapri: il diff è già in cache, non si richiama il service.
        cut.FindAll("button").First(b => b.TextContent.Contains("Diff")).Click();
        Assert.DoesNotContain("Separazioni", cut.Markup);
        cut.FindAll("button").First(b => b.TextContent.Contains("Diff")).Click();
        Assert.Equal(1, fake.DiffCalls);
    }

    [Fact]
    public void Pubblica_Ora_Invia_La_Nota_E_Ricarica()
    {
        var fake = Arrange();
        var cut = Render();

        cut.Find("input.app-in").Change("motivo della pubblicazione");
        cut.FindAll("button").First(b => b.TextContent.Contains("PublishNow")).Click();

        Assert.Equal(1, fake.PublishedNow);
        Assert.Equal("motivo della pubblicazione", fake.LastNote);
    }

    [Fact]
    public void Published_Avvisa_L_Host_Dopo_Ogni_Pubblicazione()
    {
        // «Pubblica ora» promuove la bozza a versione pubblicata, ma il pannello ricarica solo le PROPRIE release:
        // senza questo avviso l'editor ospitante continuava a mostrare «Bozza vN» a pubblicazione avvenuta
        // (visto su /services/vsop/libb/editor).
        var avvisi = 0;
        Arrange();
        var cut = Render(onPublished: () => avvisi++);

        cut.FindAll("button").First(b => b.TextContent.Contains("PublishNow")).Click();
        Assert.Equal(1, avvisi);

        cut.FindAll("button").First(b => b.TextContent.Contains("ScheduleAtCycle")).Click();
        Assert.Equal(2, avvisi);   // anche lo schedulato: cambia la timeline che l'host può mostrare
    }

    [Fact]
    public void Published_Non_Avvisa_Se_BeforePublishAsync_Annulla()
    {
        var avvisi = 0;
        var fake = Arrange();
        var cut = Render(beforePublish: () => Task.FromResult(false), onPublished: () => avvisi++);

        cut.FindAll("button").First(b => b.TextContent.Contains("PublishNow")).Click();

        Assert.Equal(0, fake.PublishedNow);   // niente pubblicazione…
        Assert.Equal(0, avvisi);              // …quindi niente avviso: l'host non deve ricaricare per nulla
    }

    [Fact]
    public void Programma_Al_Ciclo_Usa_Il_Ciclo_Selezionato()
    {
        var fake = Arrange();
        var cut = Render();

        cut.Find("select.app-in").Change("2609");
        cut.FindAll("button").First(b => b.TextContent.Contains("ScheduleAtCycle")).Click();

        Assert.Equal(1, fake.Published);
        Assert.Equal("2609", fake.LastCycle);
    }

    [Fact]
    public void BeforePublishAsync_Che_Ritorna_False_Annulla_La_Pubblicazione()
    {
        var fake = Arrange();
        var chiamato = false;
        var cut = Render(beforePublish: () => { chiamato = true; return Task.FromResult(false); });

        cut.FindAll("button").First(b => b.TextContent.Contains("PublishNow")).Click();

        Assert.True(chiamato);
        Assert.Equal(0, fake.PublishedNow);   // il passo preliminare ha annullato
    }

    [Fact]
    public void BeforePublishAsync_Che_Ritorna_True_Prosegue()
    {
        var fake = Arrange();
        var cut = Render(beforePublish: () => Task.FromResult(true));

        cut.FindAll("button").First(b => b.TextContent.Contains("PublishNow")).Click();

        Assert.Equal(1, fake.PublishedNow);
    }

    /// <summary>
    /// ⚠️ La conferma e' IN LINEA, non il `confirm()` nativo del browser. Quello bloccava il circuito Blazor
    /// finche' non si rispondeva, e il testo utile — QUALE release si sta annullando — finiva in una
    /// finestrella di sistema invece che accanto al tasto. Questi due test presidiavano il `confirm`: ora
    /// presidiano il gesto vero, cioe' che il primo clic CHIEDE e non fa niente.
    /// </summary>
    [Fact]
    public void Annulla_Release_Chiede_Prima_E_Il_Primo_Clic_Non_Annulla_Niente()
    {
        var fake = Arrange(Rel(9));
        var cut = Render(allowCancel: true);

        cut.FindAll("button").First(b => b.TextContent.Contains("✕")).Click();

        Assert.Equal(0, fake.Canceled);                       // il primo clic apre la domanda
        Assert.Contains("Rel_CancelPrompt", cut.Markup);      // ...e la domanda NOMINA la release
        Assert.Contains("2608", cut.Markup);   // il ciclo del bersaglio finto
    }

    [Fact]
    public void Annulla_Release_Procede_Alla_Conferma()
    {
        var fake = Arrange(Rel(9));
        var cut = Render(allowCancel: true);

        cut.FindAll("button").First(b => b.TextContent.Contains("✕")).Click();
        // Il tasto di conferma dell'InlineConfirm, non quello che ha aperto la domanda.
        cut.FindAll("button").First(b => b.TextContent.Contains("Rel_CancelYes")).Click();

        Assert.Equal(1, fake.Canceled);
    }

    [Fact]
    public void La_Factory_Di_Anteprima_Costruisce_Il_Link_Della_Pagina_Ospite()
    {
        Arrange(Rel(42));

        var cut = Render(previewUrl: id => $"/services/vsop/lirr/apps/vipi?app=LIRP_APP&as=rel:{id}");

        Assert.Equal("/services/vsop/lirr/apps/vipi?app=LIRP_APP&as=rel:42", cut.Find("a.btn").GetAttribute("href"));
    }

    [Fact]
    public void Senza_Factory_Il_Target_App_Non_Espone_Anteprima()
    {
        // Il default per-target copre solo AccVipi: per gli altri l'host deve passare la factory (come fanno
        // AppEditorPage e AeroportoEditorPage), altrimenti nessun link — meglio assente che rotto.
        Arrange(Rel(42));

        var cut = Render();

        Assert.Empty(cut.FindAll("a.btn"));
    }

    [Fact]
    public void Il_Pannello_Espone_L_Ancora_Del_Tour_Di_Onboarding()
    {
        Arrange();

        var cut = Render();

        Assert.NotNull(cut.Find("[data-tour=release]"));
    }

    // ---- doc 13 §3i: l'involucro della sezione lo porta il pannello, non ogni editor ----

    [Fact]
    public void The_panel_carries_its_own_anchor_title_and_help()
    {
        Arrange();

        var cut = Render();

        // L'ancora serve alla voce di menu degli editor: era ricostruita a mano in due editor su quattro e
        // mancava del tutto in quello della vIPI ACC.
        Assert.Contains($"id=\"{ReleasePanel.SectionAnchor}\"", cut.Markup);
        Assert.Contains("Rel_SectionTitle", cut.Markup);
        Assert.Contains("Rel_SectionHelp", cut.Markup);
    }

    [Fact]
    public void The_header_can_be_left_to_the_host_that_already_has_one()
    {
        Arrange();

        var cut = RenderComponent<ReleasePanel>(p => p
            .Add(x => x.Target, ReleaseTargetType.Airport)
            .Add(x => x.Key, "LIRF")
            .Add(x => x.ShowSectionHeader, false));

        Assert.DoesNotContain("Rel_SectionHelp", cut.Markup);
        Assert.Contains($"id=\"{ReleasePanel.SectionAnchor}\"", cut.Markup);   // l'ancora resta
    }
}
