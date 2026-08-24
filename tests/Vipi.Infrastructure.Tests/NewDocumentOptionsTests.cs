using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Che cosa <c>/services/vsop/editor/new-document</c> offre a <b>questa</b> persona.
///
/// <para>La pagina era dietro <c>IsAdmin</c> mentre i servizi che chiama autorizzano per <b>grant di
/// ACC</b>: il responsabile di LIRR trovava la porta chiusa pur avendo la chiave — bastava che arrivasse
/// all'URL dell'editor per creare lo stesso il documento. È lo stesso difetto chiuso su
/// <c>/services/vsop/versions</c> il 21 agosto.</para>
///
/// <para>⚠️ Il servizio <b>filtra, non autorizza</b>: chi crea davvero passa comunque da
/// <c>EnsureCanEditAccAsync</c>. Una tendina è una comodità, non una guardia.</para>
///
/// <para>Sul repository <b>vero</b> e non su un finto: il contratto di <c>IStructureEditingRepository</c> ha
/// venti metodi e un fake sarebbe rumore — ma soprattutto qui si prova anche il <c>DocumentId</c> nuovo di
/// <c>ListAllAirportsAsync</c>, che con un fake non si proverebbe affatto.</para>
/// </summary>
public class NewDocumentOptionsTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfStructureEditingRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);
        await RomaContentSeed.SeedAsync(_db);   // dà alla vIPI di Roma il suo documento
        _repo = new EfStructureEditingRepository(_db);

        // Un secondo ACC italiano e uno estero: senza, «vede solo i suoi» non proverebbe niente.
        var milano = new Acc { Code = "LIMM", Name = "Milano ACC", CountryPrefix = "LI" };
        var parigi = new Acc { Code = "LFFF", Name = "Paris ACC", CountryPrefix = "LF", IsForeign = true };
        _db.Accs.AddRange(milano, parigi);
        await _db.SaveChangesAsync();
        _db.Sectors.AddRange(
            new Sector { AccId = milano.Id, Callsign = "LIMM_CTR", Name = "Milano", Type = SectorType.Ctr, Kind = SectorKind.Acc },
            new Sector { AccId = parigi.Id, Callsign = "LFFF_CTR", Name = "Paris", Type = SectorType.Ctr, Kind = SectorKind.Acc });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private NewDocumentOptionsService Servizio(string? puoEditare) => new(_repo, new Authz(puoEditare));

    [Fact]
    public async Task Un_responsabile_vede_solo_i_suoi_ACC()
    {
        var opts = await Servizio("LIRR").LoadAsync();

        Assert.Equal(new[] { "LIRR" }, opts.MyAccs.Select(a => a.Code));
    }

    [Fact]
    public async Task Un_admin_li_vede_tutti()
    {
        var opts = await Servizio(null).LoadAsync();

        Assert.Contains(opts.MyAccs, a => a.Code == "LIRR");
        Assert.Contains(opts.MyAccs, a => a.Code == "LIMM");
    }

    /// <summary>Senza nessun permesso l'elenco è vuoto: è così che la pagina sa di non doversi aprire.</summary>
    [Fact]
    public async Task Senza_permessi_non_c_e_niente_da_offrire()
    {
        Assert.Empty((await Servizio("NESSUNO").LoadAsync()).MyAccs);
    }

    /// <summary>
    /// ⚠️ Gli ACC esteri NON si filtrano per permesso: non ci si crea niente sopra, fanno solo da controparte
    /// di una vLOA. Filtrarli renderebbe impossibile creare la vLOA proprio con l'ACC che serve.
    /// </summary>
    [Fact]
    public async Task Gli_esteri_ci_sono_anche_senza_permessi_su_di_loro()
    {
        var opts = await Servizio("LIRR").LoadAsync();

        Assert.Contains(opts.ForeignAccs, a => a.Code == "LFFF" && a.AreaSectors.Count > 0);
        // e non compaiono fra quelli su cui si crea
        Assert.DoesNotContain(opts.MyAccs, a => a.Code == "LFFF");
    }

    /// <summary>Ogni tendina vede la sua specie: i CTR (e gli APP remotizzati) sono candidati di vLOA, gli
    /// APP standalone hanno un documento proprio, e le due cose non si mescolano.</summary>
    [Fact]
    public async Task Ogni_elenco_porta_la_sua_specie()
    {
        var acc = (await Servizio("LIRR").LoadAsync()).MyAccs.Single();

        Assert.All(acc.AreaSectors, s => Assert.DoesNotContain(s.Key, acc.StandaloneApps.Select(x => x.Key)));
        Assert.Contains(acc.StandaloneApps, s => s.Key.EndsWith("_APP", StringComparison.Ordinal));
        Assert.Contains(acc.Airports, a => a.Key == "LIRF");
    }

    /// <summary>
    /// ⚠️ «Ha già un documento» è il dato che fa dire al tasto «Apri» invece di «Crea»: la pagina si chiama
    /// «Nuovo documento» e per tre tipi su quattro apre l'esistente. Viene da chi lo possiede — il settore e
    /// l'aeroporto lo tengono in <c>DocumentId</c> — non da una seconda lettura dell'elenco documenti.
    /// </summary>
    [Fact]
    public async Task Dice_quali_bersagli_hanno_gia_un_documento()
    {
        var acc = (await Servizio("LIRR").LoadAsync()).MyAccs.Single();

        // Il seed dà un documento alla vIPI di Roma: almeno un settore d'area lo dichiara.
        Assert.Contains(acc.AreaSectors, s => s.HasDocument);

        // E un aeroporto senza documento non lo dichiara: è la metà che rende il dato utile.
        var senza = await NuovoAeroportoSenzaDocumentoAsync("LIRZ", "Prova");
        acc = (await Servizio("LIRR").LoadAsync()).MyAccs.Single();
        Assert.False(acc.Airports.Single(a => a.Key == senza).HasDocument);
    }

    private async Task<string> NuovoAeroportoSenzaDocumentoAsync(string icao, string nome)
    {
        var accId = await _db.Accs.Where(a => a.Code == "LIRR").Select(a => a.Id).FirstAsync();
        _db.Airports.Add(new Airport { AccId = accId, Icao = icao, Name = nome });
        await _db.SaveChangesAsync();
        return icao;
    }

    /// <summary>Autorizzazione finta: <c>null</c> = admin (può tutto), altrimenti solo l'ACC nominato.</summary>
    private sealed class Authz : IEditAuthorizationService
    {
        private readonly string? _acc;
        public Authz(string? acc) => _acc = acc;

        public bool IsAdmin => _acc is null;
        public int? CurrentUserId => 704798;
        public string? CurrentName => "test";
        public Task<bool> CanEditAccAsync(string accCode, CancellationToken ct = default) =>
            Task.FromResult(_acc is null || string.Equals(_acc, accCode, StringComparison.OrdinalIgnoreCase));
        public Task EnsureCanEditAccAsync(string accCode, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureCanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> CanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> CanEditAnythingAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<GrantRow>> ListGrantsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GrantRow>>(Array.Empty<GrantRow>());
        public Task<int> AddGrantAsync(int UserId, string? displayName, string accCode, CancellationToken ct = default) => Task.FromResult(0);
        public Task RevokeGrantAsync(int grantId, CancellationToken ct = default) => Task.CompletedTask;
        public void EnsureAdmin() { }
    }
}
