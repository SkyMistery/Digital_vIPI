using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La cache del resolver deve invecchiare quando il catalogo ACC cambia.
///
/// <para><b>Il guasto vero, 19 agosto 2026.</b> Rendendo visibili due ACC nascosti, la pagina degli accordi
/// continuava a mostrare l'elenco di prima e — con l'ACC scelto ormai irrisolvibile — restava su titolo e
/// riga ACC, apparentemente rotta. Il menu in alto invece era già giusto: il chrome è SSR e ha uno scope per
/// richiesta, la pagina è interattiva e il suo scope è il <b>circuito</b>, cioè l'intera sessione. Due elenchi
/// diversi nella stessa schermata, e nessuno dei due sbagliato dal proprio punto di vista.</para>
///
/// <para>Da qui il contatore di processo: chi scrive lo alza, chi legge se ne accorge. Questi test tengono le
/// due metà — non rileggere quando nulla è cambiato, rileggere quando qualcosa è cambiato.</para>
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

    [Fact]
    public void Senza_cambiamenti_legge_il_database_una_volta_sola()
    {
        var dir = new DirettorioFinto("LIBB", "LIRR");
        var resolver = new StationResolver(dir, new StationCatalogVersion());

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
        var resolver = new StationResolver(dir, versione);

        Assert.Equal(2, resolver.Accs.Count);
        Assert.Null(resolver.Resolve("LIVK"));   // ancora nascosto

        // Qualcuno lo rende visibile: nella vita vera è AccAdminService.SetHiddenAsync, che scrive e poi alza
        // il contatore. Il resolver di QUESTA sessione non ha fatto nulla e non sa nulla.
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
        var resolver = new StationResolver(dir, versione);

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
    /// stanno guardando, non solo a sé. È la ragione per cui è un singleton e non un servizio scoped.
    /// </summary>
    [Fact]
    public void Il_contatore_vale_per_tutte_le_sessioni_aperte()
    {
        var versione = new StationCatalogVersion();
        var dirA = new DirettorioFinto("LIBB");
        var dirB = new DirettorioFinto("LIBB");
        var sessioneA = new StationResolver(dirA, versione);
        var sessioneB = new StationResolver(dirB, versione);

        _ = sessioneA.Accs;
        _ = sessioneB.Accs;

        dirA.Mostra("LIVK");
        dirB.Mostra("LIVK");
        versione.Bump();   // lo alza chi ha scritto: una sola volta, per tutti

        Assert.NotNull(sessioneA.Resolve("LIVK"));
        Assert.NotNull(sessioneB.Resolve("LIVK"));
    }
}
