using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyTransferResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Fallback",
                table: "TransferPoints");

            migrationBuilder.DropColumn(
                name: "ManualChainJson",
                table: "TransferPoints");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Fallback",
                table: "TransferPoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ManualChainJson",
                table: "TransferPoints",
                type: "TEXT",
                nullable: true);
        }
    }
}
