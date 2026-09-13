using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Ivao;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// 🔴 T-034 (revisione del 13 settembre 2026): la fotografia degli ATC online non scadeva. Con il whazzup giù il
/// poller tiene l'ultima fotografia, e il pallino «in frequenza», la presidenza degli aeroporti e i punti di
/// trasferimento mostravano per ore controllori che avevano staccato.
/// </summary>
public class OnlineAtcCacheScadenzaTests
{
    private static OnlineAtcSnapshot Fotografia(DateTimeOffset quando) => new()
    {
        Callsigns = new HashSet<string>(new[] { "LIRF_TWR" }, StringComparer.OrdinalIgnoreCase),
        Details = new[] { new OnlineAtc("LIRF_TWR", 704798, "UserId 704798", 5) },
        AsOf = quando,
    };

    [Fact]
    public void Una_fotografia_vecchia_di_un_ora_non_mostra_nessuno_online()
    {
        var cache = new OnlineAtcCache();
        cache.Set(Fotografia(DateTimeOffset.UtcNow.AddHours(-1)));

        var letta = cache.GetCurrent();

        Assert.Empty(letta.Callsigns);
        Assert.Empty(letta.Details);
    }

    [Fact]
    public void La_scadenza_tiene_l_ora_dell_ultimo_dato_e_lo_dice()
    {
        var orologio = new Orologio(new DateTimeOffset(2026, 9, 13, 18, 0, 0, TimeSpan.Zero));
        var cache = new OnlineAtcCache(orologio, TimeSpan.FromMinutes(5));
        var ultima = orologio.GetUtcNow();
        cache.Set(Fotografia(ultima));

        orologio.Avanti(TimeSpan.FromMinutes(5));
        var ancoraBuona = cache.GetCurrent();
        Assert.False(ancoraBuona.Expired);
        Assert.Contains("LIRF_TWR", ancoraBuona.Callsigns);

        orologio.Avanti(TimeSpan.FromSeconds(1));
        var scaduta = cache.GetCurrent();
        Assert.True(scaduta.Expired);
        Assert.Empty(scaduta.Callsigns);
        Assert.Equal(ultima, scaduta.AsOf);          // la pagina live dice ancora «ultimo dato alle 18:00Z»
    }

    [Fact]
    public void Una_fotografia_nuova_torna_a_mostrare_gli_online()
    {
        var orologio = new Orologio(new DateTimeOffset(2026, 9, 13, 18, 0, 0, TimeSpan.Zero));
        var cache = new OnlineAtcCache(orologio, TimeSpan.FromMinutes(5));
        cache.Set(Fotografia(orologio.GetUtcNow()));
        orologio.Avanti(TimeSpan.FromHours(1));
        Assert.True(cache.GetCurrent().Expired);

        cache.Set(Fotografia(orologio.GetUtcNow()));

        Assert.False(cache.GetCurrent().Expired);
        Assert.Contains("LIRF_TWR", cache.GetCurrent().Callsigns);
    }

    [Fact]
    public void La_soglia_non_scende_sotto_tre_giri_del_poller()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), OnlineAtcCache.ScadenzaPer(TimeSpan.FromSeconds(60)));
        Assert.Equal(TimeSpan.FromMinutes(15), OnlineAtcCache.ScadenzaPer(TimeSpan.FromMinutes(5)));
    }

    private sealed class Orologio(DateTimeOffset adesso) : TimeProvider
    {
        private DateTimeOffset _adesso = adesso;
        public override DateTimeOffset GetUtcNow() => _adesso;
        public void Avanti(TimeSpan quanto) => _adesso += quanto;
    }
}
