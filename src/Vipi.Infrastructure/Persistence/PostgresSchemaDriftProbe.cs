using System.Data;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Diagnostics;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Legge lo schema fisico da <c>information_schema</c> e lo confronta col modello EF
/// (<see cref="SchemaDriftAnalyzer"/>). No-op fuori da Npgsql: dove le migrazioni EF girano davvero — SQLite in
/// sviluppo — il drift non si accumula, perché lo schema lo costruisce la cronologia delle migrazioni.
/// <para>
/// Solo lettura, una query. Sta nel report di consistenza (quindi <c>/vsop/admin/diagnostica</c> e
/// <c>/vsop/health</c>), non nella sonda <c>/vsop/health/ready</c> che l'orchestratore ripete di continuo.
/// </para>
/// </summary>
public sealed class PostgresSchemaDriftProbe : ISchemaDriftProbe
{
    private readonly VipiDbContext _db;
    public PostgresSchemaDriftProbe(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<ConsistencyFinding>> RunAsync(CancellationToken ct = default)
    {
        if (_db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) != true)
            return Array.Empty<ConsistencyFinding>();

        var model = ModelColumns();
        var actual = await ActualColumnsAsync(ct);
        return SchemaDriftAnalyzer.Compare(model, actual);
    }

    /// <summary>Colonne attese, dal modello relazionale EF (stessa fonte che usa il reconciler).</summary>
    private IReadOnlyList<SchemaColumn> ModelColumns() =>
        _db.Model.GetRelationalModel().Tables
            .Where(t => string.IsNullOrEmpty(t.Schema) || string.Equals(t.Schema, "public", StringComparison.Ordinal))
            .SelectMany(t => t.Columns.Select(c => new SchemaColumn(t.Name, c.Name, c.StoreType)))
            .ToList();

    /// <summary>Colonne realmente presenti nello schema <c>public</c>.</summary>
    private async Task<IReadOnlyList<SchemaColumn>> ActualColumnsAsync(CancellationToken ct)
    {
        var rows = new List<SchemaColumn>();
        var conn = _db.Database.GetDbConnection();
        var mustClose = conn.State != ConnectionState.Open;
        if (mustClose) await _db.Database.OpenConnectionAsync(ct);
        try
        {
            using var cmd = conn.CreateCommand();
            // udt_name copre i casi che data_type appiattisce su «USER-DEFINED» o «ARRAY».
            cmd.CommandText =
                "SELECT table_name, column_name, COALESCE(NULLIF(data_type,'USER-DEFINED'), udt_name) " +
                "FROM information_schema.columns WHERE table_schema = 'public'";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                rows.Add(new SchemaColumn(r.GetString(0), r.GetString(1), r.IsDBNull(2) ? "" : r.GetString(2)));
        }
        finally
        {
            if (mustClose) await _db.Database.CloseConnectionAsync();
        }
        return rows;
    }
}
