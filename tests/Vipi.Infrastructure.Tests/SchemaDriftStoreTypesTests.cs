using Microsoft.EntityFrameworkCore;
using Vipi.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il probe di drift confronta il tipo store del modello col <c>data_type</c> di <c>information_schema</c>, che per
/// lo stesso tipo usa un altro nome (<c>varchar</c> contro <c>character varying</c>). La normalizzazione copre gli
/// alias noti: questo test presidia il presupposto, cioè che il modello non usi tipi fuori da quell'elenco.
/// Se fallisce, qualcuno ha aggiunto una colonna con un tipo esotico e va esteso <c>SchemaDriftAnalyzer.Canonical</c>
/// — altrimenti quella colonna comparirebbe come falso «tipo divergente» a ogni apertura della diagnostica.
/// </summary>
public class SchemaDriftStoreTypesTests
{
    private readonly ITestOutputHelper _out;
    public SchemaDriftStoreTypesTests(ITestOutputHelper output) => _out = output;

    /// Nomi base (senza precisione) che la normalizzazione sa già far combaciare con information_schema.
    private static readonly HashSet<string> Coperti = new(StringComparer.OrdinalIgnoreCase)
    {
        "text", "varchar", "character varying", "char", "character", "bpchar",
        "int2", "int4", "int8", "smallint", "integer", "bigint",
        "numeric", "decimal", "real", "float4", "double precision", "float8",
        "bool", "boolean", "bytea", "uuid",
        "timestamp", "timestamptz", "timestamp with time zone", "timestamp without time zone",
        "date", "time", "timetz", "time with time zone", "time without time zone",
        "interval", "json", "jsonb",
    };

    [Fact]
    public void Ogni_tipo_store_del_modello_npgsql_e_coperto_dalla_normalizzazione()
    {
        // Il modello si costruisce senza connettersi: serve solo che il provider sia Npgsql, non un DB vivo.
        var options = new DbContextOptionsBuilder<VipiDbContext>()
            .UseNpgsql("Host=nowhere;Database=x;Username=u;Password=p")
            .Options;
        using var db = new VipiDbContext(options);

        var tipi = db.Model.GetRelationalModel().Tables
            .SelectMany(t => t.Columns.Select(c => Base(c.StoreType)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _out.WriteLine("tipi store nel modello Npgsql: " + string.Join(", ", tipi));

        var scoperti = tipi.Where(t => !Coperti.Contains(t)).ToList();
        Assert.True(scoperti.Count == 0,
            "tipi non coperti da SchemaDriftAnalyzer.Canonical: " + string.Join(", ", scoperti));
    }

    private static string Base(string? storeType)
    {
        var t = (storeType ?? "").Trim();
        var p = t.IndexOf('(');
        return p >= 0 ? t[..p].Trim() : t;
    }
}
