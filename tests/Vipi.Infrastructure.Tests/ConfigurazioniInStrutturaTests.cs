using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
/// Le configurazioni possibili dal capo del database: si scrivono per gruppo, si rileggono, e <b>arrivano nella
/// topologia</b> — che è da dove le prendono la sonda, la scala e il banco.
///
/// <para>Carta <c>docs/feature/2026-10-08-configurazioni-possibili.md</c>. Milano com'è in produzione: i quattro
/// settori d'area ordinari, MIL e FSS, e l'ente Torino–Genova con le sue tre posizioni.</para>
/// </summary>
public class ConfigurazioniInStrutturaTests : IAsyncLifetime
{
    private const string Ws2 = "LIMM_WS2_CTR", Es2 = "LIMM_ES2_CTR", Ws5 = "LIMM_WS5_CTR", Es5 = "LIMM_ES5_CTR";
    private const string Mil = "LIMM_MIL_CTR", Fss = "LIMM_FSS";
    private const string Ww0 = "LIMF_WW0_APP", Wn0 = "LIMF_WN0_APP", Ws0 = "LIMJ_WS0_APP", Twr = "LIMF_TWR";

    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfSectorConfigurationService _svc = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();

        var limm = new Acc { Code = "LIMM", Name = "Milano ACC", CountryPrefix = "LI" };
        _db.Accs.Add(limm);
        await _db.SaveChangesAsync();

        Sector Settore(string cs, SectorType tipo, Sector? padre = null, bool attivo = true) => new()
        {
            Acc = limm, Callsign = cs, Name = cs, Type = tipo, IsActive = attivo, IsProjected = true,
            Kind = tipo == SectorType.Ctr ? SectorKind.Acc : SectorKind.Airport, ParentSector = padre,
        };
        var ws2 = Settore(Ws2, SectorType.Ctr);
        var es2 = Settore(Es2, SectorType.Ctr, ws2);
        var ww0 = Settore(Ww0, SectorType.App, ws2);
        _db.Sectors.AddRange(ws2, es2, Settore(Ws5, SectorType.Ctr, ws2), Settore(Es5, SectorType.Ctr, es2),
            Settore(Mil, SectorType.Ctr, ws2), Settore(Fss, SectorType.Ctr, ws2),
            ww0, Settore(Wn0, SectorType.App, ww0), Settore(Ws0, SectorType.App, ww0),
            Settore(Twr, SectorType.Twr, ww0),
            // Un CTR spento dalla proiezione: non si può scegliere.
            Settore("LIMM_OLD_CTR", SectorType.Ctr, ws2, attivo: false));

        _db.AtcUnits.Add(new AtcUnit
        {
            Code = Ww0, Name = "Milano West - Torino - Genova", Acc = limm, Mode = AtcUnitMode.InAccVipi,
            GroupKey = "grp:011a3a53",
            Positions =
            {
                new AtcUnitPosition { Callsign = Ww0, Order = 0 },
                new AtcUnitPosition { Callsign = Wn0, Order = 1 },
                new AtcUnitPosition { Callsign = Ws0, Order = 2 },
            },
        });
        await _db.SaveChangesAsync();

        _svc = new EfSectorConfigurationService(_db, new AllowAuthz(), LockDiRisorsaConcesso.Instance);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private static AccConfiguration Cfg(string nome, params string[] aperti) => new()
    {
        Key = "cfg:" + nome,
        Name = nome,
        Open = aperti.Select(a => new AccConfigOpen { Callsign = a }).ToList(),
    };

    private static readonly AccConfiguration[] Milano =
    {
        Cfg("Conf 1", Ws2), Cfg("Conf 2", Es2, Ws2), Cfg("Conf 2 b", Ws2, Ws5), Cfg("Conf 3", Es2, Es5, Ws2, Ws5),
    };

    private Task ScriviMilano(bool completo = true) =>
        _svc.ReplaceAsync(ConfigurationGroupKind.AccArea, "LIMM", Milano, completo);

    // =====================================================================================================

    [Fact]
    public async Task L_elenco_si_scrive_e_si_rilegge_nell_ordine_scritto()
    {
        await ScriviMilano();

        var lette = await _svc.ListAsync(ConfigurationGroupKind.AccArea, "limm");
        Assert.Equal(new[] { "Conf 1", "Conf 2", "Conf 2 b", "Conf 3" }, lette.Select(c => c.Name));
        Assert.Equal(new[] { Es2, Es5, Ws2, Ws5 }, lette[3].OpenCallsigns);
        Assert.Equal("cfg:Conf 1", lette[0].Key);
    }

