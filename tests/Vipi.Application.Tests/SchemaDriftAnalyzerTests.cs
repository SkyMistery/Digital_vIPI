using Vipi.Application.Diagnostics;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il reconciler Postgres è additivo: aggiunge le colonne mancanti e non tocca il resto. Questi test presidiano
/// ciò che resta scoperto — e soprattutto il caso silenzioso, la rinomina, che non lancia niente e lascia l'app a
/// leggere una colonna nuova e vuota mentre i dati stanno ancora in quella vecchia.
/// </summary>
public class SchemaDriftAnalyzerTests
{
    private static SchemaColumn C(string t, string c, string type = "text") => new(t, c, type);

    [Fact]
    public void Schema_allineato_non_produce_finding()
    {
        var cols = new[] { C("Documents", "Id", "integer"), C("Documents", "Title") };
        Assert.Empty(SchemaDriftAnalyzer.Compare(cols, cols));
    }

    [Fact]
    public void Rinomina_lascia_la_colonna_vecchia_e_viene_segnalata()
    {
        // Il modello dice «Title», lo schema ha ancora «Titolo» (più «Title» che il reconciler ha appena creato).
        var model = new[] { C("Documents", "Id", "integer"), C("Documents", "Title") };
        var actual = new[] { C("Documents", "Id", "integer"), C("Documents", "Title"), C("Documents", "Titolo") };

        var f = Assert.Single(SchemaDriftAnalyzer.Compare(model, actual));
        Assert.Equal("Colonna orfana nello schema", f.Category);
        Assert.Equal("Documents.Titolo", f.Entity);
        Assert.Contains("dati sono ancora QUI", f.Detail);
    }

    [Fact]
    public void Colonna_attesa_ma_assente_e_un_errore()
    {
        // Succede se l'ADD COLUMN del reconciler è fallito: è best-effort e prosegue.
        var model = new[] { C("Documents", "Id", "integer"), C("Documents", "Title") };
        var actual = new[] { C("Documents", "Id", "integer") };

        var f = Assert.Single(SchemaDriftAnalyzer.Compare(model, actual));
        Assert.Equal("Colonna mancante nello schema", f.Category);
        Assert.Equal(ConsistencySeverity.Error, f.Severity);
    }

    [Fact]
    public void Tipo_divergente_viene_segnalato_con_entrambi_i_valori()
    {
        var model = new[] { C("Documents", "Rank", "integer") };
        var actual = new[] { C("Documents", "Rank", "text") };

        var f = Assert.Single(SchemaDriftAnalyzer.Compare(model, actual));
        Assert.Equal("Tipo colonna divergente", f.Category);
        Assert.Contains("integer", f.Detail);
        Assert.Contains("text", f.Detail);
    }

    /// <summary>
    /// EF e information_schema chiamano lo stesso tipo in modi diversi. Se questi producessero finding, la
    /// diagnostica sarebbe piena di falsi allarmi — e una diagnostica rumorosa non la legge più nessuno.
    /// </summary>
    [Theory]
    [InlineData("varchar(200)", "character varying")]
    [InlineData("timestamptz", "timestamp with time zone")]
    [InlineData("timestamp", "timestamp without time zone")]
    [InlineData("numeric(18,2)", "numeric")]
    [InlineData("bool", "boolean")]
    [InlineData("int4", "integer")]
    [InlineData("int8", "bigint")]
    [InlineData("float8", "double precision")]
    [InlineData("TEXT", "text")]
    public void Alias_dello_stesso_tipo_non_sono_drift(string modelType, string actualType)
    {
        var model = new[] { C("Documents", "X", modelType) };
        var actual = new[] { C("Documents", "X", actualType) };
        Assert.Empty(SchemaDriftAnalyzer.Compare(model, actual));
    }

    [Fact]
    public void Le_tabelle_fuori_dal_modello_sono_ignorate()
    {
        // Vivono nel DB di proposito: DataProtectionKeys la crea l'avvio, __EFMigrationsHistory la crea EF.
        var model = new[] { C("Documents", "Id", "integer") };
        var actual = new[]
        {
            C("Documents", "Id", "integer"),
            C("DataProtectionKeys", "Id", "integer"),
            C("__EFMigrationsHistory", "MigrationId"),
        };
        Assert.Empty(SchemaDriftAnalyzer.Compare(model, actual));
    }

    [Fact]
    public void Tabella_del_modello_del_tutto_assente_non_genera_una_riga_per_colonna()
    {
        // Non è drift di colonna: è uno schema mai creato, e se ne accorge prima EnsureCreated. Emettere una
        // riga per ogni colonna seppellirebbe i finding veri.
        var model = new[] { C("Documents", "Id", "integer"), C("Sectors", "Id", "integer"), C("Sectors", "Name") };
        var actual = new[] { C("Documents", "Id", "integer") };

        Assert.Empty(SchemaDriftAnalyzer.Compare(model, actual));
    }
}
