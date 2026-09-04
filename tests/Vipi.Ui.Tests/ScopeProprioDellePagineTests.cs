using System.Text.RegularExpressions;

namespace Vipi.Ui.Tests;

/// <summary>
/// <b>Su una pagina interattiva il DbContext è UNO, e non è della pagina: è del CIRCUITO.</b>
///
/// <para>In Blazor Server uno <c>scoped</c> vive quanto la <b>sessione</b>, non quanto la richiesta né quanto
/// la pagina. Un servizio preso con <c>@@inject</c> su una pagina interattiva porta dentro il
/// <c>DbContext</c> del circuito, che è lo stesso di ogni altro componente a schermo — e di quel che la
/// pagina <b>precedente</b> ha lasciato in volo. Due flussi asincroni su quell'istanza sono
/// <c>InvalidOperationException: A second operation was started on this context instance</c>, e a ruota
/// tante <c>ObjectDisposedException</c> quante sono le cose che stavano ancora girando.</para>
///
/// <para>🔴 <b>Misurato in produzione il 4 settembre 2026</b>, non dedotto: tre gruppi di errori in
/// <c>errori-richieste.txt</c>. Alle 08:15 e alle 10:16 <c>AirportSectionsEditor.LoadAsyncCore</c> contro
/// <c>DocumentUnionService.ForDocumentAsync</c>; alle 12:06 <c>MilSectionsEditor.LoadAsyncCore</c> contro la
/// lista dei documenti, con <b>quattro</b> query aperte insieme; alle 07:22 <c>MilListPage.CaricaAsync</c>
/// contro una query rimasta in volo dalla pagina di prima.</para>
///
/// <para>⚠️ E la coda dell'editor (<c>DocumentEditorShell.InFilaAsync</c>) <b>non</b> era la risposta, benché
/// esista apposta: sta sul COMPONENTE, e quando la pagina legge in <c>OnParametersSetAsync</c> il
/// riferimento al componente è ancora <c>null</c> — lo scrive il primo render, che deve ancora avvenire. La
/// risposta è quella già usata due volte nel prodotto: <c>OwningComponentBase</c>, cioè uno scope PROPRIO.</para>
/// </summary>
public sealed class ScopeProprioDellePagineTests
{
    /// <summary>
    /// Le tre pagine convertite il 4 settembre 2026, con il servizio che le aveva fatte cadere.
    /// ⚠️ Il servizio non deve tornare fra gli <c>@@inject</c>: da lì verrebbe di nuovo dal circuito, e il
    /// difetto tornerebbe identico con lo scope proprio ancora dichiarato sopra.
    /// </summary>
    public static TheoryData<string, string> Convertite() => new()
    {
        { "AeroportoEditorPage", "IDocumentUnionService" },
        { "MilEditorPage",       "IDocumentUnionService" },
        { "MilListPage",         "IMilitaryDocumentService" },
    };

    [Theory]
    [MemberData(nameof(Convertite))]
    public void Le_pagine_corrette_tengono_lo_scope_proprio(string pagina, string servizio)
    {
        var testo = File.ReadAllText(Path.Combine(Radice(), "Pages", pagina + ".razor"));

        Assert.Contains("@inherits OwningComponentBase", testo);
        Assert.DoesNotContain($"@inject {servizio} ", testo);
        Assert.Contains($"ScopedServices.GetRequiredService<{servizio}>()", testo);
    }

    /// <summary>
    /// I servizi che su una pagina interattiva si possono prendere dal circuito senza pensarci: non toccano
    /// il database. ⚠️ <c>IEditAuthorizationService</c> ci sta per una ragione precisa e pagata — dal 28
    /// agosto 2026 il livello si risolve <b>senza una query</b>, ed è così che è morta la prima domanda di
    /// ogni pagina.
    /// </summary>
    private static readonly HashSet<string> Sicuri = new()
    {
        "IStringLocalizer", "IEditAuthorizationService", "IJSRuntime", "NavigationManager", "EnglishStrings",
        "StringheDelSito", "ILogger", "IOptions", "IDocRoutesRegistry", "ICurrentUserProvider",
        "ReadingLanguageContext", "IAiracService", "IProssimoAiracService",
    };

