using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il giro che porta le radioassistenze dal sectorfile all'anagrafica, e il <b>tasto</b> che lo chiede adesso
/// (pagina <c>/services/vsop/admin/navaids</c>).
///
/// <para>⚠️ Qui si presidiano le tre cose che dal codice non si vedono: il tasto <b>rilegge</b> la sorgente e
/// il giro notturno no; un giro che ha davvero letto <b>timbra</b> lo stato d'import, così il tasto conta
/// quanto l'orologio; e un giro <b>saltato</b> non timbra niente — o la pagina Sorgenti direbbe «ultimo giro
/// riuscito: adesso» di un giro che non ha letto una riga.</para>
/// </summary>
public class ImportRadioassistenzeTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfNavaidCatalog _anagrafica = default!;
    private EfImportStateStore _stati = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _anagrafica = new EfNavaidCatalog(_db);
        _stati = new EfImportStateStore(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    /// <summary>La sorgente finta conta le due letture: è l'unica differenza fra il tasto e l'orologio.</summary>
    private sealed class SorgenteFinta : INavaidSource
    {
        private readonly NavaidCatalog _catalogo;
        public SorgenteFinta(params NavaidName[] righe) => _catalogo = new NavaidCatalog(righe);
        public int Letture { get; private set; }
        public int Riletture { get; private set; }

        public Task<NavaidCatalog> GetAsync(CancellationToken ct = default)
        {
            Letture++;
            return Task.FromResult(_catalogo);
        }

        public Task<NavaidCatalog> RefreshAsync(CancellationToken ct = default)
        {
            Riletture++;
            return Task.FromResult(_catalogo);
        }
    }

    private sealed class PolicyFinta : IImportPolicyStore
    {
        private readonly bool _importate;
        public PolicyFinta(bool importate) => _importate = importate;
        public Task<ImportPolicySnapshot> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(ImportPolicySnapshot.AllImported with { Navaids = _importate });
        public Task<ImportPolicyInfo> GetInfoAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task SaveAsync(ImportPolicySnapshot policy, int updatedByUserId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private static NavaidName Mnl() => new("MNL", NavaidKind.Vor, 41.5476, 15.6898, "115.25", "99Y");

    private NavaidImporter Importatore(SorgenteFinta sorgente, bool importate = true) =>
        new(sorgente, _anagrafica, new PolicyFinta(importate), _stati);

    /// <summary>
    /// ⚠️ Il tasto <b>rilegge</b>, il giro notturno prende quel che c'è. Chi preme un tasto d'import lo preme
    /// perché il sectorfile è cambiato oggi: sulla copia in memoria — vecchia fino a ventiquattro ore — la
    /// risposta sarebbe «0 create, 0 aggiornate» con la riga nuova già pronta sul repository.
    /// </summary>
    [Fact]
    public async Task Il_tasto_rilegge_la_sorgente_il_giro_notturno_no()
    {
        var sorgente = new SorgenteFinta(Mnl());
        var importatore = Importatore(sorgente);

        await importatore.RunAsync();
        Assert.Equal(1, sorgente.Letture);
        Assert.Equal(0, sorgente.Riletture);

        await importatore.RunNowAsync();
        Assert.Equal(1, sorgente.Letture);
        Assert.Equal(1, sorgente.Riletture);
    }

    /// <summary>Il giro porta le righe, chiunque l'abbia chiesto: il tasto non è un secondo import.</summary>
    [Fact]
    public async Task Il_tasto_e_il_giro_notturno_portano_le_stesse_righe()
    {
        var esito = await Importatore(new SorgenteFinta(Mnl())).RunNowAsync();

        Assert.Equal(1, esito.DallaSorgente);
        Assert.Equal(1, esito.Esito!.Create);
        var riga = Assert.Single(await _anagrafica.ListAsync());
        Assert.Equal("MNL", riga.Code);
        Assert.Equal(NavaidRules.FamigliaVhf, riga.Kind);
    }

    /// <summary>
    /// ⚠️ Un giro arrivato in fondo <b>timbra</b> lo stato, e lo timbra il corpo — non il solo giro gestito.
    /// Senza, la pagina Sorgenti direbbe «ferma da tre giorni» di un'anagrafica riempita un minuto fa dal
    /// tasto.
    /// </summary>
    [Fact]
    public async Task Un_giro_riuscito_timbra_lo_stato_dell_import()
    {
        Assert.Null(await _stati.GetLastSuccessAsync(ImportCategories.Navaid));

        await Importatore(new SorgenteFinta(Mnl())).RunNowAsync();

        Assert.NotNull(await _stati.GetLastSuccessAsync(ImportCategories.Navaid));
    }

    /// <summary>
    /// ⚠️ E un giro <b>saltato</b> non timbra niente, per tutte e due le ragioni: la policy che esclude le
    /// radioassistenze è una <b>decisione</b>, la sorgente muta è un <b>guasto</b> — ma nessuna delle due ha
    /// letto una riga, e «ultimo giro riuscito: adesso» sarebbe falso in entrambi i casi.
    /// </summary>
    [Theory]
    [InlineData(false, true)]    // la policy esclude: le gestisce una persona
    [InlineData(true, false)]    // la sorgente non ha risposto: repository spostato, rete giù
    public async Task Un_giro_saltato_non_timbra_niente(bool importate, bool sorgentePiena)
    {
        var sorgente = sorgentePiena ? new SorgenteFinta(Mnl()) : new SorgenteFinta();

        var esito = await Importatore(sorgente, importate).RunNowAsync();

        Assert.Null(esito.Esito);
        Assert.NotNull(esito.Saltato);
        Assert.Null(await _stati.GetLastSuccessAsync(ImportCategories.Navaid));
        Assert.Empty(await _anagrafica.ListAsync());
    }

    /// <summary>
    /// ⚠️ Il tasto premuto due volte non crea doppioni, e non consuma le due conferme che autorizzano
    /// un'eliminazione: il penultimo timbro scorre solo se fra i due giri è passato abbastanza
    /// (<see cref="SogliaEliminazione"/>).
    /// </summary>
    [Fact]
    public async Task Premuto_due_volte_non_raddoppia_niente()
    {
        var importatore = Importatore(new SorgenteFinta(Mnl()));

        await importatore.RunNowAsync();
        var secondo = await importatore.RunNowAsync();

        Assert.Single(await _anagrafica.ListAsync());
        Assert.Equal(0, secondo.Esito!.Create);
        Assert.Null(await _stati.GetPrevSuccessAsync(ImportCategories.Navaid));
    }
}
