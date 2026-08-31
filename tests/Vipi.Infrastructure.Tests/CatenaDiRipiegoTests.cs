using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Aor;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La catena di ripiego dichiarata, dal capo del database: si scrive, si rilegge, e soprattutto <b>arriva
/// nella topologia</b> — che è il punto in cui la ricaduta la usa davvero.
///
/// <para>Carta <c>docs/feature/2026-08-31-ricaduta-verticale-e-cicli.md</c> §2.</para>
/// </summary>
public class CatenaDiRipiegoTests : IAsyncLifetime
{
    private const string Ws2 = "LIMM_WS2_CTR", Es2 = "LIMM_ES2_CTR", Ws5 = "LIMM_WS5_CTR", Es5 = "LIMM_ES5_CTR";
    private const string Zrh = "LSAZ_CTR";
    private const string Del = "LIMC_DEL";

    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfSectorFallbackService _svc = default!;
    private TopologyBuilder _topo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();

        var acc = new Acc { Code = "LIMM", Name = "Milano ACC", CountryPrefix = "LI" };
        _db.Accs.Add(acc);
        await _db.SaveChangesAsync();

        foreach (var cs in new[] { Ws2, Es2, Ws5, Es5 })
            _db.AccSectors.Add(new AccSector { ComposePosition = cs, CenterId = "LIMM", Position = "CTR" });

        // Un ACC estero con un settore nella STESSA fascia di quota: sta qui per essere escluso.
        _db.Accs.Add(new Acc { Code = "LSAZ", Name = "Zurigo ACC", CountryPrefix = "LS" });
        await _db.SaveChangesAsync();
        _db.AccSectors.Add(new AccSector { ComposePosition = Zrh, CenterId = "LSAZ", Position = "CTR" });
        await _db.SaveChangesAsync();

        // Una posizione a terra dello stesso ACC: sta qui per essere esclusa (nessun poligono).
        _db.Airports.Add(new Airport { Icao = "LIMC", Name = "Malpensa", AccId = acc.Id });
        await _db.SaveChangesAsync();
        _db.AirportSectors.Add(new AirportSector
        {
            ComposePosition = Del, AirportIcao = "LIMC", AccCode = "LIMM", Position = "DEL",
        });
        await _db.SaveChangesAsync();

        _topo = new TopologyBuilder(_db);
        _svc = new EfSectorFallbackService(_db, new AllowAuthz(), _topo, new VolumiFinti());
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    // =====================================================================================================

    [Fact]
    public async Task Le_righe_si_scrivono_e_si_rileggono_in_ordine()
    {
        await _svc.ReplaceAsync(Es5, new[]
        {
            new FallbackRowEdit(Ws5, 32500, null),
            new FallbackRowEdit(Ws2, null, null),
        });

        var righe = await _svc.ListAsync(Es5);

        Assert.Equal(2, righe.Count);
        Assert.Equal(Ws5, righe[0].TargetCallsign);
        Assert.Equal(32500, righe[0].BaseFeet);
        Assert.Equal(Ws2, righe[1].TargetCallsign);
        Assert.Null(righe[1].BaseFeet);
    }

    /// <summary>Sostituzione, non fusione: una lista vuota toglie tutto e riporta la ricaduta ai soli padri.</summary>
    [Fact]
    public async Task Una_lista_vuota_cancella_le_righe()
    {
        await _svc.ReplaceAsync(Es5, new[] { new FallbackRowEdit(Ws5, 32500, null) });
        await _svc.ReplaceAsync(Es5, Array.Empty<FallbackRowEdit>());

        Assert.Empty(await _svc.ListAsync(Es5));
    }

