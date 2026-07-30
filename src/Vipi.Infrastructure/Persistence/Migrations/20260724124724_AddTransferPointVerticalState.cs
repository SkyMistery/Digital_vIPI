using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferPointVerticalState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VerticalState",
                table: "TransferPoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "Unspecified");

            // Backfill: prima la parola «in discesa/salita/stabile» derivava dal vincolo di livello. Ora lo stato
            // verticale è un campo indipendente: mappa una-tantum le righe esistenti così le frasi restano identiche.
            // (Special → Unspecified, già dal default: il livello «per aerovia» non aveva parola di stato.)
            migrationBuilder.Sql("UPDATE TransferPoints SET VerticalState = 'Descending' WHERE LevelConstraint = 'AtOrBelow';");
            migrationBuilder.Sql("UPDATE TransferPoints SET VerticalState = 'Climbing' WHERE LevelConstraint = 'AtOrAbove';");
            migrationBuilder.Sql("UPDATE TransferPoints SET VerticalState = 'Level' WHERE LevelConstraint = 'Exact';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VerticalState",
                table: "TransferPoints");
        }
    }
}
