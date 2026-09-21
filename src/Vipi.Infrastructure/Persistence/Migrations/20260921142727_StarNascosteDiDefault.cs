using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Le sezioni «STAR» degli aeroporti (civili e militari) diventano NASCOSTE, una volta sola. Chiesto dal
    /// committente il 21 settembre 2026: «tutte le sezioni STAR nascoste di default; se le vogliamo vedere l'editor
    /// le metterà visibili».
    /// <para>⚠️ È una migrazione e non un passo della manutenzione d'avvio apposta: gira UNA volta per database.
    /// Un passo d'avvio ripasserebbe a ogni riavvio e rispegnerebbe la STAR che un editore ha acceso. Le sezioni che
    /// nascono dopo nascono già nascoste (<c>SectionDescriptor.BornHidden</c>).</para>
    /// <para>Tutte le versioni, non solo la bozza: le copie pubbliche stanno negli snapshot di release e non si
    /// toccano — cambiano alla prossima pubblicazione.</para>
    /// <para>Il <c>Down</c> non fa niente: non si sa quali fossero state nascoste a mano prima, e riaccenderle tutte
    /// sarebbe una scelta nuova, non un ritorno.</para>
    /// </summary>
    public partial class StarNascosteDiDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE DocumentSections SET IsHidden = 1 WHERE SectionKey = 'stars'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
