using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// 🔴 U-141 (revisione totale 3): il documento <b>nascosto</b> chiude la pagina del vSOP militare, ma l'elenco
/// pubblico dei vSOP e il ponte civile↔militare guardavano solo la release. L'elenco lo mostrava ancora, il clic
/// portava a «Nessun vSOP militare pubblicato», e la vIPI civile teneva il collegamento. Nel verso opposto lo
/// stesso per la vIPI civile nascosta.
/// </summary>
public class MilitareNascostoTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfMilitaryDocumentService _mil = default!;
    private int _milDoc;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);

        var campo = await _db.Airports.FirstAsync(a => a.Icao == "LIRP");
        campo.Category = AirportCategory.MilitaryWithCivilPresence;
        campo.HasMilitaryPresence = true;
        var civile = new Document { Type = DocumentType.Vipi, Title = "vIPI — LIRP", Language = Language.It, Status = DocumentStatus.Published, LastUpdatedAiracCycle = "2606" };
        _db.Documents.Add(civile);
        await _db.SaveChangesAsync();
        campo.DocumentId = civile.Id;
        await _db.SaveChangesAsync();

        var editing = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));
        _mil = new EfMilitaryDocumentService(_db, new AiracService(), LivelloFisso.Editor, editing,
            new EfSpecialAreaRepository(_db), new EfNavaidCatalog(_db, LivelloFisso.Editor),
            new Vipi.Application.Content.DocumentLockGuard(editing, LivelloFisso.Editor));
        _milDoc = await _mil.CreaAsync("LIRP");

        foreach (var tipo in new[] { ReleaseTargetType.AirportMil, ReleaseTargetType.Airport })
            _db.DocReleases.Add(new DocRelease
            {
                TargetType = tipo, TargetKey = "LIRP", VersionNumber = 1, ReleaseAiracCycle = "2606",
                ReleaseEffectiveUtc = DateTime.UtcNow.AddDays(-1), Status = ReleaseStatus.Effective,
                PayloadJson = "{}", CreatedUtc = DateTime.UtcNow,
            });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private async Task NascondiAsync(int docId)
    {
        var d = await _db.Documents.FirstAsync(x => x.Id == docId);
        d.IsHidden = true;
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Pubblicato_e_visibile_sta_in_elenco_e_nel_ponte()
    {
        Assert.Contains(await _mil.ListAsync(perStaff: false), r => r.Icao == "LIRP");
        Assert.True(await _mil.HasPublishedAsync("LIRP"));
        Assert.True((await _mil.GetCivilEditionAsync("LIRP")).Pubblicata);
    }

    [Fact]
    public async Task Il_vSOP_nascosto_esce_dall_elenco_pubblico_e_dal_ponte()
    {
        await NascondiAsync(_milDoc);

        Assert.DoesNotContain(await _mil.ListAsync(perStaff: false), r => r.Icao == "LIRP");
        Assert.False(await _mil.HasPublishedAsync("LIRP"));
        // Allo staff resta raggiungibile: nascosto non vuol dire sparito.
        Assert.Contains(await _mil.ListAsync(perStaff: true), r => r.Icao == "LIRP");
    }

    [Fact]
    public async Task La_vIPI_civile_nascosta_esce_dal_ponte_ma_resta_esistente()
    {
        var civile = (await _db.Airports.FirstAsync(a => a.Icao == "LIRP")).DocumentId!.Value;
        await NascondiAsync(civile);

        var ed = await _mil.GetCivilEditionAsync("LIRP");
        Assert.True(ed.Esiste);   // l'editor militare continua a mandare i dati dello scalo all'editor civile
        Assert.False(ed.Pubblicata);
    }
}
