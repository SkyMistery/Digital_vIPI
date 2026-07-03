using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccProfileRoot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccProfiles_AccId",
                table: "AccProfiles");

            migrationBuilder.AddColumn<string>(
                name: "RootCallsign",
                table: "AccProfiles",
                type: "TEXT",
                nullable: true);

            // Backfill: assegna ogni profilo legacy alla radice CTR primaria del suo ACC
            // (CTR senza genitore, attivo; ordine CoverageOrder poi callsign). Gli altri alberi restano vuoti.
            migrationBuilder.Sql(@"
                UPDATE AccProfiles
                SET RootCallsign = (
                    SELECT s.Callsign FROM Sectors s
                    WHERE s.AccId = AccProfiles.AccId
                      AND s.Type = 'Ctr' AND s.ParentSectorId IS NULL AND s.IsActive = 1
                    ORDER BY s.CoverageOrder, s.Callsign
                    LIMIT 1
                )
                WHERE RootCallsign IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_AccProfiles_AccId_RootCallsign",
                table: "AccProfiles",
                columns: new[] { "AccId", "RootCallsign" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccProfiles_AccId_RootCallsign",
                table: "AccProfiles");

            migrationBuilder.DropColumn(
                name: "RootCallsign",
                table: "AccProfiles");

            migrationBuilder.CreateIndex(
                name: "IX_AccProfiles_AccId",
                table: "AccProfiles",
                column: "AccId",
                unique: true);
        }
    }
}
