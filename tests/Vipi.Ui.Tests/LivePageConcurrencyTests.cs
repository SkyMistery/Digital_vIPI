using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
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

        private static void InterlockedMax(ref int bersaglio, int valore)
        {
            int visto;
            while ((visto = Volatile.Read(ref bersaglio)) < valore &&
                   Interlocked.CompareExchange(ref bersaglio, valore, visto) != visto) { }
        }
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    [Fact]
    public async Task Il_callback_live_non_si_sovrappone_al_caricamento_del_ciclo_di_vita()
    {
        var servizio = new ServizioCheContaLeSovrapposizioni();
        Services.AddSingleton<ILiveViewService>(servizio);
        Services.AddSingleton<IDocRoutesRegistry>(new DocRoutesRegistry(Array.Empty<IDocKindRoutes>()));
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
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
