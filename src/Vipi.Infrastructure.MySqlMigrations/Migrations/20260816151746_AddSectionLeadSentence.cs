using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddSectionLeadSentence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        // ⚠️ `defaultValue: false` è il valore GIUSTO qui, e vale la pena dirlo perche' su questo progetto la
        // stessa riga è già stata una trappola (vedi ImportPolicy.ImportSids): un bool nuovo nasce false ovunque,
        // il che avvelena i flag OPT-OUT. Questo è opt-IN — false = prosa distesa, il comportamento storico —
        // quindi ogni sezione già scritta continua a leggersi esattamente come prima, ed è cio' che si vuole.
            migrationBuilder.AddColumn<bool>(
                name: "LeadSentence",
                table: "DocumentSections",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeadSentence",
                table: "DocumentSections");
        }
    }
}
