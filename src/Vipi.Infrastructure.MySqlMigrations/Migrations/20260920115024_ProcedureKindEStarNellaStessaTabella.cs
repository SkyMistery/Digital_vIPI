using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <summary>
    /// Gemella MySQL di <c>ProcedureKindEStarNellaStessaTabella</c>: <c>AirportSids</c> diventa
    /// <c>AirportProcedures</c> e prende la colonna <c>Kind</c> (<c>Sid</c>/<c>Star</c>).
    ///
    /// <para>🔴 <b>Corpo scritto a mano.</b> Lo scaffolding proponeva <c>DropTable</c> + <c>CreateTable</c>:
    /// in produzione sono le ~1470 righe SID — priorità, pubblicazioni forzate, correzioni del punto —
    /// <b>buttate via</b>. Qui si rinomina e si aggiunge una colonna.</para>
    ///
    /// <para>Anche il nome dell'indice e quello della chiave esterna si rinominano: un nome che cita una
    /// tabella che non esiste più fa fallire la prima migrazione futura che prova a lasciarli cadere.</para>
    /// </summary>
    public partial class ProcedureKindEStarNellaStessaTabella : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AirportSids_Airports_AirportId",
                table: "AirportSids");

            migrationBuilder.DropIndex(
                name: "IX_AirportSids_AirportId_Order",
                table: "AirportSids");

            migrationBuilder.RenameTable(
                name: "AirportSids",
                newName: "AirportProcedures");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "AirportProcedures",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Sid",
                collation: "utf8mb4_uca1400_as_cs");

            migrationBuilder.CreateIndex(
                name: "IX_AirportProcedures_AirportId_Order",
                table: "AirportProcedures",
                columns: new[] { "AirportId", "Order" });

            migrationBuilder.AddForeignKey(
                name: "FK_AirportProcedures_Airports_AirportId",
                table: "AirportProcedures",
                column: "AirportId",
                principalTable: "Airports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠️ Tornando indietro le STAR non hanno più un posto dove stare: si cancellano PRIMA del rename,
            // o resterebbero nella tabella delle SID come righe senza verso.
            migrationBuilder.Sql("DELETE FROM `AirportProcedures` WHERE `Kind` = 'Star';");

            migrationBuilder.DropForeignKey(
                name: "FK_AirportProcedures_Airports_AirportId",
                table: "AirportProcedures");

            migrationBuilder.DropIndex(
                name: "IX_AirportProcedures_AirportId_Order",
                table: "AirportProcedures");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "AirportProcedures");

            migrationBuilder.RenameTable(
                name: "AirportProcedures",
                newName: "AirportSids");

            migrationBuilder.CreateIndex(
                name: "IX_AirportSids_AirportId_Order",
                table: "AirportSids",
                columns: new[] { "AirportId", "Order" });

            migrationBuilder.AddForeignKey(
                name: "FK_AirportSids_Airports_AirportId",
                table: "AirportSids",
                column: "AirportId",
                principalTable: "Airports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
