using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La sezione NOTAM negli editor (committente, 12 ottobre 2026: «la sezione NOTAM deve essere visibile nell'editor,
/// come lo è la sezione METAR/TAF»; carta <c>docs/feature/2026-10-12-notam-sui-documenti.md</c> §8).
/// </summary>
public class NotamNellEditorTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    /// <summary>Risponde coi NOTAM dati a chi chiede, e conta quante volte glielo chiedono.</summary>
    private sealed class Lettore(params NotamAvviso[] avvisi) : INotamDeiDocumenti
    {
        public int Letture;

        public Task<IReadOnlyList<NotamAvviso>> PerAsync(AmbitoNotam ambito, CancellationToken ct = default)
        {
            Letture++;
            return Task.FromResult<IReadOnlyList<NotamAvviso>>(avvisi);
        }

        public Task<IReadOnlyList<IReadOnlyList<NotamAvviso>>> PerAsync(IReadOnlyList<AmbitoNotam> ambiti,
                                                                       CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private static readonly NotamAvviso Napoli = new(
        1, "IT-A010/26", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc),
        "E) NAPOLI TWR: PROVA", false, new[] { "LIRN" }, Array.Empty<string>());

    private Lettore Registra(params NotamAvviso[] avvisi)
    {
        var lettore = new Lettore(avvisi);
        // ⚠️ Scoped: il componente lo prende dal suo scope PROPRIO, non dal circuito.
        Services.AddScoped<INotamDeiDocumenti>(_ => lettore);
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<StringheDelSito>();
        return lettore;
    }

    private IRenderedComponent<NotamNellEditor> Monta(string chiave = "apt:LIRN") =>
        RenderComponent<NotamNellEditor>(p => p
            .Add(x => x.Chiave, chiave)
            .Add(x => x.Ambito, AmbitoNotam.Aeroporti(new[] { "LIRN" })));

    /// <summary>
    /// Come il meteo: la sezione c'è SEMPRE, anche senza NOTAM. Chi redige deve sapere che in quel punto del
    /// documento pubblico compare — e adesso lo dice chiaro, invece di non mostrare niente.
    /// </summary>
    [Fact]
    public void Senza_NOTAM_la_sezione_c_e_lo_stesso_e_lo_dice()
    {
        Registra();

        var cut = Monta();

        var nota = cut.Find(".notam-ed > .callout.info");
        Assert.Contains("NotamEd_Title", nota.TextContent);
        Assert.Contains("NotamEd_Body", nota.TextContent);
        Assert.Contains("NotamEd_None", nota.TextContent);
        Assert.Empty(cut.FindAll(".notam-box"));
    }

    /// <summary>Con un NOTAM: la nota, e sotto il riquadro com'è nel documento pubblico.</summary>
    [Fact]
    public void Con_un_NOTAM_mostra_il_riquadro_del_documento()
    {
        Registra(Napoli);

        var cut = Monta();

        Assert.Contains("NotamEd_Body", cut.Find(".notam-ed > .callout.info").TextContent);
        Assert.DoesNotContain("NotamEd_None", cut.Markup);
        Assert.Equal("IT-A010/26", cut.Find(".notam-ed > .notam-box .notam-num").TextContent);
    }

    /// <summary>
    /// ⚠️ Un editor si ridisegna a ogni tasto, e l'ambito è un oggetto nuovo a ogni render: l'archivio si rilegge
    /// solo quando cambia la CHIAVE. Senza, scrivere una riga di testo farebbe decine di letture uguali.
    /// </summary>
    [Fact]
    public void Rilegge_solo_quando_cambia_la_chiave()
    {
        var lettore = Registra(Napoli);
        var cut = Monta("blk:LIMJ_WS0_APP");
        Assert.Equal(1, lettore.Letture);

        cut.SetParametersAndRender(p => p
            .Add(x => x.Chiave, "blk:LIMJ_WS0_APP")
            .Add(x => x.Ambito, AmbitoNotam.BloccoApp(new[] { "LIMJ_WS0_APP" })));
        Assert.Equal(1, lettore.Letture);

        // Aggiunto un APP al blocco: la chiave cambia, e si rilegge.
        cut.SetParametersAndRender(p => p
            .Add(x => x.Chiave, "blk:LIMJ_WS0_APP,LIMF_WN0_APP")
            .Add(x => x.Ambito, AmbitoNotam.BloccoApp(new[] { "LIMJ_WS0_APP", "LIMF_WN0_APP" })));
        Assert.Equal(2, lettore.Letture);
    }

    // ---- Dove la montano gli editor --------------------------------------------------------------------

    /// <summary>
    /// I quattro editor la montano con lo STESSO ambito della loro pagina pubblica: lo scalo per aeroporto e vSOP,
    /// lo scalo dell'ente per l'APP, l'ACC in testa alla vIPI di ACC e il blocco dentro ogni gruppo APP. Guardia
    /// sul sorgente: un editor che la perde non dà errore, semplicemente non la mostra.
    /// </summary>
    [Theory]
    [InlineData("Components/Doc/AirportSectionsEditor.razor", "Chiave=\"@($\"apt:{_icao}\")\" Ambito=\"@AmbitoNotam.Aeroporti(new[] { _icao })\"")]
    [InlineData("Components/Doc/MilSectionsEditor.razor", "Chiave=\"@($\"apt:{_icao}\")\" Ambito=\"@AmbitoNotam.Aeroporti(new[] { _icao })\"")]
    [InlineData("Components/Doc/AppSectionsEditor.razor", "Ambito=\"@AmbitoNotam.Aeroporti(new[] { AmbitoNotam.AeroportoDi(_app) })\"")]
    [InlineData("Pages/AccEditorPage.razor", "Ambito=\"@AmbitoNotam.Acc(_accCode, Array.Empty<string>())\"")]
    [InlineData("Pages/AccEditorPage.razor", "Ambito=\"@AmbitoNotam.BloccoApp(blk.Block.MemberCallsigns)\"")]
    public void Ogni_editor_monta_la_sezione_con_l_ambito_della_sua_pagina(string relativo, string montaggio)
    {
        var sorgente = Leggi(relativo);

        Assert.Single(Regex.Matches(sorgente, Regex.Escape(montaggio)));
        Assert.Contains("<NotamNellEditor ", sorgente, StringComparison.Ordinal);
    }

    /// <summary>Nei tre editor a sezioni sta IN TESTA alla colonna, dove sta nel documento: nello slot che
    /// l'editor delle sezioni disegna prima di ogni altra cosa.</summary>
    [Theory]
    [InlineData("Components/Doc/AirportSectionsEditor.razor")]
    [InlineData("Components/Doc/MilSectionsEditor.razor")]
    [InlineData("Components/Doc/AppSectionsEditor.razor")]
    public void Negli_editor_a_sezioni_sta_nello_slot_di_testa(string relativo)
    {
        var sorgente = Leggi(relativo);
        var apre = sorgente.IndexOf("<BeforeSections>", StringComparison.Ordinal);
        var chiude = sorgente.IndexOf("</BeforeSections>", StringComparison.Ordinal);

        Assert.InRange(sorgente.IndexOf("<NotamNellEditor ", StringComparison.Ordinal), apre, chiude);
    }

    [Fact]
    public void L_editor_delle_sezioni_disegna_lo_slot_di_testa_prima_di_tutto()
    {
        var sorgente = Leggi("Components/DocumentSectionsEditor.razor");
        var corpo = sorgente.IndexOf("private RenderFragment SectionsBody => __builder =>", StringComparison.Ordinal);
        var slot = sorgente.IndexOf("@BeforeSections", corpo, StringComparison.Ordinal);

        Assert.InRange(slot, corpo, sorgente.IndexOf("_sidDaRivedere.Count > 0", corpo, StringComparison.Ordinal));
    }

    /// <summary>Nella vIPI di ACC il blocco di un gruppo APP porta la sua sezione sopra le sue sezioni, e solo i
    /// gruppi APP: il blocco dei settori di aerovia non ha uno scalo.</summary>
    [Fact]
    public void Nell_editor_della_vIPI_di_ACC_il_blocco_APP_ha_la_sua_sezione()
    {
        var sorgente = Leggi("Pages/AccEditorPage.razor");
        var blocco = sorgente.IndexOf("AmbitoNotam.BloccoApp(blk.Block.MemberCallsigns)", StringComparison.Ordinal);
        var prima = sorgente.LastIndexOf("@if (isGroup)", blocco, StringComparison.Ordinal);
        var sezioni = sorgente.IndexOf("RootSections=\"blockSection.Children\"", blocco, StringComparison.Ordinal);

        Assert.InRange(blocco - prima, 0, 400);
        Assert.True(sezioni > blocco);
        // In testa, quella dell'ACC: prima del ciclo dei blocchi.
        Assert.InRange(sorgente.IndexOf("AmbitoNotam.Acc(_accCode, Array.Empty<string>())", StringComparison.Ordinal),
            0, sorgente.IndexOf("@foreach (var bs in _shell.Doc.Sections)", StringComparison.Ordinal));
    }

    private static string Leggi(string relativo)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Vipi.Ui")))
            dir = dir.Parent;
        var radice = dir?.FullName ?? throw new DirectoryNotFoundException("Radice del repo non trovata.");
        return File.ReadAllText(Path.Combine(radice, "src", "Vipi.Ui", relativo.Replace('/', Path.DirectorySeparatorChar)));
    }
}
