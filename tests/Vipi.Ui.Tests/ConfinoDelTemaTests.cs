using System.Text;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// <b>Le regole del tema stanno sotto <c>.vipi-root</c>, e non è più un modo di dire.</b>
///
/// <para>ADR-0005 D3 promette che «tutte le regole del tema sono confinate sotto il contenitore
/// <c>.vipi-root</c>», e <c>integration.md</c> lo ripete al sito ospitante come garanzia. La revisione del
/// 6 settembre 2026 l'ha misurato: su 2 031 regole ne erano confinate <b>48</b>, e le altre stavano su
/// nomi da collisione garantita — <c>.wrap</c>, <c>.block</c>, <c>.pill</c>, <c>.toc</c>, <c>.topbar</c>,
/// e <c>details</c>, che è un selettore d'elemento. Con il <c>&lt;link&gt;</c> globale che la guida chiede
/// all'host, ogni <c>&lt;details&gt;</c> del SITO cambiava aspetto (R-008).</para>
///
/// <para>⚠️ <b>Il confino si scrive <c>:where(.vipi-root)</c></b>: specificità zero, quindi i pesi relativi
/// del foglio restano quelli di prima. Col prefisso nudo il foglio si riordina — provato a schermo: sei
/// pagine su otto cominciavano a scorrere in orizzontale.</para>
///
/// <para>Questo test è la garanzia al posto della promessa: una regola nuova scritta senza contenitore lo
/// dice qui, non sul sito di qualcun altro.</para>
/// </summary>
public class ConfinoDelTemaTests
{
    /// <summary>
    /// Chi può stare in cima senza contenitore, e perché.
    /// <list type="bullet">
    /// <item><c>:root</c> — porta le VARIABILI, che è giusto stiano in alto (le legge anche chi sta fuori,
    /// per esempio il riquadro della riconnessione).</item>
    /// <item><c>.vipi-rec</c> — quel riquadro: sta in <c>App.razor</c> FUORI dal layout, perché Blazor cerca
    /// <c>#components-reconnect-modal</c> per nome e a circuito morto la pagina potrebbe non esserci.</item>
    /// <item><c>.vipi-root</c> e <c>.vipi-dense .vipi-root</c> — il contenitore stesso e chi lo governa da un
    /// antenato: confinarli darebbe selettori che non trovano niente.</item>
    /// </list>
    /// </summary>
    private static bool Confinata(string selettore)
    {
        var s = selettore.Trim();
        return s.Length == 0
            || s.Contains(".vipi-root", StringComparison.Ordinal)
            || s.StartsWith(":root", StringComparison.Ordinal)
            || s.StartsWith(".vipi-rec", StringComparison.Ordinal);
    }

    /// <summary>
    /// Le due eccezioni del <b>foglio di stampa</b>, dichiarate in testa a quel file: <c>@page</c> e il reset
    /// di zoom e fondo su <c>html</c>/<c>body</c>. Sono globali per natura — <c>@page</c> non ha un elemento
    /// su cui stare, e lo zoom vive sull'<c>html</c> — e in stampa valgono anche per l'host.
    /// <para>⚠️ È il prezzo dichiarato di un foglio caricato con <c>media="print"</c>, non una dimenticanza:
    /// per questo l'elenco è chiuso e sta qui, invece di essere una regola generale che copre tutto.</para>
    /// </summary>
    private static readonly HashSet<string> EccezioniDiStampa =
        new(StringComparer.Ordinal) { "@page", "html", "body" };

    [Theory]
    [InlineData("vipi-theme.css")]
    [InlineData("vipi-aor3d.css")]
    [InlineData("vipi-print.css")]
    [InlineData("vipi-swapper.css")]
    public void Ogni_regola_del_foglio_sta_sotto_il_contenitore(string foglio)
    {
        var css = File.ReadAllText(Path.Combine(Radice(), "wwwroot", foglio));
        var fuori = new List<string>();

        Percorri(css, 0, css.Length, fuori);
        fuori.RemoveAll(s => foglio == "vipi-print.css" && EccezioniDiStampa.Contains(s));

        Assert.True(fuori.Count == 0,
            $"In {foglio} ci sono {fuori.Count} regole fuori dal contenitore del modulo:\n  " +
            string.Join("\n  ", fuori.Take(20)) +
            "\n\nSu un host che carica il foglio con un <link> globale queste regole colpiscono il SITO " +
            "(ADR-0005 D3). Si confinano scrivendo `:where(.vipi-root) ` davanti al selettore: `:where()` " +
            "ha specificità zero, quindi non cambia chi vince.");
    }

