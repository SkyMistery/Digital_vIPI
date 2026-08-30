#if NET8_0
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Vipi.Infrastructure.MySqlMigrations;
using Vipi.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La guardia della <b>finestra cieca</b>: dal 31 agosto al 16 settembre 2026 non si può consegnare un
/// database, e una migrazione che va male non si può riparare.
///
/// <para><b>Perché esiste, in tre fatti.</b> In produzione il provider è MySQL e
/// <c>MigrateVipiDatabase</c> chiama <c>Database.Migrate()</c> <b>all'avvio</b>: il committente carica il
/// pacchetto via FTP, Passenger rigenera il processo, e le migrazioni girano da sole (è successo il 23
/// agosto 2026, voce A12 di <c>docs/lavori-aperti.md</c>). Il DDL di MariaDB <b>non è transazionale</b>,
/// quindi una migrazione che si ferma a metà lascia lo schema a metà. E fino al 16 settembre <b>nessuno
/// può rimettere in piedi il database</b>: chi lo amministra è in ferie, e la consegna del 30 agosto è
/// l'ultima. Le tre cose insieme fanno una sola frase: <b>in questa finestra una migrazione sbagliata è
/// il sito giù, e giù resta</b>.</para>
///
/// <para><b>Perché una finestra e non una regola per sempre.</b> Togliere una colonna è lecito e questo
/// progetto lo fa spesso — quattro migrazioni non additive fra il 24 e il 30 agosto su ventidue. Non è una
/// cattiva abitudine da correggere: è normale amministrazione <i>quando c'è una rete sotto</i>. Qui la rete
/// non c'è, e solo per diciassette giorni.</para>
///
/// <para>⚠️ <b>Quando la finestra si chiude</b> (16 settembre 2026, o alla prima consegna di database
/// successiva) questo file va <b>cancellato</b>, non aggiornato spostando le date in avanti: sarebbe una
/// regola permanente travestita da eccezione temporanea. Se serve di nuovo, si riscrive con le date vere e
/// la ragione vera.</para>
///
/// <para><b>Non è un divieto senza uscita.</b> Una migrazione distruttiva che serve davvero si può fare:
/// si aggiunge il suo id a <see cref="RevisionateAMano"/> con la ragione. Il punto non è impedirla, è che
/// non possa succedere <b>per distrazione</b> — e che chi la fa lo scriva nel diff, dove si vede in
/// revisione.</para>
///
/// <para>Il set guardato è quello <b>MySQL</b>: è l'unico che gira in produzione. Le migrazioni SQLite
/// sono di sviluppo, e su una copia di lavoro un guasto costa un ripristino di file.</para>
/// </summary>
public class MigrazioniDellaFinestraCiecaTests
{
    private readonly ITestOutputHelper _out;
    public MigrazioniDellaFinestraCiecaTests(ITestOutputHelper output) => _out = output;

    /// <summary>
    /// Primo id di migrazione dentro la finestra. Il dump del 30 agosto porta con sé lo schema <b>e</b>
    /// <c>__EFMigrationsHistory</c>: tutto ciò che è datato fino a quel giorno arriva in produzione già
    /// applicato e non gira. Il rischio comincia dalla prima migrazione emessa dopo.
    /// </summary>
    private const string PrimoIdDellaFinestra = "20260831";

    /// <summary>
    /// Primo id <b>fuori</b> dalla finestra: il 16 settembre si può consegnare di nuovo un database, quindi
    /// dal 17 vale di nuovo il regime normale. Confronto lessicografico sui primi otto caratteri dell'id,
    /// che sono <c>yyyyMMdd</c>: è l'unico dato di data che EF mette nel nome della migrazione.
    /// </summary>
    private const string PrimoIdDopoLaFinestra = "20260917";

    /// <summary>
    /// Le operazioni che, applicate da sole su dati veri e senza nessuno che guardi, possono lasciare il
    /// database in uno stato da cui l'applicazione non riparte.
    ///
    /// <para><c>DropTable</c> e <c>DropColumn</c> buttano dati che nessun <c>Down</c> restituisce — il
    /// <c>Down</c> ricrea la struttura vuota, e lo dice il commento di
    /// <c>DropLegacyTransferTables</c>. <c>RenameColumn</c> e <c>RenameTable</c> hanno la stessa proprietà
    /// se qualcosa a valle si aspetta il nome vecchio. <c>AlterColumn</c> su MariaDB <b>riscrive la
    /// tabella</b>: su <c>AtcSessions</c>, che a regime cresce di parecchie centinaia di migliaia di
    /// righe, è un blocco lungo mentre Passenger aspetta l'avvio.</para>
    ///
    /// <para><c>Sql</c> è la più insidiosa: è codice che nessun tipo controlla, scritto una volta e
    /// eseguito su un archivio che non è quello su cui è stato provato.</para>
    /// </summary>
    private static readonly HashSet<string> OperazioniVietate = new(StringComparer.Ordinal)
    {
        nameof(DropTableOperation),
        nameof(DropColumnOperation),
        nameof(RenameTableOperation),
        nameof(RenameColumnOperation),
        nameof(AlterColumnOperation),
        nameof(SqlOperation),
    };

