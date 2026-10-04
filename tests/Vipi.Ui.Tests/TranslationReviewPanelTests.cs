using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Translation;
using Vipi.Domain.Entities;
using Vipi.Ui;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il correttore delle traduzioni dentro l'editor, e le due regole sul <c>DbContext</c> che questo file
/// aveva perso.
///
/// <para>🔴 <b>Il guasto.</b> Il 31 agosto 2026, su <c>/services/vsop/libb/editor</c>, due volte in dieci
/// secondi (codici <c>00-bea549b1…</c> e <c>00-a0ff0695…</c>):</para>
/// <code>
/// A second operation was started on this context instance
///    at EfEditingRepository.LoadForEditAsync ← DocumentTranslationReview.RigheAsync
///    ← TranslationReviewPanel.OnParametersSetAsync
/// </code>
///
/// <para>Due difetti nello stesso metodo, ognuno già scritto altrove e nessuno dei due applicato qui:</para>
/// <list type="number">
///   <item><b>Il pannello non era isolato</b>: leggeva sul contesto del circuito, cioè quello che l'editor
///         genitore sta usando mentre monta i figli. È la stessa cosa per cui sei componenti hanno un loro
///         scope dal 30 luglio 2026.</item>
///   <item><b>La lettura non era condizionata al cambio dei parametri</b>:
///         <c>OnParametersSetAsync</c> scatta a ogni ridisegno del genitore, e l'editor si ridisegna al
///         primo <c>await</c> di qualunque suo gestore. È la regola pagata il 1 agosto 2026 con
///         <c>ReleasePanel</c>.</item>
/// </list>
///
/// <para>⚠️ E il costo non era una query: <c>RigheAsync</c> carica il <b>documento intero</b>, e il pannello
/// lo caricava <b>due volte</b> (la seconda con l'altra lingua, solo per capire se il vuoto significasse
/// «stessa lingua»). Per un blocco che è chiuso di suo.</para>
/// </summary>
public class TranslationReviewPanelTests : TestContext
{
    /// <summary>Conta le letture e sa dire con quali argomenti sono arrivate.</summary>
    private sealed class RevisioneFinta : IDocumentTranslationReview
    {
        /// <summary>La resa della riga: vuota = quella frase MANCA, ed è il caso della carta §4-bis.</summary>
        public string Resa { get; set; } = "Contact the tower.";

        public int Letture { get; private set; }
        public List<string> Lingue { get; } = new();

        /// <summary>Come il servizio vero con chi non è Editor: rifiuta la lettura.</summary>
        public bool Rifiuta { get; set; }

        public Task<RevisioneDocumento> RevisioneAsync(int documentId, string targetLang, CancellationToken ct = default)
        {
            Letture++;
            if (Rifiuta) throw new Vipi.Application.Content.EditNotAllowedException();
            Lingue.Add(targetLang);
            // ⚠️ Come il servizio vero: chiesto nella lingua in cui il documento è scritto non torna nessuna
            // riga — le righe sono la resa nell'ALTRA lingua. È il caso di chi redige in italiano.
            if (string.Equals(targetLang, "it", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new RevisioneDocumento("it", Array.Empty<RigaDaRivedere>()));

            var righe = new List<RigaDaRivedere>
            {
                new("Contatta la torre.", Resa, TranslationOrigin.Machine, false, "Regole piste"),
            };
            if (DueRighe)
                righe.Add(new("Riporta sottovento.", "Report downwind.", TranslationOrigin.Machine, false, "Circuito"));
            return Task.FromResult(new RevisioneDocumento("it", righe));
        }

        /// <summary>Vero: il documento ha due frasi da rivedere invece di una.</summary>
        public bool DueRighe { get; set; }

        /// <summary>Se c'è, il conto dei documenti toccati resta fermo finché il test non lo lascia andare: la
        /// query vera sul DB remoto. ⚠️ Un cancello e non un <c>Task.Delay</c>: col ritardo a tempo la prova era
        /// rossa a intermittenza nella corsa intera della soluzione (29 settembre 2026), verde da sola.</summary>
        public TaskCompletionSource? ContoFermo { get; set; }
        public int Conti;
        public int MassimoInsieme;
        private int _dentro;

        public async Task<int> DocumentiToccatiAsync(string sorgente, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Conti);
            var ora = Interlocked.Increment(ref _dentro);
            if (ora > MassimoInsieme) MassimoInsieme = ora;
            try { if (ContoFermo is { } fermo) await fermo.Task; return 1; }
            finally { Interlocked.Decrement(ref _dentro); }
        }

