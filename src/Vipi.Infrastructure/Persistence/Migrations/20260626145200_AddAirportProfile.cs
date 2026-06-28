using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAirportProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AtisFrequency",
                table: "Airports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransitionAltitudeFt",
                table: "Airports",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AirportFrequencyLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AirportId = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceFrequencyId = table.Column<int>(type: "INTEGER", nullable: false),
                    LabelOverride = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirportFrequencyLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AirportFrequencyLinks_Airports_AirportId",
                        column: x => x.AirportId,
                        principalTable: "Airports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AirportFrequencyLinks_Frequencies_SourceFrequencyId",
                        column: x => x.SourceFrequencyId,
                        principalTable: "Frequencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AirportRunwayRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AirportId = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    WindDirFrom = table.Column<int>(type: "INTEGER", nullable: true),
                    WindDirTo = table.Column<int>(type: "INTEGER", nullable: true),
                    WindSpeedMin = table.Column<int>(type: "INTEGER", nullable: true),
                    WindSpeedMax = table.Column<int>(type: "INTEGER", nullable: true),
                    Rain = table.Column<bool>(type: "INTEGER", nullable: true),
                    Snow = table.Column<bool>(type: "INTEGER", nullable: true),
                    DepRunways = table.Column<string>(type: "TEXT", nullable: false),
                    ArrRunways = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirportRunwayRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AirportRunwayRules_Airports_AirportId",
                        column: x => x.AirportId,
                        principalTable: "Airports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AirportRunways",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AirportId = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Ident = table.Column<string>(type: "TEXT", nullable: false),
                    LengthM = table.Column<int>(type: "INTEGER", nullable: true),
                    Bearing = table.Column<int>(type: "INTEGER", nullable: true),
                    ToraM = table.Column<string>(type: "TEXT", nullable: true),
                    LdaM = table.Column<string>(type: "TEXT", nullable: true),
                    AppProcedures = table.Column<string>(type: "TEXT", nullable: true),
                    Patterns = table.Column<string>(type: "TEXT", nullable: true),
                    Circling = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirportRunways", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AirportRunways_Airports_AirportId",
                        column: x => x.AirportId,
                        principalTable: "Airports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AirportSids",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AirportId = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Runway = table.Column<string>(type: "TEXT", nullable: true),
                    Fix = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Transition = table.Column<string>(type: "TEXT", nullable: true),
                    InitialClimb = table.Column<string>(type: "TEXT", nullable: true),
                    Type = table.Column<string>(type: "TEXT", nullable: true),
                    Cat = table.Column<string>(type: "TEXT", nullable: true),
                    Wtc = table.Column<string>(type: "TEXT", nullable: true),
                    Condition = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirportSids", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AirportSids_Airports_AirportId",
                        column: x => x.AirportId,
                        principalTable: "Airports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AirportTransitionLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AirportId = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    QnhFrom = table.Column<int>(type: "INTEGER", nullable: true),
                    QnhTo = table.Column<int>(type: "INTEGER", nullable: true),
                    Level = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirportTransitionLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AirportTransitionLevels_Airports_AirportId",
                        column: x => x.AirportId,
                        principalTable: "Airports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AirportFrequencyLinks_AirportId_Order",
                table: "AirportFrequencyLinks",
                columns: new[] { "AirportId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_AirportFrequencyLinks_SourceFrequencyId",
                table: "AirportFrequencyLinks",
                column: "SourceFrequencyId");

            migrationBuilder.CreateIndex(
                name: "IX_AirportRunwayRules_AirportId_Order",
                table: "AirportRunwayRules",
                columns: new[] { "AirportId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_AirportRunways_AirportId_Order",
                table: "AirportRunways",
                columns: new[] { "AirportId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_AirportSids_AirportId_Order",
                table: "AirportSids",
                columns: new[] { "AirportId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_AirportTransitionLevels_AirportId_Order",
                table: "AirportTransitionLevels",
                columns: new[] { "AirportId", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AirportFrequencyLinks");

            migrationBuilder.DropTable(
                name: "AirportRunwayRules");

            migrationBuilder.DropTable(
                name: "AirportRunways");

            migrationBuilder.DropTable(
                name: "AirportSids");

            migrationBuilder.DropTable(
                name: "AirportTransitionLevels");

            migrationBuilder.DropColumn(
                name: "AtisFrequency",
                table: "Airports");

            migrationBuilder.DropColumn(
                name: "TransitionAltitudeFt",
                table: "Airports");
        }
    }
}
