using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La <b>rinomina</b> applicata: <c>LIBD_CS0_APP</c> → <c>LIBD_CS1_APP</c>, stessa riga alla sorgente
/// (id 4242).
///
/// <para>Quel che si prova qui è una cosa sola, ed è il punto di tutta la carta: <b>l'<c>Id</c> del
/// settore non cambia</b>. Tutto ciò che vi punta — documento, accordi, vLOA, blocchi, figli — continua a
/// funzionare senza essere toccato, e le poche cose che tengono il nominativo come <i>stringa</i> vengono
/// riscritte in un colpo solo.</para>
/// </summary>
public class RinominaSettoreTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    private const int IdSorgente = 4242;
    private const string Vecchio = "LIBD_CS0_APP";
    private const string Nuovo = "LIBD_CS1_APP";

    private int _settoreId, _docId, _accordoId, _figlioId, _bloccoId;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();

        var lirr = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(lirr);
        _db.Airports.Add(new Airport { Icao = "LIBD", Name = "Bari", Acc = lirr, ParentCallsign = Vecchio });
        await _db.SaveChangesAsync();

        _db.AirportSectors.AddRange(
            new AirportSector
            {
                IvaoId = IdSorgente, ComposePosition = Vecchio, AirportIcao = "LIBD", AccCode = "LIRR",
                Position = "APP", MiddleIdentifier = "CS0",
            },
            // Una TORRE che pende dall'APP: il legame è per callsign, e va seguito.
            new AirportSector
            {
                IvaoId = 4243, ComposePosition = "LIBD_TWR", AirportIcao = "LIBD", AccCode = "LIRR",
                Position = "TWR", ParentCallsign = Vecchio,
            });

        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = "APP Bari", Language = Language.It, LastUpdatedAiracCycle = "2608",
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        _docId = doc.Id;

        var settore = new Sector
        {
            Acc = lirr, Callsign = Vecchio, Name = "Bari APP", Type = SectorType.App, Kind = SectorKind.Airport,
            AirportIcao = "LIBD", IsProjected = true, IsActive = true, DocumentId = doc.Id, IsPrimary = true,
        };
        var altro = new Sector
        {
            Acc = lirr, Callsign = "LIRR_NE_CTR", Name = "Roma", Type = SectorType.Ctr, Kind = SectorKind.Acc,
            IsProjected = true, IsActive = true,
        };
        var figlio = new Sector
        {
            Acc = lirr, Callsign = "LIBD_TWR", Name = "Bari TWR", Type = SectorType.Twr, Kind = SectorKind.Airport,
            AirportIcao = "LIBD", IsProjected = true, IsActive = true, ParentSector = settore,
        };
        _db.Sectors.AddRange(settore, altro, figlio);
        await _db.SaveChangesAsync();
        _settoreId = settore.Id;
        _figlioId = figlio.Id;

        // Un accordo che ha il settore per lato (punta all'Id: non deve accorgersi di niente).
        var accordo = new CoordinationAgreement
        {
            OwnerAccId = lirr.Id,
            SideASectorId = Math.Min(settore.Id, altro.Id),
            SideBSectorId = Math.Max(settore.Id, altro.Id),
        };
        _db.CoordinationAgreements.Add(accordo);

        // La release del documento, e un blocco con la configurazione AoR: entrambi per nominativo.
        _db.DocReleases.Add(new DocRelease
        {
            TargetType = ReleaseTargetType.App, TargetKey = Vecchio, VersionNumber = 1,
            ReleaseAiracCycle = "2608", ReleaseEffectiveUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            PayloadJson = "{}",
        });
        _db.DocReleases.Add(new DocRelease
        {
            TargetType = ReleaseTargetType.AccVipi, TargetKey = $"LIRR|{Vecchio}", VersionNumber = 1,
            ReleaseAiracCycle = "2608", ReleaseEffectiveUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            PayloadJson = "{}",
        });

        var versione = new DocumentVersion { DocumentId = doc.Id, VersionNumber = 1, AiracCycle = "2608" };
        _db.DocumentVersions.Add(versione);
        await _db.SaveChangesAsync();
        _accordoId = accordo.Id;

        var sezione = new DocumentSection
        {
            DocumentVersionId = versione.Id, Title = "AoR", Order = 0, Depth = 0, SectionKey = "aor",
        };
        _db.DocumentSections.Add(sezione);
        await _db.SaveChangesAsync();

        var blocco = new ContentBlock
        {
            DocumentVersionId = versione.Id, SectionId = sezione.Id, Order = 0, Format = BlockFormat.Table,
            BodyJson = $$"""{"Callsigns":["{{Vecchio}}","LIBD_TWR"],"Name":"Conf 1"}""",
        };
        _db.ContentBlocks.Add(blocco);

        // Una sessione ATC: è STORIA, e dev'essere ancora lì col nominativo di allora.
        _db.AtcSessions.Add(new AtcSession
        {
            SessionId = 99001, UserId = 123456, Callsign = Vecchio,
            StartUtc = new DateTime(2026, 7, 1, 20, 0, 0, DateTimeKind.Utc),
        });
        await _db.SaveChangesAsync();
        _bloccoId = blocco.Id;
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private Task<RenameOutcome> Rinomina(string vecchio = Vecchio, string nuovo = Nuovo, int id = IdSorgente) =>
        new EfCallsignRenameService(_db).ApplyAsync(
            new[] { new CallsignRename(SourceCatalog.AirportPosition, id, vecchio, nuovo) });

    // ---- il cuore ----------------------------------------------------------------------------------

    [Fact]
    public async Task Il_settore_cambia_nome_ma_non_identita()
    {
        await Rinomina();

        var settore = await _db.Sectors.AsNoTracking().SingleAsync(s => s.Id == _settoreId);
        Assert.Equal(Nuovo, settore.Callsign);
        Assert.Equal(_docId, settore.DocumentId);      // il documento è ancora suo
        Assert.True(settore.IsPrimary);
        Assert.True(settore.IsActive);
    }

    [Fact]
    public async Task Non_nasce_un_secondo_settore()
    {
        await Rinomina();

        Assert.Equal(0, await _db.Sectors.CountAsync(s => s.Callsign == Vecchio));
        Assert.Equal(1, await _db.Sectors.CountAsync(s => s.Callsign == Nuovo));
    }

    /// <summary>Quel che punta all'Id non si tocca, e infatti non si è toccato: è il senso di tutto.</summary>
    [Fact]
    public async Task Accordi_e_figli_non_si_accorgono_di_niente()
    {
        await Rinomina();

        var accordo = await _db.CoordinationAgreements.AsNoTracking().SingleAsync(a => a.Id == _accordoId);
        Assert.True(accordo.SideASectorId == _settoreId || accordo.SideBSectorId == _settoreId);

        var figlio = await _db.Sectors.AsNoTracking().SingleAsync(s => s.Id == _figlioId);
        Assert.Equal(_settoreId, figlio.ParentSectorId);
    }

    // ---- quel che invece il nominativo lo tiene per stringa -----------------------------------------

    [Fact]
    public async Task La_riga_di_catalogo_prende_il_nome_nuovo()
    {
        await Rinomina();

        var riga = await _db.AirportSectors.AsNoTracking().SingleAsync(x => x.IvaoId == IdSorgente);
        Assert.Equal(Nuovo, riga.ComposePosition);
    }

    [Fact]
    public async Task La_gerarchia_per_callsign_segue_in_tutti_e_tre_i_posti()
    {
        await Rinomina();

        Assert.Equal(Nuovo, (await _db.AirportSectors.AsNoTracking()
            .SingleAsync(x => x.ComposePosition == "LIBD_TWR")).ParentCallsign);
        Assert.Equal(Nuovo, (await _db.Airports.AsNoTracking().SingleAsync(a => a.Icao == "LIBD")).ParentCallsign);
        Assert.Equal(0, await _db.AccSectors.CountAsync(x => x.ParentCallsign == Vecchio));
    }

    [Fact]
    public async Task Le_chiavi_di_release_seguono_nelle_due_forme()
    {
        await Rinomina();

        var chiavi = await _db.DocReleases.AsNoTracking().Select(r => r.TargetKey).ToListAsync();
        Assert.Contains(Nuovo, chiavi);              // App: il callsign nudo
        Assert.Contains($"LIRR|{Nuovo}", chiavi);    // AccVipi: {acc}|{callsign}
        Assert.DoesNotContain(chiavi, k => k.Contains(Vecchio));
    }

    [Fact]
    public async Task I_puntatori_dentro_i_blocchi_seguono()
    {
        await Rinomina();

        var body = (await _db.ContentBlocks.AsNoTracking().SingleAsync(b => b.Id == _bloccoId)).BodyJson!;
        Assert.Contains(Nuovo, body);
        Assert.DoesNotContain(Vecchio, body);
        Assert.Contains("LIBD_TWR", body);          // gli altri restano
        Assert.Contains("Conf 1", body);            // e i nomi liberi pure
    }

    // ---- la storia, che non si tocca ----------------------------------------------------------------

    /// <summary>
    /// La sessione ATC dice quale nominativo un controllore ha usato quella sera: riscriverla sarebbe
    /// falsificare un fatto. È esattamente perché resta com'è che serve l'alias.
    /// </summary>
    [Fact]
    public async Task La_sessione_atc_resta_col_nominativo_di_allora()
    {
        await Rinomina();

        Assert.Equal(1, await _db.AtcSessions.CountAsync(s => s.Callsign == Vecchio));
    }

    [Fact]
    public async Task L_alias_racconta_dove_e_finito_il_vecchio_nominativo()
    {
        await Rinomina();

        var alias = await _db.CallsignAliases.AsNoTracking().SingleAsync();
        Assert.Equal(Vecchio, alias.OldCallsign);
        Assert.Equal(Nuovo, alias.NewCallsign);
        Assert.Equal(IdSorgente, alias.IvaoId);
        Assert.Equal(SourceCatalog.AirportPosition, alias.Catalog);
        Assert.Equal(_settoreId, alias.SectorId);
    }

    // ---- quando non si può ---------------------------------------------------------------------------

    /// <summary>
    /// Il nominativo di destinazione è già di un'altra riga: applicare la rinomina violerebbe l'indice unico a
    /// metà giro, e scegliere chi cede il nome vuol dire scegliere quale documento perdere. Si riferisce.
    /// </summary>
    [Fact]
    public async Task Un_nominativo_gia_occupato_ferma_la_rinomina_senza_rompere_niente()
    {
        var esito = await Rinomina(nuovo: "LIBD_TWR");

        Assert.Empty(esito.Applied);
        var rifiutata = Assert.Single(esito.Refused);
        Assert.Contains("LIBD_TWR", rifiutata.Reason);

        // E niente si è mosso.
        Assert.Equal(Vecchio, (await _db.Sectors.AsNoTracking().SingleAsync(s => s.Id == _settoreId)).Callsign);
        Assert.Empty(await _db.CallsignAliases.AsNoTracking().ToListAsync());
    }

    /// <summary>Una rinomina rifiutata non deve impedire le altre: un import non si ferma per una riga strana.</summary>
    [Fact]
    public async Task Una_rifiutata_non_blocca_le_altre()
    {
        var esito = await new EfCallsignRenameService(_db).ApplyAsync(new[]
        {
            new CallsignRename(SourceCatalog.AirportPosition, IdSorgente, Vecchio, "LIBD_TWR"),   // occupato
            new CallsignRename(SourceCatalog.AirportPosition, 4243, "LIBD_TWR", "LIBD_N_TWR"),    // buona
        });

        Assert.Single(esito.Refused);
        Assert.Equal("LIBD_N_TWR", Assert.Single(esito.Applied).NewCallsign);
        Assert.Equal("LIBD_N_TWR", (await _db.Sectors.AsNoTracking().SingleAsync(s => s.Id == _figlioId)).Callsign);
    }

    [Fact]
    public async Task Senza_rinomine_non_succede_niente() =>
        Assert.False((await new EfCallsignRenameService(_db)
            .ApplyAsync(Array.Empty<CallsignRename>())).Any);
}
