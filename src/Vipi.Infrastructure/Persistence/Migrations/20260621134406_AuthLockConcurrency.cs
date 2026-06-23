using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuthLockConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DocumentSections",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockExpiresUtc",
                table: "Documents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedAtUtc",
                table: "Documents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockedByName",
                table: "Documents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LockedByVid",
                table: "Documents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ContentBlocks",
                type: "BLOB",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EditGrants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Vid = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    FirId = table.Column<int>(type: "INTEGER", nullable: false),
                    GrantedByVid = table.Column<int>(type: "INTEGER", nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EditGrants_Firs_FirId",
                        column: x => x.FirId,
                        principalTable: "Firs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EditGrants_FirId",
                table: "EditGrants",
                column: "FirId");

            migrationBuilder.CreateIndex(
                name: "IX_EditGrants_Vid_FirId",
                table: "EditGrants",
                columns: new[] { "Vid", "FirId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EditGrants");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DocumentSections");

            migrationBuilder.DropColumn(
                name: "LockExpiresUtc",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "LockedAtUtc",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "LockedByName",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "LockedByVid",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ContentBlocks");
        }
    }
}
