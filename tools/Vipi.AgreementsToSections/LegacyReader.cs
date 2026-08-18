using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;

namespace Vipi.AgreementsToSections;

/// <summary>
/// Legge il modello **vecchio** — quello di ferragosto — in SQL grezzo.
///
/// <para>⚠️ <b>Non con EF, e non è pigrizia.</b> Il <c>VipiDbContext</c> descrive il modello NUOVO: per lui
/// <c>AgreementParties</c> non esiste, <c>CoordinationAgreements.TrafficKind</c> nemmeno, e
/// <c>AgreementClauses.Direction</c> neanche. Leggerle a mano è l'unico modo di vederle — ed è anche la
/// garanzia che questa lettura non possa dipendere per sbaglio da qualcosa che il modello nuovo racconta.</para>
///
/// <para>Delle clausole si prende solo la <b>posizione</b> (id, verso, ordine, gruppo, profondità): i loro dati
/// — punti, livelli, condizioni, faccetta — non si toccano <b>mai</b>. Le righe restano dove sono e cambiano
/// solo padre, che è la ragione per cui la rete di caratterizzazione può ancora dire «la derivazione non è
/// cambiata».</para>
/// </summary>
public static class LegacyReader
{
    public static async Task<IReadOnlyList<LegacyAgreement>> LeggiAsync(VipiDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            if (!await EsisteTabellaAsync(db, "AgreementParties"))
                return Array.Empty<LegacyAgreement>();

            // ⚠️ «Order» è una parola riservata, e i due archivi la citano in modo DIVERSO: SQLite vuole le
            // virgolette doppie, MariaDB i backtick — dove le virgolette doppie, senza ANSI_QUOTES, sarebbero
            // una STRINGA e non una colonna. Nessuna delle due query darebbe errore: darebbero l'ordine
            // sbagliato, che qui vuol dire clausole rimescolate.
            var ord = Ordine(db);

            var parti = new Dictionary<int, (List<int> A, List<int> B)>();
            await LeggiAsync(db, $"select AgreementId, Side, SectorId from AgreementParties order by AgreementId, {ord}, Id",
                r =>
                {
                    var id = r.GetInt32(0);
                    if (!parti.TryGetValue(id, out var lati)) parti[id] = lati = (new List<int>(), new List<int>());
                    (r.GetString(1) == "A" ? lati.A : lati.B).Add(r.GetInt32(2));
                });

            var aeroporti = new Dictionary<int, List<AgreementAirportRow>>();
            await LeggiAsync(db, $"select AgreementId, Icao, Name, {ord} from AgreementAirports order by AgreementId, {ord}, Id",
                r =>
                {
                    var id = r.GetInt32(0);
                    if (!aeroporti.TryGetValue(id, out var elenco)) aeroporti[id] = elenco = new List<AgreementAirportRow>();
                    elenco.Add(new AgreementAirportRow(r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetInt32(3)));
                });

            var clausole = new Dictionary<int, List<LegacyClause>>();
            await LeggiAsync(db, $"select AgreementId, Id, Direction, {ord}, VariantGroup, VariantDepth "
                                 + $"from AgreementClauses order by AgreementId, {ord}, Id",
                r =>
                {
                    var id = r.GetInt32(0);
                    if (!clausole.TryGetValue(id, out var elenco)) clausole[id] = elenco = new List<LegacyClause>();
                    elenco.Add(new LegacyClause(
                        r.GetInt32(1),
                        Enum.Parse<AgreementDirection>(r.GetString(2)),
                        r.GetInt32(3),
                        r.IsDBNull(4) ? null : r.GetInt32(4),
                        r.GetInt32(5)));
                });

            var accordi = new List<LegacyAgreement>();
            await LeggiAsync(db, $"select Id, OwnerAccId, TrafficKind, Description, {ord} "
                                 + $"from CoordinationAgreements order by {ord}, Id",
                r =>
                {
                    var id = r.GetInt32(0);
                    var lati = parti.TryGetValue(id, out var p) ? p : (A: new List<int>(), B: new List<int>());
                    accordi.Add(new LegacyAgreement(
                        id,
                        r.GetInt32(1),
                        Enum.Parse<TransferFlowKind>(r.GetString(2)),
                        r.IsDBNull(3) ? null : r.GetString(3),
                        r.GetInt32(4),
                        lati.A,
                        lati.B,
                        aeroporti.TryGetValue(id, out var apt) ? apt : Array.Empty<AgreementAirportRow>(),
                        clausole.TryGetValue(id, out var cl) ? cl : Array.Empty<LegacyClause>()));
                });

            return accordi;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    /// <summary>
    /// La tabella c'è ancora? Se no, la conversione è già stata fatta — e il tool deve dirlo, non schiantarsi.
    /// <para>La domanda si fa in modo portabile con <c>information_schema</c>, che SQLite non ha: là si guarda
    /// <c>sqlite_master</c>. Sono i due soli archivi su cui questo tool gira.</para>
    /// </summary>
    private static async Task<bool> EsisteTabellaAsync(VipiDbContext db, string nome)
    {
        var sql = Sqlite(db)
            ? $"select count(*) from sqlite_master where type = 'table' and name = '{nome}'"
            : $"select count(*) from information_schema.tables where table_schema = database() and table_name = '{nome}'";

        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = sql;
        var n = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(n) > 0;
    }

    /// <summary>Il provider è SQLite? È la sola distinzione che questo tool fa: gli archivi sono due.</summary>
    private static bool Sqlite(VipiDbContext db) =>
        db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>La colonna <c>Order</c> citata come vuole l'archivio in cui si sta leggendo.</summary>
    private static string Ordine(VipiDbContext db) => Sqlite(db) ? "\"Order\"" : "`Order`";

    private static async Task LeggiAsync(VipiDbContext db, string sql, Action<DbDataReader> riga)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = sql;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) riga(r);
    }
}
