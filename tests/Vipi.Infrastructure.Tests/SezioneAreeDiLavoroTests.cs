using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Le «Aree di lavoro» del vSOP militare: la selezione delle aree e — solo qui — <b>che attività</b> si vola
/// in ognuna (carta <c>2026-08-27-vsop-militari.md</c> §12h).
///
/// <para>
/// ⚠️ <b>Il test che conta è il primo.</b> Selezione e attività stanno nello <b>stesso</b> oggetto JSON, e il
/// salvataggio della selezione — che è un'altra tendina, toccata da un'altra persona in un altro momento —
/// serializzava la sola selezione: senza la conservazione, ogni chip aggiunta o tolta avrebbe cancellato
/// tutte le attività. Nessun errore, e chi le aveva scritte non tocca mai quella tendina.
/// </para>
/// </summary>
public class SezioneAreeDiLavoroTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    private sealed class AllowAuthz : IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public VipiRole Role => VipiRole.Admin;
        public int? CurrentUserId => 42;
        public string? CurrentName => "test";
        public void EnsureAdmin() { }
    }

    private EfMilitaryDocumentService Militari() =>
        new(_db, new AiracService(), new AllowAuthz(),
            new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db)),
            new EfSpecialAreaRepository(_db), new EfNavaidCatalog(_db), new EfAirportNameLookup(_db));

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();

        var acc = new Acc { Code = "LIBB", Name = "Brindisi" };
        _db.Accs.Add(acc);
        _db.Airports.Add(new Airport
        {
            Icao = "LIBA", Name = "Amendola", Acc = acc, HasMilitaryPresence = true, IsMilitaryOnly = true,
        });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private static RegulatedSelection Selezione(params string[] ids) =>
        new() { OwnAuto = false, OwnIds = ids.ToList() };

    /// <summary>⚠️ Salvare la SELEZIONE non deve cancellare le ATTIVITÀ: stanno nello stesso oggetto.</summary>
    [Fact]
    public async Task Cambiare_le_aree_scelte_non_cancella_le_attivita()
    {
        var m = Militari();
        await m.CreaAsync("LIBA");
        await m.SaveRegulatedAsync("LIBA", Selezione("A1", "A2"));
        await m.SaveAreaActivityAsync("LIBA", "A1", MilActivity.AirToAir | MilActivity.AirToGround);

        // Un'altra persona, un altro momento: aggiunge un'area con le chip.
        await m.SaveRegulatedAsync("LIBA", Selezione("A1", "A2", "A3"));

        var attivita = await m.GetAreaActivitiesAsync("LIBA");
        Assert.Equal(MilActivity.AirToAir | MilActivity.AirToGround, attivita["A1"]);
        Assert.Equal(new[] { "A1", "A2", "A3" }, (await m.GetRegulatedAsync("LIBA")).OwnIds);
    }

    /// <summary>Un'area tolta dalla selezione si porta via la sua attività: un payload che cresce a ogni
    /// ripensamento è un payload di cui nessuno sa più quali righe contano.</summary>
    [Fact]
    public async Task Togliere_un_area_ne_scarta_l_attivita()
    {
        var m = Militari();
        await m.CreaAsync("LIBA");
        await m.SaveRegulatedAsync("LIBA", Selezione("A1", "A2"));
        await m.SaveAreaActivityAsync("LIBA", "A2", MilActivity.AirToGround);

        await m.SaveRegulatedAsync("LIBA", Selezione("A1"));

        Assert.Empty(await m.GetAreaActivitiesAsync("LIBA"));
    }

    /// <summary>Si scrive UN'area alla volta: due persone che marcano due aree diverse non si
    /// sovrascrivono — è la stessa regola dei campi delle radioassistenze.</summary>
    [Fact]
    public async Task Due_aree_diverse_non_si_sovrascrivono()
    {
        var m = Militari();
        await m.CreaAsync("LIBA");
        await m.SaveRegulatedAsync("LIBA", Selezione("A1", "A2"));

        await m.SaveAreaActivityAsync("LIBA", "A1", MilActivity.AirToAir);
        await m.SaveAreaActivityAsync("LIBA", "A2", MilActivity.AirToGround);

        var attivita = await m.GetAreaActivitiesAsync("LIBA");
        Assert.Equal(MilActivity.AirToAir, attivita["A1"]);
        Assert.Equal(MilActivity.AirToGround, attivita["A2"]);
    }

    /// <summary>Spegnere tutti i gettoni toglie la riga invece di salvare «niente»: in archivio «nessuna
    /// attività» e «non l'ha ancora detto nessuno» devono essere la stessa cosa.</summary>
    [Fact]
    public async Task Spegnere_tutto_toglie_l_attivita()
    {
        var m = Militari();
        await m.CreaAsync("LIBA");
        await m.SaveRegulatedAsync("LIBA", Selezione("A1"));
        await m.SaveAreaActivityAsync("LIBA", "A1", MilActivity.AirToAir);

        await m.SaveAreaActivityAsync("LIBA", "A1", MilActivity.None);

        Assert.Empty(await m.GetAreaActivitiesAsync("LIBA"));
    }

    // ---- Le due tabelle a colonne fisse ----------------------------------------------------------------

    /// <summary>Il giro completo di «Nominativi»: sono una sotto-sezione, quindi anche qui il payload deve
    /// saper scendere nei figli.</summary>
    [Fact]
    public async Task I_nominativi_si_salvano_e_si_rileggono()
    {
        var m = Militari();
        await m.CreaAsync("LIBA");

        await m.SaveFixedTableAsync("LIBA", "callsigns", MilTablePayload.Nominativi, new[]
        {
            new[] { "13° Gruppo", "IBIS", "IAM 1234", "QRA 01" },
            new[] { "101° Gruppo", "SPARVIERO", "IAM 5678", "" },
        }, 4);

        var righe = await m.GetFixedTableAsync("LIBA", "callsigns", 4);
        Assert.Equal(2, righe.Count);
        Assert.Equal("QRA 01", righe[0][3]);
        Assert.Equal("", righe[1][3]);
    }

    [Fact]
    public async Task I_parcheggi_si_salvano_e_si_rileggono()
    {
        var m = Militari();
        await m.CreaAsync("LIBA");

        await m.SaveFixedTableAsync("LIBA", "parkings", MilTablePayload.Parcheggi,
            new[] { new[] { "Piazzale Nord", "1-12", "13° Gruppo" } }, 3);

        Assert.Equal("Piazzale Nord", (await m.GetFixedTableAsync("LIBA", "parkings", 3)).Single()[0]);
    }

    /// <summary>Le due sezioni sono figlie, come tutte le altre del profilo militare.</summary>
    [Theory]
    [InlineData("callsigns")]
    [InlineData("parkings")]
    public async Task Le_sezioni_a_mano_sono_figlie(string chiave)
    {
        var docId = await Militari().CreaAsync("LIBA");

        var sezione = await _db.DocumentSections.AsNoTracking()
            .FirstAsync(s => s.DocumentVersion!.DocumentId == docId && s.SectionKey == chiave);

        Assert.NotNull(sezione.ParentSectionId);
    }

    [Fact]
    public async Task Un_campo_senza_documento_non_esplode()
    {
        Assert.Empty(await Militari().GetFixedTableAsync("LIRF", "callsigns", 4));
        Assert.Empty(await Militari().GetAreaActivitiesAsync("LIRF"));
    }
}
