using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Vipi.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il gemello SQLite di <see cref="MySqlMigrationsTests"/>: il modello e lo snapshot delle migrazioni SQLite
/// devono coincidere.
///
/// <para><b>Perché (T-087, revisione del 13 settembre 2026).</b> Il set MySQL aveva la sua guardia, quello
/// SQLite no: un'entità o una colonna aggiunta rigenerando solo le migrazioni MySQL passava verde, e in locale
/// (e in ogni test su database vero) lo schema nasceva senza. Quel giorno la misura con
/// <c>has-pending-model-changes</c> dava zero differenze: mancava la garanzia, non c'era deriva.</para>
///
/// <para>Rimedio quando fallisce: <c>dotnet ef migrations add &lt;Nome&gt; --project
/// src/Vipi.Infrastructure --startup-project src/Vipi.Infrastructure -o Persistence/Migrations</c> (README).</para>
/// </summary>
public class SqliteMigrationsTests
{
    private readonly ITestOutputHelper _out;
    public SqliteMigrationsTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void Le_migrazioni_sqlite_sono_allineate_al_modello()
    {
        // La connessione non si apre: il confronto legge il modello e lo snapshot, non il database.
        using var db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>()
            .UseSqlite("Data Source=:memory:").Options);

        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot;
        Assert.NotNull(snapshot);

        // ⚠️ Un confronto che non vede niente non prova niente: se lo snapshot fosse vuoto o quello sbagliato,
        // le differenze sarebbero TUTTE le tabelle, non zero. Ma se fosse lo stesso oggetto del modello
        // sarebbero zero per costruzione: la riga qui sotto controlla che le tabelle ci siano davvero.
        var modelloSnapshot = db.GetService<IModelRuntimeInitializer>()
            .Initialize((IModel)snapshot!.Model, designTime: true, validationLogger: null);
        Assert.True(modelloSnapshot.GetRelationalModel().Tables.Count() > 50,
            "lo snapshot SQLite ha troppo poche tabelle: il confronto non starebbe guardando il set vero");

        var differenze = db.GetService<IMigrationsModelDiffer>().GetDifferences(
            modelloSnapshot.GetRelationalModel(),
            db.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        foreach (var op in differenze) _out.WriteLine(op.GetType().Name);

        Assert.True(differenze.Count == 0,
            $"il modello ha {differenze.Count} differenze non ancora emesse come migrazione SQLite " +
            "(vedi l'output del test per i tipi di operazione mancanti)");
    }
}
