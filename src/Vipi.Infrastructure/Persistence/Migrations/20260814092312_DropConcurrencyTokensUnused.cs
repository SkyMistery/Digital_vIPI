using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Via le quattro colonne <c>RowVersion</c> che dichiaravano un token di concorrenza mai valorizzato da
    /// nessun percorso di scrittura: sempre <c>NULL</c>, quindi <c>WHERE … AND RowVersion IS NULL</c> sempre
    /// vera e il secondo editor che salvava sovrascriveva il primo in silenzio. Per <c>UnificationRule</c>,
    /// <c>TransferFlow</c>, <c>SharedBlock</c> e <c>DocumentProfile</c> il last-write-wins è stato confermato
    /// come voluto (sono modificate da un editor alla volta, sotto lock), quindi la difesa nominale sparisce
    /// invece di essere resa vera. Audit del 14 agosto 2026 §A1.
    ///
    /// <para>Le tre entità che il token lo tengono — <c>Document</c>, <c>DocumentSection</c>,
    /// <c>ContentBlock</c> — ora se lo vedono ruotare da <c>VipiDbContext.SaveChangesAsync</c>, e non più da
    /// un singolo metodo di repository.</para>
    ///
    /// <para>Gemella MySQL: <c>20260814092329_EnumLengthsAndDropUnusedTokens</c>, che porta anche le
    /// lunghezze degli enum — modifica che su SQLite non ha effetto e infatti qui non compare.</para>
    /// </summary>
    public partial class DropConcurrencyTokensUnused : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "UnificationRules");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "TransferFlows");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SharedBlocks");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DocumentProfiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "UnificationRules",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "TransferFlows",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SharedBlocks",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DocumentProfiles",
                type: "BLOB",
                nullable: true);
        }
    }
}