    /// <summary>⚠️ Il punto vero: le righe devono arrivare nella <see cref="Topology"/>, o non le legge nessuno.</summary>
    [Fact]
    public async Task Le_righe_arrivano_nella_topologia_e_cambiano_la_ricaduta()
    {
        // Alberatura: ES5 figlio di ES2, ES2 e WS5 figli di WS2.
        _db.Sectors.AddRange(
            Settore(Ws2), Settore(Es2), Settore(Ws5), Settore(Es5));
        await _db.SaveChangesAsync();
        await Aggancia(Es2, Ws2); await Aggancia(Ws5, Ws2); await Aggancia(Es5, Es2);

        await _svc.ReplaceAsync(Es5, new[] { new FallbackRowEdit(Ws5, 32500, null) });

        var topo = await _topo.BuildGlobalAsync();
        var online = new HashSet<string>(new[] { Ws2, Es2, Ws5 }, StringComparer.OrdinalIgnoreCase);

        var alto = TransferOnlineResolver.Resolve(
            FallbackChain.Candidates(Es5, 35000, topo.Fallbacks, topo.ParentOf), online);
        var basso = TransferOnlineResolver.Resolve(
            FallbackChain.Candidates(Es5, 25000, topo.Fallbacks, topo.ParentOf), online);

        Assert.Equal(Ws5, alto.Handler);      // sopra FL305: la riga dichiarata
        Assert.Equal(Es2, basso.Handler);     // sotto: il padre, come sempre
    }

    // =====================================================================================================
    //  Quel che si rifiuta
    // =====================================================================================================

    [Fact]
    public async Task Un_bersaglio_inesistente_e_rifiutato() =>
        await Assert.ThrowsAsync<ValidationException>(
            () => _svc.ReplaceAsync(Es5, new[] { new FallbackRowEdit("LXXX_CTR", null, null) }));

    [Fact]
    public async Task Un_settore_non_puo_ripiegare_su_se_stesso() =>
        await Assert.ThrowsAsync<ValidationException>(
            () => _svc.ReplaceAsync(Es5, new[] { new FallbackRowEdit(Es5, null, null) }));

    /// <summary>
    /// Il tetto è ESCLUSO: con piede uguale al tetto la fascia è vuota e la riga non varrebbe mai. Una riga
    /// scritta che non fa niente è peggio di una riga mancante — si vede in tabella e non si vede all'opera.
    /// </summary>
    [Theory]
    [InlineData(32500, 32500)]
    [InlineData(40000, 32500)]
    public async Task Una_fascia_vuota_e_rifiutata(int piede, int tetto) =>
        await Assert.ThrowsAsync<ValidationException>(
            () => _svc.ReplaceAsync(Es5, new[] { new FallbackRowEdit(Ws5, piede, tetto) }));

    /// <summary>Una riga lasciata a metà nell'editor (bersaglio vuoto) si scarta, non fa fallire il salvataggio.</summary>
    [Fact]
    public async Task Una_riga_senza_bersaglio_si_scarta()
    {
        await _svc.ReplaceAsync(Es5, new[]
        {
            new FallbackRowEdit(Ws5, 32500, null),
            new FallbackRowEdit("  ", null, null),
        });

        Assert.Single(await _svc.ListAsync(Es5));
    }

    /// <summary>Lo stesso bersaglio due volte con due fasce diverse è legittimo, non un doppione.</summary>
    [Fact]
    public async Task Lo_stesso_bersaglio_puo_comparire_in_due_fasce()
    {
        await _svc.ReplaceAsync(Es5, new[]
        {
            new FallbackRowEdit(Ws5, null, 19500),
            new FallbackRowEdit(Ws5, 32500, null),
        });

        Assert.Equal(2, (await _svc.ListAsync(Es5)).Count);
    }

    // =====================================================================================================
    //  B — la geometria propone
    // =====================================================================================================

    /// <summary>
    /// ⚠️ La proposta si ferma all'ACC. La sovrapposizione in quota da sola non e' un criterio: <b>tutti</b> i
    /// settori alti d'Europa stanno nella stessa fascia. Misurato dal vivo il 31 agosto 2026 su
    /// <c>LIMM_WS5_CTR</c>: 155 proposte, con Algeri, Vienna, Zurigo e Belgrado in cima — un elenco cosi'
    /// nasconde l'unica riga che serviva.
    /// </summary>
    [Fact]
    public async Task La_proposta_non_esce_dall_ACC()
    {
        var p = await _svc.SuggestAsync(Ws5);

        Assert.DoesNotContain(p, x => x.TargetCallsign == Zrh);
    }

