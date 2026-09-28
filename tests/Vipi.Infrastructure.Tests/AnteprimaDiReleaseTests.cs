using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// 🔴 U-053 (revisione totale 3): in anteprima di una release (<c>?as=rel:N</c>) le derivate si mostravano di OGGI,
/// e <see cref="FrozenSectionReader"/> sapeva leggere solo la release in vigore. Dentro
/// <see cref="AnteprimaDiRelease"/> il lettore legge le congelate di QUELLA release; fuori, e per ogni altro
/// bersaglio, resta la release in vigore.
/// </summary>
public class AnteprimaDiReleaseTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private FrozenSectionReader _lettore = default!;
    private int _programmata;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        var releases = TestReleaseTargets.ReleaseRepo(_db);
        _lettore = new FrozenSectionReader(releases);

        await releases.SaveReleaseAsync(ReleaseTargetType.Airport, "LIRF", "2609", DateTime.UtcNow.AddDays(-3),
            Payload("TORA 3000"), createdByUserId: 1, note: null);
        _programmata = await releases.SaveReleaseAsync(ReleaseTargetType.Airport, "LIRF", "2611",
            DateTime.UtcNow.AddDays(20), Payload("TORA 3100"), createdByUserId: 1, note: null);
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private static string Payload(string valore) => JsonSerializer.Serialize(new DocReleasePayload
    {
        Doc = null!,
        FrozenSections = new Dictionary<int, string> { [1] = JsonSerializer.Serialize(new Pista(valore)) },
    });

    public sealed record Pista(string Tora);

    private async Task<string?> ToraAsync(ReleaseTargetType tipo = ReleaseTargetType.Airport, string chiave = "LIRF") =>
        (await _lettore.LoadAsync(tipo, chiave)).Get<Pista>(1)?.Tora;

    [Fact]
    public async Task Fuori_dall_anteprima_si_legge_la_release_in_vigore()
    {
        Assert.Equal("TORA 3000", await ToraAsync());
    }

    [Fact]
    public async Task Dentro_l_anteprima_si_legge_la_release_mostrata()
    {
        using (AnteprimaDiRelease.Apri(ReleaseTargetType.Airport, "lirf", _programmata))
            Assert.Equal("TORA 3100", await ToraAsync());

        // Chiusa l'anteprima, si torna alla release in vigore.
        Assert.Equal("TORA 3000", await ToraAsync());
    }

    /// <summary>L'anteprima vale per il suo bersaglio: la stessa chiave di un'altra famiglia no.</summary>
    [Fact]
    public async Task L_anteprima_non_si_estende_a_un_altro_bersaglio()
    {
        using (AnteprimaDiRelease.Apri(ReleaseTargetType.AirportMil, "LIRF", _programmata))
            Assert.Equal("TORA 3000", await ToraAsync());
    }

    /// <summary>Una release di un altro bersaglio, passata per errore, non si legge: il lettore ricontrolla.</summary>
    [Fact]
    public async Task Una_release_di_un_altro_bersaglio_non_si_legge()
    {
        using (AnteprimaDiRelease.Apri(ReleaseTargetType.Airport, "LIRA", _programmata))
            Assert.Null(await ToraAsync(chiave: "LIRA"));
    }
}
