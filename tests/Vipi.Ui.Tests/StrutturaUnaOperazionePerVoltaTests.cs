using System.Reflection;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// 🔴 U-012 (revisione totale 3): nella Struttura un clic su un nodo mentre un'altra operazione era in volo faceva
/// cadere il circuito. Riprodotto dal vivo con un VERO doppio clic su un nodo (ObjectDisposedException a
/// <c>StrutturaPage.razor</c> ~1069/1115). La pagina prende i servizi dal circuito, tutti sullo stesso
/// <c>DbContext</c>: <c>Select</c> leggeva i ripieghi senza fila e senza guardare <c>_busy</c>, e
/// <c>Guarded</c> non aveva sentinella — un nodo cliccato mentre un trascinamento salvava e riproiettava era
/// «A second operation was started».
///
/// <para>Il finto qui sotto è il <c>DbContext</c>: conta quante operazioni gli stanno addosso insieme. Più di una
/// è la corsa, anche dove in produzione sarebbe uscita come messaggio invece che come circuito caduto.</para>
/// </summary>
public class StrutturaUnaOperazionePerVoltaTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class Editore : IEditAuthorizationService
    {
        public VipiRole Role => VipiRole.Editor;
        public bool IsAdmin => false;
        public int? CurrentUserId => 1;
        public string? CurrentName => "test";
    }

    /// <summary>Il contesto del circuito: uno solo, e conta chi ci sta dentro nello stesso momento.</summary>
    private sealed class Contesto
    {
        private int _dentro;
        public int MassimoInsieme;

        public async Task<T> Dentro<T>(Func<Task<T>> lavoro)
        {
            var ora = Interlocked.Increment(ref _dentro);
            if (ora > MassimoInsieme) MassimoInsieme = ora;
            try { return await lavoro(); }
            finally { Interlocked.Decrement(ref _dentro); }
        }

        public Task<T> Pausa<T>(T esito) => Dentro(async () => { await Task.Delay(40); return esito; });
    }

    /// <summary>Tre nodi: un ACC radice, un suo settore, un APP. <c>SetParentAsync</c> resta in volo finché il
    /// test non lo lascia andare — è il salvataggio più la riproiezione, che in produzione dura centinaia di ms.</summary>
    private sealed class GerarchiaFinta(Contesto db) : IHierarchyEditingService
    {
        public readonly TaskCompletionSource Salvataggio = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Salvataggi;

        public Task<IReadOnlyList<HierarchyNode>> LoadTreeAsync(CancellationToken ct = default) =>
            db.Pausa<IReadOnlyList<HierarchyNode>>(new[]
            {
                new HierarchyNode(HierarchyNodeKind.Acc, 1, "LIMM_CTR", "LIMM_CTR", "LIMM", null, false),
                new HierarchyNode(HierarchyNodeKind.Acc, 2, "LIMM_N_CTR", "LIMM_N_CTR", "LIMM", "LIMM_CTR", false),
                new HierarchyNode(HierarchyNodeKind.AirportPosition, 3, "LIML_APP", "LIML_APP", "LIMM", "LIMM_CTR", false),
            });

        public Task<IReadOnlySet<string>> ListConfiningForeignCallsignsAsync(CancellationToken ct = default) =>
            db.Pausa<IReadOnlySet<string>>(new HashSet<string>());

        public Task SetParentAsync(HierarchyNodeKind kind, int nodeId, string? parentCallsign, CancellationToken ct = default) =>
            db.Dentro(async () => { Interlocked.Increment(ref Salvataggi); await Salvataggio.Task; return 0; });
    }

    private sealed class RipieghiFinti(Contesto db) : ISectorFallbackService
    {
        public int Letture;

        /// <summary>Le righe salvate di ogni settore. Vuoto = nessun ripiego dichiarato.</summary>
        public List<FallbackRowEdit> Righe { get; } = new();

        /// <summary>Se c'è, il suggerimento lancia questo: un guasto che la pagina non traduce (U-190).</summary>
        public Exception? LanciaProposte { get; set; }

        public Task<IReadOnlyList<FallbackRowEdit>> ListAsync(string sectorCallsign, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Letture);
            return db.Pausa<IReadOnlyList<FallbackRowEdit>>(Righe.ToList());
        }

        public Task<string?> RipiegoAutomaticoAsync(string sectorCallsign, CancellationToken ct = default) =>
            db.Pausa<string?>(null);

        public Task ReplaceAsync(string sectorCallsign, IReadOnlyList<FallbackRowEdit> rows, CancellationToken ct = default) =>
            db.Pausa(0);

        public Task<IReadOnlyList<FallbackSuggestion>> SuggestAsync(string sectorCallsign, CancellationToken ct = default) =>
            LanciaProposte is not null
                ? Task.FromException<IReadOnlyList<FallbackSuggestion>>(LanciaProposte)
                : db.Pausa<IReadOnlyList<FallbackSuggestion>>(Array.Empty<FallbackSuggestion>());
    }

    private sealed class OrfaniFinti(Contesto db) : IOrphanSectorService
    {
        public Task<IReadOnlyList<OrphanSectorRow>> ListAsync(string? accCode = null, CancellationToken ct = default) =>
            db.Pausa<IReadOnlyList<OrphanSectorRow>>(Array.Empty<OrphanSectorRow>());
        public Task<IReadOnlyList<ReattachTargetRow>> ReattachTargetsAsync(int orphanSectorId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task ReattachAsync(int orphanSectorId, int targetSectorId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class NessunoOnline : IOnlineAtcProvider
    {
        public OnlineAtcSnapshot GetCurrent() => OnlineAtcSnapshot.Empty;
    }

    /// <summary>Il lock di struttura è già nostro: i comandi sono accesi dall'apertura.</summary>
    private sealed class LockMio : IResourceLockService
    {
        private static LockInfo Mio => new() { Locked = true, IsMine = true, ByUserId = 1, ByName = "test" };
        public Task<LockInfo> InspectAsync(string k, CancellationToken ct = default) => Task.FromResult(Mio);
        public Task<LockInfo> AcquireAsync(string k, CancellationToken ct = default) => Task.FromResult(Mio);
        public Task<LockInfo> HeartbeatAsync(string k, CancellationToken ct = default) => Task.FromResult(Mio);
        public Task ForceUnlockAsync(string k, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureHeldAsync(string k, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReleaseAsync(string k, CancellationToken ct = default) => Task.CompletedTask;
    }

    /// <summary>Del servizio della struttura la pagina chiede solo l'elenco degli ACC (per i nomi delle card).</summary>
    public class StrutturaFinta : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? m, object?[]? a) => m!.Name switch
        {
            nameof(IStructureEditingService.ListAccsAsync) =>
                Task.FromResult<IReadOnlyList<AccRow>>(new[] { new AccRow(1, "LIMM", "Milano", "LI", 3) }),
            _ => throw new NotSupportedException($"{m.Name} non doveva essere chiamato."),
        };
    }

    public class Nessuno : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? m, object?[]? a) =>
            throw new NotSupportedException($"{m?.Name} non doveva essere chiamato.");
    }

    private readonly Contesto _db = new();
    private readonly GerarchiaFinta _gerarchia;
    private readonly RipieghiFinti _ripieghi;

    public StrutturaUnaOperazionePerVoltaTests()
    {
        _gerarchia = new GerarchiaFinta(_db);
        _ripieghi = new RipieghiFinti(_db);
        Services.AddSingleton<IHierarchyEditingService>(_gerarchia);
        Services.AddSingleton<ISectorFallbackService>(_ripieghi);
        Services.AddSingleton<IOrphanSectorService>(new OrfaniFinti(_db));
        Services.AddSingleton(DispatchProxy.Create<IStructureEditingService, StrutturaFinta>());
        Services.AddSingleton(DispatchProxy.Create<IDeletionService, Nessuno>());
        Services.AddSingleton<IOnlineAtcProvider>(new NessunoOnline());
        Services.AddSingleton<IEditAuthorizationService>(new Editore());
        Services.AddScoped<IResourceLockService>(_ => new LockMio());
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<StringheDelSito>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<StrutturaPage> Apri()
    {
        var cut = RenderComponent<StrutturaPage>();
        cut.WaitForElement("#hn-Acc-2", TimeSpan.FromSeconds(3));
        return cut;
    }

    /// <summary>Ricerca e gesto sul dispatcher: fra le due un render da un altro thread può cambiare l'albero, e
    /// il gestore trovato non esisterebbe più (rosso intermittente visto sul runner il 27 settembre 2026).</summary>
    private static Task Premi(IRenderedComponent<StrutturaPage> cut, string selettore, Func<AngleSharp.Dom.IElement, Task> gesto) =>
        cut.InvokeAsync(() => gesto(cut.Find(selettore)));

    private async Task NessunaCaduta()
    {
        var caduta = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(200));
        if (caduta == Renderer.UnhandledException) Assert.Fail("Circuito caduto: " + await Renderer.UnhandledException);
    }

    /// <summary>La riproduzione dal vivo: il doppio clic vero su un nodo sono due <c>Select</c>. Senza fila la seconda
    /// lettura partiva sopra la prima. Dal 28 settembre 2026 (U-192) il secondo clic, arrivato il suo turno, trova il
    /// nodo già scelto e non rilegge niente: una lettura sola, e nessuna sovrapposizione.</summary>
    [Fact]
    public async Task Doppio_clic_su_un_nodo_legge_una_volta_per_volta()
    {
        var cut = Apri();

        var primo = Premi(cut, "#hn-Acc-2", e => e.ClickAsync(new()));
        var secondo = Premi(cut, "#hn-Acc-2", e => e.ClickAsync(new()));
        await Task.WhenAll(primo, secondo);
        cut.WaitForAssertion(() => Assert.True(_ripieghi.Letture >= 1), TimeSpan.FromSeconds(3));
        await Task.Delay(150);

        Assert.Equal(1, _db.MassimoInsieme);
        Assert.Equal(1, _ripieghi.Letture);
        await NessunaCaduta();
    }

    /// <summary>
    /// 🔴 U-192 (revisione totale 3): cliccare un nodo dell'albero — anche lo STESSO — rileggeva la catena di ripiego e
    /// buttava via in silenzio quella che si stava scrivendo. Ora lo stesso nodo non rilegge, e con righe non salvate
    /// cambiare nodo chiede prima: «Resta» le tiene, «Scarta e cambia» le butta davvero.
    /// </summary>
    [Fact]
    public async Task Con_la_catena_non_salvata_cambiare_nodo_chiede_prima()
    {
        _ripieghi.Righe.Add(new FallbackRowEdit("LIMM_CTR", null, 24500));
        var cut = Apri();
        await Premi(cut, "#hn-Acc-2", e => e.ClickAsync(new()));
        cut.WaitForAssertion(() => Assert.Equal(1, _ripieghi.Letture), TimeSpan.FromSeconds(3));

        // Si toglie la riga: la catena a schermo non è più quella salvata.
        await cut.InvokeAsync(() => cut.Find("button[title=Struct_Fallback_Remove]").Click());
        cut.WaitForAssertion(() => Assert.Contains("Common_Unsaved", cut.Markup));

        // Lo stesso nodo: niente.
        await Premi(cut, "#hn-Acc-2", e => e.ClickAsync(new()));
        await Task.Delay(100);
        Assert.Equal(1, _ripieghi.Letture);
        Assert.Contains("Common_Unsaved", cut.Markup);

        // Un altro nodo: la domanda, e le righe restano.
        await Premi(cut, "#hn-AirportPosition-3", e => e.ClickAsync(new()));
        cut.WaitForAssertion(() => Assert.Contains("Struct_Fallback_UnsavedSwitch", cut.Markup));
        Assert.Equal(1, _ripieghi.Letture);

        await cut.InvokeAsync(() => cut.FindAll("button").First(x => x.TextContent.Contains("Struct_Fallback_Stay")).Click());
        Assert.DoesNotContain("Struct_Fallback_UnsavedSwitch", cut.Markup);
        Assert.Contains("Common_Unsaved", cut.Markup);

        // E se si sceglie di scartare, si cambia davvero.
        await Premi(cut, "#hn-AirportPosition-3", e => e.ClickAsync(new()));
        cut.WaitForAssertion(() => Assert.Contains("Struct_Fallback_UnsavedSwitch", cut.Markup));
        await cut.InvokeAsync(() => cut.FindAll("button").First(x => x.TextContent.Contains("Struct_Fallback_DiscardSwitch")).ClickAsync(new()));
        cut.WaitForAssertion(() => Assert.Equal(2, _ripieghi.Letture), TimeSpan.FromSeconds(3));
        await NessunaCaduta();
    }

    /// <summary>
    /// 🔴 U-192: «Fine modifica» con la catena non salvata non rilascia il lock (come le righe dei limiti in ACC): senza
    /// lock le righe resterebbero a schermo senza più modo di salvarle. E il perché si vede SUBITO: la barra chiama un
    /// <c>Func</c>, non un <c>EventCallback</c>, e dal vivo il messaggio compariva solo al gesto dopo.
    /// </summary>
    [Fact]
    public async Task Fine_modifica_con_la_catena_non_salvata_non_rilascia_e_dice_perche()
    {
        _ripieghi.Righe.Add(new FallbackRowEdit("LIMM_CTR", null, 24500));
        var cut = Apri();
        await Premi(cut, "#hn-Acc-2", e => e.ClickAsync(new()));
        cut.WaitForAssertion(() => Assert.Equal(1, _ripieghi.Letture), TimeSpan.FromSeconds(3));
        await cut.InvokeAsync(() => cut.Find("button[title=Struct_Fallback_Remove]").Click());

        await cut.InvokeAsync(() => cut.FindAll("button").First(x => x.TextContent.Contains("Lock_FinishEdit")).ClickAsync(new()));

        Assert.Contains("Struct_Fallback_UnsavedRelease", cut.Markup);
        Assert.Contains(cut.FindAll("button"), x => x.TextContent.Contains("Lock_FinishEdit"));   // il lock resta

        // Scartata la catena, l'avviso non vale più e sparisce.
        await Premi(cut, "#hn-AirportPosition-3", e => e.ClickAsync(new()));
        cut.WaitForAssertion(() => Assert.Contains("Struct_Fallback_UnsavedSwitch", cut.Markup));
        await cut.InvokeAsync(() => cut.FindAll("button").First(x => x.TextContent.Contains("Struct_Fallback_DiscardSwitch")).ClickAsync(new()));
        cut.WaitForAssertion(() => Assert.DoesNotContain("Struct_Fallback_UnsavedRelease", cut.Markup));
        await NessunaCaduta();
    }

    /// <summary>🔴 U-190: «Proponi» era in fila ma senza catch: un guasto del servizio usciva dal gestore.</summary>
    [Fact]
    public async Task Un_guasto_delle_proposte_resta_un_messaggio()
    {
        _ripieghi.LanciaProposte = new InvalidOperationException("geometria illeggibile");
        var cut = Apri();
        await Premi(cut, "#hn-Acc-2", e => e.ClickAsync(new()));
        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll("button"), x => x.TextContent.Contains("Struct_Fallback_Suggest")),
            TimeSpan.FromSeconds(3));

        await cut.InvokeAsync(() => cut.FindAll("button").First(x => x.TextContent.Contains("Struct_Fallback_Suggest")).ClickAsync(new()));

        await NessunaCaduta();
        cut.WaitForAssertion(() => Assert.Contains("geometria illeggibile", cut.Markup));
    }

    /// <summary>Lo scenario della scheda: si trascina l'APP sotto un nuovo padre e, mentre salva e riproietta, si
    /// clicca un altro nodo per controllarlo. Il clic aspetta il suo turno — e arriva, non si perde.</summary>
    [Fact]
    public async Task Clic_su_un_nodo_mentre_il_trascinamento_salva_aspetta_il_suo_turno()
    {
        var cut = Apri();

        await Premi(cut, "#hn-AirportPosition-3", e => e.DragStartAsync(new()));
        var trascina = Premi(cut, "#hn-Acc-2", e => e.DropAsync(new()));
        cut.WaitForAssertion(() => Assert.Equal(1, _gerarchia.Salvataggi), TimeSpan.FromSeconds(3));

        var clic = Premi(cut, "#hn-Acc-1", e => e.ClickAsync(new()));
        await Task.Delay(150);   // il clic è partito mentre il salvataggio è ancora in volo
        _gerarchia.Salvataggio.SetResult();
        await Task.WhenAll(trascina, clic);

        cut.WaitForAssertion(() => Assert.Contains("sel", cut.Find("#hn-Acc-1").ClassList), TimeSpan.FromSeconds(3));
        await Task.Delay(150);

        Assert.Equal(1, _db.MassimoInsieme);
        await NessunaCaduta();
    }

    /// <summary>Due rilasci nello stesso istante: la sentinella di <c>Guarded</c> ne fa passare uno.</summary>
    [Fact]
    public async Task Due_rilasci_nello_stesso_istante_salvano_una_volta()
    {
        var cut = Apri();

        await Premi(cut, "#hn-AirportPosition-3", e => e.DragStartAsync(new()));
        var primo = Premi(cut, "#hn-Acc-2", e => e.DropAsync(new()));
        await Premi(cut, "#hn-AirportPosition-3", e => e.DragStartAsync(new()));
        var secondo = Premi(cut, "#hn-Acc-2", e => e.DropAsync(new()));
        await Task.Delay(150);
        _gerarchia.Salvataggio.SetResult();
        await Task.WhenAll(primo, secondo);
        await Task.Delay(150);

        Assert.Equal(1, _gerarchia.Salvataggi);
        Assert.Equal(1, _db.MassimoInsieme);
        await NessunaCaduta();
    }
}
