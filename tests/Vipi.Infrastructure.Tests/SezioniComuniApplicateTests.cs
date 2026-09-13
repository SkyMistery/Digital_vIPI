using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// 🔴 T-026 (revisione del 13 settembre 2026): «Sezioni in comune» di un'unione si applicava sezione per sezione.
/// Con il piano [nascondi nel civile, mostra nel militare] e il lock del militare in mano a un altro, il primo passo
/// riusciva e il secondo sollevava: METAR restava nascosto in TUTTI e due i documenti — la pagina unita perdeva
/// il dato che la scheda doveva solo non ripetere.
/// </summary>
public class SezioniComuniApplicateTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private int _civileId, _militareId;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _civileId = await CreaDocumentoAsync("vIPI — LIMN", metarNascosto: false);
        _militareId = await CreaDocumentoAsync("vSOP MIL — LIMN", metarNascosto: true);
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private async Task<int> CreaDocumentoAsync(string titolo, bool metarNascosto)
    {
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = titolo, Language = Language.It, Edition = DocumentEdition.Civil,
            Status = DocumentStatus.Draft, LastUpdatedAiracCycle = "2609",
        };
        var ver = new DocumentVersion
        {
            Document = doc, VersionNumber = 1, Status = DocumentStatus.Draft, AiracCycle = "2609",
            CreatedUtc = DateTime.UtcNow,
        };
        doc.Versions.Add(ver);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        _db.DocumentSections.Add(new DocumentSection
        {
            DocumentVersion = ver, Title = "METAR", Order = 1, Depth = 0, SectionKey = "metar",
            IsHidden = metarNascosto, RowVersion = Guid.NewGuid().ToByteArray(),
        });
        doc.CurrentVersionId = ver.Id;
        await _db.SaveChangesAsync();
        return doc.Id;
    }

    private EfEditingRepository Repo() =>
        new(_db, new Vipi.Domain.Services.AiracService(), new EfMediaMaintenance(_db));

    private EditingService Servizio() =>
        new(Repo(), new AllowAuthz(),
            Microsoft.Extensions.Options.Options.Create(new Vipi.Application.ReleaseRetentionOptions()));

    private Task<bool> MetarNascostoAsync(int documentId) =>
        _db.DocumentSections.AsNoTracking()
           .Where(s => s.DocumentVersion!.DocumentId == documentId && s.SectionKey == "metar")
           .Select(s => s.IsHidden).SingleAsync();

    [Theory]
    [InlineData(true)]    // manca il lock di chi deve MOSTRARE
    [InlineData(false)]   // manca il lock di chi deve NASCONDERE
    public async Task Senza_il_lock_di_UN_membro_non_si_scrive_in_NESSUNO(bool mancaIlMilitare)
    {
        var svc = Servizio();
        var mio = mancaIlMilitare ? _civileId : _militareId;
        var suo = mancaIlMilitare ? _militareId : _civileId;
        await svc.AcquireLockAsync(mio);
        await Repo().AcquireOrInspectLockAsync(suo, 2, "Altro", 30);             // di un collega

        var membri = new[] { (_civileId, ReleaseTargetType.Airport), (_militareId, ReleaseTargetType.AirportMil) };
        await Assert.ThrowsAsync<EditConflictException>(() =>
            svc.ApplicaSezioniComuniAsync(new[] { _civileId }, membri, new[] { "metar" }));

        // Nessun passo a metà: il civile lo mostra ancora, il militare lo nasconde ancora.
        Assert.False(await MetarNascostoAsync(_civileId));
        Assert.True(await MetarNascostoAsync(_militareId));
    }

    [Fact]
    public async Task Con_tutti_i_lock_si_applica_il_piano_intero()
    {
        var svc = Servizio();
        await svc.AcquireLockAsync(_civileId);
        await svc.AcquireLockAsync(_militareId);

        var membri = new[] { (_civileId, ReleaseTargetType.Airport), (_militareId, ReleaseTargetType.AirportMil) };
        var passi = await svc.ApplicaSezioniComuniAsync(new[] { _civileId }, membri, new[] { "metar" });

        Assert.Equal(2, passi);
        Assert.True(await MetarNascostoAsync(_civileId));
        Assert.False(await MetarNascostoAsync(_militareId));
    }

    private sealed class AllowAuthz : IEditAuthorizationService
    {
        public VipiRole Role => VipiRole.Admin;
        public bool IsAdmin => true;
        public int? CurrentUserId => 1;
        public string? CurrentName => "test";
        public void EnsureAdmin() { }
    }
}
