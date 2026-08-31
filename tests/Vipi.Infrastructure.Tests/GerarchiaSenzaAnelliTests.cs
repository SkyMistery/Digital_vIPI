using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vipi.Application;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Presidio del difetto visto in produzione su <c>atc.it.ivao.aero</c> il 31 agosto 2026: un settore che
/// risultava <b>nipote di sé stesso</b> nella pagina Struttura.
///
/// <para>Il gesto che lo produceva è quello che qui si prova per intero: mettere «eredita» (padre scritto a
/// <c>null</c>) su una posizione da cui, per la scaletta, pende l'aeroporto. Prima della correzione quel gesto
/// <b>non passava da nessun controllo</b> — la validazione stava dentro <c>if (parentCallsign is not null)</c>
/// — e il padre che ne nasceva era quello DERIVATO, che la mappa della guardia non conteneva.</para>
///
/// <para>Carta <c>docs/feature/2026-08-31-ricaduta-verticale-e-cicli.md</c> §1.</para>
/// </summary>
public class GerarchiaSenzaAnelliTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfHierarchyEditingService _svc = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();

        _svc = new EfHierarchyEditingService(
            _db, new AllowAuthz(), new ProiezioneFinta(),
            Options.Create(new NeighboursOptions()),
            Options.Create(new DivisionOptions { IcaoPrefixes = new List<string> { "LI" } }));

        // La configurazione di LIMF com'è in produzione: due APP sullo stesso scalo, l'aeroporto agganciato a
        // una delle due, e la seconda con un padre d'area scritto (lo stato SANO, prima del gesto).
        // ⚠️ Ordine obbligato dalle chiavi esterne: ACC, poi l'aeroporto (AirportSector.AirportIcao punta a
        // Airport.Icao), poi le posizioni.
        var acc = new Acc { Code = "LIMM", Name = "Milano ACC", CountryPrefix = "LI" };
        _db.Accs.Add(acc);
        await _db.SaveChangesAsync();

        _db.Airports.Add(new Airport { Icao = "LIMF", Name = "Torino Caselle", AccId = acc.Id, ParentCallsign = "LIMF_WN0_APP" });
        _db.AccSectors.Add(new AccSector { ComposePosition = "LIMM_WS2_CTR", CenterId = "LIMM", Position = "CTR" });
        await _db.SaveChangesAsync();

        _db.AirportSectors.Add(new AirportSector
        {
            ComposePosition = "LIMF_WW0_APP", AirportIcao = "LIMF", AccCode = "LIMM",
            Position = "APP", ParentCallsign = "LIMM_WS2_CTR",
        });
        _db.AirportSectors.Add(new AirportSector
        {
            ComposePosition = "LIMF_WN0_APP", AirportIcao = "LIMF", AccCode = "LIMM",
            Position = "APP", ParentCallsign = "LIMF_WW0_APP",
        });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private async Task<int> IdPosizione(string callsign) =>
        (await _db.AirportSectors.AsNoTracking().SingleAsync(s => s.ComposePosition == callsign)).Id;

    // =====================================================================================================

    /// <summary>
    /// Il gesto esatto della produzione: «eredita» su WW0, da cui — per il padre dello scalo — pende WN0, che è
    /// suo figlio. Il gesto <b>non viene rifiutato</b>: viene <b>disinnescato</b>. È la mossa 1 della carta, e
    /// la differenza conta — un rifiuto lascerebbe l'admin senza modo di staccare quella posizione, mentre così
    /// il nodo diventa una radice orfana, che il filtro «da agganciare» mette in evidenza da sola.
    /// </summary>
    [Fact]
    public async Task Mettere_eredita_produce_un_orfano_non_un_anello()
    {
        var ww0 = await IdPosizione("LIMF_WW0_APP");

        await _svc.SetParentAsync(HierarchyNodeKind.AirportPosition, ww0, null);

        var albero = await _svc.LoadTreeAsync();
        Assert.Null(albero.Single(n => n.Callsign == "LIMF_WW0_APP").EffectiveParentCallsign);
        // E l'anello che si vedeva in produzione non c'è: WN0 pende ancora da WW0, e WW0 da nessuno.
        Assert.Equal("LIMF_WW0_APP", albero.Single(n => n.Callsign == "LIMF_WN0_APP").EffectiveParentCallsign);
    }

    /// <summary>
    /// Ciò che la mossa 1 <b>non</b> può vedere, e che quindi tocca alla guardia: un anello che si chiude
    /// FUORI dallo scalo. <c>AirportPositionLadder</c> conosce solo le posizioni del proprio aeroporto — un
    /// candidato che esce sul CTR e da lì torna indietro non gli è visibile.
    /// </summary>
    [Fact]
    public async Task La_guardia_prende_l_anello_che_si_chiude_fuori_dallo_scalo()
    {
        // Un secondo settore d'area, così WW0 ha un padre scritto che NON è il padre dello scalo: prima della
        // mossa l'albero è sano, e l'anello lo crea davvero la modifica.
        _db.AccSectors.Add(new AccSector { ComposePosition = "LIMM_N_CTR", CenterId = "LIMM", Position = "CTR" });
        var ctr = await _db.AccSectors.SingleAsync(s => s.ComposePosition == "LIMM_WS2_CTR");
        ctr.ParentCallsign = "LIMF_WW0_APP";           // il CTR pende da WW0: legame lecito
        var ww0Riga = await _db.AirportSectors.SingleAsync(s => s.ComposePosition == "LIMF_WW0_APP");
        ww0Riga.ParentCallsign = "LIMM_N_CTR";
        var scalo = await _db.Airports.SingleAsync();
        scalo.ParentCallsign = "LIMM_WS2_CTR";         // e lo scalo pende dal CTR
        await _db.SaveChangesAsync();

        // «Eredita» su WW0: la scaletta lo manderebbe sul padre dello scalo — LIMM_WS2_CTR — che pende da lui.
        var ww0 = await IdPosizione("LIMF_WW0_APP");
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _svc.SetParentAsync(HierarchyNodeKind.AirportPosition, ww0, null));

        Assert.Contains("LIMF_WW0_APP", ex.Message);
        Assert.Contains("LIMM_WS2_CTR", ex.Message);

        // E NON ha scritto: un rifiuto che lascia il dato a metà è peggio del difetto.
        Assert.Equal("LIMM_N_CTR",
            (await _db.AirportSectors.AsNoTracking().SingleAsync(s => s.ComposePosition == "LIMF_WW0_APP")).ParentCallsign);
    }

    /// <summary>
    /// La stessa mossa dall'altro capo: agganciare l'AEROPORTO a una posizione che gli pende sotto. Anche qui
    /// il risultato è un orfano, non un anello — e la posizione resta modificabile.
    /// </summary>
    [Fact]
    public async Task Agganciare_lo_scalo_a_una_propria_posizione_non_produce_un_anello()
    {
        var scalo = await _db.Airports.SingleAsync();
        scalo.ParentCallsign = null;
        await _db.SaveChangesAsync();

        var ww0 = await IdPosizione("LIMF_WW0_APP");
        await _svc.SetParentAsync(HierarchyNodeKind.AirportPosition, ww0, null);

        // Rimettere lo scalo su WN0 riporta la configurazione di produzione: deve restare senza anelli.
        await _svc.SetParentAsync(HierarchyNodeKind.Airport, scalo.Id, "LIMF_WN0_APP");

        var albero = await _svc.LoadTreeAsync();
        Assert.Null(albero.Single(n => n.Callsign == "LIMF_WW0_APP").EffectiveParentCallsign);
    }

    /// <summary>Il caso diretto, che già si rifiutava: un padre scritto che è un proprio discendente.</summary>
    [Fact]
    public async Task Un_padre_scritto_discendente_e_rifiutato()
    {
        var ww0 = await IdPosizione("LIMF_WW0_APP");

        await Assert.ThrowsAsync<ValidationException>(
            () => _svc.SetParentAsync(HierarchyNodeKind.AirportPosition, ww0, "LIMF_WN0_APP"));
    }

    /// <summary>Una modifica sana passa: la guardia non deve bloccare il lavoro normale.</summary>
    [Fact]
    public async Task Una_riparentazione_sana_passa()
    {
        var wn0 = await IdPosizione("LIMF_WN0_APP");

        await _svc.SetParentAsync(HierarchyNodeKind.AirportPosition, wn0, "LIMM_WS2_CTR");

        Assert.Equal("LIMM_WS2_CTR",
            (await _db.AirportSectors.AsNoTracking().SingleAsync(s => s.ComposePosition == "LIMF_WN0_APP")).ParentCallsign);
    }

    /// <summary>
    /// ⚠️ Con un anello GIÀ in archivio la pagina deve restare usabile: è l'unico posto da cui lo si scioglie.
    /// Si rifiutano gli anelli che la modifica <b>crea</b>, non quelli che trova.
    /// </summary>
    [Fact]
    public async Task Con_un_anello_gia_presente_si_puo_ancora_modificare_altrove()
    {
        // Anello piantato a mano fra due settori d'area, come se lo avesse scritto un import.
        _db.AccSectors.Add(new AccSector { ComposePosition = "LIRR_A_CTR", CenterId = "LIMM", Position = "CTR", ParentCallsign = "LIRR_B_CTR" });
        _db.AccSectors.Add(new AccSector { ComposePosition = "LIRR_B_CTR", CenterId = "LIMM", Position = "CTR", ParentCallsign = "LIRR_A_CTR" });
        await _db.SaveChangesAsync();

        var wn0 = await IdPosizione("LIMF_WN0_APP");
        await _svc.SetParentAsync(HierarchyNodeKind.AirportPosition, wn0, "LIMM_WS2_CTR");   // nessuna eccezione

        Assert.Equal("LIMM_WS2_CTR",
            (await _db.AirportSectors.AsNoTracking().SingleAsync(s => s.ComposePosition == "LIMF_WN0_APP")).ParentCallsign);
    }

    /// <summary>L'albero che l'editor DISEGNA è lo stesso su cui la guardia decide: un solo albero, non due.</summary>
    [Fact]
    public async Task Il_padre_derivato_mostrato_dall_editor_non_e_mai_un_proprio_discendente()
    {
        var ww0 = await IdPosizione("LIMF_WW0_APP");
        var riga = await _db.AirportSectors.SingleAsync(s => s.Id == ww0);
        riga.ParentCallsign = null;                 // stato di produzione, scritto SCAVALCANDO la guardia
        await _db.SaveChangesAsync();

        var albero = await _svc.LoadTreeAsync();
        var nodo = albero.Single(n => n.Callsign == "LIMF_WW0_APP");

        Assert.Null(nodo.EffectiveParentCallsign);   // orfano e visibile, non ciclico e muto
    }

    // =====================================================================================================

    private sealed class AllowAuthz : IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public VipiRole Role => VipiRole.Admin;
        public int? CurrentUserId => 1;
        public string? CurrentName => "test";
        public void EnsureAdmin() { }
    }

    private sealed class ProiezioneFinta : ISectorProjectionService
    {
        public Task<int> SyncFromCatalogsAsync(CancellationToken ct = default) => Task.FromResult(0);
    }
}
