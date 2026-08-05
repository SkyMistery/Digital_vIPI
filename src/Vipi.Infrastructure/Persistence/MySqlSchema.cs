namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Dove vivono le migrazioni MySQL. Il nome è una stringa e non un <c>typeof</c> per una ragione di
/// dipendenze: il progetto delle migrazioni referenzia <c>Vipi.Infrastructure</c> — è di lì che viene il
/// <c>VipiDbContext</c> — quindi il riferimento inverso sarebbe un ciclo. EF risolve l'assembly per nome a
/// runtime, e basta che sia nell'output dell'applicazione (ce lo mette l'host, che lo referenzia).
///
/// <para>Il prezzo di una stringa è che una rinomina del progetto non rompe la compilazione: romperebbe
/// l'avvio, e con un messaggio EF che parla di migrazioni mancanti invece che di un assembly introvabile.
/// Per questo <c>MySqlMigrationsTests</c> verifica che questo nome coincida con quello vero dell'assembly.</para>
/// </summary>
public static class MySqlSchema
{
    public const string MigrationsAssemblyName = "Vipi.Infrastructure.MySqlMigrations";
}
