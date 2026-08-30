using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FormaCheHaContato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShapeSource",
                table: "AtcSessionTraffic",
                type: "TEXT",
                nullable: false,
                // ⚠️ NON il "" che genera EF: questa colonna è un ENUM scritto come stringa, e una riga con
                // il vuoto dentro non si rilegge più — le tratte già in archivio diventerebbero illeggibili al
                // primo caricamento. Il valore giusto è `Source`: sono state contate con la forma
                // dell'anagrafica, che è l'unica che ci fosse quando sono state scritte.
                defaultValue: "Source");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShapeSource",
                table: "AtcSessionTraffic");
        }
    }
}
