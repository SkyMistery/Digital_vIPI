using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// 🔴 U-037 (revisione totale 3): il sectorfile lo scriviamo in anticipo sul ciclo, e una frequenza o una posizione
/// cambiata entrava subito — anche nella release del ciclo in corso pubblicata dopo il giro. Ora, come per le aree
/// di settore, il cambio entra dal ciclo SUCCESSIVO: fino ad allora una release congela i valori in vigore.
/// </summary>
public class RadioassistenzeCicloAiracTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private readonly Orologio _orologio = new();
    private readonly ShapeReleaseContext _cattura = new();

    /// <summary>Il 28 settembre 2026: ciclo corrente 2609, successivo 2610 (dal 1° ottobre).</summary>
    private sealed class Orologio : TimeProvider
    {
        public DateTimeOffset Adesso { get; set; } = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Adesso;
    }

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private EfNavaidCatalog Anagrafica() => new(_db, LivelloFisso.Editor, _cattura, new AiracService(), _orologio);

    private static SourceNavaid Mnl(string freq, double lat) => new("MNL", "VHF", freq, "99Y", lat, 15.6898);
    private static readonly NavaidKey Chiave = new("MNL", "VHF", "99Y");

    private async Task<NavaidRow> AlCiclo(string? ciclo)
    {
        if (ciclo is null) return (await Anagrafica().GetManyAsync(new[] { Chiave })).Single();
        using (_cattura.Capturing(ciclo))
            return (await Anagrafica().GetManyAsync(new[] { Chiave })).Single();
    }

    [Fact]
    public async Task La_prima_volta_i_valori_sono_in_vigore_subito()
    {
        await Anagrafica().ImportFromSourceAsync(new[] { Mnl("115.25", 41.5476) });

        var riga = await _db.Navaids.AsNoTracking().SingleAsync();
        Assert.Null(riga.SourceAiracCycle);
        Assert.Equal("115.25", (await AlCiclo("2609")).Frequency);
    }

    [Fact]
    public async Task Un_valore_cambiato_entra_dal_ciclo_successivo()
    {
        await Anagrafica().ImportFromSourceAsync(new[] { Mnl("115.25", 41.5476) });
        await Anagrafica().ImportFromSourceAsync(new[] { Mnl("115.30", 41.6000) });

        var riga = await _db.Navaids.AsNoTracking().SingleAsync();
        Assert.Equal("2610", riga.SourceAiracCycle);
        Assert.Equal("115.25", riga.FrequencyInForce);

        // Una release del ciclo in corso congela quel che vale adesso; una del prossimo quello nuovo.
        var oggi = await AlCiclo("2609");
        Assert.Equal("115.25", oggi.Frequency);
        Assert.Equal(41.5476, oggi.Latitude!.Value, 6);
        Assert.Equal("115.30", (await AlCiclo("2610")).Frequency);
        // Fuori dalla cattura — l'anagrafica, l'editor — si vede la corrente.
        Assert.Equal("115.30", (await AlCiclo(null)).Frequency);
    }

    [Fact]
    public async Task Due_cambi_prima_del_ciclo_tengono_in_vigore_il_primo_valore()
    {
        await Anagrafica().ImportFromSourceAsync(new[] { Mnl("115.25", 41.5476) });
        await Anagrafica().ImportFromSourceAsync(new[] { Mnl("115.30", 41.5476) });
        await Anagrafica().ImportFromSourceAsync(new[] { Mnl("115.35", 41.5476) });

        Assert.Equal("115.25", (await AlCiclo("2609")).Frequency);
    }

    [Fact]
    public async Task Arrivato_il_ciclo_il_giro_chiude_il_differimento()
    {
        await Anagrafica().ImportFromSourceAsync(new[] { Mnl("115.25", 41.5476) });
        await Anagrafica().ImportFromSourceAsync(new[] { Mnl("115.30", 41.5476) });

        _orologio.Adesso = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        await Anagrafica().ImportFromSourceAsync(new[] { Mnl("115.30", 41.5476) });

        var riga = await _db.Navaids.AsNoTracking().SingleAsync();
        Assert.Null(riga.SourceAiracCycle);
        Assert.Null(riga.FrequencyInForce);
    }

    /// <summary>La forzatura valeva per quel valore: un cambio nuovo della sorgente la spegne, e la release di
    /// adesso torna a congelare quello in vigore.</summary>
    [Fact]
    public async Task Un_cambio_nuovo_spegne_la_forzatura()
    {
        await Anagrafica().ImportFromSourceAsync(new[] { Mnl("115.25", 41.5476) });
        await Anagrafica().ImportFromSourceAsync(new[] { Mnl("115.30", 41.5476) });
        var id = (await _db.Navaids.SingleAsync()).Id;
        await new EfSectorfileGateRepository(_db).ForceAsync(new[] { (DeferredKind.Radioassistenza, id) });
        Assert.Equal("115.30", (await AlCiclo("2609")).Frequency);

        await Anagrafica().ImportFromSourceAsync(new[] { Mnl("115.35", 41.5476) });

        Assert.False((await _db.Navaids.AsNoTracking().SingleAsync()).SourceForcePublished);
        Assert.Equal("115.25", (await AlCiclo("2609")).Frequency);
    }

    [Fact]
    public async Task Senza_cambi_nessun_differimento()
    {
        await Anagrafica().ImportFromSourceAsync(new[] { Mnl("115.25", 41.5476) });
        await Anagrafica().ImportFromSourceAsync(new[] { Mnl("115.25", 41.5476) });

        Assert.Null((await _db.Navaids.AsNoTracking().SingleAsync()).SourceAiracCycle);
    }
}
