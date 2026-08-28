using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class ArchivioAtcMondiale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ⚠️ Qui `defaultValue: false` è il valore GIUSTO, non la solita trappola del bool nuovo: le
            // righe già in tabella sono tutte di divisione (verificato sull'archivio reale del 28 agosto
            // 2026: 21 133 sessioni, zero callsign fuori dai prefissi). La colonna è stata dichiarata in
            // negativo apposta perché il default coincidesse con la verità dello storico — su tutti e tre i
            // percorsi che creano schema: migrazione, EnsureCreated e PostgresSchemaReconciler.
            migrationBuilder.AddColumn<bool>(
                name: "IsOutsideDivision",
                table: "AtcSessions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_AtcSessions_IsOutsideDivision_StartUtc",
                table: "AtcSessions",
                columns: new[] { "IsOutsideDivision", "StartUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AtcSessions_IsOutsideDivision_StartUtc",
                table: "AtcSessions");

            migrationBuilder.DropColumn(
                name: "IsOutsideDivision",
                table: "AtcSessions");
        }
    }
}