    /// <summary>
    /// ⚠️ E il contenitore si scrive con <c>:where()</c>: il prefisso nudo confina lo stesso, ma alza di una
    /// classe la specificità di ogni regola e riordina il foglio. La misura che lo dice sta nell'ADR.
    /// </summary>
    [Fact]
    public void Il_contenitore_non_alza_la_specificita()
    {
        var css = File.ReadAllText(Path.Combine(Radice(), "wwwroot", "vipi-theme.css"));

        // Le poche regole scritte col prefisso nudo sono quelle nate così prima del 7 settembre 2026 e
        // volute: il contenitore stesso, e i blocchi dove serve vincere su una regola d'elemento.
        var nude = System.Text.RegularExpressions.Regex.Matches(css, @"(?m)^\.vipi-root [^{,]+\{")
            .Select(m => m.Value.Trim())
            .ToList();

        Assert.True(nude.Count <= 20,
            $"Regole col contenitore NUDO: {nude.Count}. Il confino si scrive `:where(.vipi-root)`, che " +
            "non alza la specificità:\n  " + string.Join("\n  ", nude.Take(20)));
    }

    // ---- il lettore di graffe: quel che serve, non un parser CSS ----------------------------------------

    private static readonly string[] AtSenzaSelettori =
        { "@keyframes", "@-webkit-keyframes", "@font-face", "@property", "@import", "@charset" };

    private static readonly string[] AtContenitori = { "@media", "@supports", "@layer", "@container" };

    private static void Percorri(string css, int inizio, int fine, List<string> fuori)
    {
        var i = inizio;
        var prologo = inizio;
        var profondita = 0;
        var apertura = -1;

        while (i < fine)
        {
            var c = css[i];

            if (c == '/' && i + 1 < fine && css[i + 1] == '*')
            {
                var j = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = j < 0 ? fine : j + 2;
                continue;
            }

            if (c is '"' or '\'')
            {
                var j = i + 1;
                while (j < fine && css[j] != c) j += css[j] == '\\' ? 2 : 1;
                i = j + 1;
                continue;
            }

            if (c == '{')
            {
                if (profondita == 0) apertura = i;
                profondita++;
            }
            else if (c == '}')
            {
                profondita--;
                if (profondita == 0)
                {
                    var testa = SenzaCommenti(css[prologo..apertura]).Trim();

                    if (AtContenitori.Any(a => testa.StartsWith(a, StringComparison.OrdinalIgnoreCase)))
                        Percorri(css, apertura + 1, i, fuori);
                    else if (!AtSenzaSelettori.Any(a => testa.StartsWith(a, StringComparison.OrdinalIgnoreCase)))
                        foreach (var sel in Selettori(testa).Where(s => !Confinata(s)))
                            fuori.Add(sel);

                    prologo = i + 1;
                }
            }
            i++;
        }
    }

    private static string SenzaCommenti(string testo)
    {
        var sb = new StringBuilder();
        var i = 0;
        while (i < testo.Length)
        {
            if (testo[i] == '/' && i + 1 < testo.Length && testo[i + 1] == '*')
            {
                var j = testo.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = j < 0 ? testo.Length : j + 2;
                continue;
            }
            sb.Append(testo[i]);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>La lista separata da virgole. ⚠️ Le virgole dentro <c>:is()</c>/<c>:not()</c> non separano.</summary>
    private static IEnumerable<string> Selettori(string testa)
    {
        var pezzi = new List<string>();
        var corrente = new StringBuilder();
        var livello = 0;

        foreach (var c in testa)
        {
            if (c == '(') livello++;
            else if (c == ')') livello--;

            if (c == ',' && livello == 0)
            {
                pezzi.Add(corrente.ToString());
                corrente.Clear();
            }
            else corrente.Append(c);
        }
        pezzi.Add(corrente.ToString());
        return pezzi.Select(p => p.Trim()).Where(p => p.Length > 0);
    }

    private static string Radice()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui");
            if (Directory.Exists(Path.Combine(c, "wwwroot"))) return c;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"src/Vipi.Ui non trovata risalendo da {AppContext.BaseDirectory}");
    }
}
