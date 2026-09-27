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

        public Task<IReadOnlyList<FallbackRowEdit>> ListAsync(string sectorCallsign, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Letture);
            return db.Pausa<IReadOnlyList<FallbackRowEdit>>(Array.Empty<FallbackRowEdit>());
        }

        public Task<string?> RipiegoAutomaticoAsync(string sectorCallsign, CancellationToken ct = default) =>
            db.Pausa<string?>(null);

        public Task ReplaceAsync(string sectorCallsign, IReadOnlyList<FallbackRowEdit> rows, CancellationToken ct = default) =>
            db.Pausa(0);

        public Task<IReadOnlyList<FallbackSuggestion>> SuggestAsync(string sectorCallsign, CancellationToken ct = default) =>
            db.Pausa<IReadOnlyList<FallbackSuggestion>>(Array.Empty<FallbackSuggestion>());
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

    private async Task NessunaCaduta()
    {
        var caduta = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(200));
        if (caduta == Renderer.UnhandledException) Assert.Fail("Circuito caduto: " + await Renderer.UnhandledException);
    }

    /// <summary>La riproduzione dal vivo: il doppio clic vero su un nodo sono due <c>Select</c>, e ognuno legge i
    /// ripieghi. Senza fila la seconda lettura partiva sopra la prima.</summary>
    [Fact]
    public async Task Doppio_clic_su_un_nodo_legge_una_volta_per_volta()
    {
        var cut = Apri();

        var primo = cut.Find("#hn-Acc-2").ClickAsync(new());
        var secondo = cut.Find("#hn-Acc-2").ClickAsync(new());
        await Task.WhenAll(primo, secondo);
        cut.WaitForAssertion(() => Assert.True(_ripieghi.Letture >= 2), TimeSpan.FromSeconds(3));
        await Task.Delay(150);

        Assert.Equal(1, _db.MassimoInsieme);
        await NessunaCaduta();
    }

    /// <summary>Lo scenario della scheda: si trascina l'APP sotto un nuovo padre e, mentre salva e riproietta, si
    /// clicca un altro nodo per controllarlo. Il clic aspetta il suo turno — e arriva, non si perde.</summary>
    [Fact]
    public async Task Clic_su_un_nodo_mentre_il_trascinamento_salva_aspetta_il_suo_turno()
    {
        var cut = Apri();

        await cut.Find("#hn-AirportPosition-3").DragStartAsync(new());
        var trascina = cut.Find("#hn-Acc-2").DropAsync(new());
        cut.WaitForAssertion(() => Assert.Equal(1, _gerarchia.Salvataggi), TimeSpan.FromSeconds(3));

        var clic = cut.Find("#hn-Acc-1").ClickAsync(new());
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

        await cut.Find("#hn-AirportPosition-3").DragStartAsync(new());
        var primo = cut.Find("#hn-Acc-2").DropAsync(new());
        await cut.Find("#hn-AirportPosition-3").DragStartAsync(new());
        var secondo = cut.Find("#hn-Acc-2").DropAsync(new());
        await Task.Delay(150);
        _gerarchia.Salvataggio.SetResult();
        await Task.WhenAll(primo, secondo);
        await Task.Delay(150);

        Assert.Equal(1, _gerarchia.Salvataggi);
        Assert.Equal(1, _db.MassimoInsieme);
        await NessunaCaduta();
    }
}
