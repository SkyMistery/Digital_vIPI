using System.Text.RegularExpressions;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// T-044 (revisione del 13 settembre 2026): <c>vipiLive.unsubscribe(null)</c> svuotava TUTTI i sottoscrittori —
/// una vista live smontata col subscribe in volo staccava anche il badge della barra. Le due metà: il JS ignora il
/// null, e nessun componente lo manda.
/// </summary>
public class VipiLiveUnsubscribeTests
{
    [Fact]
    public void Il_JS_non_svuota_tutto_su_un_id_nullo()
    {
        var js = File.ReadAllText(Path.Combine(RadiceUi(), "wwwroot", "vipi-live.js"));
        Assert.DoesNotContain("_subs.clear()", js);
    }

    [Theory]
    [InlineData("Pages/LivePage.razor", "_liveSubId")]
    [InlineData("Components/LiveBadge.razor", "_subId")]
    public void Nessun_componente_manda_un_id_che_puo_essere_nullo(string file, string campo)
    {
        var testo = File.ReadAllText(Path.Combine(RadiceUi(), file));
        Assert.DoesNotMatch(new Regex($@"""vipiLive\.unsubscribe"",\s*{Regex.Escape(campo)}\)"), testo);
    }

    private static string RadiceUi()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui");
            if (Directory.Exists(Path.Combine(c, "Pages"))) return c;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("src/Vipi.Ui");
    }
}
