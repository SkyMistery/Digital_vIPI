using Xunit;

namespace Vipi.Assets.Tests;

/// <summary>
/// 🔴 <b>Un foglio caricato PIGRO può vestire solo ciò che compare INSIEME a lui.</b>
///
/// <para>Misurato dal campo il 21 settembre 2026: aprendo un documento con una AoR, ogni tanto il toggle
/// «2D map / 3D view» compariva <b>nudo</b> — due bottoni di sistema al posto delle due linguette. Non era
/// intermittente per caso: è una corsa fra il primo disegno e la rete, quindi si vede solo quando la rete è
/// lenta o la cache è fredda.</para>
///
/// <para><b>La causa.</b> Il toggle lo rende il <b>server</b> (<c>AccAor.razor</c>) e sta in pagina dal primo
/// disegno; i suoi stili stavano però in <c>vipi-aor3d.css</c>, che è un foglio <b>pigro</b> — lo inietta
/// <c>vipi-aor3d.js</c>, che a sua volta arriva solo dove c'è uno stage 3D. Il ragionamento scritto là non
/// era sbagliato, era <b>parziale</b>: «lo stage compare dopo un gesto, quindi non c'è nessun primo disegno
/// da rovinare» vale per lo <b>stage</b>, e non per il <b>tasto che quel gesto lo deve ancora ricevere</b>.</para>
///
/// <para>⚠️ È la seconda volta che questa regola si paga: <c>.print-only</c> era uscita da
/// <c>vipi-print.css</c> per la stessa ragione — quel foglio è dichiarato <c>media="print"</c>, quindi il
/// browser non lo aspetta per disegnare, e l'unica sua regola che valeva anche a schermo restava spenta.</para>
///
/// <para>⚠️ <b>Questo test non sostituisce l'occhio</b>, lo inchioda: la prova a schermo di una corsa che si
/// vede una volta su dieci non è ripetibile, e senza una regola scritta la prossima riga aggiunta a un
/// foglio pigro tornerebbe a rompere la stessa cosa.</para>
/// </summary>
public sealed class FoglioPigroVesteSoloIlSuoTests
{
    /// <summary>
    /// I fogli che NON stanno nel <c>&lt;head&gt;</c> di ogni schermata: arrivano dopo, per gesto o per rotta.
    /// <para>⚠️ <c>vipi-aor3d.css</c> sta nel <c>&lt;head&gt;</c> <b>solo</b> sulla pagina intera del 3D
    /// (<c>/services/vsop/aor3d/…</c>), dove lo stage È la pagina. Ovunque altro lo inietta il modulo.</para>
    /// </summary>
    private static readonly string[] FogliPigri = { "vipi-aor3d.css" };

    /// <summary>
    /// I selettori che il <b>server</b> mette in pagina prima di qualunque gesto, e che quindi un foglio
    /// pigro non può vestire. Vengono da <c>AccAor.razor</c>, che rende il toggle e le due viste.
    /// <para>⚠️ Elenco a mano e non dedotto dal markup, come <c>SenzaAttesaNoto</c>: dedurlo vorrebbe dire
    /// riscrivere un parser di Razor per prendere un difetto che si conta su una mano. Chi aggiunge una
    /// linguetta al blocco AoR aggiunge la sua riga qui.</para>
    /// </summary>
    private static readonly string[] RestiInPaginaDalPrimoDisegno =
    {
        ".aor-viewmode",
        ".aor-vm-btn",
        ".aor-view[hidden]",
    };

    [Fact]
    public void Un_foglio_pigro_non_veste_quel_che_e_gia_in_pagina()
    {
        var colpevoli = new List<string>();
        foreach (var foglio in FogliPigri)
        {
            var testo = File.ReadAllText(Path.Combine(Wwwroot(), foglio));
            foreach (var sel in RestiInPaginaDalPrimoDisegno)
                if (DichiaraUnaRegolaPer(testo, sel))
                    colpevoli.Add($"{foglio} veste {sel}");
        }

        Assert.True(colpevoli.Count == 0,
            "Un foglio caricato PIGRO veste un selettore che sta in pagina dal PRIMO disegno:\n" +
            string.Join("\n", colpevoli.Select(c => "  " + c)) +
            "\n\nQuel foglio arriva DOPO, quindi per un istante quell'elemento si vede nudo — e si vede solo " +
            "a rete lenta o cache fredda, cioe' proprio dove non lo si prova mai. Le regole di quel " +
            "selettore vanno in vipi-theme.css, che sta nel <head> di ogni schermata.\n" +
            "Nel foglio pigro resta solo cio' che COMPARE col gesto che lo carica.");
    }

    /// <summary>⚠️ Il rovescio, e serve quanto l'altro: se quelle regole non stanno nemmeno nel tema, il
    /// toggle e' nudo <b>sempre</b> invece che ogni tanto — e il test qui sopra sarebbe verde.</summary>
    [Fact]
    public void E_il_tema_le_veste_davvero()
    {
        var tema = File.ReadAllText(Path.Combine(Wwwroot(), "vipi-theme.css"));

        var orfani = RestiInPaginaDalPrimoDisegno.Where(s => !DichiaraUnaRegolaPer(tema, s)).ToList();

        Assert.True(orfani.Count == 0,
            "vipi-theme.css NON veste questi selettori, che stanno in pagina dal primo disegno: " +
            string.Join(", ", orfani) +
            ". Toglierli da un foglio pigro senza rimetterli qui li lascia nudi per sempre.");
    }

    /// <summary>
    /// Vero se il foglio dichiara almeno una regola per quel selettore. ⚠️ Si cerca il selettore seguito da
    /// cio' che puo' seguirlo in una regola vera — <c>{</c>, una virgola, uno spazio, un <c>:</c> di
    /// pseudo-classe — e non la semplice presenza del testo: il nome compare anche nei commenti, e questi
    /// due file di commenti ne hanno parecchi (compreso quello che spiega perche' la regola se n'e' andata).
    /// </summary>
    private static bool DichiaraUnaRegolaPer(string css, string selettore) =>
        SenzaCommenti(css).Contains(selettore + "{", StringComparison.Ordinal) ||
        SenzaCommenti(css).Contains(selettore + " {", StringComparison.Ordinal) ||
        SenzaCommenti(css).Contains(selettore + ",", StringComparison.Ordinal) ||
        SenzaCommenti(css).Contains(selettore + ":", StringComparison.Ordinal) ||
        SenzaCommenti(css).Contains(selettore + ".", StringComparison.Ordinal);

    /// <summary>Via i commenti <c>/* … */</c>: un nome citato in un commento non veste niente.</summary>
    private static string SenzaCommenti(string css) =>
        System.Text.RegularExpressions.Regex.Replace(css, @"/\*.*?\*/", " ",
            System.Text.RegularExpressions.RegexOptions.Singleline);

    /// <summary>La wwwroot dei sorgenti, risalendo dall'output dei test (come fanno gli altri test del repo).</summary>
    private static string Wwwroot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui", "wwwroot");
            if (Directory.Exists(c)) return c;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"wwwroot non trovata risalendo da {AppContext.BaseDirectory}");
    }
}