        public Task CorreggiAsync(int documentId, string targetLang, string sorgente, string tradotto,
            CancellationToken ct = default) => Task.CompletedTask;
    }

    /// <summary>Un orologio finto: l'ultimo giro è passato quando dico io.</summary>
    private sealed class AttesaFinta : IAttesaTraduzione
    {
        public int Letture { get; private set; }
        public DateTime? Ultimo { get; set; } = DateTime.UtcNow.AddMinutes(-9);
        public bool InCorso { get; set; }

        public Task<AttesaDelGiro> AttesaAsync(CancellationToken ct = default)
        {
            Letture++;
            return Task.FromResult(new AttesaDelGiro(Ultimo, InCorso, Array.Empty<EsitoDelGiro>()));
        }
    }

    /// <summary>Il tasto «traduci ora»: conta le pressioni e risponde quel che gli si dice.</summary>
    private sealed class TraduciOraFinto : ITraduciOra
    {
        public int Pressioni { get; private set; }
        public RispostaTraduciOra Risposta { get; set; } =
            new(EsitoDellaPressione.Fatto, Tradotti: 1, Mancavano: 1, Motore: "azure");

        public Task<RispostaTraduciOra> EseguiAsync(int documentId, CancellationToken ct = default)
        {
            Pressioni++;
            return Task.FromResult(Risposta);
        }
    }

    /// <summary>
    /// «A che punto è» questo documento. ⚠️ È la lettura che rende il pannello utile a chi scrive in
    /// italiano: le righe lì non ci sono, la percentuale sì.
    /// </summary>
    private sealed class StatoFinto : Vipi.Application.Translation.IStatoTraduzione
    {
        public int Letture { get; private set; }
        public RigaStatoTraduzione? Riga { get; set; } = new(
            7, "LIBD", "it", "en", Bloccata: false,
            Bozza: new TranslationCoverage(10, 8, 3),
            AMano: 0,
            Pubblicato: new TranslationCoverage(10, 4, 2),
            HaReleaseEfficace: true,
            ReleaseCongela: true);

        public Task<QuadroStatoTraduzione> QuadroAsync(CancellationToken ct = default) =>
            Task.FromResult(QuadroStatoTraduzione.Vuoto);

        public Task<RigaStatoTraduzione?> DocumentoAsync(int documentId, CancellationToken ct = default)
        {
            Letture++;
            return Task.FromResult(Riga);
        }

        public Task<MancantiDelDocumento?> MancantiAsync(int documentId, CancellationToken ct = default) =>
            Task.FromResult<MancantiDelDocumento?>(null);
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Enumerable.Empty<LocalizedString>();
    }

    private AttesaFinta _attesa = new();
    private TraduciOraFinto _traduciOra = new();
    private StatoFinto _stato = new();

    private RevisioneFinta Arrangia()
    {
        var revisione = new RevisioneFinta();
        _attesa = new AttesaFinta();
        _traduciOra = new TraduciOraFinto();
        // L'attesa la legge dallo scope proprio (parte dal ciclo di vita); il tasto dal circuito, perché
        // deve vedere chi sta premendo.
        Services.AddScoped<IAttesaTraduzione>(_ => _attesa);
        Services.AddScoped<ITraduciOra>(_ => _traduciOra);
        // ⚠️ Registrato come SCOPED, non singleton: il pannello lo risolve dal proprio ScopedServices, e un
        // servizio che non fosse risolvibile da uno scope figlio farebbe fallire il test per il motivo
        // sbagliato. Registrando l'istanza come scoped si copre entrambe le strade.
        Services.AddScoped<IDocumentTranslationReview>(_ => revisione);
        // Anche lo stato parte dal ciclo di vita, quindi dallo scope proprio: scoped come gli altri.
        _stato = new StatoFinto();
        Services.AddScoped<Vipi.Application.Translation.IStatoTraduzione>(_ => _stato);
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        JSInterop.Mode = JSRuntimeMode.Loose;
        return revisione;
    }

