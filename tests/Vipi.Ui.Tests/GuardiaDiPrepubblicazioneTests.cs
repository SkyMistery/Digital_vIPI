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

    /// <summary>
    /// 🔴 <b>L'elenco di governo deve dire QUANTI documenti quel tasto pubblichera'.</b> Trovato
    /// guidando l'app il 3 settembre 2026, subito dopo aver corretto il difetto grosso: da
    /// <c>/services/vsop/{acc}/versions</c> la pubblicazione ora esce accoppiata — giusto — ma la domanda
    /// diceva ancora «il documento diventa pubblico» al <b>singolare</b> mentre ne mandava fuori due.
    /// <para>⚠️ È la terza volta in questo giro che lo stesso conteggio sbaglia: prima la domanda
    /// dell'annullamento sottostimava, poi sovrastimava, qui taceva. Il conto va sempre da
    /// <c>Unito(d)</c>, mai dedotto.</para>
    /// </summary>
    [Fact]
    public void L_elenco_di_governo_dice_quanti_documenti_pubblica()
    {
        var sorgente = File.ReadAllText(Path.Combine(Radice(), Path.Combine("Pages", "VersioniPage.razor")));

        // la domanda del «pubblica ora» e il titolo del «pubblica al ciclo» passano dall'avviso, non dal
        // testo nudo della chiave.
        Assert.Contains("Prompt=\"@PromptPubblicaOra(d)\"", sorgente, StringComparison.Ordinal);
        Assert.Contains("title=\"@AvvisoUnione(d)\"", sorgente, StringComparison.Ordinal);
        // e l'avviso conta i membri con l'indexer che INTERPOLA
        Assert.Contains("L[\"Rel_UnionTitle\", n].Value", sorgente, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Format(L[\"Rel_UnionTitle\"]", sorgente, StringComparison.Ordinal);
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
