using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixSingleLetterAppNotRemotized : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix dato esistente: un APP/DEP a 3+ pezzi il cui pezzo di MEZZO è mono-carattere (es. LIPE_W_APP,
            // LIPE_E_APP, LIRN_G_APP) NON è remotizzato. La vecchia regola lo marcava di ACC per errore.
            // Estrazione del mezzo in SQLite: rest = substr dopo il primo '_'; middle = substr di rest fino al '_' successivo.
            migrationBuilder.Sql(@"
                UPDATE AirportSectors
                SET IsAccApp = 0
                WHERE IsAccApp = 1
                  AND upper(trim(Position)) IN ('APP','DEP')
                  AND (length(ComposePosition) - length(replace(ComposePosition,'_',''))) >= 2
                  AND length(
                        substr(
                          substr(ComposePosition, instr(ComposePosition,'_')+1),
                          1,
                          instr(substr(ComposePosition, instr(ComposePosition,'_')+1), '_') - 1
                        )
                      ) <= 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
