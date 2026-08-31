using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il catalogo delle stazioni deve invecchiare quando qualcuno lo cambia, e <b>non</b> rileggersi quando
/// nessuno l'ha toccato.
///
/// <para><b>Il guasto vero, 19 agosto 2026.</b> Rendendo visibili due ACC nascosti, la pagina degli accordi
/// continuava a mostrare l'elenco di prima e — con l'ACC scelto ormai irrisolvibile — restava su titolo e
/// riga ACC, apparentemente rotta. Il menu in alto invece era già giusto: il chrome è SSR e ha uno scope per
/// richiesta, la pagina è interattiva e il suo scope è il <b>circuito</b>, cioè l'intera sessione. Due elenchi
/// diversi nella stessa schermata, e nessuno dei due sbagliato dal proprio punto di vista.</para>
///
/// <para>Da qui il contatore di processo: chi scrive lo alza, chi legge se ne accorge.</para>
///
/// <para>⚠️ <b>31 agosto 2026 — la copia non sta più nel resolver ma in <see cref="CatalogoStazioni"/>, che
/// è singleton.</b> Quel che questi test dicevano resta vero parola per parola; cambia che adesso la
/// lettura è <b>una per processo</b> e non una per sessione, e c'è un test in fondo che tiene ferma proprio
/// quella differenza. È il rimedio alla radice delle corse sul <c>DbContext</c>: una lettura che avviene
/// una volta sola non ha nessuno contro cui correre.</para>
/// </summary>
public class StationResolverCacheTests
{
    private sealed class DirettorioFinto : IStationDirectory
    {
        private readonly List<AccInfo> _accs;
        public int LettureAcc { get; private set; }
        public int LettureAeroporti { get; private set; }

        public DirettorioFinto(params string[] codici) =>
            _accs = codici.Select(c => new AccInfo(c, c + " name")).ToList();

        public void Mostra(string codice) => _accs.Add(new AccInfo(codice, codice + " name"));

        public IReadOnlyList<AccInfo> ListAccs()
        {
            LettureAcc++;
            return _accs.ToList();
        }

        public IReadOnlyList<AirportStation> ListAirports()
        {
            LettureAeroporti++;
            return new[] { new AirportStation("LIRF", "LIRR") };
        }
    }

    /// <summary>
    /// Il catalogo di processo, nuovo a ogni test. ⚠️ Nella vita vera è <b>uno solo</b> e vive quanto il
    /// processo: qui se ne fa uno per test, o i test si passerebbero le copie fra loro.
    /// </summary>
    private static StationResolver Sessione(IStationDirectory dir, ICatalogoStazioni catalogo) =>
        new(dir, catalogo);

    [Fact]
    public void Senza_cambiamenti_legge_il_database_una_volta_sola()
    {
        var dir = new DirettorioFinto("LIBB", "LIRR");
        var resolver = Sessione(dir, new CatalogoStazioni(new StationCatalogVersion()));

        _ = resolver.Accs;
        _ = resolver.Accs;
        _ = resolver.Resolve("LIRR");

        Assert.Equal(1, dir.LettureAcc);
    }

    [Fact]
    public void Dopo_un_cambio_di_catalogo_rilegge()
    {
        var dir = new DirettorioFinto("LIBB", "LIRR");
        var versione = new StationCatalogVersion();
        var resolver = Sessione(dir, new CatalogoStazioni(versione));

        Assert.Equal(2, resolver.Accs.Count);
        Assert.Null(resolver.Resolve("LIVK"));   // ancora nascosto

        // Qualcuno lo rende visibile. Nella vita vera la spinta non la dà più un servizio che se ne ricorda,
        // ma BumpCatalogoStazioniInterceptor, sul salvataggio.
        dir.Mostra("LIVK");
        versione.Bump();

        Assert.Equal(3, resolver.Accs.Count);
        Assert.NotNull(resolver.Resolve("LIVK"));
        Assert.Equal(2, dir.LettureAcc);
    }

    [Fact]
    public void Il_bump_scade_anche_la_mappa_degli_aeroporti()
    {
        var dir = new DirettorioFinto("LIRR");
        var versione = new StationCatalogVersion();
        var resolver = Sessione(dir, new CatalogoStazioni(versione));

        resolver.Prewarm();
        Assert.Equal(1, dir.LettureAeroporti);

        resolver.Prewarm();
        Assert.Equal(1, dir.LettureAeroporti);   // niente è cambiato: nessuna query

        versione.Bump();
        resolver.Prewarm();
        Assert.Equal(2, dir.LettureAeroporti);
    }

