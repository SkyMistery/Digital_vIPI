using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitTransferConditionColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Aggiungi la nuova colonna PRIMA del backfill (deve esistere per l'UPDATE).
            migrationBuilder.AddColumn<string>(
                name: "ConditionCustomLabel",
                table: "TransferPoints",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            // 2) Migra il modello a-kind-singolo → tre colonne indipendenti, finché ConditionKind esiste ancora:
            //    Custom → ConditionCustomLabel; Area → ConditionAreaLabel; entrambe azzerano ConditionLabel/RefId (erano piste).
            //    Runway/None restano invariate (ConditionLabel = piste; l'eventuale area overlay è già in ConditionAreaLabel).
            migrationBuilder.Sql(
                "UPDATE TransferPoints SET ConditionCustomLabel = ConditionLabel, ConditionLabel = NULL, ConditionRefId = NULL WHERE ConditionKind = 'Custom';");
            migrationBuilder.Sql(
                "UPDATE TransferPoints SET ConditionAreaLabel = ConditionLabel, ConditionLabel = NULL, ConditionRefId = NULL WHERE ConditionKind = 'Area';");

            // 3) Elimina la colonna kind, ora priva di significato.
            migrationBuilder.DropColumn(
                name: "ConditionKind",
                table: "TransferPoints");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConditionKind",
                table: "TransferPoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "None");

            // Ricostruisce un kind ragionevole dalle tre colonne (una sola dimensione per riga nel modello vecchio):
            // pista → Runway; altrimenti area → Area; altrimenti personalizzata → Custom; altrimenti None.
            migrationBuilder.Sql("UPDATE TransferPoints SET ConditionKind = 'Custom' WHERE ConditionCustomLabel IS NOT NULL;");
            migrationBuilder.Sql("UPDATE TransferPoints SET ConditionKind = 'Area', ConditionLabel = ConditionAreaLabel WHERE ConditionAreaLabel IS NOT NULL AND ConditionLabel IS NULL;");
            migrationBuilder.Sql("UPDATE TransferPoints SET ConditionKind = 'Runway' WHERE ConditionLabel IS NOT NULL;");
            migrationBuilder.Sql("UPDATE TransferPoints SET ConditionCustomLabel = NULL WHERE ConditionKind <> 'Custom';");

            migrationBuilder.DropColumn(
                name: "ConditionCustomLabel",
                table: "TransferPoints");
        }
    }
}
