using System.Data;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Diagnostics;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Legge <c>@@sql_mode</c> e <c>@@max_allowed_packet</c> dal server e li passa a
/// <see cref="ServerSettingsAnalyzer"/>. No-op fuori da MySQL/MariaDB: su SQLite e PostgreSQL nessuna delle
/// due impostazioni esiste, e la domanda non si pone.
///
/// <para>Una query, sola lettura. Sta nel report di consistenza (<c>/vsop/admin/diagnostica</c> e
/// <c>/vsop/health</c>), non nella sonda <c>ready</c>. Stesso posto e stessa forma di
/// <see cref="PostgresSchemaDriftProbe"/>, per la stessa ragione: è l'unico punto letto da entrambi.</para>
///
/// <para>⚠️ La lettura non passa da <c>ExecuteSqlRaw</c> ma dalla connessione, come fa la sonda di drift:
/// <c>@@variabile</c> non è una query su una tabella e non ha bisogno del machinery di EF.</para>
/// </summary>
public sealed class MySqlServerSettingsProbe : IServerSettingsProbe
{
    private readonly VipiDbContext _db;
    public MySqlServerSettingsProbe(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<ConsistencyFinding>> RunAsync(CancellationToken ct = default)
    {
        if (_db.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) != true)
            return Array.Empty<ConsistencyFinding>();

        var conn = _db.Database.GetDbConnection();
        var daChiudere = conn.State != ConnectionState.Open;
        if (daChiudere) await _db.Database.OpenConnectionAsync(ct);
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT @@sql_mode, @@max_allowed_packet";
            using var r = await cmd.ExecuteReaderAsync(ct);

            if (!await r.ReadAsync(ct)) return ServerSettingsAnalyzer.Analyze(null, null);

            var sqlMode = r.IsDBNull(0) ? null : r.GetString(0);
            // Il tipo di ritorno di @@max_allowed_packet varia fra le versioni (int/bigint/ulong):
            // GetInt64 su un ulong lancerebbe, quindi si passa da Convert.
            long? packet = r.IsDBNull(1) ? null : Convert.ToInt64(r.GetValue(1));

            return ServerSettingsAnalyzer.Analyze(sqlMode, packet);
        }
        finally
        {
            if (daChiudere) await _db.Database.CloseConnectionAsync();
        }
    }
}
