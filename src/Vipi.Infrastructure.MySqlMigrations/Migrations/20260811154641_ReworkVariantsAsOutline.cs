using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Gemella MySQL di <c>ReworkVariantsAsOutline</c>: il gruppo di varianti diventa un outline
    /// (<c>VariantDepth</c> + <c>IsGroupWide</c> al posto di <c>IsOtherwise</c>). Nessun backfill: la colonna
    /// non è mai stata scritta da nessuno.
    /// <para>⚠️ Anche qui lo scaffolding proponeva un <c>RenameColumn</c>, e l'ha proposto <b>diverso</b> da
    /// quello scelto per SQLite: là <c>IsOtherwise → VariantDepth</c>, qui <c>IsOtherwise → IsGroupWide</c>.
    /// Due provider, due inferenze diverse dalla stessa modifica — che è la prova che il rename è una
    /// supposizione sui tipi, non un'intenzione letta dal modello. Un <c>true</c> sopravvissuto diventerebbe
    /// «riga che scavalca le alternative» senza che nulla lo segnali. Drop e add sono espliciti.</para>
    /// </remarks>
    public partial class ReworkVariantsAsOutline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransferPoints_FlowId_VariantGroup",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "IsOtherwise",
                table: "TransferPoints");

            migrationBuilder.AddColumn<int>(
                name: "VariantDepth",
                table: "TransferPoints",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsGroupWide",
                table: "TransferPoints",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // L'Order entra nell'indice perché in un outline l'ordine è la struttura, non la presentazione.
            migrationBuilder.CreateIndex(
                name: "IX_TransferPoints_FlowId_VariantGroup_Order",
                table: "TransferPoints",
                columns: new[] { "FlowId", "VariantGroup", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransferPoints_FlowId_VariantGroup_Order",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "IsGroupWide",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "VariantDepth",
                table: "TransferPoints");

            migrationBuilder.AddColumn<bool>(
                name: "IsOtherwise",
                table: "TransferPoints",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_TransferPoints_FlowId_VariantGroup",
                table: "TransferPoints",
                columns: new[] { "FlowId", "VariantGroup" });
        }
    }
}
