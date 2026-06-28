using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Rename_Vid_To_UserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Vid",
                table: "StaffMembers",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "Vid",
                table: "EditGrants",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "GrantedByVid",
                table: "EditGrants",
                newName: "GrantedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_EditGrants_Vid_FirId",
                table: "EditGrants",
                newName: "IX_EditGrants_UserId_FirId");

            migrationBuilder.RenameColumn(
                name: "CreatedByVid",
                table: "DocumentVersions",
                newName: "CreatedByUserId");

            migrationBuilder.RenameColumn(
                name: "LockedByVid",
                table: "Documents",
                newName: "LockedByUserId");

            migrationBuilder.RenameColumn(
                name: "Vid",
                table: "AuditLogs",
                newName: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "StaffMembers",
                newName: "Vid");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "EditGrants",
                newName: "Vid");

            migrationBuilder.RenameColumn(
                name: "GrantedByUserId",
                table: "EditGrants",
                newName: "GrantedByVid");

            migrationBuilder.RenameIndex(
                name: "IX_EditGrants_UserId_FirId",
                table: "EditGrants",
                newName: "IX_EditGrants_Vid_FirId");

            migrationBuilder.RenameColumn(
                name: "CreatedByUserId",
                table: "DocumentVersions",
                newName: "CreatedByVid");

            migrationBuilder.RenameColumn(
                name: "LockedByUserId",
                table: "Documents",
                newName: "LockedByVid");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "AuditLogs",
                newName: "Vid");
        }
    }
}
