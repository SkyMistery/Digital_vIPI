using System.Text.RegularExpressions;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// <b>«Zero letterali fuori dal livello 1, ed è verificato».</b> Adesso è vero anche la seconda metà.
///
/// <para><c>docs/design/regole-brand.md</c> §1 descrive <c>vipi-theme.css</c> a tre livelli e chiude la
/// tabella dicendo che nel corpo del foglio gli esadecimali sono «zero, ed è verificato». Non li verificava
/// nessuno: i test sul tema erano parecchi — la minificazione, il rosso dell'avviso che viene dal token, la
/// gerarchia dei titoli — ma nessuno contava i letterali, e in assenza del controllo ne erano entrati
/// quattro (revisione del 6 settembre 2026, R-027).</para>
///
/// <para>⚠️ Un colore scritto a mano non segue il brand quando il brand cambia, e non si gira quando il tema
/// si gira: è la ragione della regola, e le eccezioni qui sotto sono quelle in cui una delle due cose non
/// vale — un fondo che è chiaro in tutti e due i temi, o un colore che nella palette non c'è.</para>
/// </summary>
public class LetteraliDiColoreTests
{
    /// <summary>
    /// Il velo: bianco e nero con alfa su una superficie già colorata. Non sono colori di brand, sono opacità.
    /// </summary>
    /// <remarks>⚠️ Due forme, non una: `rgba(0,0,0,.28)` e `rgb(0 0 0 / .18)`. La seconda è la sintassi
    /// moderna a spazi, e chi conosce solo le virgole la prende per un colore scritto a mano.</remarks>
    private static readonly Regex Velo = new(
        @"^(#fff|#ffffff|#000|#000000"
        + @"|rgba?\(\s*255\s*,\s*255\s*,\s*255|rgba?\(\s*0\s*,\s*0\s*,\s*0"
        + @"|rgba?\(\s*255\s+255\s+255|rgba?\(\s*0\s+0\s+0)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Un colore scritto a mano: l'esadecimale, o un <c>rgb()</c>/<c>rgba()</c> con dei NUMERI dentro.
    /// <para>⚠️ La funzione si prende INTERA, fino alla parentesi chiusa: presa a pezzi, il velo
    /// <c>rgba(255,255,255,.88)</c> arriva al confronto come «rgba(2» e nessuna eccezione lo riconosce —
    /// quarantacinque falsi positivi al primo giro.</para>
    /// </summary>
    private static readonly Regex Letterale = new(
        @"#[0-9a-fA-F]{3,8}\b|rgba?\(\s*\d[^)]*\)", RegexOptions.Compiled);

    /// <summary>
    /// Le eccezioni <b>nominate</b>, ognuna con la sua ragione — che è ciò che distingue un'eccezione da una
    /// deroga. Se ne serve una nuova, si aggiunge qui <i>e</i> in <c>regole-brand.md</c>: due posti, apposta.
    /// </summary>
    private static readonly (string Dove, string Perche)[] Ammesse =
    {
        (".mva-label",
         "etichetta disegnata SOPRA una tile topografica, che è chiara in tutti e due i temi: col token, " +
         "in tema scuro diventerebbe bianca con alone scuro sopra la stessa identica superficie"),
        ("--nbr-ink",
         "estensione locale: il viola non esiste nella palette di brand come colore semantico, e il solo " +
         "viola del brand (product.creators) non passa AA né su bianco (4,23:1) né sul fondo scuro (4,09:1)"),
    };

    [Fact]
    public void Nel_corpo_del_foglio_non_ci_sono_colori_letterali()
    {
        var righe = File.ReadAllLines(Path.Combine(Radice(), "wwwroot", "vipi-theme.css"));
        var fuori = new List<string>();

        var dentroScala = false;
        var scalaFinita = false;
        var dentroCommento = false;
        var selettore = "(inizio del foglio)";

        for (var i = 0; i < righe.Length; i++)
        {
            var riga = SenzaCommento(righe[i], ref dentroCommento);

            // Livello 1: il PRIMO blocco `:root`, cioè la scala di brand copiata alla lettera. È l'unico
            // posto dove un esadecimale è al suo posto — e finisce dove finisce il blocco.
            if (!scalaFinita && riga.TrimStart().StartsWith(":root", StringComparison.Ordinal)) dentroScala = true;
            if (dentroScala)
            {
                if (riga.Contains('}')) { dentroScala = false; scalaFinita = true; }
                continue;
            }

            if (riga.Contains('{')) selettore = riga[..riga.IndexOf('{')].Trim();

            foreach (Match m in Letterale.Matches(riga))
            {
                if (Velo.IsMatch(m.Value)) continue;

                var contesto = selettore + " " + riga;
                if (Ammesse.Any(a => contesto.Contains(a.Dove, StringComparison.Ordinal))) continue;

                fuori.Add($"riga {i + 1} — {m.Value} in «{selettore}»");
            }
        }

        Assert.True(fuori.Count == 0,
            $"Colori letterali nel corpo di vipi-theme.css ({fuori.Count}):\n  " +
            string.Join("\n  ", fuori.Take(20)) +
            "\n\nRegola: un colore si scrive UNA volta, nella scala di brand, e nel corpo si usa un token " +
            "(docs/design/regole-brand.md §1). Serve una sfumatura che non c'è? Si aggiunge un token, o si " +
            "deriva con `color-mix()`. Se è davvero un'eccezione, va NOMINATA qui e nella regola, con la " +
            "sua ragione — non lasciata passare in silenzio.");
    }

    /// <summary>
    /// ⚠️ E le eccezioni devono <b>esistere</b>: un elenco che nomina un selettore sparito smette di
    /// misurare senza dirlo, e resta verde — lo stesso rovescio dell'elenco del debito sugli scope.
    /// </summary>
    [Fact]
    public void Le_eccezioni_nominate_esistono_ancora_nel_foglio()
    {
        var css = File.ReadAllText(Path.Combine(Radice(), "wwwroot", "vipi-theme.css"));
        var fantasmi = Ammesse.Where(a => !css.Contains(a.Dove, StringComparison.Ordinal)).ToList();

        Assert.True(fantasmi.Count == 0,
            "L'elenco delle eccezioni nomina cose che nel foglio non ci sono più: " +
            string.Join(", ", fantasmi.Select(f => f.Dove)) +
            ". Vanno tolte da qui e da regole-brand.md.");
    }

    /// <summary>
    /// La riga senza la parte commentata. ⚠️ Lo stato del commento si porta avanti fra le righe: i commenti
    /// di questo foglio sono lunghi e RACCONTANO i colori sbagliati che hanno prodotto una regola
    /// («il fondo usciva rgb(59,59,59) contro un --surface di #21212e»). Letti riga per riga, quei racconti
    /// diventano due letterali che nessuno ha scritto.
    /// </summary>
    private static string SenzaCommento(string riga, ref bool dentroCommento)
    {
        var fuori = new System.Text.StringBuilder();
        var i = 0;
        while (i < riga.Length)
        {
            if (dentroCommento)
            {
                var fine = riga.IndexOf("*/", i, StringComparison.Ordinal);
                if (fine < 0) return fuori.ToString();
                dentroCommento = false;
                i = fine + 2;
                continue;
            }

            var apre = riga.IndexOf("/*", i, StringComparison.Ordinal);
            if (apre < 0) { fuori.Append(riga[i..]); break; }

            fuori.Append(riga[i..apre]);
            dentroCommento = true;
            i = apre + 2;
        }
        return fuori.ToString();
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
