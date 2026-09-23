using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vipi.Infrastructure.MySqlMigrations.Migrations
{
    /// <summary>
    /// La vista <c>v_share_atc_sessions</c>: l'archivio delle sessioni ATC letto dall'<b>IVAO Division Hub</b>, un
    /// sito separato con un database suo sullo stesso server MariaDB (contratto deciso il 14 settembre 2026 nella
    /// nota dell'hub <c>docs/internal/decisions/2026-09-14-dati-condivisi-con-vipi.md</c>; carta
    /// <c>docs/feature/2026-09-23-vista-condivisa-sessioni-atc.md</c>).
    ///
    /// <para>🔴 <b>La vista è il contratto con l'hub, e cambia solo in modo additivo.</b> I dieci nomi a destra di
    /// <c>AS</c> sono quelli che l'hub legge: una colonna nuova si aggiunge con un'altra migrazione e un altro
    /// <c>CREATE OR REPLACE</c>; una colonna non si rinomina e non si toglie mai. La tabella sotto invece può
    /// cambiare: se una colonna di <c>AtcSessions</c> cambia nome, si riscrive il lato sinistro, mai l'alias.</para>
    ///
    /// <para><b>Solo MySQL/MariaDB, senza gemella SQLite.</b> Come <c>SpecialAreasHardening</c> ed
    /// <c>EnumLengthsAndDropUnusedTokens</c>: la vista non è nel modello EF (le guardie di allineamento non la
    /// vedono) e serve solo in produzione, dove l'hub la legge. In locale su SQLite e su Postgres non c'è nessuno
    /// che la legga.</para>
    ///
    /// <para>⚠️ <b>Il GRANT non sta qui.</b> L'hub legge con un utente MariaDB suo, che ha solo <c>SELECT</c> su
    /// questa vista: il permesso lo dà chi amministra il server
    /// (<c>GRANT SELECT ON itivao_atc.v_share_atc_sessions TO '&lt;utente dell'hub&gt;'@'&lt;host&gt;';</c>).
    /// L'utente della vIPI deve poter creare viste (<c>CREATE VIEW</c>), o questa migrazione ferma l'avvio.</para>
    ///
    /// <para><c>SQL SECURITY DEFINER</c>: chi legge la vista lo fa coi permessi di chi l'ha creata, quindi all'utente
    /// dell'hub basta il <c>SELECT</c> sulla vista e non gli serve niente su <c>AtcSessions</c>. Il traffico, le piste,
    /// <c>ShiftKey</c> e le colonne di servizio restano fuori: l'hub chiede solo «chi era online, quando».</para>
    ///
    /// <para>⚠️ La copia del database (§A47) porta anche questa vista: <c>MySqlDumpSource</c> accetta le viste
    /// <c>v_share_</c> e le riscrive senza <c>DEFINER</c>, dopo le tabelle.</para>
    /// </summary>
    public partial class VistaCondivisaSessioniAtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR REPLACE SQL SECURITY DEFINER VIEW v_share_atc_sessions AS
SELECT SessionId         AS session_id,
       UserId            AS vid,
       Callsign          AS callsign,
       Position          AS position,
       Frequency         AS frequency,
       StartUtc          AS start_utc,
       EndUtc            AS end_utc,
       DurationSeconds   AS duration_seconds,
       Rating            AS rating,
       IsOutsideDivision AS is_outside_division
FROM AtcSessions;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_share_atc_sessions;");
        }
    }
}
