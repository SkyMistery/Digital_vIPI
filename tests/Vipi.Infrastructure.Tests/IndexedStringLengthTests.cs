using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Vipi.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Presidia la copertura di <see cref="MySqlStringLengths"/>: ogni proprietà stringa che partecipa a un
/// indice, a una chiave o a una foreign key — <b>o che ha un valore di default</b> — deve avere una
/// lunghezza dichiarata, e non oltre il tetto indicizzabile di InnoDB.
///
/// <para>Le due condizioni hanno cause diverse e la seconda è meno intuitiva. Una stringa senza lunghezza
/// diventa <c>longtext</c>, e <c>longtext</c> non si può <b>indicizzare</b> (limite di 3072 byte) ma
/// nemmeno gli si può dare un <b>default</b>: MySQL risponde «BLOB, TEXT, GEOMETRY or JSON column X can't
/// have a default value» e la migrazione si ferma. Il caso concreto è <c>DocumentSection.RenderMode</c>,
/// un enum salvato come stringa con <c>HasDefaultValue(Frozen)</c> che non è indicizzato: la prima
/// versione di questo test non lo copriva, e infatti è saltato fuori applicando le migrazioni a un MySQL
/// vero, non generando la DDL.</para>
///
/// <para>Il rischio vero non è oggi — è la colonna che qualcuno aggiungerà fra tre mesi senza sapere che
/// esiste un provider a cui la lunghezza serve. Quel fallimento arriverebbe al deploy; questo test lo
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

    /// <summary>
    /// Il presupposto della regola sugli enum (<see cref="MySqlStringLengths.EnumChars"/>): nessun nome di
    /// valore supera i 32 caratteri. Se qualcuno ne aggiungesse uno più lungo, su MariaDB in strict mode la
    /// scrittura <b>lancerebbe</b>, e — peggio — su un server non-strict <b>troncherebbe in silenzio</b>,
    /// producendo una stringa che l'enum non sa più rileggere. Il difetto arriverebbe alla prima riga
    /// salvata con quel valore, cioè lontano da qui.
    ///
    /// <para>Il controllo gira sull'insieme reale del modello, non su un elenco scritto a mano: sono gli
    /// enum che <c>OnModelCreating</c> converte in stringa, quindi comprende anche quelli aggiunti domani.</para>
    /// </summary>
    [Fact]
    public void Nessun_nome_di_valore_di_enum_supera_la_lunghezza_della_regola()
    {
        using var db = Model();

        var troppoLunghi = db.Model.GetEntityTypes()
            .SelectMany(et => et.GetProperties())
            .Select(p => Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType)
            .Where(t => t.IsEnum)
            .Distinct()
            .SelectMany(t => Enum.GetNames(t).Select(n => (Tipo: t.Name, Nome: n)))
            .Where(x => x.Nome.Length > MySqlStringLengths.EnumChars)
            .ToList();

        Assert.True(troppoLunghi.Count == 0,
            $"valori di enum più lunghi di {MySqlStringLengths.EnumChars} caratteri, cioè della lunghezza " +
            "che MySqlStringLengths dà a ogni colonna enum: in strict mode la scrittura fallisce, senza " +
            "strict mode tronca in silenzio e il valore non si rilegge più.\n  " +
            string.Join("\n  ", troppoLunghi.Select(x => $"{x.Tipo}.{x.Nome} ({x.Nome.Length} caratteri)")));
    }

    /// <summary>
    /// L'altra metà: la regola deve <b>arrivare</b>, non solo esistere. Applicata su un modello MySQL, ogni
    /// enum salvato come stringa dev'essere uscito con una lunghezza — dalla mappa o dalla regola. Senza
    /// questo controllo, un errore nell'ordine delle due fonti in <c>Apply</c> passerebbe inosservato fino
    /// alla DDL.
    /// </summary>
    [Fact]
    public void Ogni_enum_del_modello_esce_con_una_lunghezza()
    {
        var builder = new ModelBuilder();
        // Si riproduce la sequenza di OnModelCreating: prima la conversione enum→stringa, poi le lunghezze.
        // Costruire qui il modello (invece di aprire un context MySQL) tiene il test su entrambi i TFM.
        builder.Entity<Vipi.Domain.Entities.Document>();
        builder.Entity<Vipi.Domain.Entities.ContentBlock>();
        builder.Entity<Vipi.Domain.Entities.DocumentVersion>();
        builder.Entity<Vipi.Domain.Entities.TransferFlow>();

        foreach (var et in builder.Model.GetEntityTypes())
            foreach (var p in et.GetProperties())
            {
                var t = Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType;
                if (t.IsEnum) p.SetProviderClrType(typeof(string));
            }

        MySqlStringLengths.Apply(builder);

        var senzaLunghezza = builder.Model.GetEntityTypes()
            .SelectMany(et => et.GetProperties().Select(p => (Et: et, P: p)))
            .Where(x => ((Nullable.GetUnderlyingType(x.P.ClrType) ?? x.P.ClrType)).IsEnum)
            .Where(x => x.P.GetMaxLength() is null)
            .Select(x => $"{x.Et.ClrType.Name}.{x.P.Name}")
            .ToList();

        Assert.True(senzaLunghezza.Count == 0,
            "enum salvati come stringa rimasti senza lunghezza dopo MySqlStringLengths.Apply (su MySQL " +
            "nascerebbero longtext): " + string.Join(", ", senzaLunghezza));
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

            // Colonne con un DEFAULT: non è una questione di indici ma di tipo. In MySQL una colonna
            // BLOB/TEXT non può avere un valore di default — «BLOB, TEXT, GEOMETRY or JSON column X can't
            // have a default value» — e una stringa senza lunghezza è longtext. Quindi anche queste vanno
            // dimensionate, o la migrazione si ferma al CREATE TABLE.
            //
            // Si guardano le ANNOTAZIONI e non GetDefaultValue(): quest'ultimo risponde anche per gli enum
            // salvati come stringa, dove il sentinella è il valore zero convertito, e segnalerebbe 21
            // colonne che nessuno ha configurato e per cui EF non emette alcun DEFAULT nella DDL.
            foreach (var p in et.GetProperties().Where(HaUnDefaultDichiarato))
                Aggiungi([p], "valore di default");
        }

        return trovate.Values.OrderBy(c => c.Entity).ThenBy(c => c.Property).ToList();
    }

    /// <summary>
    /// Vero anche per gli enum: <c>OnModelCreating</c> li salva come stringa via <c>SetProviderClrType</c>,
    /// quindi sono colonne stringa indicizzate senza sembrarlo guardando il tipo CLR. È la trappola che il
    /// piano segnala per <c>Document.Type</c>/<c>Status</c>, <c>DocRelease.TargetType</c>, <c>EditorTask.Status</c>.
    /// </summary>
    /// <summary>
    /// Un default <b>dichiarato</b> da qualcuno (<c>HasDefaultValue</c> / <c>HasDefaultValueSql</c>), che è
    /// l'unico che EF traduce in una clausola <c>DEFAULT</c> nella DDL.
    /// </summary>
    private static bool HaUnDefaultDichiarato(IProperty p) =>
        p.FindAnnotation(RelationalAnnotationNames.DefaultValue) is not null ||
        p.FindAnnotation(RelationalAnnotationNames.DefaultValueSql) is not null;

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
