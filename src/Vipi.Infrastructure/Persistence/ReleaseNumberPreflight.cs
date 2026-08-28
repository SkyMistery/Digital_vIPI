using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Il controllo che va fatto <b>prima</b> di posare l'indice unico su
/// <c>DocReleases (TargetType, TargetKey, VersionNumber)</c>: se in tabella ci sono già due rilasci con lo
/// stesso numero sullo stesso bersaglio, la migrazione <c>UniqueReleaseNumberPerTarget</c> non può
/// applicarsi, e il posto in cui lo scopre è l'avvio dell'applicazione in produzione.
///
/// <para><b>Perché non basta la SELECT sul database.</b> Era scritto nei lavori aperti da agosto — «fare la
/// SELECT dei duplicati prima del deploy» — ma <b>non è eseguibile con gli accessi che abbiamo</b>: il 3306
/// del server sta sul suo <c>localhost</c> (vedi <c>deploy/mariadb/README.md</c>), non è raggiungibile da
/// fuori, e sull'host non c'è un pannello da cui aprire una console SQL. L'unico programma che quel
/// database lo raggiunge è <b>questo</b>. Quindi il controllo lo fa lui, appena prima di migrare.</para>
///
/// <para><b>Che cosa cambia, se i duplicati ci sono.</b> Non l'esito — l'avvio si ferma comunque, come si
/// fermerebbe da solo — ma <b>che cosa si legge</b>. Senza questo controllo il guasto è
/// <c>Duplicate entry '...' for key 'IX_DocReleases_TargetType_TargetKey_VersionNumber'</c>: dice la chiave,
/// non le righe, e arriva da dentro una migrazione a metà. Con questo controllo, in
/// <c>avvio-errore.txt</c> — il file che si scarica via FTP, l'unico canale disponibile — c'è l'elenco dei
/// bersagli e dei numeri da sistemare.</para>
///
/// <para>⚠️ <b>Non ripara.</b> Rinumerare un rilascio vuol dire cambiare un «rilascio #N» che qualcuno può
/// aver già letto o citato: è una decisione di chi pubblica, non di una routine d'avvio.</para>
///
/// <para>⚠️ È l'<b>unico</b> indice unico della coda che possa fallire. Gli altri quattro
/// (<c>Airports.DocumentId</c>, <c>Airports.MilDocumentId</c>, <c>AccSectors.IvaoId</c>,
/// <c>AirportSectors.IvaoId</c>) stanno su colonne <b>create dalla stessa migrazione</b>: nascono tutte
/// nulle, e un indice unico ammette quanti nulli vuole. <c>CallsignAliases</c> è una tabella nuova.</para>
/// </summary>
public static class ReleaseNumberPreflight
{
    /// <summary>
    /// La migrazione che posa l'indice: fuori dalla coda dei pendenti, non c'è niente da controllare.
    ///
    /// <para>⚠️ È il <b>nome</b> senza il timbro, e non l'identificativo completo: la stessa migrazione ha due
    /// id diversi nei due insiemi — <c>20260825151953</c> in <c>Vipi.Infrastructure</c> (SQLite) e
    /// <c>20260825152005</c> in <c>Vipi.Infrastructure.MySqlMigrations</c> — perché sono state emesse a
    /// dodici secondi di distanza. Con l'id esatto il controllo sarebbe stato <b>muto su uno dei due
    /// provider</b>, in silenzio: l'ha trovato un test, non una rilettura.</para>
    /// </summary>
    public const string MigrazioneCheImponeLUnicita = "UniqueReleaseNumberPerTarget";

    /// <summary>Un numero di rilascio assegnato due volte allo stesso bersaglio.</summary>
    public readonly record struct Doppione(string TargetType, string TargetKey, int VersionNumber, int Quante);

    /// <summary>
    /// Controlla e <b>lancia</b> se trova doppioni. Da chiamare subito prima di <c>Migrate()</c>.
    ///
    /// <para>Silenziosa quando non c'è niente da fare, che è il caso normale: se la migrazione è già
    /// applicata l'indice esiste già, e i doppioni non possono esserci per costruzione.</para>
    /// </summary>
    public static void Verifica(DbContext db)
    {
        if (!db.Database.GetPendingMigrations()
                .Any(m => m.EndsWith(MigrazioneCheImponeLUnicita, StringComparison.Ordinal))) return;

        var doppioni = Cerca(db);
        if (doppioni.Count == 0) return;

        throw new InvalidOperationException(Messaggio(doppioni));
    }

    /// <summary>
    /// I doppioni in tabella. Best-effort di proposito: su un database **vuoto** la tabella non esiste
    /// ancora — la migrazione la crea più avanti nella stessa coda — e «non c'è la tabella» è il caso in
    /// cui i doppioni non ci sono, non un guasto da propagare.
    /// </summary>
    public static IReadOnlyList<Doppione> Cerca(DbContext db)
    {
        var esito = new List<Doppione>();
        try
        {
            db.Database.OpenConnection();
            using var cmd = db.Database.GetDbConnection().CreateCommand();

            // SQL nudo e portabile: gira uguale su SQLite e su MariaDB, e non dipende dal modello EF —
            // che a questo punto descrive lo schema DOPO la migrazione, non quello che c'è in tabella.
            cmd.CommandText =
                "SELECT TargetType, TargetKey, VersionNumber, COUNT(*) " +
                "FROM DocReleases GROUP BY TargetType, TargetKey, VersionNumber HAVING COUNT(*) > 1";

            using var r = cmd.ExecuteReader();
            while (r.Read())
                esito.Add(new Doppione(r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetInt32(3)));
        }
        catch (Exception)
        {
            return Array.Empty<Doppione>();
        }
        finally
        {
            db.Database.CloseConnection();
        }

        return esito;
    }

    /// <summary>
    /// Il testo che finisce in <c>avvio-errore.txt</c>. Separato dalla query perché è la parte che qualcuno
    /// legge da solo, via FTP, senza nessuno accanto: dice che cosa è successo, quali righe, e che cosa fare.
    /// </summary>
    public static string Messaggio(IReadOnlyList<Doppione> doppioni)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            $"Migrazione fermata PRIMA di applicarla: in DocReleases ci sono {doppioni.Count} " +
            "numeri di rilascio assegnati due volte allo stesso bersaglio, e la migrazione " +
            $"{MigrazioneCheImponeLUnicita} vi posa sopra un indice UNICO.");
        sb.AppendLine();
        sb.AppendLine("Righe da sistemare (bersaglio, numero, quante volte):");
        foreach (var d in doppioni)
            sb.AppendLine($"  {d.TargetType} {d.TargetKey}  #{d.VersionNumber}  ×{d.Quante}");
        sb.AppendLine();
        sb.AppendLine(
            "Come si sistema: si rinumerano i rilasci in eccesso (il piu' recente prende max+1 sul suo " +
            "bersaglio), poi si riavvia. Non lo fa questa routine di proposito: cambiare un «rilascio #N» " +
            "significa cambiare un numero che qualcuno puo' aver gia' letto, ed e' una decisione di chi " +
            "pubblica.");
        sb.AppendLine();
        sb.AppendLine(
            "Perche' il numero puo' essersi ripetuto: lo assegna max+1 letto in memoria, quindi due " +
            "pubblicazioni concorrenti sullo stesso bersaglio prendono lo stesso numero. L'indice unico " +
            "esiste per trasformare quel silenzio in un conflitto rumoroso.");
        return sb.ToString();
    }
}
