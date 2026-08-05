#if NET8_0
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Lunghezze e collation sono applicate al modello <b>solo</b> sotto MySQL, e la selezione avviene con un
/// controllo sul nome del provider dentro <c>OnModelCreating</c>. È esattamente il tipo di guardia che può
/// smettere di scattare senza che niente si rompa: se il controllo non passasse, su MySQL le colonne
/// nascerebbero <c>longtext</c> (CREATE TABLE fallito) e i confronti tornerebbero case-insensitive.
/// Questi test verificano che scatti dove deve e che non scatti dove non deve.
///
/// <para>Il modello si costruisce senza connettersi: serve solo che il provider sia registrato. Si legge
/// però il modello <b>design-time</b> e non <c>DbContext.Model</c>: quello a runtime è read-optimized e
/// non conserva la collation, che serve solo a generare la DDL.</para>
/// </summary>
public class MySqlModelTuningTests
{
    private static IModel Modello(Action<DbContextOptionsBuilder<VipiDbContext>> provider)
    {
        var b = new DbContextOptionsBuilder<VipiDbContext>();
        provider(b);
        using var db = new VipiDbContext(b.Options);
        return db.GetService<IDesignTimeModel>().Model;
    }

    private static IModel MySql() =>
        Modello(b => b.UseMySql("Server=nowhere;Database=x;User Id=u;Password=p", MySqlSchema.ResolveServerVersion(null)));

    private static IModel PerNome(string provider) => provider switch
    {
        "Sqlite" => Modello(b => b.UseSqlite("Data Source=:memory:")),
        "Postgres" => Modello(b => b.UseNpgsql("Host=nowhere;Database=x;Username=u;Password=p")),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "provider non previsto dal test"),
    };

    private static IProperty Proprieta(IModel model, string entita, string proprieta)
    {
        var et = model.GetEntityTypes().FirstOrDefault(e => e.ClrType.Name == entita);
        Assert.NotNull(et);
        var p = et!.FindProperty(proprieta);
        Assert.NotNull(p);
        return p!;
    }

    [Fact]
    public void Sotto_mysql_le_colonne_indicizzate_hanno_la_lunghezza_della_mappa()
    {
        var model = MySql();

        var divergenti = MySqlStringLengths.Map
            .Select(kv => (kv.Key, Attesa: kv.Value, Trovata: Proprieta(model, kv.Key.Entity, kv.Key.Property).GetMaxLength()))
            .Where(x => x.Trovata != x.Attesa)
            .Select(x => $"{x.Key.Entity}.{x.Key.Property}: attesa {x.Attesa}, trovata {x.Trovata?.ToString() ?? "nessuna"}")
            .ToList();

        Assert.True(divergenti.Count == 0, string.Join("\n", divergenti));
    }

    [Fact]
    public void Sotto_mysql_le_colonne_stringa_hanno_la_collation_sensibile()
    {
        var model = MySql();

        // Un campione che copre le tre famiglie a rischio: chiave naturale, enum salvato come stringa,
        // e l'hash content-addressed, che è quello dove una fusione silenziosa costerebbe di più.
        foreach (var (entita, proprieta) in new[]
                 {
                     ("Acc", "Code"),
                     ("Sector", "Callsign"),
                     ("Document", "Type"),
                     ("MediaAsset", "Sha256"),
                 })
            Assert.Equal(MySqlCollation.Name, Proprieta(model, entita, proprieta).GetCollation());
    }

    [Fact]
    public void Sotto_mysql_nessuna_colonna_stringa_resta_senza_collation()
    {
        var scoperte = MySql().GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => (p.GetProviderClrType() ?? Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType) == typeof(string))
            .Where(p => p.GetCollation() is null)
            .Select(p => $"{p.DeclaringType.ClrType.Name}.{p.Name}")
            .ToList();

        Assert.True(scoperte.Count == 0,
            "colonne stringa senza collation esplicita sotto MySQL — erediterebbero il default del server, " +
            "che è case- e accent-insensitive: " + string.Join(", ", scoperte));
    }

    /// <summary>
    /// Il contrappeso dei test sopra: su SQLite e Postgres il modello NON deve cambiare. Le lunghezze su
    /// Postgres sarebbero un cambio di tipo colonna (<c>text</c> → <c>varchar(n)</c>) che il reconciler non
    /// sa applicare e che il drift probe segnalerebbe su ogni colonna toccata; la collation MySQL su
    /// Postgres non esisterebbe proprio come nome.
    /// </summary>
    [Theory]
    [InlineData("Sqlite")]
    [InlineData("Postgres")]
    public void Sugli_altri_provider_il_modello_resta_intatto(string provider)
    {
        var model = PerNome(provider);

        // Campione di colonne che sotto MySQL sono dimensionate: qui devono restare senza lunghezza.
        foreach (var (entita, proprieta) in new[] { ("Acc", "Code"), ("Sector", "Callsign"), ("Document", "Type") })
        {
            var p = Proprieta(model, entita, proprieta);
            Assert.Null(p.GetMaxLength());
            Assert.Null(p.GetCollation());
        }

        // MediaAsset.Sha256 invece ha una lunghezza dichiarata nel modello per tutti i provider: è
        // content-addressed, la lunghezza è fissa per definizione dell'algoritmo. Se sparisse qui,
        // vorrebbe dire che qualcuno l'ha spostata dentro il ramo MySQL.
        var sha = Proprieta(model, "MediaAsset", "Sha256");
        Assert.Equal(64, sha.GetMaxLength());
        Assert.Null(sha.GetCollation());
    }
}
#endif