    /// <summary>
    /// 🔴 T-024 (revisione del 13 settembre 2026, riprodotto dal vivo): chi non è Editor apriva l'editor
    /// d'aeroporto e riceveva un 500. L'editor ricadeva in sola lettura ma montava lo stesso questo pannello, la
    /// cui lettura richiede l'Editor e non catturava il rifiuto. Ogni visita scriveva 6,3 kB di stack nel
    /// registro errori, spingendo fuori quelli veri. Il rifiuto è una risposta, non un guasto.
    /// </summary>
    [Fact]
    public async Task Chi_non_e_Editor_vede_accesso_riservato_non_un_errore()
    {
        var revisione = Arrangia();
        revisione.Rifiuta = true;

        var cut = RenderComponent<TranslationReviewPanel>(p => p.Add(x => x.DocumentId, 7));

        var caduta = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(300));
        if (caduta == Renderer.UnhandledException)
            Assert.Fail("Eccezione non gestita: " + await Renderer.UnhandledException);
        Assert.Contains("Common_AccessReserved", cut.Markup);
    }

    /// <summary>
    /// 🔴 U-108 (revisione totale 3, riprodotto dal vivo su LIBD e LIBB): due clic ravvicinati sulle righe erano
    /// due <c>ApriAsync</c>, e ognuno contava i documenti toccati sullo stesso contesto — «A second operation…»,
    /// una volta «Packet received out-of-order», e il circuito giù. Il secondo clic, finché il primo conta, non
    /// parte.
    ///
    /// <para>⚠️ Due righe e non una, per un limite della prova: dal vivo il secondo clic arriva sulla STESSA riga
    /// perché il server tiene vivo il gestore finché il browser non conferma il disegno; bUnit lo smaltisce
    /// subito, e il tasto della prima riga — diventata campo — non c'è già più.</para>
    /// </summary>
    [Fact]
    public async Task Due_clic_ravvicinati_sulle_righe_ne_aprono_una()
    {
        var revisione = Arrangia();
        revisione.ContoFermo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        revisione.DueRighe = true;

        var cut = RenderComponent<TranslationReviewPanel>(p => p.Add(x => x.DocumentId, 7));
        cut.WaitForState(() => cut.FindAll("button.tr-open").Count == 2, TimeSpan.FromSeconds(10));

        // ⚠️ Ricerca e clic sul dispatcher (cut.InvokeAsync): fra le due, un render arrivato da un altro thread
        // cambiava l'albero e il gestore trovato non c'era più — rosso intermittente sul runner, 27 settembre 2026.
        // Il primo clic resta dentro il conto finché il secondo non è passato: nessun tempo in gioco.
        var primo = cut.InvokeAsync(() => cut.FindAll("button.tr-open").First().ClickAsync(new()));
        var secondo = cut.InvokeAsync(() => cut.FindAll("button.tr-open").Last().ClickAsync(new()));
        // Un giro a vuoto dopo i due clic: quando torna, il secondo gestore ha già deciso (uscito, o in fila dietro
        // la porta). Aspettare il secondo clic stesso, senza sentinella, sarebbe aspettare per sempre.
        await cut.InvokeAsync(() => { });
        revisione.ContoFermo.SetResult();
        await Task.WhenAll(primo, secondo);
        cut.WaitForElement("textarea.tr-edit", TimeSpan.FromSeconds(10));

        Assert.Equal(1, revisione.MassimoInsieme);
        Assert.Equal(1, revisione.Conti);
        Assert.Single(cut.FindAll("textarea.tr-edit"));
        var caduta = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(200));
        if (caduta == Renderer.UnhandledException)
            Assert.Fail("Circuito caduto: " + await Renderer.UnhandledException);
    }

    /// <summary>
    /// Il documento si carica <b>una volta</b> al montaggio: prima erano due, la seconda solo per dedurre la
    /// lingua sorgente.
    /// </summary>
    [Fact]
    public void Al_montaggio_il_documento_si_legge_una_volta_sola()
    {
        var revisione = Arrangia();

        RenderComponent<TranslationReviewPanel>(p => p.Add(x => x.DocumentId, 7));

        Assert.Equal(1, revisione.Letture);
    }

    /// <summary>
    /// ⚠️ Il ridisegno del genitore <b>non</b> rilegge. È il difetto vero: nell'editor ogni salvataggio, ogni
    /// sezione aggiunta, ogni blocco aperto ridisegna la pagina, e ogni ridisegno ricaricava il documento.
    /// Non è l'<c>@if</c> sui dati caricati a proteggere: il pericolo è il RI-render, non il montaggio.
    /// </summary>
    [Fact]
    public void Un_ridisegno_del_genitore_non_rilegge_il_documento()
    {
        var revisione = Arrangia();

        var cut = RenderComponent<TranslationReviewPanel>(p => p.Add(x => x.DocumentId, 7));
        Assert.Equal(1, revisione.Letture);

        // Tre giri di parametri identici: è quel che fa il genitore quando si ridisegna.
        for (var i = 0; i < 3; i++) cut.SetParametersAndRender(p => p.Add(x => x.DocumentId, 7));

        Assert.Equal(1, revisione.Letture);
    }

    /// <summary>Ma se il documento cambia davvero, si rilegge: la guardia non deve diventare una cache cieca.</summary>
    [Fact]
    public void Se_cambia_il_documento_si_rilegge()
    {
        var revisione = Arrangia();

        var cut = RenderComponent<TranslationReviewPanel>(p => p.Add(x => x.DocumentId, 7));
        cut.SetParametersAndRender(p => p.Add(x => x.DocumentId, 9));

        Assert.Equal(2, revisione.Letture);
    }

    // ---- «Quanto ci vuole», e il tasto (carta §4-bis) --------------------------------------------------

    /// <summary>
    /// 🔴 <b>Anche a zero mancanti lo stato si dice, e il tasto c'è.</b> Fino al 6 settembre 2026 era il
    /// contrario — l'attesa non si chiedeva nemmeno e il tasto spariva — e il risultato è che il cruscotto
    /// si spegneva proprio quando il lavoro era a posto: chi guardava non poteva distinguere «tutto
    /// tradotto» da «il pannello non sa niente», e infatti l'ha letto come un guasto.
    /// </summary>
    [Fact]
    public void Anche_a_zero_mancanti_lo_stato_si_legge_e_il_tasto_resta()
    {
        Arrangia();

        var cut = RenderComponent<TranslationReviewPanel>(p => p.Add(x => x.DocumentId, 7));

        Assert.Equal(1, _attesa.Letture);
        Assert.Equal(1, _stato.Letture);
        Assert.Single(cut.FindAll("button"), b => b.TextContent.Contains("TrEd_Now"));
        // Le due coperture, non una media: sono la risposta a «a che punto sono».
        var riga = cut.Find(".tr-wait").TextContent;
        Assert.Contains("TrEd_State_Draft", riga);
        Assert.Contains("TrEd_State_Published", riga);
    }

    /// <summary>
    /// 🔴 <b>E si dice anche a chi legge nella lingua del documento</b>, cioè a chi lo sta scrivendo: là le
    /// righe da rivedere non esistono — sono la resa nell'ALTRA lingua — e il pannello si fermava alla frase
    /// «passa all'altra lingua», senza percentuale, senza orologio e senza tasto. Il conto del documento
    /// contro la memoria non dipende dalla lingua in cui lo si guarda.
    /// </summary>
    [Fact]
    public void In_italiano_niente_righe_ma_lo_stato_e_il_tasto_ci_sono()
    {
        Arrangia();
        var prima = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("it");
        try
        {
            var cut = RenderComponent<TranslationReviewPanel>(p => p.Add(x => x.DocumentId, 7));

            Assert.Contains("TrEd_SameLanguage", cut.Markup);
            Assert.Contains("TrEd_State_Draft", cut.Find(".tr-wait").TextContent);
            Assert.Single(cut.FindAll("button"), b => b.TextContent.Contains("TrEd_Now"));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = prima;
        }
    }

    /// <summary>
    /// ⚠️ I mancanti si contano sul DOCUMENTO, non sulle righe mostrate: in italiano le righe sono zero, e
    /// contare lì diceva «non manca niente» di un documento tradotto a metà.
    /// </summary>
    [Fact]
    public void I_mancanti_vengono_dallo_stato_del_documento()
    {
        Arrangia();

        var cut = RenderComponent<TranslationReviewPanel>(p => p.Add(x => x.DocumentId, 7));

        // La finta dice 10 segmenti e 8 tradotti: due mancanti, e la pastiglia in testata lo deve dire
        // anche se le righe mostrate hanno tutte una resa. (Il localizzatore dei test rende la chiave, non
        // la frase: quel che si prova è QUALE frase si è scelta, non come suona.)
        Assert.Contains("TrEd_Wait_Missing", cut.Find("summary").TextContent);
        Assert.DoesNotContain("TrEd_Wait_None", cut.Find(".tr-wait").TextContent);
    }

    /// <summary>Senza il servizio dello stato il pannello si monta lo stesso: è un di più, non una dipendenza.</summary>
    [Fact]
    public void Senza_il_servizio_dello_stato_il_pannello_si_monta()
    {
        var revisione = new RevisioneFinta();
        _attesa = new AttesaFinta();
        _traduciOra = new TraduciOraFinto();
        Services.AddScoped<IAttesaTraduzione>(_ => _attesa);
        Services.AddScoped<ITraduciOra>(_ => _traduciOra);
        Services.AddScoped<IDocumentTranslationReview>(_ => revisione);
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<TranslationReviewPanel>(p => p.Add(x => x.DocumentId, 7));

        Assert.Contains("TrEd_Title", cut.Markup);
        Assert.Single(cut.FindAll("button"), b => b.TextContent.Contains("TrEd_Now"));
    }

    /// <summary>Se qualcosa manca, l'attesa si legge e il tasto compare.</summary>
    [Fact]
    public void Con_una_frase_mancante_si_dice_quanto_manca_e_si_puo_premere()
    {
        var revisione = Arrangia();
        revisione.Resa = "";

        var cut = RenderComponent<TranslationReviewPanel>(p => p.Add(x => x.DocumentId, 7));

        Assert.Equal(1, _attesa.Letture);
        var bottoni = cut.FindAll("button").Where(b => b.TextContent.Contains("TrEd_Now")).ToList();
        Assert.Single(bottoni);

        bottoni[0].Click();

        Assert.Equal(1, _traduciOra.Pressioni);
        // Dopo una pressione riuscita il documento si rilegge: chi ha premuto deve vedere la resa nuova.
        Assert.Equal(2, revisione.Letture);
    }

    /// <summary>
    /// ⚠️ Se un giro sta già girando, la pressione NON rilegge il documento: non è successo niente, e una
    /// rilettura direbbe a chi guarda che qualcosa è cambiato.
    /// </summary>
    [Fact]
    public void Se_un_giro_sta_gia_girando_non_si_rilegge_niente()
    {
        var revisione = Arrangia();
        revisione.Resa = "";
        _traduciOra.Risposta = new RispostaTraduciOra(EsitoDellaPressione.GiroInCorso);

        var cut = RenderComponent<TranslationReviewPanel>(p => p.Add(x => x.DocumentId, 7));
        cut.FindAll("button").First(b => b.TextContent.Contains("TrEd_Now")).Click();

        Assert.Equal(1, _traduciOra.Pressioni);
        Assert.Equal(1, revisione.Letture);
    }
}