    /// <summary>
    /// Le migrazioni della finestra che sono state <b>guardate a mano</b> e approvate lo stesso.
    ///
    /// <para>Si aggiunge l'id per intero (<c>20260902103000_NomeDellaMigrazione</c>) e, accanto, la ragione:
    /// perché quell'operazione è necessaria adesso invece che dopo il 16 settembre, e che cosa succede se
    /// fallisce a metà. Una voce senza ragione è una voce che nessuno ha valutato.</para>
    ///
    /// <para>Nasce vuota, ed è giusto che si veda: al 30 agosto 2026 nella finestra non c'è ancora niente.</para>
    /// </summary>
    private static readonly HashSet<string> RevisionateAMano = new(StringComparer.Ordinal)
    {
        // (nessuna)
    };

    /// <summary>Stesso wiring dell'host: provider MySQL + assembly di migrazioni dedicato.</summary>
    private static VipiDbContext Contesto() =>
        new MySqlMigrationsDesignTimeFactory().CreateDbContext([]);

    /// <summary>
    /// Le migrazioni della finestra, già istanziate: <c>UpOperations</c> è il modo <b>strutturale</b> di
    /// leggerle. Cercare «DropColumn» nel testo del <c>.cs</c> troverebbe anche i <c>Down</c>, che sono
    /// l'inverso di una <c>CreateTable</c> e non fanno niente di male — è l'errore che rende inutile un
    /// controllo del genere fatto con grep.
    /// </summary>
    private static IEnumerable<(string Id, Migration Migrazione)> NellaFinestra(VipiDbContext db)
    {
        var assembly = db.GetService<IMigrationsAssembly>();
        var provider = db.GetService<IDatabaseProvider>().Name;

        foreach (var (id, tipo) in assembly.Migrations.OrderBy(m => m.Key, StringComparer.Ordinal))
        {
            if (string.CompareOrdinal(id, PrimoIdDellaFinestra) < 0) continue;
            if (string.CompareOrdinal(id, PrimoIdDopoLaFinestra) >= 0) continue;
            if (RevisionateAMano.Contains(id)) continue;

            yield return (id, assembly.CreateMigration(tipo, provider));
        }
    }

    /// <summary>
    /// Vero se la colonna aggiunta è una stringa NOT NULL che nasce senza un valore di riposo vero. È il
    /// predicato usato dal controllo <b>e</b> dal suo auto-test: se fosse scritto due volte, l'auto-test
    /// proverebbe la copia e non la guardia.
    /// </summary>
    private static bool DefaultDiRiposoMancante(AddColumnOperation a) =>
        !a.IsNullable
        && a.ClrType == typeof(string)
        && a.DefaultValue is not string { Length: > 0 }
        && a.DefaultValueSql is not { Length: > 0 };

    /// <summary>
    /// Vero se l'indice unico cade su una tabella che <b>non</b> nasce nella stessa migrazione, cioè su
    /// righe che esistono già e che possono contenere un doppione.
    /// </summary>
    private static bool IndiceUnicoRischioso(CreateIndexOperation i, IReadOnlySet<string> nateQui) =>
        i.IsUnique && !nateQui.Contains(i.Table);

