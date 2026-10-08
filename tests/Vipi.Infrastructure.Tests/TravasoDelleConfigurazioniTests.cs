using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il travaso delle configurazioni dal documento alla Struttura, all'avvio. Carta
/// <c>docs/feature/2026-10-08-configurazioni-possibili.md</c> §5.
///
/// <para>Il banco è la vIPI di Roma del seme: il blocco Aerovia e un gruppo APP («Pisa», col suo ente), con le
/// configurazioni scritte nel documento <b>come si scrivevano prima</b> dell'8 ottobre 2026.</para>
/// </summary>
public class TravasoDelleConfigurazioniTests : IAsyncLifetime
{
    private const string Acc = "LIRR";

    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private AccDocumentService _documento = default!;
    private EfEditingRepository _editing = default!;
    private EfSectorConfigurationService _struttura = default!;
    private EfDocumentMaintenance _manutenzione = default!;
    private int _cfgAerovia, _cfgPisa;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);

        var authz = new AllowAuthz();
        _editing = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));
        // ⚠️ Senza il servizio delle configurazioni: i blocchi dicono quel che è scritto nel documento, che è
        // quel che il travaso deve leggere.
        _documento = new AccDocumentService(new EfAccDerivationRepository(_db), _editing, authz,
            TestReleaseTargets.ReleaseRepo(_db), LockConcesso.Instance, new EfAtcUnitRepository(_db));
        _struttura = new EfSectorConfigurationService(_db, authz, LockDiRisorsaConcesso.Instance);
        _manutenzione = new EfDocumentMaintenance(_db);

        var modello = await _documento.LoadForEditAsync(Acc);
        var gruppo = await _documento.AddGroupAsync(Acc, modello.VersionId, "Pisa");
        await _documento.SaveBlockMetaAsync(Acc, gruppo,
            new AccBlockMeta { Key = "grp:pisa", Kind = AccBlockKind.AppGroup, MemberCallsigns = { "LIRP_APP" } });

        var blocchi = (await _documento.LoadForEditAsync(Acc)).Blocks;
        _cfgAerovia = blocchi.Single(b => b.Block.Kind == AccBlockKind.Aerovia).ChildSectionIdsByKey["configurations"];
        _cfgPisa = blocchi.Single(b => b.Block.Kind == AccBlockKind.AppGroup).ChildSectionIdsByKey["configurations"];
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private static AccConfiguration Cfg(string nome, params string[] aperti) => new()
    {
        Key = "cfg:" + nome, Name = nome,
        Open = aperti.Select(a => new AccConfigOpen { Callsign = a, CenterPoint = "GINEL", Range = "140" }).ToList(),
    };

    /// <summary>Scrive il <c>BodyJson</c> della sezione <c>configurations</c>, com'era prima.</summary>
    private Task NelDocumento(int sezione, params AccConfiguration[] configurazioni) =>
        _editing.SaveSectionBlockJsonBySectionAsync(sezione, JsonSerializer.Serialize(configurazioni), 1);

    [Fact]
    public async Task Le_configurazioni_dell_aerovia_e_del_gruppo_APP_arrivano_in_struttura()
    {
        await NelDocumento(_cfgAerovia, Cfg("Conf 1", "LIRR_NE_CTR"));
        await NelDocumento(_cfgPisa, Cfg("Pisa unico", "LIRP_APP"));

        Assert.Equal(2, await _manutenzione.TravasaConfigurazioniAsync());

        var area = Assert.Single(await _struttura.ListAsync(ConfigurationGroupKind.AccArea, Acc));
        Assert.Equal("Conf 1", area.Name);
        Assert.Equal("cfg:Conf 1", area.Key);                       // la chiave resta: è quella delle chip della mappa
        Assert.Equal(("LIRR_NE_CTR", "GINEL", "140"), (area.Open[0].Callsign, area.Open[0].CenterPoint, area.Open[0].Range));

        // Il gruppo APP si riconosce dal suo ENTE (codice = il primo membro), non dalla chiave del blocco.
        Assert.Equal("Pisa unico", Assert.Single(await _struttura.ListAsync(ConfigurationGroupKind.AtcUnit, "LIRP_APP")).Name);
    }

    [Fact]
    public async Task Rifatto_non_porta_niente_e_non_tocca_quel_che_in_struttura_e_stato_cambiato()
    {
        await NelDocumento(_cfgAerovia, Cfg("Conf 1", "LIRR_NE_CTR"));
        Assert.Equal(1, await _manutenzione.TravasaConfigurazioniAsync());

        await _struttura.ReplaceAsync(ConfigurationGroupKind.AccArea, Acc, new[] { Cfg("Cambiata in Struttura", "LIRR_NE_CTR") });

        Assert.Equal(0, await _manutenzione.TravasaConfigurazioniAsync());
        Assert.Equal("Cambiata in Struttura",
            Assert.Single(await _struttura.ListAsync(ConfigurationGroupKind.AccArea, Acc)).Name);
    }

    /// <summary>
    /// 🔴 Un elenco svuotato in Struttura resta vuoto: la riga c'è apposta, e dice «qui si è già deciso». Senza,
    /// al riavvio dopo tornerebbero le configurazioni che il documento porta ancora scritte.
    /// </summary>
    [Fact]
    public async Task Un_elenco_svuotato_in_struttura_non_torna_dal_documento()
    {
        await NelDocumento(_cfgAerovia, Cfg("Conf 1", "LIRR_NE_CTR"));
        await _manutenzione.TravasaConfigurazioniAsync();
        await _struttura.ReplaceAsync(ConfigurationGroupKind.AccArea, Acc, Array.Empty<AccConfiguration>());

        Assert.Equal(0, await _manutenzione.TravasaConfigurazioniAsync());
        Assert.Empty(await _struttura.ListAsync(ConfigurationGroupKind.AccArea, Acc));
    }

    [Fact]
    public async Task Un_documento_senza_configurazioni_non_lascia_righe()
    {
        // Una «New configuration» mai riempita non è un elenco.
        await NelDocumento(_cfgPisa, new AccConfiguration { Key = "cfg:x", Name = "New configuration" });

        Assert.Equal(0, await _manutenzione.TravasaConfigurazioniAsync());
        Assert.Equal(0, await _db.SectorConfigurationSets.CountAsync());
    }

    /// <summary>
    /// MIL e FSS non stanno nelle configurazioni dei settori d'area dal 21 settembre 2026, ma quelle scritte prima
    /// li portano ancora: il travaso li toglie, come li toglieva la lettura del documento — altrimenti la prima
    /// riscrittura dell'elenco in Struttura verrebbe rifiutata («non è un settore del gruppo»).
    /// </summary>
    [Fact]
    public async Task I_settori_MIL_e_FSS_scritti_prima_non_passano()
    {
        await NelDocumento(_cfgAerovia, Cfg("Conf 1", "LIRR_NE_CTR", "LIRR_MIL_CTR", "LIRR_FSS"));

        await _manutenzione.TravasaConfigurazioniAsync();

        var area = Assert.Single(await _struttura.ListAsync(ConfigurationGroupKind.AccArea, Acc));
        Assert.Equal(new[] { "LIRR_NE_CTR" }, area.OpenCallsigns);
    }

    [Fact]
    public async Task Dal_documento_non_si_cancella_niente()
    {
        await NelDocumento(_cfgAerovia, Cfg("Conf 1", "LIRR_NE_CTR"));
        await _manutenzione.TravasaConfigurazioniAsync();

        // Senza il servizio delle configurazioni il blocco dice ancora quel che è scritto nel documento.
        var aerovia = (await _documento.LoadForEditAsync(Acc)).Blocks.Single(b => b.Block.Kind == AccBlockKind.Aerovia);
        Assert.Equal("Conf 1", Assert.Single(aerovia.Block.Configurations).Name);
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
