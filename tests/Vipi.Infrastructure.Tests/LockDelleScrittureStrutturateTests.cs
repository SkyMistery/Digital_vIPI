using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Aor;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// 🔴 <b>T-004 e T-063</b> (revisione del 13 settembre 2026): le scritture strutturate di APP, ACC e vSOP militare
/// pretendono il lock del documento, come la prosa. Prima controllavano il solo ruolo: chi il lock l'aveva perso
/// — scaduto, o tolto da «sblocca comunque» — riscriveva per intero il corpo della sezione sopra il lavoro di chi
/// l'aveva preso dopo. E la vIPI ACC scriveva qualunque sezione le si passasse, anche di un altro documento.
/// </summary>
public class LockDelleScrittureStrutturateTests : IAsyncLifetime
{
    private const int Io = 1;
    private const int Altro = 2;

    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfEditingRepository _editing = default!;
    private readonly Authz _authz = new();

    private sealed class Authz : IEditAuthorizationService
    {
        public VipiRole Role => VipiRole.Admin;
        public bool IsAdmin => true;
        public int? CurrentUserId => Io;
        public string? CurrentName => "io";
        public void EnsureAdmin() { }
    }

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);
        _editing = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private DocumentLockGuard Guardia() => new(_editing, _authz);

    private Task PrendiLockAsync(int docId, int chi) =>
        _editing.AcquireOrInspectLockAsync(docId, chi, chi == Io ? "io" : "altro", 30);

    // ------------------------------------------------------------------ APP

    private AppDocumentService App()
    {
        var topo = new TopologyBuilder(_db);
        var forme = new EfSectorShapeResolver(_db, new EfSectorAirspaceBindings(_db), new EfSectorShapeParts(_db));
        return new AppDocumentService(new EfAppDerivationRepository(_db), new EfSpecialAreaRepository(_db), _editing,
            _authz, topo, new AgreementService(new EfAgreementRepository(_db), _authz, topo, LockDiRisorsaConcesso.Instance),
            new StubCoordinationSentenceTemplate(), new EfDocumentProfileRepository(_db),
            new Vipi.Application.Aor.AorService(), new NoMinimaSource(), forme, Guardia());
    }

    [Fact]
    public async Task APP_senza_lock_le_separazioni_non_si_salvano()
    {
        var app = App();
        var docId = await app.EnsureAsync("LIRP_APP");

        await Assert.ThrowsAsync<EditConflictException>(() =>
            app.SaveSeparationsAsync("LIRP_APP", new[] { new AppSeparationRow("1000 ft", "5 NM") }));
        Assert.Empty(await app.GetSeparationsAsync("LIRP_APP"));

        await PrendiLockAsync(docId, Io);
        await app.SaveSeparationsAsync("LIRP_APP", new[] { new AppSeparationRow("1000 ft", "5 NM") });
        Assert.Single(await app.GetSeparationsAsync("LIRP_APP"));
    }

    /// <summary>Lo scenario del registro: il lock ce l'ha un ALTRO, e la pagina vecchia prova a scrivere.</summary>
    [Fact]
    public async Task APP_col_lock_di_un_altro_non_si_riscrive_il_suo_lavoro()
    {
        var app = App();
        var docId = await app.EnsureAsync("LIRP_APP");
        await PrendiLockAsync(docId, Altro);

        await Assert.ThrowsAsync<EditConflictException>(() =>
            app.SaveFrequencyOrderAsync("LIRP_APP", new[] { new AppFreqOrderOverride("LIRP_TWR", 0) }));
        await Assert.ThrowsAsync<EditConflictException>(() =>
            app.SaveVfrAsync("LIRP_APP", new AppVfrContent("sovrascritto", Array.Empty<AppVfrRow>())));
    }

    // ------------------------------------------------------------------ ACC

    private AccDocumentService Acc() =>
        new(new EfAccDerivationRepository(_db), _editing, _authz, TestReleaseTargets.ReleaseRepo(_db), Guardia());

    [Fact]
    public async Task ACC_senza_lock_non_si_salva_e_non_si_aggiungono_gruppi()
    {
        var acc = Acc();
        var model = await acc.LoadForEditAsync("LIRR");
        var sepId = Assert.Single(model.Blocks).ChildSectionIdsByKey["separations"];

        await Assert.ThrowsAsync<EditConflictException>(() =>
            acc.SaveSeparationsAsync("LIRR", sepId, new[] { new AppSeparationRow("1000 ft", "5 NM") }));
        await Assert.ThrowsAsync<EditConflictException>(() => acc.AddGroupAsync("LIRR", model.VersionId, "Gruppo"));

        await PrendiLockAsync(model.DocumentId, Io);
        await acc.SaveSeparationsAsync("LIRR", sepId, new[] { new AppSeparationRow("1000 ft", "5 NM") });
        Assert.Single(Assert.Single((await acc.LoadForEditAsync("LIRR")).Blocks).Block.Separations);
    }

    /// <summary>T-004, l'altra metà: <c>accCode</c> non si ignora. Una sezione di un altro documento non si scrive
    /// passando da questa porta, nemmeno col lock di quest'ACC in mano.</summary>
    [Fact]
    public async Task ACC_una_sezione_di_un_altro_documento_si_rifiuta()
    {
        var acc = Acc();
        var model = await acc.LoadForEditAsync("LIRR");
        await PrendiLockAsync(model.DocumentId, Io);

        var docApp = await App().EnsureAsync("LIRP_APP");
        var sezioneApp = await _db.DocumentSections.AsNoTracking()
            .Where(s => s.DocumentVersion!.DocumentId == docApp && s.SectionKey == "separations")
            .Select(s => s.Id).FirstAsync();

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() =>
            acc.SaveSeparationsAsync("LIRR", sezioneApp, new[] { new AppSeparationRow("99 ft", "1 NM") }));
    }

    /// <summary>T-063: si elimina un gruppo APP, non l'Aerovia e non una sezione qualunque dentro un blocco.</summary>
    [Fact]
    public async Task ACC_si_eliminano_solo_i_gruppi()
    {
        var acc = Acc();
        var model = await acc.LoadForEditAsync("LIRR");
        await PrendiLockAsync(model.DocumentId, Io);
        var aerovia = Assert.Single(model.Blocks);

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() =>
            acc.RemoveGroupAsync("LIRR", aerovia.BlockSectionId));
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() =>
            acc.RemoveGroupAsync("LIRR", aerovia.ChildSectionIdsByKey["separations"]));
        Assert.Single((await acc.LoadForEditAsync("LIRR")).Blocks);

        var gruppo = await acc.AddGroupAsync("LIRR", model.VersionId, "Gruppo Pisa");
        await acc.RemoveGroupAsync("LIRR", gruppo);
        Assert.Single((await acc.LoadForEditAsync("LIRR")).Blocks);
    }

    // ------------------------------------------------------------------ vSOP militare

    [Fact]
    public async Task Militare_senza_lock_le_tabelle_non_si_salvano()
    {
        var campo = await _db.Airports.FirstAsync(a => a.Icao == "LIRP");
        campo.Category = AirportCategory.MilitaryOnly;
        campo.HasMilitaryPresence = true;
        await _db.SaveChangesAsync();

        var mil = new EfMilitaryDocumentService(_db, new AiracService(), _authz, _editing,
            new EfSpecialAreaRepository(_db), new EfNavaidCatalog(_db), Guardia());
        var docId = await mil.CreaAsync("LIRP");   // creare non chiede il lock: lo fa l'elenco

        await Assert.ThrowsAsync<EditConflictException>(() =>
            mil.SaveNavaidsAsync("LIRP", new[] { new NavaidKey("MNL", "VHF", "99Y") }));
        await Assert.ThrowsAsync<EditConflictException>(() =>
            mil.SaveRegulatedAsync("LIRP", SectionKeys.Regulated, new RegulatedSelection { OwnIds = { "X" } }));

        await PrendiLockAsync(docId, Io);
        await mil.SaveRegulatedAsync("LIRP", SectionKeys.Regulated, new RegulatedSelection { OwnIds = { "X" } });
        Assert.Equal(new[] { "X" }, (await mil.GetRegulatedAsync("LIRP", SectionKeys.Regulated)).OwnIds);
    }
}
