using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Faccetta trasferimento (controllo + comunicazioni), velocità e gruppo di varianti su TransferPoints.
    /// Tutto additivo: i default riproducono il comportamento storico (HandoffKind = Unspecified ⇒ il
    /// trasferimento coincide con l'ingresso), quindi nessun backfill.
    /// <para>I default degli enum ("Unspecified"/"Fl"/"AtOrAbove") NON sono un caso: arrivano da
    /// <c>HasDefaultValue</c> dichiarato in <c>VipiDbContext</c>. Questi enum vivono su colonna testuale, e senza
    /// default dichiarato lo scaffolding scriverebbe <c>""</c> — un valore che l'enum non sa rileggere. Dichiararlo
    /// nel modello copre anche il PostgresSchemaReconciler del deploy Render, che ha lo stesso problema.</para>
    /// </remarks>
    public partial class AddTransferHandoffSpeedAndVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommsHandoffKind",
                table: "TransferPoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "Unspecified");

            migrationBuilder.AddColumn<string>(
                name: "CommsHandoffLabel",
                table: "TransferPoints",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HandoffKind",
                table: "TransferPoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "Unspecified");

            migrationBuilder.AddColumn<string>(
                name: "HandoffLabel",
                table: "TransferPoints",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HandoffLevelConstraint",
                table: "TransferPoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "AtOrAbove");

            migrationBuilder.AddColumn<string>(
                name: "HandoffLevelUnit",
                table: "TransferPoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "Fl");

            migrationBuilder.AddColumn<int>(
                name: "HandoffLevelValue",
                table: "TransferPoints",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOtherwise",
                table: "TransferPoints",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SpeedConstraint",
                table: "TransferPoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "Unspecified");

            migrationBuilder.AddColumn<int>(
                name: "SpeedValue",
                table: "TransferPoints",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VariantGroup",
                table: "TransferPoints",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferPoints_FlowId_VariantGroup",
                table: "TransferPoints",
                columns: new[] { "FlowId", "VariantGroup" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransferPoints_FlowId_VariantGroup",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "CommsHandoffKind",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "CommsHandoffLabel",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "HandoffKind",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "HandoffLabel",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "HandoffLevelConstraint",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "HandoffLevelUnit",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "HandoffLevelValue",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "IsOtherwise",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "SpeedConstraint",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "SpeedValue",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "VariantGroup",
                table: "TransferPoints");
        }
    }
}
