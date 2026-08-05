using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.MySqlMigrations;

/// <summary>
/// Costruisce il <see cref="VipiDbContext"/> per gli strumenti EF (<c>dotnet ef migrations add</c>) senza
/// passare da un progetto di avvio e dalla sua configurazione.
///
/// <para><b>Perché non si usa l'host come startup-project.</b> Generare queste migrazioni richiederebbe di
/// avere <c>Persistence:Provider=MySql</c> attivo nella configurazione dell'host al momento del comando —
/// cioè di cambiare la configurazione di sviluppo per emettere una migrazione, e ricordarsi di rimetterla
/// a posto. La factory rende il comando autosufficiente e sempre uguale:</para>
///
/// <code>
/// dotnet ef migrations add &lt;Nome&gt; \
///   --project src/Vipi.Infrastructure.MySqlMigrations \
///   --startup-project src/Vipi.Infrastructure.MySqlMigrations \
///   -o Migrations
/// </code>
///
/// <para>La connection string qui è un segnaposto: la generazione dello scaffolding non apre connessioni,
/// legge solo il modello. Non va confusa con quella di produzione, che arriva da <c>ConnectionStrings:Vipi</c>.</para>
/// </summary>
public sealed class MySqlMigrationsDesignTimeFactory : IDesignTimeDbContextFactory<VipiDbContext>
{
    /// <summary>Segnaposto: serve a scegliere il provider, non a collegarsi.</summary>
    private const string ConnessioneFittizia = "Server=localhost;Port=3306;Database=itivao_atc;User Id=design;Password=design";

    public VipiDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<VipiDbContext>()
            .UseMySQL(ConnessioneFittizia, my => my
                // Senza questo le migrazioni finirebbero in Vipi.Infrastructure, insieme a quelle
                // SQLite-flavored, ed EF le applicherebbe tutte insieme sul primo provider che incontra.
                .MigrationsAssembly(typeof(MySqlMigrationsDesignTimeFactory).Assembly.GetName().Name))
            .Options;

        return new VipiDbContext(options);
    }
}