    [Fact]
    public async Task Riscrivere_sostituisce_e_non_lascia_due_righe_per_lo_stesso_gruppo()
    {
        await ScriviMilano();
        await _svc.ReplaceAsync(ConfigurationGroupKind.AccArea, "LIMM", new[] { Cfg("Sola", Ws2) }, completo: false);

        Assert.Equal("Sola", Assert.Single(await _svc.ListAsync(ConfigurationGroupKind.AccArea, "LIMM")).Name);
        Assert.Equal(1, await _db.SectorConfigurationSets.CountAsync());
    }

    [Fact]
    public async Task Center_point_e_range_viaggiano_con_il_settore_e_il_resto_si_ripulisce()
    {
        var cfg = new AccConfiguration
        {
            Key = "", Name = "  ",
            Open =
            {
                new AccConfigOpen { Callsign = " limm_ws2_ctr ", CenterPoint = " GEN ", Range = "150" },
                new AccConfigOpen { Callsign = Ws2 },          // due volte lo stesso: ne resta uno
                new AccConfigOpen { Callsign = "  " },         // una riga a metà
            },
        };
        await _svc.ReplaceAsync(ConfigurationGroupKind.AccArea, "LIMM", new[] { cfg }, completo: false);

        var letta = Assert.Single(await _svc.ListAsync(ConfigurationGroupKind.AccArea, "LIMM"));
        var aperto = Assert.Single(letta.Open);
        Assert.Equal(Ws2, aperto.Callsign);                 // col nominativo del catalogo, non come è stato battuto
        Assert.Equal("GEN", aperto.CenterPoint);
        Assert.Equal("150", aperto.Range);
        Assert.Equal("Conf 1", letta.Name);                 // un nome vuoto non resta vuoto
        Assert.StartsWith("cfg:", letta.Key);
        Assert.True(letta.Key.Length > 4);
    }

