using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Vid = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", nullable: false),
                    EntityId = table.Column<string>(type: "TEXT", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DetailsJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoordinationPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ident = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    AiracCycle = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoordinationPoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Firs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CountryPrefix = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Firs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NavReferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Ident = table.Column<string>(type: "TEXT", nullable: false),
                    AiracCycle = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NavReferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SectorGeometries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Format = table.Column<string>(type: "TEXT", nullable: false),
                    Data = table.Column<string>(type: "TEXT", nullable: false),
                    SourceCallsign = table.Column<string>(type: "TEXT", nullable: true),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectorGeometries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SharedBlocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Format = table.Column<string>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: true),
                    BodyJson = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedBlocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StaffMembers",
                columns: table => new
                {
                    Vid = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    AtcRating = table.Column<string>(type: "TEXT", nullable: true),
                    StaffPositionsCsv = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    FirstSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLoginUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastVerifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffMembers", x => x.Vid);
                });

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

            migrationBuilder.CreateTable(
                name: "Transfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirId = table.Column<int>(type: "INTEGER", nullable: false),
                    RelationKey = table.Column<string>(type: "TEXT", nullable: false),
                    RelationLabel = table.Column<string>(type: "TEXT", nullable: false),
                    Phase = table.Column<string>(type: "TEXT", nullable: false),
                    AirportIcao = table.Column<string>(type: "TEXT", nullable: false),
                    Cop = table.Column<string>(type: "TEXT", nullable: false),
                    FlRule = table.Column<string>(type: "TEXT", nullable: false),
                    HandlerChainJson = table.Column<string>(type: "TEXT", nullable: false),
                    StandardFallback = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transfers_Firs_FirId",
                        column: x => x.FirId,
                        principalTable: "Firs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UnificationRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    ConditionJson = table.Column<string>(type: "TEXT", nullable: false),
                    AssignmentJson = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnificationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnificationRules_Firs_FirId",
                        column: x => x.FirId,
                        principalTable: "Firs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentBlocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentVersionId = table.Column<int>(type: "INTEGER", nullable: false),
                    SectionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Tier = table.Column<string>(type: "TEXT", nullable: false),
                    Format = table.Column<string>(type: "TEXT", nullable: false),
                    Visibility = table.Column<string>(type: "TEXT", nullable: false),
                    CollapsedByDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    CalloutKind = table.Column<string>(type: "TEXT", nullable: true),
                    ScopeSectorId = table.Column<int>(type: "INTEGER", nullable: true),
                    FromSectorId = table.Column<int>(type: "INTEGER", nullable: true),
                    ToSectorId = table.Column<int>(type: "INTEGER", nullable: true),
                    SharedBlockId = table.Column<int>(type: "INTEGER", nullable: true),
                    Body = table.Column<string>(type: "TEXT", nullable: true),
                    BodyJson = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentBlocks_SharedBlocks_SharedBlockId",
                        column: x => x.SharedBlockId,
                        principalTable: "SharedBlocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentParties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    SectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentParties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Language = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CurrentVersionId = table.Column<int>(type: "INTEGER", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdatedAiracCycle = table.Column<string>(type: "TEXT", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true),
                    LockedByVid = table.Column<int>(type: "INTEGER", nullable: true),
                    LockedByName = table.Column<string>(type: "TEXT", nullable: true),
                    LockedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LockExpiresUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedByVid = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AiracCycle = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentVersions_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sectors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Callsign = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    FirId = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    ApproachKind = table.Column<string>(type: "TEXT", nullable: true),
                    AirportIcao = table.Column<string>(type: "TEXT", nullable: true),
                    FacilityId = table.Column<int>(type: "INTEGER", nullable: true),
                    DefaultFrequency = table.Column<string>(type: "TEXT", nullable: true),
                    CoverageOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    ParentSectorId = table.Column<int>(type: "INTEGER", nullable: true),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    GeometryId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sectors_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Sectors_Firs_FirId",
                        column: x => x.FirId,
                        principalTable: "Firs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sectors_SectorGeometries_GeometryId",
                        column: x => x.GeometryId,
                        principalTable: "SectorGeometries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Sectors_Sectors_ParentSectorId",
                        column: x => x.ParentSectorId,
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentVersionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentSectionId = table.Column<int>(type: "INTEGER", nullable: true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Depth = table.Column<int>(type: "INTEGER", nullable: false),
                    SectionKind = table.Column<string>(type: "TEXT", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentSections_DocumentSections_ParentSectionId",
                        column: x => x.ParentSectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentSections_DocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Frequencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    Callsign = table.Column<string>(type: "TEXT", nullable: false),
                    FrequencyMhz = table.Column<string>(type: "TEXT", nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Frequencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Frequencies_Sectors_SectorId",
                        column: x => x.SectorId,
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VectoringMinimaSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScopeSectorId = table.Column<int>(type: "INTEGER", nullable: true),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    SourceAiracCycle = table.Column<string>(type: "TEXT", nullable: false),
                    SourceCommit = table.Column<string>(type: "TEXT", nullable: true),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VectoringMinimaSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VectoringMinimaSets_Sectors_ScopeSectorId",
                        column: x => x.ScopeSectorId,
                        principalTable: "Sectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VectoringMinimaRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SetId = table.Column<int>(type: "INTEGER", nullable: false),
                    AreaName = table.Column<string>(type: "TEXT", nullable: false),
                    MinimaFt = table.Column<int>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VectoringMinimaRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VectoringMinimaRows_VectoringMinimaSets_SetId",
                        column: x => x.SetId,
                        principalTable: "VectoringMinimaSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentBlocks_DocumentVersionId_SectionId_Order",
                table: "ContentBlocks",
                columns: new[] { "DocumentVersionId", "SectionId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentBlocks_FromSectorId",
                table: "ContentBlocks",
                column: "FromSectorId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentBlocks_ScopeSectorId",
                table: "ContentBlocks",
                column: "ScopeSectorId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentBlocks_SectionId",
                table: "ContentBlocks",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentBlocks_SharedBlockId",
                table: "ContentBlocks",
                column: "SharedBlockId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentBlocks_ToSectorId",
                table: "ContentBlocks",
                column: "ToSectorId");

            migrationBuilder.CreateIndex(
                name: "IX_CoordinationPoints_Ident",
                table: "CoordinationPoints",
                column: "Ident");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentParties_DocumentId",
                table: "DocumentParties",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentParties_SectorId",
                table: "DocumentParties",
                column: "SectorId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CurrentVersionId",
                table: "Documents",
                column: "CurrentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Type_Status",
                table: "Documents",
                columns: new[] { "Type", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSections_DocumentVersionId_ParentSectionId_Order",
                table: "DocumentSections",
                columns: new[] { "DocumentVersionId", "ParentSectionId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSections_ParentSectionId",
                table: "DocumentSections",
                column: "ParentSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_DocumentId_VersionNumber",
                table: "DocumentVersions",
                columns: new[] { "DocumentId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EditGrants_FirId",
                table: "EditGrants",
                column: "FirId");

            migrationBuilder.CreateIndex(
                name: "IX_EditGrants_Vid_FirId",
                table: "EditGrants",
                columns: new[] { "Vid", "FirId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Firs_Code",
                table: "Firs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Frequencies_SectorId",
                table: "Frequencies",
                column: "SectorId");

            migrationBuilder.CreateIndex(
                name: "IX_NavReferences_Type_Ident_AiracCycle",
                table: "NavReferences",
                columns: new[] { "Type", "Ident", "AiracCycle" });

            migrationBuilder.CreateIndex(
                name: "IX_Sectors_Callsign",
                table: "Sectors",
                column: "Callsign",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sectors_DocumentId",
                table: "Sectors",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Sectors_FirId",
                table: "Sectors",
                column: "FirId");

            migrationBuilder.CreateIndex(
                name: "IX_Sectors_GeometryId",
                table: "Sectors",
                column: "GeometryId");

            migrationBuilder.CreateIndex(
                name: "IX_Sectors_ParentSectorId",
                table: "Sectors",
                column: "ParentSectorId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedBlocks_Key",
                table: "SharedBlocks",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_IsActive",
                table: "StaffMembers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_FirId_RelationKey_Phase_Order",
                table: "Transfers",
                columns: new[] { "FirId", "RelationKey", "Phase", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_UnificationRules_FirId_Priority",
                table: "UnificationRules",
                columns: new[] { "FirId", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_VectoringMinimaRows_SetId",
                table: "VectoringMinimaRows",
                column: "SetId");

            migrationBuilder.CreateIndex(
                name: "IX_VectoringMinimaSets_ScopeSectorId",
                table: "VectoringMinimaSets",
                column: "ScopeSectorId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContentBlocks_DocumentSections_SectionId",
                table: "ContentBlocks",
                column: "SectionId",
                principalTable: "DocumentSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentBlocks_DocumentVersions_DocumentVersionId",
                table: "ContentBlocks",
                column: "DocumentVersionId",
                principalTable: "DocumentVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentBlocks_Sectors_FromSectorId",
                table: "ContentBlocks",
                column: "FromSectorId",
                principalTable: "Sectors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentBlocks_Sectors_ScopeSectorId",
                table: "ContentBlocks",
                column: "ScopeSectorId",
                principalTable: "Sectors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentBlocks_Sectors_ToSectorId",
                table: "ContentBlocks",
                column: "ToSectorId",
                principalTable: "Sectors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentParties_Documents_DocumentId",
                table: "DocumentParties",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentParties_Sectors_SectorId",
                table: "DocumentParties",
                column: "SectorId",
                principalTable: "Sectors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_DocumentVersions_CurrentVersionId",
                table: "Documents",
                column: "CurrentVersionId",
                principalTable: "DocumentVersions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_DocumentVersions_CurrentVersionId",
                table: "Documents");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "ContentBlocks");

            migrationBuilder.DropTable(
                name: "CoordinationPoints");

            migrationBuilder.DropTable(
                name: "DocumentParties");

            migrationBuilder.DropTable(
                name: "EditGrants");

            migrationBuilder.DropTable(
                name: "Frequencies");

            migrationBuilder.DropTable(
                name: "NavReferences");

            migrationBuilder.DropTable(
                name: "StaffMembers");

            migrationBuilder.DropTable(
                name: "Transfers");

            migrationBuilder.DropTable(
                name: "UnificationRules");

            migrationBuilder.DropTable(
                name: "VectoringMinimaRows");

            migrationBuilder.DropTable(
                name: "DocumentSections");

            migrationBuilder.DropTable(
                name: "SharedBlocks");

            migrationBuilder.DropTable(
                name: "VectoringMinimaSets");

            migrationBuilder.DropTable(
                name: "Sectors");

            migrationBuilder.DropTable(
                name: "Firs");

            migrationBuilder.DropTable(
                name: "SectorGeometries");

            migrationBuilder.DropTable(
                name: "DocumentVersions");

            migrationBuilder.DropTable(
                name: "Documents");
        }
    }
}
