using System.Data.Common;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Un solo processo alla volta migra lo stesso database MariaDB (U-096, revisione totale 3).
///
/// <para><b>Perché.</b> Su Passenger due processi dell'applicazione partono insieme più spesso di quanto si
/// creda: il vecchio che non è ancora morto e il nuovo dopo un carico, due <c>restart.txt</c> ravvicinati, il
/// keep-alive che bussa ogni 10 s mentre il primo avvio è ancora dentro <c>Migrate()</c>. Senza turno entrambi
/// vedono la stessa migrazione pendente ed eseguono le stesse DDL: il secondo cade su «Table already exists» o
/// su un vincolo a metà. Le DDL rieseguibili (<see cref="MigrazioniRieseguibili"/>) riducono il danno, il turno
/// lo toglie: chi arriva secondo aspetta, poi trova la coda vuota.</para>
///
/// <para>⚠️ <c>GET_LOCK</c> vale per la <b>sessione</b>: la connessione deve restare aperta da prima di
/// prenderlo a dopo <c>Migrate()</c>, e dev'essere la stessa che usa EF. Se il processo muore, il server lo
/// rilascia da sé: niente lucchetti orfani.</para>
/// </summary>
public static class TurnoDelleMigrazioni
{
    /// <summary>Quanto si aspetta chi arriva secondo. Più di una migrazione vera su una tabella grande, meno di
    /// un avvio che resta appeso per sempre.</summary>
    public static readonly TimeSpan Attesa = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Prende il turno sulla connessione <b>già aperta</b>. Il nome contiene il database: due installazioni sullo
    /// stesso server non si aspettano a vicenda.
    /// </summary>
    public static void Prendi(DbConnection connessione, TimeSpan attesa)
    {
        using var cmd = connessione.CreateCommand();
        cmd.CommandText = "SELECT GET_LOCK(CONCAT('vipi-migrazioni:', DATABASE()), @attesa)";
        // Il comando aspetta il lucchetto: il suo timeout dev'essere più lungo dell'attesa, o scade prima lui.
        cmd.CommandTimeout = (int)attesa.TotalSeconds + 30;
        var p = cmd.CreateParameter();
        p.ParameterName = "@attesa";
        p.Value = (int)attesa.TotalSeconds;
        cmd.Parameters.Add(p);
        Verifica(cmd.ExecuteScalar(), attesa);
    }

    /// <summary>Rilascia il turno. Chiamarlo senza averlo preso non fa danni (MariaDB risponde NULL).</summary>
    public static void Rilascia(DbConnection connessione)
    {
        using var cmd = connessione.CreateCommand();
        cmd.CommandText = "SELECT RELEASE_LOCK(CONCAT('vipi-migrazioni:', DATABASE()))";
        cmd.ExecuteScalar();
    }

    /// <summary>
    /// Legge la risposta di <c>GET_LOCK</c>: 1 = preso, 0 = scaduta l'attesa, NULL = errore del server. Solo 1
    /// lascia migrare, e gli altri due fermano l'avvio con un messaggio che dice cosa è successo: migrare senza
    /// turno è proprio il guasto che il turno toglie.
    /// </summary>
    public static void Verifica(object? risposta, TimeSpan attesa)
    {
        var valore = risposta is null or DBNull ? (long?)null : Convert.ToInt64(risposta);
        if (valore == 1) return;
        throw new InvalidOperationException(valore == 0
            ? $"Un altro processo sta migrando questo database da più di {attesa.TotalMinutes:0} minuti: l'avvio " +
              "si ferma invece di migrare insieme a lui. Al riavvio successivo, se quello ha finito, non c'è più niente da fare."
            : "GET_LOCK ha risposto NULL: il server non ha potuto dare il turno delle migrazioni, e l'avvio si ferma " +
              "invece di migrare senza.");
    }
}
