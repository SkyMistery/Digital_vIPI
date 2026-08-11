using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Il gruppo di varianti diventa un OUTLINE: <c>IsOtherwise</c> (un flag binario, «la riga negli altri
    /// casi») lascia il posto a <c>VariantDepth</c> (int) + <c>IsGroupWide</c> (bool). Le alternative di primo
    /// livello sono pari-grado fra loro e ognuna può avere eccezioni, che a loro volta possono averne.
    /// <para>Nessun backfill: <c>IsOtherwise</c> non è mai stato scritto da nessuno — la migrazione che
    /// l'aveva introdotto (11 agosto) non è stata applicata né alla produzione né al DB di sviluppo.</para>
    /// <para>⚠️ Lo scaffolding proponeva un <c>RenameColumn</c> da <c>IsOtherwise</c> a <c>VariantDepth</c>:
    /// tecnicamente passa, semanticamente no. Un <c>true</c> sopravvissuto in qualche copia diventerebbe
    /// profondità <b>1</b>, cioè un'eccezione di una riga qualsiasi, senza che nulla lo segnali. Drop e add
    /// sono espliciti e costano zero, visto che dati non ce ne sono.</para>
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
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsGroupWide",
                table: "TransferPoints",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // L'Order entra nell'indice perché in un outline l'ordine NON è presentazione: è la struttura (una
            // riga appartiene all'ultima meno profonda che la precede), quindi il gruppo si legge sempre ordinato.
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
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_TransferPoints_FlowId_VariantGroup",
                table: "TransferPoints",
                columns: new[] { "FlowId", "VariantGroup" });
        }
    }
}