    /// <summary>
    /// ⚠️ Il contatore è di PROCESSO, non di sessione: chi nasconde un ACC lo nasconde a tutti quelli che
    /// stanno guardando, non solo a sé.
    /// </summary>
    [Fact]
    public void Il_contatore_vale_per_tutte_le_sessioni_aperte()
    {
        var versione = new StationCatalogVersion();
        var catalogo = new CatalogoStazioni(versione);
        var dirA = new DirettorioFinto("LIBB");
        var dirB = new DirettorioFinto("LIBB");
        var sessioneA = Sessione(dirA, catalogo);
        var sessioneB = Sessione(dirB, catalogo);

        _ = sessioneA.Accs;
        _ = sessioneB.Accs;

        dirA.Mostra("LIVK");
        dirB.Mostra("LIVK");
        versione.Bump();   // lo alza chi ha scritto: una sola volta, per tutti

        Assert.NotNull(sessioneA.Resolve("LIVK"));
        Assert.NotNull(sessioneB.Resolve("LIVK"));
    }

    // ────────────────────────────────────────────────────────────────────────────────────────────────
    // 31 agosto 2026 — la copia è di PROCESSO
    // ────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Venti sessioni, <b>una</b> lettura. È il cuore del rimedio: prima ogni circuito rileggeva ACC e
    /// aeroporti per conto suo, e quella lettura — che cade nel ciclo di vita di ogni pagina — è la stessa
    /// che è finita nello stack di tre guasti in una settimana.
    /// </summary>
    [Fact]
    public void Venti_sessioni_leggono_il_catalogo_una_volta_sola()
    {
        var catalogo = new CatalogoStazioni(new StationCatalogVersion());
        var dir = new DirettorioFinto("LIBB", "LIRR");

        for (var i = 0; i < 20; i++) Sessione(dir, catalogo).Prewarm();

        Assert.Equal(1, dir.LettureAcc);
        Assert.Equal(1, dir.LettureAeroporti);
    }

    /// <summary>
    /// ⚠️ E la partenza a freddo è proprio il momento in cui arrivano <b>insieme</b>: con
    /// <c>MaximumPoolSize=20</c>, venti letture uguali in parallelo sono il modo di prendersi il pool intero
    /// per un elenco di sette righe. Ne deve partire una, e le altre aspettano quella.
    /// </summary>
    [Fact]
    public async Task Anche_arrivando_tutte_insieme_la_lettura_e_una_sola()
    {
        var catalogo = new CatalogoStazioni(new StationCatalogVersion());
        var dir = new DirettorioFinto("LIBB", "LIRR");
        var via = new TaskCompletionSource();

        var sessioni = Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            await via.Task;                  // partono tutte allo stesso istante
            Sessione(dir, catalogo).Prewarm();
        })).ToArray();

        via.SetResult();
        await Task.WhenAll(sessioni);

        Assert.Equal(1, dir.LettureAcc);
        Assert.Equal(1, dir.LettureAeroporti);
    }

    /// <summary>
    /// Se la lettura <b>fallisce</b>, non si mette in cache il guasto: il prossimo che passa ritenta.
    /// ⚠️ Senza questo, un singhiozzo del database durante la prima lettura dopo un riavvio spegnerebbe il
    /// catalogo per tutti e per sempre — che è esattamente il difetto che questa classe deve togliere, non
    /// spostare più in alto.
    /// </summary>
    [Fact]
    public void Una_lettura_fallita_non_si_tiene()
    {
        var catalogo = new CatalogoStazioni(new StationCatalogVersion());
        var tentativi = 0;

        IReadOnlyList<AccInfo> Rotta()
        {
            tentativi++;
            throw new InvalidOperationException("Cannot Open when State is Connecting.");
        }

        Assert.Throws<InvalidOperationException>(() => catalogo.Accs(Rotta));
        Assert.Throws<InvalidOperationException>(() => catalogo.Accs(Rotta));
        Assert.Equal(2, tentativi);

        // E quando il database torna, la copia si riempie senza che nessuno debba riavviare niente.
        var buone = new[] { new AccInfo("LIBB", "Brindisi") };
        Assert.Single(catalogo.Accs(() => buone));
    }

    /// <summary>
    /// ⚠️ Una scrittura che arriva <b>mentre la query è in volo</b> non deve restare fuori. La versione si
    /// legge PRIMA della lettura: se cambia nel frattempo la copia nasce già vecchia, e il prossimo rilegge.
    /// Al contrario si terrebbe per buona una fotografia scattata prima di quella scrittura, e da lì in poi
    /// nessuno rileggerebbe più.
    /// </summary>
    [Fact]
    public void Una_scrittura_durante_la_lettura_non_resta_fuori()
    {
        var versione = new StationCatalogVersion();
        var catalogo = new CatalogoStazioni(versione);
        var letture = 0;

        IReadOnlyList<AccInfo> LeggiEIntanto()
        {
            letture++;
            if (letture == 1) versione.Bump();   // qualcuno scrive mentre questa query è in volo
            return new[] { new AccInfo("LIBB", "Brindisi") };
        }

        _ = catalogo.Accs(LeggiEIntanto);
        _ = catalogo.Accs(LeggiEIntanto);

        Assert.Equal(2, letture);
    }
}