    /// <summary>
    /// ⚠️ E nemmeno chi una forma non ce l'ha. Senza pezzi la banda risulta aperta da tutte e due le parti,
    /// cioe' sovrapposta a chiunque — e DEL e GND un poligono non ce l'hanno per costruzione. Dal vivo il
    /// 31 agosto 2026 <c>LIMC_DEL</c> e <c>LIML_GND</c> venivano proposti come ripiego di un settore d'area
    /// a FL325. «Non ho una forma» non e' «prendo tutto il cielo».
    /// </summary>
    [Fact]
    public async Task Chi_non_ha_forma_non_si_propone()
    {
        var p = await _svc.SuggestAsync(Ws5);

        Assert.DoesNotContain(p, x => x.TargetCallsign == Del);
    }

    /// <summary>E dentro l'ACC propone quel che serve: l'altro settore dello stesso strato, con la sua fascia.</summary>
    [Fact]
    public async Task Dentro_l_ACC_propone_l_altro_settore_alto()
    {
        var sola = Assert.Single(await _svc.SuggestAsync(Ws5));

        Assert.Equal(Es5, sola.TargetCallsign);
        Assert.Equal(32500, sola.BaseFeet);
        Assert.Null(sola.TopFeet);
    }

    /// <summary>Chi e' gia' scritto non si ripropone: la proposta riempie la tabella, non la ripete.</summary>
    [Fact]
    public async Task Una_riga_gia_scritta_non_si_ripropone()
    {
        await _svc.ReplaceAsync(Ws5, new[] { new FallbackRowEdit(Es5, 32500, null) });

        Assert.Empty(await _svc.SuggestAsync(Ws5));
    }

    // =====================================================================================================

    private static Sector Settore(string callsign) => new()
    {
        Callsign = callsign, Name = callsign, Type = SectorType.Ctr, Kind = SectorKind.Acc,
        IsActive = true, IsProjected = true, AccId = 1,
    };

    private async Task Aggancia(string figlio, string padre)
    {
        var f = await _db.Sectors.SingleAsync(s => s.Callsign == figlio);
        var p = await _db.Sectors.SingleAsync(s => s.Callsign == padre);
        f.ParentSectorId = p.Id;
        await _db.SaveChangesAsync();
    }

    private sealed class AllowAuthz : IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public VipiRole Role => VipiRole.Admin;
        public int? CurrentUserId => 1;
        public string? CurrentName => "test";
        public void EnsureAdmin() { }
    }

    /// <summary>
    /// Volumi finti con le bande di Milano MISURATE sul <c>vipi.db</c> reale (split FL325), piu' un settore
    /// alto svizzero: sta nella stessa identica fascia, ed e' li' per provare che NON venga proposto.
    /// </summary>
    private sealed class VolumiFinti : ISectorVolumeCatalog
    {
        private static SectorVolumeRow Riga(string cs, int? piede, int? tetto) => new(
            cs, null, SectorType.Ctr, null,
            new[] { new Vipi.Application.Airspace.ShapePart("[]", piede, tetto,
                AirspaceDatum.FlightLevel, AirspaceDatum.FlightLevel, "", "") },
            ShapeSource.Source);

        public Task<IReadOnlyList<SectorVolumeRow>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SectorVolumeRow>>(new[]
            {
                Riga(Ws2, null, 32500), Riga(Es2, null, 32500),
                Riga(Ws5, 32500, null), Riga(Es5, 32500, null),
                Riga(Zrh, 32500, null),
                SenzaForma(Del),
            });

        /// <summary>Una posizione a terra: nessun pezzo, com'e' nel dato vero (DEL e GND non hanno poligono).</summary>
        private static SectorVolumeRow SenzaForma(string cs) => new(
            cs, null, SectorType.Del, "LIMC",
            Array.Empty<Vipi.Application.Airspace.ShapePart>(), ShapeSource.Source);
    }
}
