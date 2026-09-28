using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// U-063 (revisione totale 3): il selettore delle aree componeva la lista INTERA dal parametro visto al momento del
/// clic, e l'ospite lo aggiorna solo dopo salvataggio e rilettura. Tre chip cliccate più in fretta del giro (DB
/// remoto) → restava scelta solo l'ultima, senza errore. Ora, finché un salvataggio è in volo, il clic successivo
/// parte da ciò che è già stato mandato.
/// </summary>
public class AreeSceltaInFilaTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public AreeSceltaInFilaTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static SpecialAreaPick Area(string id) => new(id, id, "R", null, null, new[] { "LIBB" });

    [Fact]
    public async Task Due_aree_scelte_mentre_si_salva_restano_scelte_tutte_e_due()
    {
        var mandate = new List<RegulatedSelection>();
        var primo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var cut = RenderComponent<RegulatedAreasEditor>(p => p
            .Add(x => x.Selection, new RegulatedSelection { OwnAuto = false })
            .Add(x => x.OwnPicks, new[] { Area("LIR-A"), Area("LIR-B"), Area("LIR-C") })
            .Add(x => x.Editing, true)
            .Add(x => x.AllowAuto, false)
            // L'ospite: mette in fila il salvataggio, e il parametro resta quello di prima finché non ricarica.
            .Add(x => x.SelectionChanged, EventCallback.Factory.Create<RegulatedSelection>(this, async (RegulatedSelection s) =>
            {
                mandate.Add(s);
                if (mandate.Count == 1) await primo.Task;
            })));

        Task a = null!, b = null!;
        await cut.InvokeAsync(() => { a = cut.FindAll(".area-pick-list button").First(x => x.TextContent.Contains("LIR-A")).ClickAsync(new()); });
        await cut.InvokeAsync(() => { b = cut.FindAll(".area-pick-list button").First(x => x.TextContent.Contains("LIR-B")).ClickAsync(new()); });
        primo.SetResult();
        await cut.InvokeAsync(() => Task.WhenAll(a, b));

        Assert.Equal(2, mandate.Count);
        Assert.Equal(new[] { "LIR-A", "LIR-B" }, mandate[1].OwnIds);
    }

    /// <summary>
    /// U-063, nel vSOP militare: tabelle fisse (nominativi, parcheggi) e alternati componevano la tabella INTERA al
    /// momento del gesto, fuori dal tornello. Ora passano una modifica, che si applica dentro, sullo stato ricaricato.
    /// ⚠️ Presidio sul sorgente: l'editor ha una ventina di servizi, e disegnarlo costerebbe più della correzione.
    /// </summary>
    [Fact]
    public void Tabelle_e_alternati_del_vsop_si_compongono_dentro_il_tornello()
    {
        var testo = File.ReadAllText(Path.Combine(Radice(), "Components", "Doc", "MilSectionsEditor.razor"));

        Assert.Contains("private Task SalvaAlternati(Func<List<MilDiversionPayload.Riga>, bool> cambia)", testo);
        Assert.Contains("Func<IReadOnlyList<IReadOnlyList<string>>, IReadOnlyList<IReadOnlyList<string>>> cambia) => _shell.GuardAsync(", testo);

        var fuori = testo.Split('\n')
            .Where(r => r.Contains("SalvaTabella(\"") && (r.Contains("_nominativi") || r.Contains("_parcheggi")))
            .ToList();
        Assert.True(fuori.Count == 0, "Tabelle composte fuori dal tornello:\n" + string.Join("\n", fuori));
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
