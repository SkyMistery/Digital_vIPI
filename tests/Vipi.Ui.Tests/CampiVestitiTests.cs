using System.Text.RegularExpressions;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Un campo di modulo porta una classe che i fogli <b>definiscono davvero</b>.
///
/// <para>
/// 🔴 <b>Una classe inventata non dà errore, non rompe niente, e si vede solo guardando.</b> È già
/// successo due volte: i moduli di glossario e traduzioni erano <c>class="in"</c> (il commento sta in
/// <c>vipi-theme.css</c>, sopra <c>.struct .inline-form</c>), e il 3 settembre 2026 i due campi del
/// selettore dei documenti uniti erano <c>class="inp"</c> — tutti <b>nudi, coi colori del browser</b> in
/// mezzo a pagine vestite. Nessun controllo li vedeva: quello che c'era — <c>classi-morte.py</c> — guarda
/// nel verso opposto, cioè le classi del foglio che nessuno nomina.
/// </para>
///
/// <para>
/// ⚠️ Guarda le sole classi <b>letterali</b>: quelle composte a pezzi (<c>class="@(…)"</c>) qui non si
/// possono risolvere, e pretenderlo darebbe falsi allarmi — è la stessa cautela di <c>classi-morte.py</c>.
/// </para>
/// </summary>
public sealed class CampiVestitiTests
{
    /// <summary>
    /// Le classi ancora <b>senza vestito</b>, con la ragione. ⚠️ Non sono assolte: sono <b>note e non
    /// ancora guardate</b>, e stanno qui perché la rete valga da subito su tutto il resto invece di
    /// aspettare che qualcuno abbia tempo per queste. Chi ne veste una, toglie anche la riga.
    /// </summary>
    private static readonly Dictionary<string, string> Tollerate = new()
    {
        // ⚠️ Le due che restano NON sono una svista: in una tabella densa un campo vestito occupa più
        // spazio, e la colonna del fix è larga 76px misurati. Il committente le guarda a schermo prima di
        // decidere (3 settembre 2026). Chi decide, veste e toglie la riga.
        ["in-cond"] = "Campo DENSO nella cella «condizione» della tabella SID: vestirlo la allargherebbe. Decisione rimandata, non dimenticata.",
        ["in-prio"] = "Come sopra, colonna «priorità» della stessa tabella.",
    };

    private static readonly Regex Campo =
        new(@"<(input|select|textarea)\b[^>]*?\bclass=""([^""@]*)""", RegexOptions.Singleline | RegexOptions.Compiled);

    [Fact]
    public void Ogni_campo_porta_una_classe_che_i_fogli_definiscono()
    {
        var definite = ClassiDeiFogli();
        var nude = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Radice(), "*.razor", SearchOption.AllDirectories))
        {
            var testo = File.ReadAllText(file);
            foreach (Match m in Campo.Matches(testo))
                foreach (var c in m.Groups[2].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    if (!definite.Contains(c) && !Tollerate.ContainsKey(c))
                        nude.Add(Path.GetFileName(file) + ": ." + c);
        }

        Assert.True(nude.Count == 0,
            "Campi con una classe che nessun foglio definisce (nudi, coi colori del browser): "
            + string.Join(" | ", nude.Distinct().OrderBy(x => x, StringComparer.Ordinal)));
    }

    /// <summary>⚠️ La tolleranza non è un posto dove parcheggiare: se una di quelle classi viene vestita, la
    /// riga va tolta, o l'elenco invecchia e smette di dire la verità.</summary>
    [Fact]
    public void Le_tollerate_sono_ancora_senza_vestito()
    {
        var definite = ClassiDeiFogli();
        var gia = Tollerate.Keys.Where(definite.Contains).ToList();

        Assert.True(gia.Count == 0,
            "Queste classi ORA i fogli le definiscono: togli la riga dalla tolleranza. " + string.Join(" | ", gia));
    }

    private static HashSet<string> ClassiDeiFogli()
    {
        var css = string.Concat(Directory.EnumerateFiles(Path.Combine(Radice(), "wwwroot"), "*.css")
                                         .Select(File.ReadAllText));
        return Regex.Matches(css, @"\.([A-Za-z][A-Za-z0-9_-]*)")
                    .Select(m => m.Groups[1].Value)
                    .ToHashSet(StringComparer.Ordinal);
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
        throw new DirectoryNotFoundException("src/Vipi.Ui non trovata risalendo da " + AppContext.BaseDirectory);
    }
}
