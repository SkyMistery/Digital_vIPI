using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Vipi.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Presidia la copertura di <see cref="MySqlStringLengths"/>: ogni proprietà stringa che partecipa a un
/// indice, a una chiave o a una foreign key deve avere una lunghezza dichiarata, e non oltre il tetto
/// indicizzabile di InnoDB.
///
/// <para>Il rischio vero non è oggi — è la colonna indicizzata che qualcuno aggiungerà fra tre mesi senza
/// sapere che esiste un provider a cui la lunghezza serve. Su MySQL quella colonna nasce <c>longtext</c>
/// e fa fallire il <c>CREATE TABLE</c>, ma il fallimento arriverebbe solo al deploy. Questo test lo
/// anticipa in CI su net10, cioè dove il ramo MySQL non viene nemmeno costruito.</para>
///
/// <para>Il modello si costruisce con SQLite perché le lunghezze qui non si leggono dai metadati EF (sono
/// applicate solo sotto MySQL): si legge <b>quali</b> proprietà sono indicizzate, che è informazione
/// provider-indipendente.</para>
/// </summary>
public class IndexedStringLengthTests
{
    private readonly ITestOutputHelper _out;
    public IndexedStringLengthTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void Ogni_colonna_stringa_indicizzata_ha_una_lunghezza_dichiarata()
    {
        // Una lunghezza vale da qualunque delle due fonti: la mappa MySQL, o un HasMaxLength già presente
        // nel modello per tutti i provider (è il caso di MediaAsset.Sha256, fisso per definizione dell'hash).
        var mancanti = ColonneStringaIndicizzate().Where(c => c.Lunghezza is null).ToList();

        foreach (var c in mancanti)
            _out.WriteLine($"[(\"{c.Entity}\", \"{c.Property}\")] = ?,   // {c.Motivo}");

        Assert.True(mancanti.Count == 0,
            $"{mancanti.Count} colonne stringa indicizzate senza lunghezza — vanno aggiunte a " +
            $"{nameof(MySqlStringLengths)}.{nameof(MySqlStringLengths.Map)}, o su MySQL nascono longtext e " +
            "il CREATE TABLE fallisce:\n" +
            string.Join("\n", mancanti.Select(c => $"  {c.Entity}.{c.Property} — {c.Motivo}")));
    }

    /// <summary>
    /// La mappa non deve accumulare voci per colonne che nessuno indicizza più: una lunghezza rimasta lì
    /// dopo la rimozione di un indice è rumore che si legge come vincolo.
    /// </summary>
    [Fact]
    public void La_mappa_non_contiene_colonne_che_non_esistono_o_non_sono_indicizzate()
    {
        var indicizzate = ColonneStringaIndicizzate()
            .Select(c => (c.Entity, c.Property))
            .ToHashSet();

        var orfane = MySqlStringLengths.Map.Keys.Where(k => !indicizzate.Contains(k)).ToList();

        Assert.True(orfane.Count == 0,
            "voci di MySqlStringLengths.Map che non corrispondono a colonne stringa indicizzate: " +
            string.Join(", ", orfane.Select(k => $"{k.Entity}.{k.Property}")));
    }

    [Fact]
    public void Nessuna_lunghezza_supera_il_tetto_indicizzabile_di_innodb()
    {
        var oltre = MySqlStringLengths.Map
            .Where(kv => kv.Value > MySqlStringLengths.MaxIndexableChars)
            .ToList();

        Assert.True(oltre.Count == 0,
            "lunghezze oltre il tetto InnoDB di " + MySqlStringLengths.MaxIndexableChars + " caratteri: " +
            string.Join(", ", oltre.Select(kv => $"{kv.Key.Entity}.{kv.Key.Property}={kv.Value}")));
    }

