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
        // 🔴 La terza sorella, rimasta indietro fino all'8 settembre 2026: stesso `LeggiUnioneAsync`,
        // stessa corsa col caricamento del componente figlio, e nessuno se n'era accorto perche' il
        // presidio guardava le AGGIUNTE, non i pari grado di una correzione gia' fatta.
        { "AppEditorPage",       "IDocumentUnionService" },
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
        // ⚠️ Questi tre non toccano il database, e il perché è la loro REGISTRAZIONE, non un'impressione:
        // `IOnlineAtcProvider` e `IWeatherProvider` sono SINGLETON (la cache whazzup e il client NOAA) — un
        // singleton non può tenersi un DbContext, o sarebbe una dipendenza prigioniera; `INavaidSource` è un
        // client HTTP tipizzato (`AddHttpClient`), quindi parla con Aurora, non con MySQL.
        "IOnlineAtcProvider", "IWeatherProvider", "INavaidSource",
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
        "AdminTasksPage", "AdminTrasferimentiPage", "AeroportiPage", "AirspacePage",
        "AuditPage", "ChangedPage", "ConfinantiAdminPage", "GlossarioPage", "LivePage", "PendingPage",
        "SearchPage", "SectorfilePage", "SorgentiAdminPage", "StrutturaPage", "TasksPage", "VloaEditorPage",
    };

    /// <summary>
    /// Le quattro convertite il <b>7 settembre 2026</b> (revisione del 6 settembre, R-016), che erano nel
    /// debito qui sopra: <c>StatsDivisionPage</c>, <c>DiagnosticaPage</c>, <c>AtcWorldArchivePage</c>,
    /// <c>CoordinateConverterPage</c>. Non hanno preso solo lo scope proprio: hanno preso anche la
    /// <b>sentinella di rientro</b>, che è l'altra porta — vedi <c>SentinellaDiRientroTests</c>.
    /// </summary>
    [Theory]
    [InlineData("StatsDivisionPage")]
    [InlineData("DiagnosticaPage")]
    [InlineData("AtcWorldArchivePage")]
    [InlineData("CoordinateConverterPage")]
    public void Le_quattro_del_7_settembre_tengono_lo_scope_proprio(string pagina)
    {
        var testo = File.ReadAllText(Path.Combine(Radice(), "Pages", pagina + ".razor"));

        Assert.Contains("@inherits OwningComponentBase", testo);
        Assert.Contains("ScopedServices.GetRequiredService<", testo);
    }

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

    /// <summary>
    /// 🔴 <b>La forma PEGGIORE del difetto: lo scope proprio dichiarato, e poi tradito.</b>
    ///
    /// <para>Un componente che scrive <c>@@inherits OwningComponentBase</c> ha già pagato il prezzo dello
    /// scope suo — ma se poi prende un servizio con <c>@@inject</c>, <b>quel</b> servizio arriva lo stesso
    /// dal circuito. La difesa c'è, è scritta in testa al file, e non copre: è peggio che non averla,
    /// perché chi legge quella riga smette di guardare.</para>
    ///
    /// <para>🔴 <b>Misurato l'8 settembre 2026</b>, e questo presidio era CIECO proprio lì: il conteggio
    /// qui sotto <b>saltava</b> i file con lo scope proprio e guardava solo <c>Pages/</c>. Intanto in
    /// produzione <c>AirportSectionsEditor</c> — scope proprio, tornello, tutto — chiamava
    /// <c>ISectorShapeResolver</c> e <c>IStationResolver</c> presi con <c>@@inject</c> dentro il proprio
    /// caricamento: quattro «A second operation was started» fra il 4 e il 7 settembre, e l'ultima ha
    /// <c>DocumentEditorShell.InFilaAsync</c> nello stack. Era già in fila, e la lettura passava da
    /// un'altra porta. Vedi <c>docs/lavori-aperti.md</c> §CD.</para>
    /// </summary>
    private static readonly Dictionary<string, string> ScopeTraditoNoto = new(StringComparer.Ordinal)
    {
        // ⚠️ Eccezione VOLUTA, documentata in testa a quel file: questi due servono alla SCRITTURA, che
        // parte da un clic e non dal render — quindi la corsa col caricamento non ce l'ha — e passa da chi
        // legge i claim. In uno scope creato dopo la richiesta l'identità non c'è più, e il salvataggio
        // verrebbe rifiutato a tutti. Non si «uniforma»: sta scritto perché è già stato pensato.
        ["TranslationReviewPanel"] = "IDocumentTranslationReview, ITraduciOra",
    };

    [Fact]
    public void Chi_dichiara_lo_scope_proprio_non_lo_tradisce_con_un_inject()
    {
        var traditori = ConScopeProprioCheIniettano();
        var nuovi = traditori.Keys.Except(ScopeTraditoNoto.Keys).OrderBy(x => x, StringComparer.Ordinal).ToList();

        Assert.True(nuovi.Count == 0,
            "Componenti che dichiarano `@inherits OwningComponentBase` e poi prendono un servizio dal " +
            "CIRCUITO con `@inject`:\n" +
            string.Join("\n", nuovi.Select(p => $"  {p}: {string.Join(", ", traditori[p])}")) +
            "\n\nE' la forma peggiore: la difesa e' dichiarata e non copre. Il servizio va risolto da " +
            "`ScopedServices.GetRequiredService<...>()` in `OnInitialized`.\n" +
            "Se davvero non tocca il database va in `Sicuri`, con scritto PERCHE'; se e' un'eccezione " +
            "voluta — una SCRITTURA che parte da un clic e legge i claim — va in `ScopeTraditoNoto`.");
    }

    /// <summary>⚠️ Il rovescio: un'eccezione che nomina un file sparito smette di misurare restando verde.</summary>
    [Fact]
    public void L_elenco_dello_scope_tradito_non_nomina_componenti_che_non_esistono()
    {
        var vivi = ConScopeProprioCheIniettano();
        var fantasmi = ScopeTraditoNoto.Keys.Where(k => !vivi.ContainsKey(k)).ToList();

        Assert.True(fantasmi.Count == 0,
            "`ScopeTraditoNoto` nomina componenti che non tradiscono piu' (o non esistono): " +
            string.Join(", ", fantasmi) + ". Se il lavoro e' fatto, la riga va tolta.");
    }

    /// <summary>
    /// Chi dichiara lo scope proprio e prende comunque qualcosa dal circuito. ⚠️ Pagine <b>e</b> componenti:
    /// il difetto del 7 settembre stava in un componente, e guardare solo <c>Pages/</c> non l'avrebbe visto.
    /// </summary>
    private static Dictionary<string, IReadOnlyList<string>> ConScopeProprioCheIniettano()
    {
        var traditori = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var f in Directory.EnumerateFiles(Radice(), "*.razor", SearchOption.AllDirectories))
        {
            var testo = File.ReadAllText(f);
            if (!testo.Contains("@inherits OwningComponentBase")) continue;

            var tipi = Regex.Matches(testo, @"^@inject\s+(\S+)", RegexOptions.Multiline)
                .Select(m => m.Groups[1].Value.Split('<')[0].Split('.')[^1])
                .Where(t => !Sicuri.Contains(t))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToList();

            if (tipi.Count > 0) traditori[Path.GetFileNameWithoutExtension(f)] = tipi;
        }
        return traditori;
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
