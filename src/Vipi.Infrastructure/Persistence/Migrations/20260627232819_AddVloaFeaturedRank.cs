using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVloaFeaturedRank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FeaturedRank",
                table: "Documents",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeaturedRank",
                table: "Documents");
        }
    }
}
