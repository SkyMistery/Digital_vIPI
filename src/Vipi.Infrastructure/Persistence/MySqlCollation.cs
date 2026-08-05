using Microsoft.EntityFrameworkCore;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Collation case- e accent-<b>sensitive</b> per le colonne stringa, applicata solo quando il provider è MySQL.
///
/// <para><b>Perché è il punto più pericoloso del passaggio a MySQL.</b> MySQL confronta le stringhe in modo
/// case- e accent-<i>insensitive</i> per impostazione predefinita (<c>utf8mb4_0900_ai_ci</c>). SQLite e
/// PostgreSQL no. Il modello ha una decina di indici unici su stringa e almeno un'identità
/// content-addressed, quindi con la collation sbagliata succedono tre cose, tutte silenziose:</para>
/// <list type="bullet">
///   <item><description><c>lirf</c> e <c>LIRF</c> collidono ⇒ violazione di unique su dati oggi legali,
///   in fase di import — cioè su dati che arrivano dalla sorgente, non da una digitazione;</description></item>
///   <item><description><c>MediaAsset.Sha256</c> <b>è</b> l'identità dell'immagine: due hash che
///   differiscono solo per maiuscole verrebbero fusi in uno;</description></item>
///   <item><description>i lookup matchano case-insensitive senza lanciare niente: nessun errore, solo un
///   comportamento diverso da quello provato su SQLite.</description></item>
/// </list>
///
/// <para><b>Come si ottiene.</b> Il piano prevedeva <c>UseCollation</c> sul modello, contando sul fatto
/// che finisse nella <c>CREATE DATABASE</c>. Non regge: il database <c>itivao_atc</c> <b>esiste già</b>,
/// l'hanno creato loro, e quella istruzione non la eseguiamo mai. Né funziona la dichiarazione per
/// colonna, che il provider scarta (vedi <see cref="Apply"/>).</para>
///
/// <para>Quello che funziona è un <c>ALTER DATABASE … COLLATE</c> eseguito <b>prima</b> di creare le
/// tabelle (<see cref="AlterDatabaseSql"/>): da lì in poi ogni tabella e ogni colonna ereditano, incluse
/// quelle delle migrazioni future. Chiude la §1.3 del piano — «possiamo impostare la collation?» — invece
/// di lasciarla appesa a una loro configurazione, ma dipende da un permesso: l'utente deve poter fare
/// <c>ALTER</c> sul proprio database. Se non lo avesse, la migrazione iniziale fallisce a voce alta al
/// primo avvio, che è il modo giusto di scoprirlo.</para>
///
/// <para>⚠️ Da verificare guidando l'app, non per ispezione (§S10 del piano): un confronto
/// case-insensitive <b>voluto</b> — la ricerca globale, il lookup di un ICAO digitato dall'utente — qui
/// diventa sensibile alle maiuscole. Dove serve, va reso esplicito nel codice invece di essere ereditato
/// dalla collation.</para>
/// </summary>
public static class MySqlCollation
{
    /// <summary>
    /// Accent-sensitive, case-sensitive, Unicode 9.0 — l'equivalente MySQL 8.0 del comportamento che
    /// SQLite e PostgreSQL hanno già. Esiste solo da MySQL 8.0: su 5.7 il ripiego sarebbe
    /// <c>utf8mb4_bin</c>, che però è <i>binary</i> e cambierebbe anche l'ordinamento.
    /// </summary>
    public const string Name = "utf8mb4_0900_as_cs";

    /// <summary>
    /// Charset e collation del database, da eseguire <b>prima</b> di creare qualunque tabella: in MySQL una
    /// tabella senza charset esplicito eredita quello del database, e una colonna quello della tabella.
    /// Applicato una volta sola nella migrazione iniziale — è una proprietà del database, non delle singole
    /// tabelle, quindi vale anche per tutte quelle che verranno dopo.
    ///
    /// <para>Il nome del database è omesso di proposito: MySQL applica l'istruzione a quello corrente,
    /// cioè quello della connection string. Così la migrazione non contiene <c>itivao_atc</c> cablato e
    /// funziona identica sul container di prova.</para>
    /// </summary>
    public const string AlterDatabaseSql = $"ALTER DATABASE CHARACTER SET utf8mb4 COLLATE {Name};";

    /// <summary>
    /// Dichiara la collation sulle colonne stringa del modello. Da chiamare <b>solo</b> quando il provider
    /// è MySQL.
    ///
    /// <para>⚠️ <b>Da sola non basta, ed è la scoperta che è costata più tempo in questa slice.</b>
    /// Verificato generando la DDL il 5 agosto 2026: <c>MySql.EntityFrameworkCore</c> 10.0.9 porta la
    /// collation fino alle <i>operazioni</i> di migrazione — il file <c>.cs</c> generato contiene
    /// <c>collation: "utf8mb4_0900_as_cs"</c> su 163 colonne — ma il suo generatore SQL la <b>scarta</b>:
    /// nel <c>CREATE TABLE</c> non compare. Scarta anche l'annotazione <c>MySQL:Charset</c> della
    /// <c>AlterDatabase</c>, che infatti non produce alcuno statement.</para>
    ///
    /// <para>Provato anche a farla viaggiare dentro il <b>tipo di colonna</b>, che è l'unica cosa che il
    /// generatore emette alla lettera: funziona per le colonne senza lunghezza (<c>longtext COLLATE …</c>)
    /// ma <b>non</b> per quelle con una, perché il provider ricostruisce il tipo come <c>varchar(n)</c>
    /// dalla dimensione e butta il resto — cioè fallisce esattamente sulle colonne indicizzate, che sono
    /// quelle per cui la collation serve. Da qui la strada del <see cref="AlterDatabaseSql"/>.</para>
    ///
    /// <para>Questa chiamata resta per due motivi: rende il modello leggibile a chi ci guarda dentro, e se
    /// una versione futura del provider imparasse a emettere la collation, il valore è già quello giusto.
    /// Ma la garanzia vera viene dal database, non da qui.</para>
    ///
    /// <para>È il primo conto della debolezza del provider messa in preventivo in ADR-0007 §D4-bis, e
    /// sarebbe passato inosservato con una verifica sul modello: lì la collation risultava presente. Per
    /// questo i test guardano la <b>DDL generata</b>.</para>
    /// </summary>
    public static void Apply(ModelBuilder b)
    {
        foreach (var entity in b.Model.GetEntityTypes())
            foreach (var prop in entity.GetProperties())
            {
                // Vale anche per gli enum: OnModelCreating li salva come stringa via SetProviderClrType,
                // quindi sono colonne stringa che non sembrano tali guardando il tipo CLR.
                var tipoClr = prop.GetProviderClrType() ?? Nullable.GetUnderlyingType(prop.ClrType) ?? prop.ClrType;
                if (tipoClr == typeof(string)) prop.SetCollation(Name);
            }
    }
}
