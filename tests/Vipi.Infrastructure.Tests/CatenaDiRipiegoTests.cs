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
            new FallbackRowEdit(Ws5, 30500, null),
            new FallbackRowEdit(Ws2, null, null),
        });

        var righe = await _svc.ListAsync(Es5);

        Assert.Equal(2, righe.Count);
        Assert.Equal(Ws5, righe[0].TargetCallsign);
        Assert.Equal(30500, righe[0].BaseFeet);
        Assert.Equal(Ws2, righe[1].TargetCallsign);
        Assert.Null(righe[1].BaseFeet);
    }

    /// <summary>Sostituzione, non fusione: una lista vuota toglie tutto e riporta la ricaduta ai soli padri.</summary>
    [Fact]
    public async Task Una_lista_vuota_cancella_le_righe()
    {
        await _svc.ReplaceAsync(Es5, new[] { new FallbackRowEdit(Ws5, 30500, null) });
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

        await _svc.ReplaceAsync(Es5, new[] { new FallbackRowEdit(Ws5, 30500, null) });

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
    [InlineData(30500, 30500)]
    [InlineData(40000, 30500)]
    public async Task Una_fascia_vuota_e_rifiutata(int piede, int tetto) =>
        await Assert.ThrowsAsync<ValidationException>(
            () => _svc.ReplaceAsync(Es5, new[] { new FallbackRowEdit(Ws5, piede, tetto) }));

    /// <summary>Una riga lasciata a metà nell'editor (bersaglio vuoto) si scarta, non fa fallire il salvataggio.</summary>
    [Fact]
    public async Task Una_riga_senza_bersaglio_si_scarta()
    {
        await _svc.ReplaceAsync(Es5, new[]
        {
            new FallbackRowEdit(Ws5, 30500, null),
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
            new FallbackRowEdit(Ws5, 30500, null),
        });

        Assert.Equal(2, (await _svc.ListAsync(Es5)).Count);
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

    private sealed class VolumiFinti : ISectorVolumeCatalog
    {
        public Task<IReadOnlyList<SectorVolumeRow>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SectorVolumeRow>>(Array.Empty<SectorVolumeRow>());
    }
}
