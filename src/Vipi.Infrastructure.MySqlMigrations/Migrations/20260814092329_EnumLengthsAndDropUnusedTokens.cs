using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <summary>
    /// Le due modifiche di schema decise dall'audit del 14 agosto 2026
    /// (<c>docs/history/audit-2026-08-14-database-mariadb.md</c>), emesse per MySQL/MariaDB.
    ///
    /// <para><b>1. Quarantotto colonne enum da <c>longtext</c> a <c>varchar(32)</c>.</b> La regola che le
    /// dimensiona sta in <c>MySqlStringLengths.Apply</c> e vale per ogni enum salvato come stringa, non più
    /// per i soli indicizzati. Il motivo non sono le prestazioni — il database ha 4 800 righe — ma il fatto
    /// che una colonna <c>longtext</c> <b>non è indicizzabile</b>: il giorno in cui servisse un indice, non
    /// si aggiunge un indice, si riscrive una tabella su un database di produzione altrui in DDL non
    /// transazionale. Farlo ora costa un <c>ALTER TABLE</c> su tabelle di poche righe.</para>
    ///
    /// <para><b>2. Quattro <c>DropColumn</c> di <c>RowVersion</c></b> (<c>UnificationRules</c>,
    /// <c>TransferFlows</c>, <c>SharedBlocks</c>, <c>DocumentProfiles</c>). Quelle colonne dichiaravano un
    /// token di concorrenza che nessun percorso di scrittura ha mai valorizzato: sempre <c>NULL</c>, quindi
    /// <c>WHERE … AND RowVersion IS NULL</c> sempre vera e nessuna protezione. Per queste entità il
    /// last-write-wins è stato confermato come voluto, quindi via la colonna: una difesa dichiarata e
    /// inefficace è peggio della sua assenza. Le tre che il token lo tengono davvero (<c>Documents</c>,
    /// <c>DocumentSections</c>, <c>ContentBlocks</c>) ora lo fanno ruotare da <c>VipiDbContext</c>.</para>
    ///
    /// <para>⚠️ <b>Perché una migrazione nuova e non un <c>InitialCreate</c> rigenerato</b>, come pure
    /// l'audit proponeva. Rigenerare avrebbe dato una DDL più pulita, ma è legittimo solo finché <b>nessuna</b>
    /// <c>__EFMigrationsHistory</c> reale contiene le quattro migrazioni esistenti — una condizione che
    /// possiamo credere ma non verificare da qui, perché il 3306 di produzione non è raggiungibile. Una
    /// migrazione in più funziona in entrambi i mondi, e su un database vuoto l'<c>ALTER TABLE</c> che
    /// aggiunge costa quanto non farlo.</para>
    ///
    /// <para>L'ordine dello scaffold è stato lasciato com'era, a differenza di
    /// <c>20260807125819_SpecialAreasHardening</c>: qui i <c>DropColumn</c> precedono gli <c>AlterColumn</c>
    /// ma non c'è nessun travaso di dati fra le due, quindi non si perde niente.</para>
    /// </summary>
    public partial class EnumLengthsAndDropUnusedTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "UnificationRules");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "TransferFlows");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SharedBlocks");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DocumentProfiles");

            migrationBuilder.AlterColumn<string>(
                name: "VerticalState",
                table: "TransferPoints",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Parity",
                table: "TransferPoints",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "LevelUnit",
                table: "TransferPoints",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "LevelConstraint",
                table: "TransferPoints",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "TransferFlows",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Format",
                table: "SharedBlocks",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Sectors",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "Sectors",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ApproachKind",
                table: "Sectors",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "NeighbourCandidates",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "TargetType",
                table: "EditorTasks",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "EditorTasks",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "DocumentVersions",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Language",
                table: "Documents",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "DocumentParties",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "DocReleases",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "CoordinationPoints",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Visibility",
                table: "ContentBlocks",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Tier",
                table: "ContentBlocks",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Format",
                table: "ContentBlocks",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "CalloutKind",
                table: "ContentBlocks",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Surface",
                table: "AirportRunwayRules",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "DateParity",
                table: "AirportRunwayRules",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "longtext",
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "UnificationRules",
                type: "longblob",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VerticalState",
                table: "TransferPoints",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Parity",
                table: "TransferPoints",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "LevelUnit",
                table: "TransferPoints",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "LevelConstraint",
                table: "TransferPoints",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "TransferFlows",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "TransferFlows",
                type: "longblob",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Format",
                table: "SharedBlocks",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SharedBlocks",
                type: "longblob",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Sectors",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "Sectors",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ApproachKind",
                table: "Sectors",
                type: "longtext",
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "NeighbourCandidates",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "TargetType",
                table: "EditorTasks",
                type: "longtext",
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "EditorTasks",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "DocumentVersions",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Language",
                table: "Documents",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DocumentProfiles",
                type: "longblob",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "DocumentParties",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "DocReleases",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "CoordinationPoints",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Visibility",
                table: "ContentBlocks",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Tier",
                table: "ContentBlocks",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Format",
                table: "ContentBlocks",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "CalloutKind",
                table: "ContentBlocks",
                type: "longtext",
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldNullable: true,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Surface",
                table: "AirportRunwayRules",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "DateParity",
                table: "AirportRunwayRules",
                type: "longtext",
                nullable: false,
                collation: "utf8mb4_uca1400_as_cs",
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32,
                oldCollation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
