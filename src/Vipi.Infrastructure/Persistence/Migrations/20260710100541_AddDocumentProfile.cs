using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    HiddenAorSectorsJson = table.Column<string>(type: "TEXT", nullable: true),
                    HiddenFrequenciesJson = table.Column<string>(type: "TEXT", nullable: true),
                    HiddenSectionsJson = table.Column<string>(type: "TEXT", nullable: true),
                    FreqOrderJson = table.Column<string>(type: "TEXT", nullable: true),
                    FreqLinksJson = table.Column<string>(type: "TEXT", nullable: true),
                    CoordinationSentenceTemplate = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentProfiles_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentProfiles_DocumentId",
                table: "DocumentProfiles",
                column: "DocumentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentProfiles");
        }
    }
}