    /// <summary>
    /// Le FK su chiave alternata devono avere la stessa lunghezza esatta della colonna principale, o MySQL
    /// rifiuta il vincolo con <c>errno 150</c> al <c>CREATE TABLE</c>. È il primo errore che salta fuori, e
    /// il messaggio di MySQL non dice quale delle due colonne sia il problema.
    /// </summary>
    [Fact]
    public void Le_fk_su_chiave_alternata_hanno_la_stessa_lunghezza_della_principale()
    {
        using var db = Model();
        var disallineate = new List<string>();

        foreach (var et in db.Model.GetEntityTypes())
            foreach (var fk in et.GetForeignKeys())
                for (var i = 0; i < fk.Properties.Count; i++)
                {
                    var dipendente = fk.Properties[i];
                    var principale = fk.PrincipalKey.Properties[i];
                    if (!EStringa(dipendente) || !EStringa(principale)) continue;

                    var lunghDip = Lunghezza(dipendente);
                    var lunghPrin = Lunghezza(principale);
                    if (lunghDip != lunghPrin)
                        disallineate.Add(
                            $"{dipendente.DeclaringType.ClrType.Name}.{dipendente.Name}={Descrivi(lunghDip)} → " +
                            $"{principale.DeclaringType.ClrType.Name}.{principale.Name}={Descrivi(lunghPrin)}");
                }

        Assert.True(disallineate.Count == 0,
            "FK fra colonne stringa di lunghezza diversa (MySQL errno 150):\n  " + string.Join("\n  ", disallineate));
    }

    // ---- enumerazione del modello ------------------------------------------------------------------

    private record Colonna(string Entity, string Property, string Motivo, int? Lunghezza);

    private static VipiDbContext Model()
    {
        var options = new DbContextOptionsBuilder<VipiDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        return new VipiDbContext(options);
    }

    private static List<Colonna> ColonneStringaIndicizzate()
    {
        using var db = Model();
        var trovate = new Dictionary<(string, string), Colonna>();

        void Aggiungi(IReadOnlyList<IProperty> props, string motivo)
        {
            foreach (var p in props)
            {
                if (!EStringa(p)) continue;
                var chiave = (p.DeclaringType.ClrType.Name, p.Name);
                if (!trovate.ContainsKey(chiave))
                    trovate[chiave] = new Colonna(chiave.Item1, chiave.Item2, motivo, Lunghezza(p));
            }
        }

        foreach (var et in db.Model.GetEntityTypes())
        {
            // Chiavi primarie e alternate: indicizzate per definizione.
            foreach (var k in et.GetKeys())
                Aggiungi(k.Properties, k.IsPrimaryKey() ? "chiave primaria" : "chiave alternata");

            // Indici dichiarati.
            foreach (var idx in et.GetIndexes())
                Aggiungi(idx.Properties, idx.IsUnique ? "indice unico" : "indice");

            // FK: MySQL crea da sé un indice sulla colonna dipendente, quindi vale la stessa regola.
            foreach (var fk in et.GetForeignKeys())
                Aggiungi(fk.Properties, "foreign key");
        }

        return trovate.Values.OrderBy(c => c.Entity).ThenBy(c => c.Property).ToList();
    }

    /// <summary>
    /// Vero anche per gli enum: <c>OnModelCreating</c> li salva come stringa via <c>SetProviderClrType</c>,
    /// quindi sono colonne stringa indicizzate senza sembrarlo guardando il tipo CLR. È la trappola che il
    /// piano segnala per <c>Document.Type</c>/<c>Status</c>, <c>DocRelease.TargetType</c>, <c>EditorTask.Status</c>.
    /// </summary>
    private static bool EStringa(IProperty p)
    {
        if (p.GetProviderClrType() == typeof(string)) return true;
        return (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType) == typeof(string);
    }

    private static int? Lunghezza(IProperty p)
    {
        var chiave = (p.DeclaringType.ClrType.Name, p.Name);
        return MySqlStringLengths.Map.TryGetValue(chiave, out var n) ? n : p.GetMaxLength();
    }

    private static string Descrivi(int? lunghezza) => lunghezza?.ToString() ?? "illimitata";
}
