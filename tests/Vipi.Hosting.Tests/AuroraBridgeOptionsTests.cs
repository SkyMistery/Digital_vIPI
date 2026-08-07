using Vipi.Application.Content;
using Vipi.Hosting;
using Xunit;

namespace Vipi.Hosting.Tests;

/// <summary>
/// Configurazione e tetto di richieste dell'endpoint del bridge Aurora. Il limitatore protegge un endpoint
/// ANONIMO interrogato in polling da ogni tool desktop: senza tetto basta un client difettoso a caricare il DB.
/// </summary>
public class AuroraBridgeOptionsTests
{
    [Fact]
    public void Senza_configurazione_valgono_i_default()
    {
        var opt = new AuroraBridgeOptions().ToMatchOptions();

        Assert.Equal(AuroraLabelConvention.Number, opt.Convention);
        Assert.Equal(8, opt.MaxCandidates);
    }

    [Theory]
    [InlineData("FlPrefixed", AuroraLabelConvention.FlPrefixed)]
    [InlineData("flprefixed", AuroraLabelConvention.FlPrefixed)]
    [InlineData("Number", AuroraLabelConvention.Number)]
    [InlineData("bislacco", AuroraLabelConvention.Number)]   // valore ignoto → default, non eccezione
    [InlineData(null, AuroraLabelConvention.Number)]
    public void La_convenzione_si_legge_dalla_configurazione(string? configured, AuroraLabelConvention expected)
    {
        var opt = new AuroraBridgeOptions { LabelConvention = configured }.ToMatchOptions();

        Assert.Equal(expected, opt.Convention);
    }

    [Fact]
    public void Un_numero_di_candidati_assurdo_ricade_sul_default()
    {
        Assert.Equal(8, new AuroraBridgeOptions { MaxCandidates = 0 }.ToMatchOptions().MaxCandidates);
        Assert.Equal(8, new AuroraBridgeOptions { MaxCandidates = -3 }.ToMatchOptions().MaxCandidates);
    }

    [Fact]
    public void Il_limitatore_lascia_passare_fino_al_tetto_e_poi_blocca()
    {
        var limiter = new RequestRateLimiter();

        for (var i = 0; i < 3; i++)
            Assert.True(limiter.TryAcquire("1.2.3.4", 3));

        Assert.False(limiter.TryAcquire("1.2.3.4", 3));
    }

    [Fact]
    public void Il_tetto_e_per_chiave()
    {
        var limiter = new RequestRateLimiter();
        Assert.True(limiter.TryAcquire("1.2.3.4", 1));
        Assert.False(limiter.TryAcquire("1.2.3.4", 1));

        // Un altro IP non paga il conto del primo.
        Assert.True(limiter.TryAcquire("5.6.7.8", 1));
    }

    [Fact]
    public void Tetto_non_positivo_significa_nessun_limite()
    {
        var limiter = new RequestRateLimiter();

        for (var i = 0; i < 50; i++)
            Assert.True(limiter.TryAcquire("1.2.3.4", 0));
    }

    /// <summary>
    /// L'endpoint non si monta se non lo si accende: è superficie pubblica e anonima, e il default deve
    /// essere il silenzio. Se questo test diventa rosso, qualcuno ha reso l'API accesa di suo.
    /// </summary>
    [Fact]
    public void Il_bridge_nasce_spento()
        => Assert.False(new AuroraBridgeOptions().Enabled);

    /// <summary>
    /// Il tetto del corpo veniva dichiarato con una costante mentre l'opzione esisteva e non la leggeva
    /// nessuno: chi la configurava non cambiava niente.
    /// </summary>
    [Theory]
    [InlineData(0, 64 * 1024)]        // «illimitato» su un endpoint anonimo non è mai ciò che si intendeva
    [InlineData(-5, 64 * 1024)]
    [InlineData(4096, 4096)]
    public void Il_tetto_del_corpo_si_legge_dalla_configurazione(int configurato, int atteso)
        => Assert.Equal(atteso, new AuroraBridgeOptions { MaxRequestBytes = configurato }.EffectiveMaxRequestBytes);

    /// <summary>
    /// Il tetto per IP non protegge da un avversario: dietro il reverse proxy l'indirizzo arriva da
    /// X-Forwarded-For e lo sceglie il chiamante. Quello complessivo sì, ed è per questo che esiste.
    /// </summary>
    [Fact]
    public void Il_tetto_complessivo_regge_anche_se_la_chiave_cambia_a_ogni_richiesta()
    {
        var limiter = new RequestRateLimiter();

        for (var i = 0; i < 5; i++)
            Assert.True(limiter.TryAcquire(RequestRateLimiter.GlobalKey, 5));

        // Stessa raffica da mille indirizzi diversi: il contatore complessivo è uno solo.
        Assert.False(limiter.TryAcquire(RequestRateLimiter.GlobalKey, 5));
    }

    /// <summary>
    /// Con la chiave spoofabile, un dizionario senza tetto è un esaurimento di memoria a colpi di richieste
    /// da 200 byte. Oltre il tetto una chiave mai vista viene rifiutata, non tracciata.
    /// </summary>
    [Fact]
    public void Le_chiavi_tracciate_hanno_un_tetto()
    {
        var limiter = new RequestRateLimiter();

        for (var i = 0; i < 3; i++)
            Assert.True(limiter.TryAcquire($"10.0.0.{i}", 10, maxTrackedKeys: 3));

        Assert.Equal(3, limiter.TrackedKeys);

        // Quarto indirizzo mai visto: rifiutato, e soprattutto NON aggiunto.
        Assert.False(limiter.TryAcquire("10.0.0.99", 10, maxTrackedKeys: 3));
        Assert.Equal(3, limiter.TrackedKeys);

        // Chi è già tracciato continua a essere servito: il tetto non lo si paga a caso.
        Assert.True(limiter.TryAcquire("10.0.0.1", 10, maxTrackedKeys: 3));
    }

    /// <summary>Il contatore complessivo non conta come «cliente tracciato»: è uno solo e non si spazza.</summary>
    [Fact]
    public void Il_contatore_complessivo_non_si_puo_impersonare()
    {
        var limiter = new RequestRateLimiter();
        Assert.True(limiter.TryAcquire(RequestRateLimiter.GlobalKey, 1));

        // Nessun indirizzo IP può contenere il carattere nullo, quindi nessuno può consumare quel contatore.
        Assert.DoesNotContain('\0', "127.0.0.1");
        Assert.True(limiter.TryAcquire("127.0.0.1", 1));
    }
}