    /// <summary>
    /// La guardia principale: dentro la finestra si aggiunge, non si toglie e non si trasforma.
    ///
    /// <para>Rimedio quando fallisce, in ordine di preferenza: (1) <b>rimandare</b> l'operazione a dopo il
    /// 16 settembre — la colonna che non serve più non fa male a nessuno finché resta lì; (2) riscrivere la
    /// migrazione in forma additiva (colonna nuova accanto alla vecchia, invece di rinominare); (3) se
    /// proprio non si può, aggiungere l'id a <see cref="RevisionateAMano"/> con la ragione scritta.</para>
    /// </summary>
    [Fact]
    public void Nessuna_migrazione_della_finestra_cieca_e_distruttiva()
    {
        using var db = Contesto();

        var colpevoli = new List<string>();
        foreach (var (id, migrazione) in NellaFinestra(db))
        {
            var vietate = migrazione.UpOperations
                .Select(op => op.GetType().Name)
                .Where(OperazioniVietate.Contains)
                .ToList();

            _out.WriteLine($"{id}: {migrazione.UpOperations.Count} operazioni" +
                           (vietate.Count > 0 ? " — VIETATE: " + string.Join(", ", vietate) : ""));

            if (vietate.Count > 0)
                colpevoli.Add($"{id} → {string.Join(", ", vietate)}");
        }

        Assert.True(colpevoli.Count == 0,
            $"{colpevoli.Count} migrazioni della finestra cieca ({PrimoIdDellaFinestra}–{PrimoIdDopoLaFinestra}) " +
            "fanno operazioni che su MariaDB non si annullano e che nessuno può riparare fino al 16 settembre " +
            "2026. In quel periodo le migrazioni girano DA SOLE all'avvio in produzione, il DDL non è " +
            "transazionale e non c'è nessuno che possa ripristinare il database.\n  " +
            string.Join("\n  ", colpevoli) +
            "\nVedi il commento di " + nameof(MigrazioniDellaFinestraCiecaTests) + " per le tre vie d'uscita.");
    }

    /// <summary>
    /// Una colonna stringa <b>non nullabile</b> aggiunta a una tabella che ha già righe deve nascere con un
    /// valore di riposo <b>vero</b>, non col <c>""</c> che EF mette quando nessuno gliene dà uno.
    ///
    /// <para><b>Non è pedanteria, ed è già stato pagato.</b> Le colonne enum di questo modello si salvano
    /// come stringa: <c>ShapeSource</c>, <c>Kind</c>, <c>Status</c>, <c>Origin</c>. Una riga storica con il
    /// vuoto dentro <b>non si rilegge più</b> — nessun valore dell'enum si chiama <c>""</c>, e il
    /// materializzatore lancia al primo caricamento. Il guasto non arriva dalla migrazione, che passa
    /// tranquilla: arriva dopo, da una pagina che smette di aprirsi. È esattamente ciò che il commento di
    /// <c>FormaCheHaContato</c> ha evitato a mano scegliendo <c>defaultValue: "Source"</c>, e ciò che la
    /// trappola dei flag opt-out (<c>ImportSids</c>, luglio 2026) aveva già insegnato in un'altra forma.</para>
    ///
    /// <para>Fuori dalla finestra il difetto si ripara con una consegna di dati. Dentro, no.</para>
    /// </summary>
    [Fact]
    public void Nessuna_colonna_stringa_non_nullabile_nasce_col_default_vuoto()
    {
        using var db = Contesto();

        var colpevoli = new List<string>();
        foreach (var (id, migrazione) in NellaFinestra(db))
            foreach (var add in migrazione.UpOperations.OfType<AddColumnOperation>())
                if (DefaultDiRiposoMancante(add))
                    colpevoli.Add($"{id} → {add.Table}.{add.Name}");

        Assert.True(colpevoli.Count == 0,
            $"{colpevoli.Count} colonne stringa NOT NULL nascono col default vuoto. Se la colonna è un enum " +
            "salvato come stringa, le righe già in archivio diventano illeggibili al primo caricamento — la " +
            "migrazione passa e il guasto esce altrove. Dare un defaultValue che sia un valore vero del " +
            "dominio (il valore di riposo: «questa riga si comporta come prima»).\n  " +
            string.Join("\n  ", colpevoli));
    }

    /// <summary>
    /// Un indice <b>unico</b> su una tabella che esiste già è l'altro modo in cui una migrazione può
    /// fermare l'avvio: se i dati di produzione hanno un doppione che quelli di sviluppo non hanno, il
    /// <c>CREATE UNIQUE INDEX</c> fallisce con un <c>Duplicate entry</c> che nomina la chiave e non le
    /// righe — e il sito non riparte.
    ///
    /// <para>⚠️ L'indice unico su una tabella <b>creata dalla stessa migrazione</b> è innocuo: nasce vuota,
    /// non c'è niente che possa collidere. Per questo si guarda solo alle tabelle preesistenti.</para>
    ///
    /// <para>Il precedente in casa: <c>ReleaseNumberPreflight</c> esiste perché quel guasto è già stato
    /// previsto una volta, per l'indice unico dei numeri di rilascio — e la difesa scelta è stata guardare
    /// i dati <b>prima</b> di migrare. Dentro la finestra la difesa è più semplice: non si aggiunge.</para>
    /// </summary>
    [Fact]
    public void Nessun_indice_unico_nuovo_su_una_tabella_che_esiste_gia()
    {
        using var db = Contesto();

        var colpevoli = new List<string>();
        foreach (var (id, migrazione) in NellaFinestra(db))
        {
            var nateQui = migrazione.UpOperations.OfType<CreateTableOperation>()
                .Select(t => t.Name)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var idx in migrazione.UpOperations.OfType<CreateIndexOperation>())
                if (IndiceUnicoRischioso(idx, nateQui))
                    colpevoli.Add($"{id} → {idx.Table} ({string.Join(", ", idx.Columns)})");
        }

