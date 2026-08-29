using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class RadioassistenzeFamigliaETipo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Navaids_Code_Kind",
                table: "Navaids");

            // ⚠️ Si SVUOTA: le righe di prima nascono da una classificazione sbagliata (la «natura» era il
            // nome del file, e `itvor.vor` contiene VOR, TACAN e VORTAC insieme), e `NaturalKey` è unica —
            // sulle righe esistenti nascerebbe vuota su tutte. Il giro d'import le rifà giuste in un minuto.
            migrationBuilder.Sql("DELETE FROM Navaids;");

            // ⚠️ E si azzera anche lo STATO dell'import, o la tabella resta vuota per ventiquattro ore: il
            // giro è «gestito» (GatedImportLoop) e riparte solo quando è passato il periodo dall'ultimo
            // successo. Svuotare senza questo vorrebbe dire un'anagrafica vuota per un giorno intero, con le
            // tabelle dei SOP che non trovano più le righe che citano. Lo svuotamento e il riempimento sono
            // lo stesso atto.
            migrationBuilder.Sql("DELETE FROM ImportStates WHERE Category = 'Navaid';");

            migrationBuilder.RenameColumn(
                name: "DisplayType",
                table: "Navaids",
                newName: "Type");

            migrationBuilder.AddColumn<string>(
                name: "NaturalKey",
                table: "Navaids",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                collation: "utf8mb4_uca1400_as_cs")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Navaids_NaturalKey",
                table: "Navaids",
                column: "NaturalKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Navaids_NaturalKey",
                table: "Navaids");

            migrationBuilder.DropColumn(
                name: "NaturalKey",
                table: "Navaids");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "Navaids",
                newName: "DisplayType");

            migrationBuilder.CreateIndex(
                name: "IX_Navaids_Code_Kind",
                table: "Navaids",
                columns: new[] { "Code", "Kind" },
                unique: true);
        }
    }
}
