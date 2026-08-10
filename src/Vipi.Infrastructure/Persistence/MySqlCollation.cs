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
/// <para><b>Come si ottiene.</b> Dichiarandola sulle proprietà del modello: Pomelo la porta fino in fondo,
/// emettendo <c>CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_as_cs</c> su ogni colonna stringa (163 nella
/// nostra DDL) più il charset sulla tabella. Non serve nient'altro.</para>
///
/// <para>⚠️ <b>Con il provider Oracle non funzionava</b>, e vale la pena saperlo perché è la ragione per
/// cui siamo su Pomelo. Quello portava la collation fino alle <i>operazioni</i> di migrazione ma la
/// scartava generando SQL; scartava anche l'annotazione charset della propria <c>AlterDatabase()</c>; e
/// nemmeno il ripiego di infilarla nel tipo di colonna reggeva, perché ricostruiva il tipo come
/// <c>varchar(n)</c> dalla lunghezza buttando il resto — fallendo esattamente sulle colonne indicizzate.
/// L'unica strada rimasta era un <c>ALTER DATABASE … COLLATE</c> innestato a mano nella migrazione
/// iniziale, che per giunta richiedeva il permesso <c>ALTER</c> sul database. Con Pomelo quel giro sparisce,
/// e con esso il permesso.</para>
///
/// <para>Il che è anche <b>più forte</b> di prima: la collation dichiarata sulla colonna sopravvive a un
/// cambio di default del database, che invece sarebbe passato inosservato.</para>
///
/// <para>⚠️ Da verificare guidando l'app, non per ispezione (§S10 del piano): un confronto
/// case-insensitive <b>voluto</b> — la ricerca globale, il lookup di un ICAO digitato dall'utente — qui
/// diventa sensibile alle maiuscole. Dove serve, va reso esplicito nel codice invece di essere ereditato
/// dalla collation.</para>
/// </summary>
public static class MySqlCollation
{
    /// <summary>
    /// Accent-sensitive, case-sensitive — il comportamento che SQLite e PostgreSQL hanno già.
    ///
    /// <para>È la collation <b>MariaDB</b> (UCA 14.0.0, disponibile da MariaDB 10.10), non quella MySQL:
    /// il server di produzione è MariaDB 11.4, e <c>utf8mb4_0900_as_cs</c> — che è il nome MySQL, usato
    /// qui fino al 5 agosto 2026 — su MariaDB <b>non esiste affatto</b>. Cambiare questa costante per
    /// tornare a MySQL non basterebbe: cambiano anche il lock di migrazione e il provider.</para>
    /// </summary>
    public const string Name = "utf8mb4_uca1400_as_cs";

    /// <summary>
    /// Dichiara la collation su ogni colonna stringa del modello. Da chiamare <b>solo</b> quando il
    /// provider è MySQL/MariaDB: sugli altri due questo nome non esiste e la DDL non sarebbe eseguibile.
    ///
    /// <para>Che poi arrivi davvero nel database non si dà per scontato: lo verifica un test sulla
    /// <b>DDL generata</b>, non sui metadati EF. Con il provider precedente la collation risultava
    /// presente nel modello e assente nella DDL, e un test sui metadati passava tranquillo.</para>
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