        Assert.True(colpevoli.Count == 0,
            $"{colpevoli.Count} indici unici aggiunti a tabelle già popolate. Un doppione presente solo in " +
            "produzione fa fallire la migrazione all'avvio, e fino al 16 settembre 2026 nessuno può " +
            "toglierlo. Rimandare l'indice, oppure aggiungere un preflight che conti i doppioni prima di " +
            "migrare (come " + nameof(ReleaseNumberPreflight) + ").\n  " +
            string.Join("\n  ", colpevoli));
    }

    /// <summary>
    /// La finestra è definita da due costanti, e il test serve solo finché la prima viene prima della
    /// seconda. Se qualcuno le invertisse per «spegnere» la guardia, i tre controlli sopra passerebbero
    /// tutti senza guardare niente — verdi e inutili, che è il modo peggiore in cui un presidio muore.
    /// </summary>
    [Fact]
    public void La_finestra_e_un_intervallo_vero()
    {
        Assert.True(string.CompareOrdinal(PrimoIdDellaFinestra, PrimoIdDopoLaFinestra) < 0,
            $"la finestra cieca è vuota ({PrimoIdDellaFinestra} ≥ {PrimoIdDopoLaFinestra}): i controlli di " +
            "questa classe non guarderebbero nessuna migrazione. Se la finestra è finita, cancellare il file " +
            "invece di svuotarlo.");
    }

    /// <summary>
    /// Il presidio dei presidi: che i tre controlli sappiano <b>riconoscere</b> ciò che cercano.
    ///
    /// <para>Le tre guardie sopra hanno una proprietà scomoda: finché nella finestra non c'è nessuna
    /// migrazione — cioè adesso, e sperabilmente per un po' — passano <b>senza esaminare niente</b>. Un
    /// verde così non distingue «nessuna migrazione pericolosa» da «il filtro è rotto e non vede
    /// nulla». Qui si costruiscono a mano le operazioni incriminate e si verifica che i predicati le
    /// riconoscano, senza dipendere da che cosa ci sia davvero nell'assembly.</para>
    /// </summary>
    [Fact]
    public void I_controlli_riconoscono_le_operazioni_che_cercano()
    {
        Assert.Contains(nameof(DropColumnOperation), OperazioniVietate);
        Assert.Contains(nameof(SqlOperation), OperazioniVietate);
        Assert.DoesNotContain(nameof(CreateTableOperation), OperazioniVietate);
        Assert.DoesNotContain(nameof(AddColumnOperation), OperazioniVietate);

        // Il default vuoto si riconosce, quello vero passa: sono i due casi di FormaCheHaContato.
        var vuota = new AddColumnOperation
        {
            Table = "AtcSessionTraffic", Name = "ShapeSource",
            ClrType = typeof(string), IsNullable = false, DefaultValue = "",
        };
        var piena = new AddColumnOperation
        {
            Table = "AtcSessionTraffic", Name = "ShapeSource",
            ClrType = typeof(string), IsNullable = false, DefaultValue = "Source",
        };
        Assert.True(DefaultDiRiposoMancante(vuota));
        Assert.False(DefaultDiRiposoMancante(piena));

        // Una colonna nullabile non ha bisogno di nessun valore di riposo: il vuoto lì è NULL, che si rilegge.
        Assert.False(DefaultDiRiposoMancante(new AddColumnOperation
        {
            Table = "AtcSessionTraffic", Name = "ShapeSource",
            ClrType = typeof(string), IsNullable = true,
        }));

        // Un indice unico su tabella nuova è innocuo, sulla stessa tabella preesistente no.
        var indice = new CreateIndexOperation { Table = "AtcSessions", IsUnique = true, Columns = ["Callsign"] };
        Assert.False(IndiceUnicoRischioso(indice, new HashSet<string>(StringComparer.Ordinal) { "AtcSessions" }));
        Assert.True(IndiceUnicoRischioso(indice, new HashSet<string>(StringComparer.Ordinal)));
    }
}
#endif
