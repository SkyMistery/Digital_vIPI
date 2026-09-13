using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Airspace;
using Vipi.Application.Content;
using Vipi.Application.Translation;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// T-060 (revisione del 13 settembre 2026), la classe di R-023: sette porte di scrittura che le pagine chiamano
/// <b>senza un servizio in mezzo</b>, e che fino a oggi si difendevano solo col bottone nascosto.
///
/// <para>Una prova per porta, da anonimo: deve rifiutare <b>e non scrivere</b>. E le due porte dei giri di
/// sfondo — l'import delle radioassistenze e la semina del glossario — devono restare aperte senza nessuno:
/// all'avvio e di notte un livello non c'è, e un cancello messo lì le spegnerebbe in silenzio.</para>
/// </summary>
public class PorteDelleAnagraficheTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private static readonly LivelloFisso Anonimo = LivelloFisso.Anonimo;

    // ---- Radioassistenze --------------------------------------------------------------------------------

    [Fact]
    public async Task Radioassistenze_da_anonimo_non_si_scrive_niente()
    {
        var riga = await new EfNavaidCatalog(_db, LivelloFisso.Editor).CreateAsync("MNL", NavaidRules.FamigliaVhf, 1);
        _db.ChangeTracker.Clear();
        var porta = new EfNavaidCatalog(_db, Anonimo);

        await Assert.ThrowsAsync<EditNotAllowedException>(() => porta.CreateAsync("PRA", NavaidRules.FamigliaVhf, 0));
        await Assert.ThrowsAsync<EditNotAllowedException>(() => porta.DeleteAsync(riga.Id, 0));
        await Assert.ThrowsAsync<EditNotAllowedException>(() => porta.SetTypeAsync(riga.Id, "VOR", 0));
        await Assert.ThrowsAsync<EditNotAllowedException>(() => porta.SetFrequencyAsync(riga.Id, "115.25", 0));
        await Assert.ThrowsAsync<EditNotAllowedException>(() => porta.SetChannelAsync(riga.Id, "99Y", 0));
        await Assert.ThrowsAsync<EditNotAllowedException>(() => porta.SetCoordinatesAsync(riga.Id, "413251N 0154123E", 0));

        var rimasta = Assert.Single(await _db.Navaids.AsNoTracking().ToListAsync());
        Assert.Null(rimasta.Type);
        Assert.Null(rimasta.Frequency);
        Assert.Null(rimasta.Latitude);
    }

    [Fact]
    public async Task Il_tasto_rileggi_adesso_da_anonimo_non_parte_e_il_giro_dell_orologio_si()
    {
        var sorgente = new SorgenteFinta(new NavaidName("MNL", NavaidKind.Vor, 41.5476, 15.6898, "115.25", "99Y"));
        var importatore = new NavaidImporter(sorgente, new EfNavaidCatalog(_db, Anonimo), new PolicyFinta(), Anonimo);

        await Assert.ThrowsAsync<EditNotAllowedException>(() => importatore.RunNowAsync());
        Assert.Equal(0, sorgente.Riletture);

        // ⚠️ Il giro notturno gira in uno scope senza persona: se il cancello finisse anche qui, l'anagrafica
        // smetterebbe di aggiornarsi e nessuna pagina lo direbbe.
        var esito = await importatore.RunAsync();
        Assert.Equal(1, esito.DallaSorgente);
        Assert.Single(await _db.Navaids.AsNoTracking().ToListAsync());
    }

    // ---- Spazi aerei dell'AIP -----------------------------------------------------------------------------

    [Fact]
    public async Task Catalogo_spazi_aerei_da_anonimo_non_carica_non_mette_in_vigore_non_elimina()
    {
        var porta = new EfAirspaceCatalog(_db, Anonimo);

        await Assert.ThrowsAsync<EditNotAllowedException>(() => porta.SaveAsync(null!, null!, DateTime.UtcNow));
        await Assert.ThrowsAsync<EditNotAllowedException>(() => porta.SetCurrentAsync(1));
        await Assert.ThrowsAsync<EditNotAllowedException>(() => porta.DeleteAsync(1));

        Assert.Empty(await _db.AirspaceImports.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Agganci_ai_volumi_da_anonimo_non_si_scrivono()
    {
        var porta = new EfSectorAirspaceBindings(_db, Anonimo);

        await Assert.ThrowsAsync<EditNotAllowedException>(() => porta.SetAsync(
            SourceCatalog.Subcenter, 1, "LIRR_CTR", Array.Empty<AirspaceVolumeKey>(), null, null));

        Assert.Empty(await _db.SectorAirspaceBindings.AsNoTracking().ToListAsync());
    }

    // ---- Alias dei fix SID --------------------------------------------------------------------------------

    [Fact]
    public async Task Alias_dei_fix_si_creano_da_editor_e_si_tolgono_solo_da_admin()
    {
        await Assert.ThrowsAsync<EditNotAllowedException>(() =>
            new EfSidFixAliasRepository(_db, Anonimo).UpsertAsync("PAL", "PALAS"));
        Assert.Empty(await _db.SidFixAliases.AsNoTracking().ToListAsync());

        await new EfSidFixAliasRepository(_db, LivelloFisso.Editor).UpsertAsync("PAL", "PALAS");
        var alias = Assert.Single(await _db.SidFixAliases.AsNoTracking().ToListAsync());

        // Toglierlo è della pagina Sorgenti, che è dell'Admin: un Editor non basta.
        await Assert.ThrowsAsync<EditNotAllowedException>(() =>
            new EfSidFixAliasRepository(_db, LivelloFisso.Editor).DeleteAsync(alias.Id));
        Assert.Single(await _db.SidFixAliases.AsNoTracking().ToListAsync());
    }

    // ---- Glossario ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Glossario_da_anonimo_non_si_scrive_e_la_semina_si()
    {
        var porta = new EfGlossaryStore(_db, Anonimo);

        await Assert.ThrowsAsync<EditNotAllowedException>(() =>
            porta.UpsertAsync("it", "en", "riporta sottovento", "report downwind", null));
        Assert.Equal(0, await porta.ContaAsync("it", "en"));

        // La semina all'avvio non ha nessuno dietro, e deve comunque riempire il glossario vuoto.
        Assert.True(await GlossarioFraseologia.SeminaAsync(porta) > 0);
        var voce = (await porta.ListAsync("it", "en"))[0];
        Assert.Null(voce.UpdatedByUserId);

        await Assert.ThrowsAsync<EditNotAllowedException>(() => porta.DeleteAsync(voce.Id));
        Assert.Contains(await porta.ListAsync("it", "en"), v => v.Id == voce.Id);
    }

    // ---- Impostazioni delle statistiche ---------------------------------------------------------------------

    [Theory]
    [InlineData(VipiRole.User)]
    [InlineData(VipiRole.IvaoStaff)]
    public async Task Classifica_pubblica_sotto_lo_staff_di_divisione_non_si_accende(VipiRole livello)
    {
        var porta = new EfStatsSettingsStore(_db, new LivelloFisso(livello));

        await Assert.ThrowsAsync<EditNotAllowedException>(() => porta.SaveAsync(true, 0));

        Assert.False((await porta.GetAsync()).PublicLeaderboard);
    }

    // ---- Finti ------------------------------------------------------------------------------------------------

    private sealed class SorgenteFinta : INavaidSource
    {
        private readonly NavaidCatalog _catalogo;
        public SorgenteFinta(params NavaidName[] righe) => _catalogo = new NavaidCatalog(righe);
        public int Riletture { get; private set; }
        public Task<NavaidCatalog> GetAsync(CancellationToken ct = default) => Task.FromResult(_catalogo);
        public Task<NavaidCatalog> RefreshAsync(CancellationToken ct = default)
        {
            Riletture++;
            return Task.FromResult(_catalogo);
        }
    }

    private sealed class PolicyFinta : IImportPolicyStore
    {
        public Task<ImportPolicySnapshot> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(ImportPolicySnapshot.AllImported);
        public Task<ImportPolicyInfo> GetInfoAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task SaveAsync(ImportPolicySnapshot policy, int updatedByUserId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
