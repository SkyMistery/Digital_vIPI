using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La guardia di pre-pubblicazione nei tre editor che possono ospitare un'unione (carta
/// <c>docs/feature/2026-09-03-documenti-uniti.md</c> §5b).
///
/// <para>
/// 🔴 Trovato in supervisione il 3 settembre 2026. Il pannello di pubblicazione sta <b>solo
/// sull'ospite</b>, e con lui la sua <c>BeforePublishAsync</c>: l'avviso «sezioni non salvate» di una vIPI
/// d'aeroporto <b>non veniva chiesto</b> quando quell'aeroporto era un MEMBRO. Si pubblicava la sua
/// fotografia senza le modifiche aperte, in silenzio — e la fotografia è quel che il pubblico legge.
/// </para>
///
/// <para>
/// ⚠️ È un controllo sul <b>sorgente</b> e non su un render, di proposito: la domanda è «questa pagina
/// PASSA la guardia al pannello?», e una guardia che nessuno passa non fallisce nessun render — non fa
/// semplicemente niente. È lo stesso motivo per cui le tre colonne si controllano sul sorgente.
/// </para>
/// </summary>
public sealed class GuardiaDiPrepubblicazioneTests
{
    /// <summary>I tre editor che possono ospitare un'unione. ⚠️ vLOA e vIPI ACC restano fuori: le loro
    /// famiglie non sono unibili (<c>DocumentUnionService.FamiglieAmmesse</c>).</summary>
    public static TheoryData<string> Ospiti() => new()
    {
        "Pages/AeroportoEditorPage.razor",
        "Pages/AppEditorPage.razor",
        "Pages/MilEditorPage.razor",
    };

    [Theory]
    [MemberData(nameof(Ospiti))]
    public void Ogni_ospite_passa_la_guardia_al_pannello_e_la_guardia_chiede_ai_MEMBRI(string relativo)
    {
        var sorgente = File.ReadAllText(Path.Combine(Radice(), relativo.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains("BeforePublishAsync=\"Prepubblicazione\"", sorgente, StringComparison.Ordinal);
        // ⚠️ E la guardia deve chiedere ANCHE ai membri: una `Prepubblicazione` che consulta solo se
        // stessa e' esattamente il difetto di prima, con un nome nuovo.
        Assert.Contains("_membri is null || await _membri.BeforePublishAsync()", sorgente, StringComparison.Ordinal);
    }

    /// <summary>⚠️ E la chiamata all'editor proprio passa dall'INTERFACCIA: il membro di default di
    /// <c>IMembroEditor</c> non si vede dal tipo concreto, e con la chiamata diretta due delle tre pagine
    /// non compilano affatto. Scritto qui perché la prossima famiglia lo eredita senza sapere il perché.</summary>
    [Theory]
    [MemberData(nameof(Ospiti))]
    public void La_guardia_dell_ospite_si_chiede_attraverso_l_interfaccia(string relativo)
    {
        var sorgente = File.ReadAllText(Path.Combine(Radice(), relativo.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains("_editor is IMembroEditor", sorgente, StringComparison.Ordinal);
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
