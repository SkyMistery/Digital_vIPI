using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSidImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ImportSids",
                table: "ImportPolicies",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ForcePublished",
                table: "AirportSids",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsImported",
                table: "AirportSids",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsFixReview",
                table: "AirportSids",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "AirportSids",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceAiracCycle",
                table: "AirportSids",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StableKey",
                table: "AirportSids",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SidFixAliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Prefix = table.Column<string>(type: "TEXT", nullable: false),
                    FixName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SidFixAliases", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SidFixAliases_Prefix",
                table: "SidFixAliases",
                column: "Prefix",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SidFixAliases");

            migrationBuilder.DropColumn(
                name: "ImportSids",
                table: "ImportPolicies");

            migrationBuilder.DropColumn(
                name: "ForcePublished",
                table: "AirportSids");

            migrationBuilder.DropColumn(
                name: "IsImported",
                table: "AirportSids");

            migrationBuilder.DropColumn(
                name: "NeedsFixReview",
                table: "AirportSids");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "AirportSids");

            migrationBuilder.DropColumn(
                name: "SourceAiracCycle",
                table: "AirportSids");

            migrationBuilder.DropColumn(
                name: "StableKey",
                table: "AirportSids");
        }
    }
}
