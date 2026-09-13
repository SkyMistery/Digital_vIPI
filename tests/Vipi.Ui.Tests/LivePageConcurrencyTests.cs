using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Application.Live;
using Vipi.Ui.Pages;
using Vipi.Application.Routing;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La vista live si ricarica da <b>due</b> ingressi indipendenti: il ciclo di vita del componente e il
/// callback SSE <c>OnLiveUpdate</c>, che il poller ATC invoca a ogni giro. Entrambi finiscono in
/// <c>LoadAsync</c>, che legge dal database attraverso il <c>DbContext</c> del circuito — e quel context
/// non ammette due operazioni sovrapposte: EF risponde «A second operation was started on this context
/// instance» e il circuito muore, lasciando la pagina ferma al prerender.
///
/// <para><b>Come è emersa.</b> Non dai test — da <c>/services/vsop/live/{callsign}</c> guidata su MariaDB nella
/// verifica live A6, dove le query divise e la latenza allargano la finestra della corsa quanto basta a
/// renderla sistematica. È una corsa, però, non un difetto del provider: su SQLite e Postgres capita solo
/// quando un aggiornamento atterra nell'istante giusto, che è il modo peggiore di avere un guasto.</para>
///
/// <para>Il test non prova a riprodurre la tempistica: sostituisce il servizio con uno che <b>si accorge</b>
/// di essere entrato due volte insieme, e lancia i due ingressi in parallelo.</para>
/// </summary>
public class LivePageConcurrencyTests : TestContext
{
    /// <summary>Servizio finto che registra la sovrapposizione: se due chiamate coesistono, se ne accorge.</summary>
    private sealed class ServizioCheContaLeSovrapposizioni : ILiveViewService
    {
        private int _dentro;
        public int MassimaSovrapposizione;
        public int Chiamate;

        public string? MyCallsign() => "LIBB_ES_CTR";
        public OnlineAtcSnapshot Snapshot() => OnlineAtcSnapshot.Empty;

        public async Task<LiveViewResult> BuildAsync(string callsign, CancellationToken ct = default)
        {
            var ora = Interlocked.Increment(ref _dentro);
            Interlocked.Increment(ref Chiamate);
            // Il massimo osservato è l'unica cosa che conta: 2 significa che due letture erano in volo insieme.
            InterlockedMax(ref MassimaSovrapposizione, ora);
            try { await Task.Delay(60, ct); }        // finestra in cui l'altra chiamata farebbe in tempo a entrare
            finally { Interlocked.Decrement(ref _dentro); }
            return LiveViewResult.NotFound(callsign);
        }

        /// <summary>Il selettore e' roba da staff di divisione: questa pagina lo rende con un utente normale,
        /// quindi l'elenco non si chiede mai. Se qualcuno lo chiedesse, il test deve accorgersene.</summary>
        public Task<IReadOnlyList<LiveStationOption>> ListStationsAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("Un utente normale non ha selettore: l'elenco non si deve chiedere.");

        private static void InterlockedMax(ref int bersaglio, int valore)
        {
            int visto;
            while ((visto = Volatile.Read(ref bersaglio)) < valore &&
                   Interlocked.CompareExchange(ref bersaglio, valore, visto) != visto) { }
        }
    }

    /// <summary>Utente normale: nessun selettore, e la propria postazione si apre lo stesso.</summary>
    private sealed class UtenteNormale : IEditAuthorizationService
    {
        public VipiRole Role => VipiRole.User;
        public bool IsAdmin => false;
        public int? CurrentUserId => null;
        public string? CurrentName => null;
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    /// <summary>Il servizio si ferma dentro il caricamento finché il test non lo lascia andare.</summary>
    private sealed class ServizioCheAspetta : ILiveViewService
    {
        public TaskCompletionSource Dentro { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Via { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _chiamate;

        public string? MyCallsign() => "LIBB_ES_CTR";
        public OnlineAtcSnapshot Snapshot() => OnlineAtcSnapshot.Empty;

        public async Task<LiveViewResult> BuildAsync(string callsign, CancellationToken ct = default)
        {
            // Il primo è il caricamento del ciclo di vita: passa. Il secondo (il tick) resta in volo.
            if (Interlocked.Increment(ref _chiamate) > 1)
            {
                Dentro.TrySetResult();
                await Via.Task;
            }
            return LiveViewResult.NotFound(callsign);
        }

        public Task<IReadOnlyList<LiveStationOption>> ListStationsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LiveStationOption>>(Array.Empty<LiveStationOption>());
    }

    private void Servizi(ILiveViewService servizio)
    {
        Services.AddSingleton(servizio);
        Services.AddSingleton<IDocRoutesRegistry>(new DocRoutesRegistry(Array.Empty<IDocKindRoutes>()));
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<IEditAuthorizationService>(new UtenteNormale());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddSingleton(new EnglishStrings());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>
    /// 🔴 T-037 (revisione del 13 settembre 2026): si lascia la vista live mentre un caricamento è in volo.
    /// <c>DisposeAsync</c> smaltiva il semaforo, e il <c>Release()</c> del caricamento che tornava dopo
    /// sollevava <c>ObjectDisposedException</c>: circuito abbattuto. È la regola di T-013 — il semaforo non si
    /// smaltisce.
    /// </summary>
    [Fact]
    public async Task Lasciare_la_pagina_a_caricamento_in_volo_non_abbatte_il_circuito()
    {
        var servizio = new ServizioCheAspetta();
        Servizi(servizio);

        var cut = RenderComponent<LivePage>(p => p.Add(x => x.Callsign, "LIBB_ES_CTR"));
        var tick = cut.InvokeAsync(() => cut.Instance.OnLiveUpdate());
        await servizio.Dentro.Task;

        DisposeComponents();
        servizio.Via.SetResult();

        var ex = await Record.ExceptionAsync(() => tick);
        Assert.Null(ex);
        var caduta = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(500));
        if (caduta == Renderer.UnhandledException)
            Assert.Fail("Eccezione non gestita: " + await Renderer.UnhandledException);
    }

    [Fact]
    public async Task Il_callback_live_non_si_sovrappone_al_caricamento_del_ciclo_di_vita()
    {
        var servizio = new ServizioCheContaLeSovrapposizioni();
        Services.AddSingleton<ILiveViewService>(servizio);
        Services.AddSingleton<IDocRoutesRegistry>(new DocRoutesRegistry(Array.Empty<IDocKindRoutes>()));
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<IEditAuthorizationService>(new UtenteNormale());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        // La briciola di pane legge le stringhe in INGLESE FISSO (regole-lingua R3): senza questo
        // servizio la pagina non si costruisce nemmeno.
        Services.AddSingleton(new EnglishStrings());
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<LivePage>(p => p.Add(x => x.Callsign, "LIBB_ES_CTR"));

        // Due tick SSE ravvicinati mentre il primo caricamento può essere ancora in volo: è lo scenario vero,
        // il poller notifica a ogni giro e non aspetta che la pagina abbia finito.
        await Task.WhenAll(
            cut.InvokeAsync(() => cut.Instance.OnLiveUpdate()),
            cut.InvokeAsync(() => cut.Instance.OnLiveUpdate()));

        Assert.True(servizio.Chiamate >= 2, $"i caricamenti dovevano essere almeno 2, sono stati {servizio.Chiamate}");
        Assert.True(servizio.MassimaSovrapposizione == 1,
            "due caricamenti della vista live si sono sovrapposti: sullo stesso DbContext di circuito questo " +
            "è «A second operation was started on this context instance», che uccide il circuito.");
    }
}
