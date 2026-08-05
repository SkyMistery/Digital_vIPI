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
/// <para><b>Perché per colonna e non per database.</b> Il piano prevedeva una riga sola,
/// <c>UseCollation</c> a livello di modello, contando sul fatto che il database ereditasse il default.
/// Non funziona nel nostro caso: il database <c>itivao_atc</c> <b>esiste già</b>, creato da loro, e noi
/// non eseguiamo la <c>CREATE DATABASE</c> in cui quella clausola finirebbe. Restare su quella strada
/// significherebbe dipendere da una loro configurazione — la §1.3 del piano, «libertà sulla collation» —
/// che non controlliamo e che nessun test può verificare da qui.</para>
///
/// <para>Dichiararla <b>su ogni colonna stringa</b> costa qualche parola in più nella DDL generata e in
/// cambio rende la semantica indipendente dal server: qualunque sia il default del database, i confronti
/// sono quelli che ci aspettiamo. Questo chiude la §1.3 invece di lasciarla aperta.</para>
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
    /// Applica la collation a ogni colonna stringa del modello. Da chiamare <b>solo</b> quando il provider
    /// è MySQL: sugli altri due il nome non esiste e la DDL non sarebbe eseguibile.
    /// </summary>
    public static void Apply(ModelBuilder b)
    {
        foreach (var entity in b.Model.GetEntityTypes())
            foreach (var prop in entity.GetProperties())
            {
                // Vale anche per gli enum: OnModelCreating li salva come stringa via SetProviderClrType,
                // quindi sono colonne stringa che non sembrano tali guardando il tipo CLR.
                var tipoStore = prop.GetProviderClrType() ?? Nullable.GetUnderlyingType(prop.ClrType) ?? prop.ClrType;
                if (tipoStore == typeof(string)) prop.SetCollation(Name);
            }
    }
}