    /// <summary>
    /// Le pagine interattive che <b>ancora</b> prendono un servizio dal circuito, misurate il 4 settembre
    /// 2026: ventisei, per settantaquattro iniezioni.
    ///
    /// <para>⚠️ Questo elenco <b>non</b> è un obiettivo raggiunto: è un debito <b>scritto</b>. Serve a una
    /// cosa sola — che non ne nascano di nuove senza che nessuno se ne accorga. Convertirne una vuol dire
    /// toglierla da qui, e il test resta verde da sé.</para>
    ///
    /// <para>⚠️ Il test fallisce solo sulle AGGIUNTE. Una pagina che sparisce da questo elenco è lavoro
    /// fatto, non una regressione, e non deve fermare nessuno.</para>
    /// </summary>
    private static readonly string[] DebitoNoto =
    {
        "AccAdminPage", "AdminAirspacePage", "AdminAttachmentsPage", "AdminNavaidsPage", "AdminRolesPage",
        "AdminTasksPage", "AdminTrasferimentiPage", "AeroportiPage", "AirspacePage", "AppEditorPage",
        "AtcWorldArchivePage", "AuditPage", "ChangedPage", "ConfinantiAdminPage", "CoordinateConverterPage",
        "DiagnosticaPage", "GlossarioPage", "LivePage", "PendingPage", "SearchPage", "SectorfilePage",
        "SorgentiAdminPage", "StatsDivisionPage", "StrutturaPage", "TasksPage", "VloaEditorPage",
    };

    [Fact]
    public void Nessuna_pagina_NUOVA_prende_un_servizio_dal_circuito()
    {
        var esposte = Esposte();
        var nuove = esposte.Keys.Except(DebitoNoto).OrderBy(x => x, StringComparer.Ordinal).ToList();

        Assert.True(nuove.Count == 0,
            "Pagine interattive che prendono un servizio dal circuito e non erano in elenco:\n" +
            string.Join("\n", nuove.Select(p => $"  {p}: {string.Join(", ", esposte[p])}")) +
            "\n\nSu una pagina interattiva lo scoped vive quanto il CIRCUITO: due flussi sullo stesso " +
            "DbContext sono «A second operation was started on this context instance».\n" +
            "La cura e' `@inherits OwningComponentBase` piu' `ScopedServices.GetRequiredService<...>()`, " +
            "come in AeroportoEditorPage.\n" +
            "Se il servizio davvero non tocca il database, va aggiunto a `Sicuri` con scritto PERCHE'.");
    }

    /// <summary>⚠️ Il rovescio, e serve quanto l'altro: un elenco che nomina pagine che non esistono più —
    /// rinominate, spezzate, cancellate — smette di misurare e nessuno se ne accorge, perché resta verde.</summary>
    [Fact]
    public void L_elenco_del_debito_non_nomina_pagine_che_non_esistono()
    {
        var pagine = Directory.GetFiles(Path.Combine(Radice(), "Pages"), "*.razor")
            .Select(Path.GetFileNameWithoutExtension).ToHashSet();
        var fantasmi = DebitoNoto.Where(p => !pagine.Contains(p)).ToList();

        Assert.True(fantasmi.Count == 0,
            "L'elenco del debito nomina pagine che non esistono piu': " + string.Join(", ", fantasmi));
    }

    /// <summary>Le pagine interattive senza scope proprio, con i servizi che prendono dal circuito.</summary>
    private static Dictionary<string, IReadOnlyList<string>> Esposte()
    {
        var fuori = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var f in Directory.GetFiles(Path.Combine(Radice(), "Pages"), "*.razor"))
        {
            var testo = File.ReadAllText(f);
            if (!testo.Contains("@rendermode InteractiveServer")) continue;
            if (testo.Contains("@inherits OwningComponentBase")) continue;

            var tipi = Regex.Matches(testo, @"^@inject\s+(\S+)", RegexOptions.Multiline)
                .Select(m => m.Groups[1].Value.Split('<')[0].Split('.')[^1])
                .Where(t => !Sicuri.Contains(t))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToList();

            if (tipi.Count > 0) fuori[Path.GetFileNameWithoutExtension(f)] = tipi;
        }
        return fuori;
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
