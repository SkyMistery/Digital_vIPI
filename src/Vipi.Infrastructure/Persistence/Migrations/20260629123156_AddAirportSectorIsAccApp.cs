using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAirportSectorIsAccApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAccApp",
                table: "AirportSectors",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Backfill: un APP/DEP a 3+ pezzi (es. LIRN_UN0_APP) è "di ACC"; a 2 pezzi (LIRP_APP) no.
            // Conta i separatori '_' nel callsign: >=2 underscore ⇒ >=3 pezzi.
            // Eccezione: lettera di mezzo 'G' (es. LIRN_G_APP = precision/PAR militare) = NON di ACC.
            migrationBuilder.Sql(@"
                UPDATE AirportSectors
                SET IsAccApp = 1
                WHERE upper(trim(Position)) IN ('APP','DEP')
                  AND (length(ComposePosition) - length(replace(ComposePosition,'_',''))) >= 2
                  AND upper(trim(coalesce(MiddleIdentifier,''))) <> 'G';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAccApp",
                table: "AirportSectors");
        }
    }
}
