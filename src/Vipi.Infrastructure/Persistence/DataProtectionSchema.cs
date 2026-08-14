namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Dove vivono le chiavi Data Protection (antiforgery, cookie di autenticazione, state OIDC) e con quale DDL
/// nasce la loro tabella.
///
/// <para><b>Perché è una decisione e non un dettaglio.</b> Il key-ring predefinito è una cartella sul disco.
/// Su un filesystem effimero — il container di Render, un processo riavviato da systemd su una macchina
/// riprovisionata — le chiavi si perdono a ogni riavvio, e il sintomo non parla di chiavi: gli utenti si
/// ritrovano sloggati e i form rispondono «antiforgery token non valido». Dove il database è persistente e il
/// disco no, il key-ring va nel database.</para>
///
/// <para><b>Perché la logica sta qui e non nell'host.</b> È l'unico punto in cui si decide, per provider, se
/// il key-ring vive nel DB — e la risposta sbagliata è silenziosa: si degrada al file-store e non lo dice
/// nessuno. Tenerla in una funzione pura la rende verificabile su entrambi i target, invece che solo
/// guidando l'app dopo un riavvio.</para>
///
/// <para>Il consumatore è <c>VipiDataProtection</c> in <c>Vipi.Host</c>, che di suo fa solo il wiring.</para>
/// </summary>
public static class DataProtectionSchema
{
    /// <summary>
    /// Nome della tabella. <b>Le maiuscole contano</b>: su Linux <c>lower_case_table_names</c> è 0, quindi
    /// <c>dataprotectionkeys</c> e <c>DataProtectionKeys</c> sono due tabelle diverse e EF cerca la seconda
    /// (convenzione: il nome del <c>DbSet</c>). Su Windows il problema non si vede.
    /// </summary>
    public const string TableName = "DataProtectionKeys";

    /// <summary>Chiave di configurazione della cartella del key-ring. Valorizzarla vince su tutto il resto.</summary>
    public const string KeyRingPathConfigKey = "DataProtection:KeyRingPath";

    /// <summary>Dove finiscono le chiavi Data Protection.</summary>
    public enum KeyRingStore
    {
        /// <summary>File-store predefinito di ASP.NET (profilo utente / <c>%LOCALAPPDATA%</c>): lo sviluppo.</summary>
        DefaultFileSystem,

        /// <summary>Tabella <c>DataProtectionKeys</c> nel database del modulo.</summary>
        Database,

        /// <summary>Cartella indicata da <see cref="KeyRingPathConfigKey"/>, che il servizio deve poter scrivere.</summary>
        ConfiguredFolder,
    }

    /// <summary>
    /// Dove tenere il key-ring, dato il provider e la cartella eventualmente configurata.
    ///
    /// <para><b>La regola, e il perché di ognuna delle tre.</b></para>
    /// <list type="number">
    ///   <item><description>Una cartella <b>configurata vince sempre</b>. È il caso di
    ///   <c>atc.it.ivao.aero</c>: lì il database è del committente, e le chiavi Data Protection firmano il
    ///   cookie di autenticazione — chi può fare <c>SELECT</c> sulla tabella può fabbricare una sessione
    ///   valida per qualunque VID, admin compresi, scavalcando tutto il modello di autorizzazione. Il dump di
    ///   consegna esclude già la tabella con questa motivazione; vale identica per la tabella viva sul loro
    ///   server. Su una macchina con systemd e una cartella di stato stabile il motivo per cui il key-ring
    ///   era finito nel database — il disco effimero — semplicemente non c'è.</description></item>
    ///   <item><description><see cref="PersistenceProvider.Sqlite"/> senza cartella: file-store predefinito.
    ///   È lo sviluppo su una macchina sola, e non aggiunge tabelle al DB di lavoro.</description></item>
    ///   <item><description>Gli altri due senza cartella: <b>database</b>. È il caso di Render, dove il
    ///   container riparte su un filesystem che non ricorda niente e una cartella non c'è: lì il database è
    ///   l'unico posto persistente, ed è comunque il <i>nostro</i>.</description></item>
    /// </list>
    ///
    /// <para>⚠️ La cartella dev'essere <b>fuori</b> dalla directory di deploy: su <c>atc.it.ivao.aero</c> si
    /// distribuisce scompattando uno zip sopra <c>/opt/vipi</c>, quindi delle chiavi tenute lì sopravvivono
    /// esattamente fino al primo aggiornamento. Il posto giusto è <c>/var/lib/vipi/keys</c>, che systemd sa
    /// creare e assegnare al servizio con <c>StateDirectory=vipi</c>. Perdere il key-ring non è un guasto
    /// grave — sono tutti sloggati una volta — ma è un guasto che si ripeterebbe a ogni deploy.</para>
    /// </summary>
    public static KeyRingStore ResolveStore(PersistenceProvider provider, string? configuredPath) =>
        !string.IsNullOrWhiteSpace(configuredPath) ? KeyRingStore.ConfiguredFolder
        : provider is PersistenceProvider.Postgres or PersistenceProvider.MySql ? KeyRingStore.Database
        : KeyRingStore.DefaultFileSystem;

    /// <summary>
    /// Se il key-ring di questo provider vive nel database <b>quando nessuna cartella è configurata</b>.
    /// Conservata perché è la domanda a cui risponde <see cref="CreateTableSql"/>: la tabella si crea solo
    /// dove potrebbe servire.
    /// </summary>
    public static bool UsesDatabaseKeyRing(PersistenceProvider provider)
        => ResolveStore(provider, null) == KeyRingStore.Database;

    /// <summary>
    /// DDL idempotente della tabella delle chiavi, nella forma attesa da
    /// <c>EntityFrameworkCoreXmlRepository</c> (<c>Id</c> identità, <c>FriendlyName</c>, <c>Xml</c>).
    ///
    /// <para>⚠️ <b>Non</b> si usa <c>EnsureCreated()</c>: verifica l'esistenza del <i>database</i>, non della
    /// tabella. Su un database che esiste già — sempre, visto che è quello del modulo — non crea niente e il
    /// primo accesso muore con «relation does not exist». È già costato una sessione su Postgres.</para>
    /// </summary>
    public static string CreateTableSql(PersistenceProvider provider) => provider switch
    {
        PersistenceProvider.Postgres => $"""
            CREATE TABLE IF NOT EXISTS "{TableName}" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "FriendlyName" text NULL,
                "Xml" text NULL
            );
            """,

        // La collation è la stessa delle altre 163 colonne stringa (MySqlCollation): non perché il confronto
        // di un FriendlyName sia critico, ma perché una tabella che eredita il default del database sarebbe
        // l'unica a comportarsi diversamente dal resto dello schema — una singolarità da spiegare ogni volta.
        PersistenceProvider.MySql => $"""
            CREATE TABLE IF NOT EXISTS `{TableName}` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `FriendlyName` longtext CHARACTER SET utf8mb4 COLLATE {MySqlCollation.Name} NULL,
                `Xml` longtext CHARACTER SET utf8mb4 COLLATE {MySqlCollation.Name} NULL,
                PRIMARY KEY (`Id`)
            ) CHARACTER SET utf8mb4;
            """,

        _ => throw new InvalidOperationException(
            $"Il provider {provider} non tiene le chiavi Data Protection nel database: " +
            $"chiamare {nameof(CreateTableSql)} solo dopo {nameof(UsesDatabaseKeyRing)}."),
    };
}
