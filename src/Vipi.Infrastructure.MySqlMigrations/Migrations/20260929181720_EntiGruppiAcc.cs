using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class EntiGruppiAcc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GroupKey",
                table: "AtcUnits",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true,
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AtcUnits_AccId_GroupKey",
                table: "AtcUnits",
                columns: new[] { "AccId", "GroupKey" },
                unique: true);

            // ⚠️ DOPO l'indice nuovo: MariaDB non toglie l'indice di una chiave esterna finché non ce n'è un altro
            // che comincia con la stessa colonna.
            migrationBuilder.DropIndex(
                name: "IX_AtcUnits_AccId",
                table: "AtcUnits");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Prima quello su AccId, poi via il composto: vedi Up.
            migrationBuilder.CreateIndex(
                name: "IX_AtcUnits_AccId",
                table: "AtcUnits",
                column: "AccId");

            migrationBuilder.DropIndex(
                name: "IX_AtcUnits_AccId_GroupKey",
                table: "AtcUnits");

            migrationBuilder.DropColumn(
                name: "GroupKey",
                table: "AtcUnits");
        }
    }
}
