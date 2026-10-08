using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Storage della vIPI ACC su Document (doc 08e-acc): EnsureAsync crea il Document chiavizzato sul settore CTR radice
/// primario col blocco Aerovia di default; LoadForEditAsync riassembla i blocchi dall'albero DocumentSection.
/// </summary>
public class AccDocumentServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private AccDocumentService _service = default!;
    private EfSectorConfigurationService _configurazioni = default!;

    private const string Acc = "LIRR";

    private static AccConfiguration Conf(string nome) => new()
    {
        Key = "cfg:" + nome, Name = nome, Open = new() { new AccConfigOpen { Callsign = "LIRR_NE_CTR" } },
    };

    /// <summary>Scrive le configurazioni dei settori d'area dove si scrivono dall'8 ottobre 2026: in Struttura.</summary>
    private Task InStruttura(params string[] nomi) =>
        _configurazioni.ReplaceAsync(ConfigurationGroupKind.AccArea, Acc, nomi.Select(Conf).ToList(), completo: false);

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);

        var repo = new EfAccDerivationRepository(_db);
        var editing = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));
        var authz = new AllowAuthz();
        _configurazioni = new EfSectorConfigurationService(_db, authz, LockDiRisorsaConcesso.Instance);
        _service = new AccDocumentService(repo, editing, authz, TestReleaseTargets.ReleaseRepo(_db),
            LockConcesso.Instance, new EfAtcUnitRepository(_db), _configurazioni);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    /// <summary>S55: i membri di un gruppo APP sono le posizioni del suo ente, e l'ente segue al salvataggio del
    /// gruppo nell'editor — non al prossimo avvio.</summary>
    [Fact]
    public async Task Salvare_i_membri_di_un_gruppo_app_riallinea_il_suo_ente()
    {
        var enti = new EfAtcUnitRepository(_db);
        var service = new AccDocumentService(new EfAccDerivationRepository(_db),
            new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db)), new AllowAuthz(),
            TestReleaseTargets.ReleaseRepo(_db), LockConcesso.Instance, enti);
        var model = await service.LoadForEditAsync(Acc);
        var gruppo = await service.AddGroupAsync(Acc, model.VersionId, "Pisa");
        Assert.Empty(await enti.ListAsync(Acc));   // senza membri, niente ente

        await service.SaveBlockMetaAsync(Acc, gruppo,
            new AccBlockMeta { Key = "grp:pisa", Kind = AccBlockKind.AppGroup, MemberCallsigns = { "LIRP_APP" } });

        var ente = Assert.Single(await enti.ListAsync(Acc));
        Assert.Equal(("LIRP_APP", "Pisa", AtcUnitMode.InAccVipi, "grp:pisa"), (ente.Code, ente.Name, ente.Mode, ente.GroupKey));
    }

    [Fact]
    public async Task Ensure_Creates_Document_Keyed_On_Primary_Ctr_Root_With_Default_Aerovia_Block()
    {
        var id0 = await _service.GetIdentityAsync(Acc);
        Assert.NotNull(id0);
        Assert.Null(id0!.DocumentId);   // non ancora migrato

        var docId = await _service.EnsureAsync(Acc);

        // Il settore radice primario è ora agganciato al Document.
        var linked = await _db.Sectors.AsNoTracking().FirstAsync(s => s.Id == id0.SectorId);
        Assert.Equal(docId, linked.DocumentId);
        Assert.True(linked.IsPrimary);

        // GetIdentity riflette il DocumentId; il documento è vIPI.
        Assert.Equal(docId, (await _service.GetIdentityAsync(Acc))!.DocumentId);
        Assert.Equal(DocumentType.Vipi, (await _db.Documents.AsNoTracking().FirstAsync(d => d.Id == docId)).Type);

        // Idempotente.
        Assert.Equal(docId, await _service.EnsureAsync(Acc));
    }

    [Fact]
    public async Task LoadForEdit_Assembles_Single_Aerovia_Block_With_Catalog_Children()
    {
        var model = await _service.LoadForEditAsync(Acc);

        Assert.True(model.IsDraft);
        Assert.Equal(Acc, model.AccCode);

        var block = Assert.Single(model.Blocks);
        Assert.Equal(AccBlockKind.Aerovia, block.Block.Kind);
        Assert.Equal("aerovia", block.Block.Key);

        // Le figlie corrispondono alle sezioni del catalogo AccAerovia (stesse chiavi, stesso ordine).
        var expected = SectionCatalog.For(SectionProfile.AccAerovia).Select(d => d.Key).ToArray();
        Assert.Equal(expected, block.Block.Sections.Select(s => s.Key).ToArray());

        // La mappa chiave-figlia → Id copre le sezioni live (per i salvataggi by-section).
        Assert.True(block.ChildSectionIdsByKey.ContainsKey("aor"));
        Assert.True(block.ChildSectionIdsByKey.ContainsKey("frequencies"));
    }

    /// <summary>
    /// Le configurazioni la versione di lavoro le legge dalla <b>Struttura</b> (carta
    /// 2026-10-08-configurazioni-possibili): il documento non le scrive più. Le separazioni restano sue.
    /// </summary>
    [Fact]
    public async Task Configurations_Come_From_Structure_And_Separations_RoundTrip_Through_Document()
    {
        var model = await _service.LoadForEditAsync(Acc);
        var block = Assert.Single(model.Blocks);
        var sepId = block.ChildSectionIdsByKey["separations"];
        Assert.Empty(block.Block.Configurations);

        await InStruttura("Conf 1");
        await _service.SaveSeparationsAsync(Acc, sepId, new[] { new AppSeparationRow("1000 ft", "5 NM") });

        var reloaded = Assert.Single((await _service.LoadForEditAsync(Acc)).Blocks).Block;
        Assert.Equal("Conf 1", Assert.Single(reloaded.Configurations).Name);
        Assert.Equal("1000 ft", Assert.Single(reloaded.Separations).Vertical);

        // Azzeramento: lista vuota → sparisce.
        await _service.SaveSeparationsAsync(Acc, sepId, Array.Empty<AppSeparationRow>());
        Assert.Empty(Assert.Single((await _service.LoadForEditAsync(Acc)).Blocks).Block.Separations);
    }

    [Fact]
    public async Task AddGroup_Then_RemoveGroup_On_Draft()
    {
        var model = await _service.LoadForEditAsync(Acc);   // bozza v1 col solo blocco Aerovia
        var groupId = await _service.AddGroupAsync(Acc, model.VersionId, "Gruppo Pisa");

        var afterAdd = await _service.LoadForEditAsync(Acc);
        Assert.Equal(2, afterAdd.Blocks.Count);
        var grp = afterAdd.Blocks[1];
        Assert.Equal(AccBlockKind.AppGroup, grp.Block.Kind);
        Assert.Equal("Gruppo Pisa", grp.Block.Title);
        // Sezioni-catalogo del profilo AccAppBlock (include vfr, assente in Aerovia).
        var expected = SectionCatalog.For(SectionProfile.AccAppBlock).Select(d => d.Key).ToArray();
        Assert.Equal(expected, grp.Block.Sections.Select(s => s.Key).ToArray());

        await _service.RemoveGroupAsync(Acc, groupId);
        Assert.Single((await _service.LoadForEditAsync(Acc)).Blocks);
    }

    [Fact]
    public async Task MoveGroup_Reorders_The_Groups_But_Aerovia_Stays_First()
    {
        var model = await _service.LoadForEditAsync(Acc);   // bozza v1 col solo blocco Aerovia
        var pisa = await _service.AddGroupAsync(Acc, model.VersionId, "Gruppo Pisa");
        var bari = await _service.AddGroupAsync(Acc, model.VersionId, "Gruppo Bari");

        async Task<string[]> Titoli() =>
            (await _service.LoadForEditAsync(Acc)).Blocks.Select(b => b.Block.Title).ToArray();

        Assert.Equal(new[] { "Settori di aerovia", "Gruppo Pisa", "Gruppo Bari" }, await Titoli());

        // Bari sale di un posto: scavalca Pisa, non l'Aerovia.
        await _service.MoveGroupAsync(Acc, bari, -1);
        Assert.Equal(new[] { "Settori di aerovia", "Gruppo Bari", "Gruppo Pisa" }, await Titoli());

        // ⚠️ Un altro passo in su non fa NIENTE: l'Aerovia resta in testa (decisione del committente).
        await _service.MoveGroupAsync(Acc, bari, -1);
        Assert.Equal(new[] { "Settori di aerovia", "Gruppo Bari", "Gruppo Pisa" }, await Titoli());

        // E in fondo all'elenco la freccia in giù non fa niente.
        await _service.MoveGroupAsync(Acc, pisa, 1);
        Assert.Equal(new[] { "Settori di aerovia", "Gruppo Bari", "Gruppo Pisa" }, await Titoli());
    }

    [Fact]
    public async Task MoveGroup_Does_Nothing_On_The_Aerovia_Block()
    {
        var model = await _service.LoadForEditAsync(Acc);
        await _service.AddGroupAsync(Acc, model.VersionId, "Gruppo Pisa");
        var aerovia = (await _service.LoadForEditAsync(Acc)).Blocks[0];
        Assert.Equal(AccBlockKind.Aerovia, aerovia.Block.Kind);

        await _service.MoveGroupAsync(Acc, aerovia.BlockSectionId, 1);

        Assert.Equal(new[] { "Settori di aerovia", "Gruppo Pisa" },
            (await _service.LoadForEditAsync(Acc)).Blocks.Select(b => b.Block.Title).ToArray());
    }

    [Fact]
    public async Task LoadForView_Null_Without_Effective_Release()
    {
        // Doc 10 §S6b: visibilità pubblica = release effettiva (uniforme alle altre famiglie). Rimosso il guscio
        // sintetico e il fallback alla versione pubblicata live.
        // Non ancora migrato: nessuna release → invisibile (null).
        Assert.Null(await _service.LoadForViewAsync(Acc));

        // Anche dopo aver pubblicato la VERSIONE (senza release effettiva) resta invisibile: serve una release.
        var docId = await _service.EnsureAsync(Acc);
        var editing = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));
        var draftVer = await _db.DocumentVersions.Where(v => v.DocumentId == docId).Select(v => v.Id).FirstAsync();
        await editing.PublishAsync(draftVer, actorUserId: 1, note: "pub");
        Assert.Null(await _service.LoadForViewAsync(Acc));
    }

    [Fact]
    public async Task LoadForView_HiddenDocument_IsNotServedToPublic()
    {
        // Stesso gate degli altri tipi (HiddenApp_WithEffectiveRelease_StaysHidden): «nascosto» vale anche
        // all'URL diretto, non solo in landing/ricerca. Qui l'ACC lo saltava: release effettiva → pagina servita.
        var docId = await _service.EnsureAsync(Acc);
        var releases = TestReleaseTargets.ReleaseRepo(_db);
        var key = $"{Acc}|{(await _service.GetIdentityAsync(Acc))!.RootCallsign}";
        var snap = await releases.SnapshotWorkingAsync(ReleaseTargetType.AccVipi, key, "2607");
        await releases.SaveReleaseAsync(ReleaseTargetType.AccVipi, key, "2607", DateTime.UtcNow.AddMinutes(-1), snap!, createdByUserId: 1, note: null);
        Assert.NotNull(await _service.LoadForViewAsync(Acc));

        var doc = await _db.Documents.FirstAsync(d => d.Id == docId);
        doc.IsHidden = true;
        await _db.SaveChangesAsync();

        Assert.Null(await _service.LoadForViewAsync(Acc));
    }

    /// <summary>
    /// Pubblica una release della vIPI ACC e la rende in vigore. <paramref name="comeNasceOggi"/> = col segno
    /// «configurazioni in Struttura» e, se <paramref name="congelate"/> non è null, con quella voce congelata nella
    /// sezione <c>configurations</c> — quel che fa <c>ReleaseService</c> con la cattura; senza = una release di
    /// prima dell'8 ottobre 2026, com'è in archivio.
    /// </summary>
    private async Task<string> PubblicaAsync(string ciclo, bool comeNasceOggi, string? congelate)
    {
        var releases = TestReleaseTargets.ReleaseRepo(_db);
        var editing = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));
        var model = await _service.LoadForEditAsync(Acc);
        var bozza = await _db.DocumentVersions.Where(v => v.DocumentId == model.DocumentId && v.Status == DocumentStatus.Draft)
            .Select(v => v.Id).FirstAsync();
        await editing.PublishAsync(bozza, actorUserId: 1, note: null);

        var key = $"{Acc}|{(await _service.GetIdentityAsync(Acc))!.RootCallsign}";
        var snap = await releases.SnapshotWorkingAsync(ReleaseTargetType.AccVipi, key, ciclo);
        Assert.NotNull(snap);
        if (comeNasceOggi)
        {
            var payload = System.Text.Json.JsonSerializer.Deserialize<DocReleasePayload>(snap!)!;
            payload.ConfigurazioniDallaStruttura = true;
            if (congelate is not null)
                payload.FrozenSections[Assert.Single(model.Blocks).ChildSectionIdsByKey["configurations"]] =
                    System.Text.Json.JsonSerializer.Serialize(new[] { Conf(congelate) });
            snap = System.Text.Json.JsonSerializer.Serialize(payload);
        }
        await releases.SaveReleaseAsync(ReleaseTargetType.AccVipi, key, ciclo, DateTime.UtcNow.AddMinutes(-1), snap!, createdByUserId: 1, note: null);
        return key;
    }

    /// <summary>Scrive nel documento il <c>BodyJson</c> della sezione <c>configurations</c>, com'era prima.</summary>
    private async Task NelDocumentoComePrima(string nome)
    {
        var model = await _service.LoadForEditAsync(Acc);
        var editing = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));
        await editing.SaveSectionBlockJsonBySectionAsync(Assert.Single(model.Blocks).ChildSectionIdsByKey["configurations"],
            System.Text.Json.JsonSerializer.Serialize(new[] { Conf(nome) }), 1);
    }

    /// <summary>
    /// 🔴 Una release <b>di prima</b> dell'8 ottobre 2026 dice le configurazioni che aveva: quelle scritte nel suo
    /// snapshot. Che in Struttura oggi ce ne siano altre non la cambia — non si tocca niente di già uscito.
    /// </summary>
    [Fact]
    public async Task Una_release_di_prima_tiene_le_configurazioni_del_suo_snapshot()
    {
        await NelDocumentoComePrima("Conf A");
        await PubblicaAsync("2607", comeNasceOggi: false, congelate: null);
        await InStruttura("Conf B");

        var view = await _service.LoadForViewAsync(Acc);
        Assert.Equal("Conf A", Assert.Single(Assert.Single(view!.Blocks).Block.Configurations).Name);
    }

    /// <summary>
    /// Una release <b>nuova</b> con la sezione congelata dice le configurazioni del momento della pubblicazione,
    /// anche se poi in Struttura cambiano — e non guarda il <c>BodyJson</c> rimasto nel documento.
    /// </summary>
    [Fact]
    public async Task Una_release_nuova_congelata_tiene_le_configurazioni_di_allora()
    {
        await NelDocumentoComePrima("Rimasta nel documento");
        await InStruttura("Conf A");
        var key = await PubblicaAsync("2607", comeNasceOggi: true, congelate: "Conf A");
        await InStruttura("Conf B");

        var view = await _service.LoadForViewAsync(Acc);
        Assert.Equal("Conf A", Assert.Single(Assert.Single(view!.Blocks).Block.Configurations).Name);

        var relId = await _db.DocReleases.Where(r => r.TargetKey == key).Select(r => r.Id).FirstAsync();
        var rv = await _service.LoadForReleaseAsync(Acc, relId);
        Assert.Equal("Conf A", Assert.Single(Assert.Single(rv!.Data.Blocks).Configurations).Name);
    }

    /// <summary>
    /// Una release nuova con la sezione <b>Live</b> (nessuna voce congelata) segue la Struttura di adesso — e,
    /// di nuovo, non il <c>BodyJson</c> rimasto nel documento: è il segno della release a dirlo.
    /// </summary>
    [Fact]
    public async Task Una_release_nuova_con_la_sezione_live_segue_la_struttura()
    {
        await NelDocumentoComePrima("Rimasta nel documento");
        await InStruttura("Conf A");
        await PubblicaAsync("2607", comeNasceOggi: true, congelate: null);
        await InStruttura("Conf B");

        var view = await _service.LoadForViewAsync(Acc);
        Assert.Equal("Conf B", Assert.Single(Assert.Single(view!.Blocks).Block.Configurations).Name);
    }

    [Fact]
    public async Task Release_Snapshot_Is_Frozen_Then_Served_By_View()
    {
        // Una release di prima (il BodyJson nel suo snapshot), e in Struttura oggi un'altra configurazione.
        await NelDocumentoComePrima("Conf A");
        var key = await PubblicaAsync("2607", comeNasceOggi: false, congelate: null);
        await InStruttura("Conf B");

        // La vista pubblica serve lo snapshot CONGELATO (Conf A), non il live (Conf B).
        var view = await _service.LoadForViewAsync(Acc);
        var configs = Assert.Single(view!.Blocks).Block.Configurations;
        Assert.Equal("Conf A", Assert.Single(configs).Name);

        // …e col ciclo AIRAC di QUELLA release (doc 13 §3h): la pagina scriveva il ciclo di oggi accanto a un
        // contenuto congelato a un ciclo diverso.
        Assert.Equal("2607", view.AiracCycle);

        // LoadForRelease per Id ritorna lo stesso snapshot col ciclo.
        var relId = await _db.DocReleases.Where(r => r.TargetKey == key).Select(r => r.Id).FirstAsync();
        var rv = await _service.LoadForReleaseAsync(Acc, relId);
        Assert.Equal("2607", rv!.AiracCycle);
        Assert.Equal("Conf A", Assert.Single(Assert.Single(rv.Data.Blocks).Configurations).Name);
    }

    /// <summary>Authz permissiva per i ctor dei service in test.</summary>
    private sealed class AllowAuthz : IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public VipiRole Role => IsAdmin ? VipiRole.Admin : VipiRole.User;
        public int? CurrentUserId => 1;
        public string? CurrentName => "test";
        public void EnsureAdmin() { }
    }
}
