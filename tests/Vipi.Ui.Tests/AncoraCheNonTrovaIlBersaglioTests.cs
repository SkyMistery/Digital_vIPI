using System.Text.RegularExpressions;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// 🔴 Un «#ancora» che non trova il bersaglio deve NON FARE NIENTE — non portare via la pagina.
///
/// <para><b>Perché esiste.</b> Segnalazione dal campo del 9 settembre 2026: riordinando i membri di
/// un'unione, l'editor d'aeroporto di LIBV se ne andava <b>in home</b> senza che nessuno avesse chiesto di
/// navigare. La causa era l'ORDINE di quattro righe in <c>wireAnchors</c>: la ricerca del bersaglio stava
/// <b>prima</b> di <c>preventDefault</c>, e con un <c>return</c> in mezzo — quindi quando la sezione non
/// era (ancora) nel DOM la protezione si sfilava, il clic proseguiva, e l'intercettore di navigazione di
/// Blazor risolveva «#id» contro la <c>&lt;base href="/"&gt;</c> ottenendo «/#id»: la home.</para>
///
/// <para>⚠️ <b>Il commento accanto lo diceva già</b> — «Con &lt;base href="/"&gt; i link "#id" verrebbero
/// risolti come "/#id" (→ home)» — e le righe sotto lo impedivano in tutti i casi tranne quello. Una
/// protezione che si sfila proprio nel caso difficile è il modo in cui una difesa scritta non copre.</para>
///
/// <para>⚠️ <b>Perché un presidio sul TESTO e non un test di comportamento.</b> Quel che va difeso è
/// l'ordine di due istruzioni dentro un gestore <c>capture</c> registrato su <c>document</c>: bUnit non ha
/// un intercettore di navigazione di Blazor da far correre contro, e montare il componente direbbe solo che
/// il markup ha gli anchor giusti — che era vero anche col difetto. È la stessa scelta di
/// <c>EditorTocDragTests.Il_selettore_con_cui_il_JS_accetta_il_rilascio_trova_le_voci_trascinabili</c>.</para>
/// </summary>
public class AncoraCheNonTrovaIlBersaglioTests
{
    private static string Corpo()
    {
        var js = File.ReadAllText(FileNellaWwwroot("vipi-ui.js"));

        // Dev'essere agganciata, non solo definita: senza la chiamata in vipiWireUi non gira mai.
        Assert.Contains("wireAnchors();", js, StringComparison.Ordinal);

        var m = Regex.Match(js, @"function\s+wireAnchors\s*\(\)\s*\{(?<c>.*?)\n    \}", RegexOptions.Singleline);
        Assert.True(m.Success, "wireAnchors non trovata in vipi-ui.js");
        return m.Groups["c"].Value;
    }

    /// <summary>
    /// 🔴 Il cuore: il clic si ferma <b>prima</b> che si vada a cercare il bersaglio. Se un giorno le due
    /// righe si riscambiano, questo test diventa rosso — ed è l'unica cosa che separa «l'ancora non fa
    /// niente» da «l'ancora ti porta in home».
    /// </summary>
    [Fact]
    public void Il_clic_su_un_ancora_si_ferma_PRIMA_di_cercare_il_bersaglio()
    {
        var corpo = Corpo();

        var arresto = corpo.IndexOf("e.preventDefault()", StringComparison.Ordinal);
        var propagazione = corpo.IndexOf("e.stopImmediatePropagation()", StringComparison.Ordinal);
        var ricerca = corpo.IndexOf("getElementById(id)", StringComparison.Ordinal);

        Assert.True(arresto >= 0, "wireAnchors non chiama piu' preventDefault");
        Assert.True(propagazione >= 0, "wireAnchors non chiama piu' stopImmediatePropagation");
        Assert.True(ricerca >= 0, "wireAnchors non cerca piu' il bersaglio con getElementById");

        Assert.True(arresto < ricerca,
            "preventDefault deve venire PRIMA di getElementById: dopo, un bersaglio assente lascia passare il clic e la <base href=\"/\"> porta in home.");
        Assert.True(propagazione < ricerca,
            "stopImmediatePropagation deve venire PRIMA di getElementById, o l'intercettore di Blazor riceve il clic lo stesso.");
    }

    /// <summary>
    /// ⚠️ E fra l'ingresso e l'arresto non deve esserci nessuna via d'uscita che dipenda dal DOM: era
    /// esattamente quella — <c>if (!el) return;</c> messo troppo presto — a produrre il difetto.
    /// Le due uscite lecite riguardano il <b>link</b>, non il bersaglio: «non è un'ancora» e «l'ancora è vuota».
    /// </summary>
    [Fact]
    public void Prima_dell_arresto_non_si_esce_mai_per_colpa_del_DOM()
    {
        var corpo = Corpo();

        // Solo il GESTORE del clic: la guardia `if (anchorsWired) return;` sta fuori e non c'entra.
        var gestore = corpo[corpo.IndexOf("function (e) {", StringComparison.Ordinal)..];
        var prima = gestore[..gestore.IndexOf("e.preventDefault()", StringComparison.Ordinal)];

        Assert.DoesNotContain("getElementById", prima, StringComparison.Ordinal);
        Assert.DoesNotContain("querySelector", prima, StringComparison.Ordinal);

        // ⚠️ Si contano le uscite nel CODICE, non nella prosa: i commenti accanto raccontano il difetto e
        // nominano `return` a parole — contarli renderebbe il presidio rosso per una spiegazione scritta bene,
        // che è il modo più sicuro di far cancellare un presidio invece di leggerlo.
        var soloCodice = Regex.Replace(prima, "//.*", "");
        var uscite = Regex.Matches(soloCodice, @"\breturn\b").Count;
        Assert.True(uscite == 2, $"nel gestore, prima di preventDefault, ci sono {uscite} `return` invece di 2 (niente ancora / niente id).");
    }

    private static string FileNellaWwwroot(string nome)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui", "wwwroot", nome);
            if (File.Exists(c)) return c;
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"{nome} non trovato risalendo da {AppContext.BaseDirectory}");
    }
}
