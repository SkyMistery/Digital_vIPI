using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameFirToAcc : Migration
    {
        // Rename non distruttivo: la tabella "Firs" diventa "Accs" e le colonne FK "FirId" → "AccId".
        // Nessun Drop/Create della tabella → le righe ACC esistenti vengono preservate.
        // Aggiunge IsMilitary/IsHidden/ImportedAtUtc (default false/null = ACC attivo).

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Airports_Firs_FirId", table: "Airports");
            migrationBuilder.DropForeignKey(name: "FK_EditGrants_Firs_FirId", table: "EditGrants");
            migrationBuilder.DropForeignKey(name: "FK_Sectors_Firs_FirId", table: "Sectors");
            migrationBuilder.DropForeignKey(name: "FK_Transfers_Firs_FirId", table: "Transfers");
            migrationBuilder.DropForeignKey(name: "FK_UnificationRules_Firs_FirId", table: "UnificationRules");

            // --- Tabella principale: rename + nuove colonne ---
            migrationBuilder.RenameTable(name: "Firs", newName: "Accs");
            migrationBuilder.RenameIndex(name: "IX_Firs_Code", table: "Accs", newName: "IX_Accs_Code");

            migrationBuilder.AddColumn<bool>(
                name: "IsMilitary", table: "Accs", type: "INTEGER", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(
                name: "IsHidden", table: "Accs", type: "INTEGER", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<DateTime>(
                name: "ImportedAtUtc", table: "Accs", type: "TEXT", nullable: true);

            // --- Colonne FK figlie: FirId → AccId (+ indici) ---
            migrationBuilder.RenameColumn(name: "FirId", table: "UnificationRules", newName: "AccId");
            migrationBuilder.RenameIndex(name: "IX_UnificationRules_FirId_Priority", table: "UnificationRules", newName: "IX_UnificationRules_AccId_Priority");

            migrationBuilder.RenameColumn(name: "FirId", table: "Transfers", newName: "AccId");
            migrationBuilder.RenameIndex(name: "IX_Transfers_FirId_RelationKey_Phase_Order", table: "Transfers", newName: "IX_Transfers_AccId_RelationKey_Phase_Order");

            migrationBuilder.RenameColumn(name: "FirId", table: "Sectors", newName: "AccId");
            migrationBuilder.RenameIndex(name: "IX_Sectors_FirId", table: "Sectors", newName: "IX_Sectors_AccId");

            migrationBuilder.RenameColumn(name: "FirId", table: "EditGrants", newName: "AccId");
            migrationBuilder.RenameIndex(name: "IX_EditGrants_UserId_FirId", table: "EditGrants", newName: "IX_EditGrants_UserId_AccId");
            migrationBuilder.RenameIndex(name: "IX_EditGrants_FirId", table: "EditGrants", newName: "IX_EditGrants_AccId");

            migrationBuilder.RenameColumn(name: "FirId", table: "Airports", newName: "AccId");
            migrationBuilder.RenameIndex(name: "IX_Airports_FirId", table: "Airports", newName: "IX_Airports_AccId");

            // --- FK ricreate verso "Accs" ---
            migrationBuilder.AddForeignKey(name: "FK_Airports_Accs_AccId", table: "Airports", column: "AccId",
                principalTable: "Accs", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_EditGrants_Accs_AccId", table: "EditGrants", column: "AccId",
                principalTable: "Accs", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(name: "FK_Sectors_Accs_AccId", table: "Sectors", column: "AccId",
                principalTable: "Accs", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_Transfers_Accs_AccId", table: "Transfers", column: "AccId",
                principalTable: "Accs", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(name: "FK_UnificationRules_Accs_AccId", table: "UnificationRules", column: "AccId",
                principalTable: "Accs", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Airports_Accs_AccId", table: "Airports");
            migrationBuilder.DropForeignKey(name: "FK_EditGrants_Accs_AccId", table: "EditGrants");
            migrationBuilder.DropForeignKey(name: "FK_Sectors_Accs_AccId", table: "Sectors");
            migrationBuilder.DropForeignKey(name: "FK_Transfers_Accs_AccId", table: "Transfers");
            migrationBuilder.DropForeignKey(name: "FK_UnificationRules_Accs_AccId", table: "UnificationRules");

            migrationBuilder.RenameColumn(name: "AccId", table: "UnificationRules", newName: "FirId");
            migrationBuilder.RenameIndex(name: "IX_UnificationRules_AccId_Priority", table: "UnificationRules", newName: "IX_UnificationRules_FirId_Priority");

            migrationBuilder.RenameColumn(name: "AccId", table: "Transfers", newName: "FirId");
            migrationBuilder.RenameIndex(name: "IX_Transfers_AccId_RelationKey_Phase_Order", table: "Transfers", newName: "IX_Transfers_FirId_RelationKey_Phase_Order");

            migrationBuilder.RenameColumn(name: "AccId", table: "Sectors", newName: "FirId");
            migrationBuilder.RenameIndex(name: "IX_Sectors_AccId", table: "Sectors", newName: "IX_Sectors_FirId");

            migrationBuilder.RenameColumn(name: "AccId", table: "EditGrants", newName: "FirId");
            migrationBuilder.RenameIndex(name: "IX_EditGrants_UserId_AccId", table: "EditGrants", newName: "IX_EditGrants_UserId_FirId");
            migrationBuilder.RenameIndex(name: "IX_EditGrants_AccId", table: "EditGrants", newName: "IX_EditGrants_FirId");

            migrationBuilder.RenameColumn(name: "AccId", table: "Airports", newName: "FirId");
            migrationBuilder.RenameIndex(name: "IX_Airports_AccId", table: "Airports", newName: "IX_Airports_FirId");

            migrationBuilder.DropColumn(name: "IsMilitary", table: "Accs");
            migrationBuilder.DropColumn(name: "IsHidden", table: "Accs");
            migrationBuilder.DropColumn(name: "ImportedAtUtc", table: "Accs");
            migrationBuilder.RenameIndex(name: "IX_Accs_Code", table: "Accs", newName: "IX_Firs_Code");
            migrationBuilder.RenameTable(name: "Accs", newName: "Firs");

            migrationBuilder.AddForeignKey(name: "FK_Airports_Firs_FirId", table: "Airports", column: "FirId",
                principalTable: "Firs", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_EditGrants_Firs_FirId", table: "EditGrants", column: "FirId",
                principalTable: "Firs", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(name: "FK_Sectors_Firs_FirId", table: "Sectors", column: "FirId",
                principalTable: "Firs", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_Transfers_Firs_FirId", table: "Transfers", column: "FirId",
                principalTable: "Firs", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(name: "FK_UnificationRules_Firs_FirId", table: "UnificationRules", column: "FirId",
                principalTable: "Firs", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
        }
    }
}