    [Theory]
    [InlineData(Mil)]              // MIL e FSS hanno le loro sezioni: non sono del gruppo
    [InlineData(Fss)]
    [InlineData(Ww0)]              // un avvicinamento non è un settore d'area
    [InlineData("LIMM_OLD_CTR")]   // spento
    [InlineData("LIRR_NE_CTR")]    // di nessuno
    public async Task Un_settore_che_non_e_del_gruppo_e_un_errore_e_non_si_scrive_niente(string intruso)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _svc.ReplaceAsync(ConfigurationGroupKind.AccArea, "LIMM", new[] { Cfg("Conf 1", Ws2), Cfg("Storta", Ws2, intruso) }, completo: false));

        Assert.Contains(intruso, ex.Message);
        Assert.Contains("Storta", ex.Message);
        Assert.Equal(0, await _db.SectorConfigurationSets.CountAsync());
    }

    [Theory]
    [InlineData(ConfigurationGroupKind.AccArea, "LIXX")]
    [InlineData(ConfigurationGroupKind.AtcUnit, "LIMM")]         // LIMM è un ACC, non un ente
    [InlineData(ConfigurationGroupKind.AccArea, Ww0)]            // e l'ente non è un ACC
    public async Task Un_gruppo_che_non_esiste_e_un_errore(ConfigurationGroupKind genere, string codice) =>
        await Assert.ThrowsAsync<ValidationException>(() => _svc.ReplaceAsync(genere, codice, Array.Empty<AccConfiguration>(), completo: false));

    [Fact]
    public async Task Svuotare_l_elenco_lascia_la_riga_cosi_il_travaso_non_lo_riporta_indietro()
    {
        await ScriviMilano();
        await _svc.ReplaceAsync(ConfigurationGroupKind.AccArea, "LIMM", Array.Empty<AccConfiguration>(), completo: true);

        Assert.Empty(await _svc.ListAsync(ConfigurationGroupKind.AccArea, "LIMM"));
        var riga = await _db.SectorConfigurationSets.AsNoTracking().SingleAsync();
        Assert.Equal("", riga.BodyJson);
        // ⚠️ Chiesto «completo», ma un elenco senza settori non può esserlo: vorrebbe dire che il gruppo non apre mai.
        Assert.False(riga.IsExhaustive);
        Assert.True((await _svc.TutteAsync()).Vuote);
    }

    // ---- i gruppi di un ACC ----

    [Fact]
    public async Task I_gruppi_di_Milano_sono_i_settori_d_area_e_poi_i_suoi_enti()
    {
        await ScriviMilano();
        var gruppi = await _svc.GruppiAsync("limm");

        Assert.Equal(2, gruppi.Count);

        var area = gruppi[0];
        Assert.Equal((ConfigurationGroupKind.AccArea, "LIMM"), (area.Genere, area.Codice));
        // ⚠️ Senza MIL, senza FSS, senza il CTR spento.
        Assert.Equal(new[] { Es2, Es5, Ws2, Ws5 }, area.Settori.Select(s => s.Callsign));
        Assert.Equal(4, area.Configurazioni.Count);

        var ente = gruppi[1];
        Assert.Equal((ConfigurationGroupKind.AtcUnit, Ww0), (ente.Genere, ente.Codice));
        Assert.Equal("Milano West - Torino - Genova", ente.Nome);
        // Le posizioni nel LORO ordine; la torre che pende da WW0 non è un avvicinamento e non entra.
        Assert.Equal(new[] { Ww0, Wn0, Ws0 }, ente.Settori.Select(s => s.Callsign));
        Assert.Empty(ente.Configurazioni);
    }

    [Fact]
    public async Task L_elenco_di_un_ente_accetta_le_sue_posizioni()
    {
        await _svc.ReplaceAsync(ConfigurationGroupKind.AtcUnit, Ww0, new[]
        {
            Cfg("Unico", Ww0), Cfg("Unico + Genova", Ww0, Ws0), Cfg("Torino", Wn0), Cfg("Torino + Genova", Wn0, Ws0),
            Cfg("Genova", Ws0),
        }, completo: true);

        var c = ConfigurazioniPossibili.Conseguenze(await _svc.ListAsync(ConfigurationGroupKind.AtcUnit, Ww0))
            .ToDictionary(x => x.Settore);
        Assert.Equal(new[] { Wn0 }, c[Ww0].MaiCon);
        Assert.True(c[Ws0].DaSolo);
    }

    /// <summary>
    /// 🔴 Scritto ma NON dichiarato completo, l'elenco non vincola: si rilegge, il gruppo dice «non completo», e
    /// nella topologia non arriva nessun vincolo. È lo stato in cui il travaso lascia ogni elenco.
    /// </summary>
    [Fact]
    public async Task Un_elenco_non_completo_si_rilegge_ma_non_vincola()
    {
        await ScriviMilano(completo: false);

        Assert.Equal(4, (await _svc.ListAsync(ConfigurationGroupKind.AccArea, "LIMM")).Count);
        Assert.False((await _svc.GruppiAsync("LIMM"))[0].Completo);
        Assert.True((await _svc.TutteAsync()).Vuote);
        Assert.True((await new TopologyBuilder(_db).BuildGlobalAsync()).Configurazioni.Vuote);

        await ScriviMilano(completo: true);

        Assert.True((await _svc.GruppiAsync("LIMM"))[0].Completo);
        Assert.False((await _svc.TutteAsync()).Vuote);
    }

    // ---- arrivano dove servono ----

    [Fact]
    public async Task Le_configurazioni_arrivano_nella_topologia_globale_e_in_quella_dell_ACC()
    {
        var topo = new TopologyBuilder(_db);
        Assert.True((await topo.BuildGlobalAsync()).Configurazioni.Vuote);   // tabella vuota: nessun vincolo

        await ScriviMilano();

        foreach (var t in new[] { await topo.BuildGlobalAsync(), (await topo.BuildByAccCodeAsync("LIMM"))! })
        {
            // È il caso di S99: chiuso WS2 non può restare aperto nessun altro settore d'area.
            Assert.Equal(new[] { Es2, Es5, Ws5 },
                t.Configurazioni.ChiusiCon(new[] { Ws2 }).OrderBy(x => x, StringComparer.Ordinal));
            Assert.Empty(t.Configurazioni.NonPreviste(new[] { Ws2, Es2, Mil, Ww0, Wn0 }));   // l'ente non ha un elenco
        }
    }

    private sealed class AllowAuthz : IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public VipiRole Role => VipiRole.Admin;
        public int? CurrentUserId => 1;
        public string? CurrentName => "test";
        public void EnsureAdmin() { }
    }
}
