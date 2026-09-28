using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Update;
#pragma warning disable EF1001
using Pomelo.EntityFrameworkCore.MySql.Infrastructure.Internal;
#pragma warning restore EF1001
using Pomelo.EntityFrameworkCore.MySql.Migrations;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Il generatore delle migrazioni MariaDB, con le DDL scritte in modo da potersi <b>rieseguire</b>.
///
/// <para>🔴 <b>Perché esiste</b> (U-096, revisione totale 3). Su MariaDB ogni DDL fa commit da sé: una migrazione
/// di più istruzioni interrotta all'istruzione k — il SIGTERM delle hh:56, un secondo <c>restart.txt</c>, un
/// errore di dati su un indice — lascia applicate le prime k−1 senza la riga in <c>__EFMigrationsHistory</c>.
/// L'avvio dopo riparte dalla prima, che cade sullo schema già cambiato («Duplicate column name»), e da lì cade
/// ogni avvio: il sito resta giù finché qualcuno non sistema lo schema a mano.</para>
///
/// <para>Con <c>IF [NOT] EXISTS</c> ogni istruzione già fatta diventa un avviso di MariaDB invece di un errore,
/// e l'avvio dopo finisce la migrazione dal punto in cui si era fermata. Si riscrive il testo che Pomelo ha già
/// generato, e solo nelle forme che Pomelo scrive davvero (elenco in <see cref="Riscritture"/>): una forma
/// nuova resta com'è, e <c>MigrazioniRieseguibiliTests</c> diventa rosso finché non la si aggiunge qui o la si
/// dichiara rieseguibile per natura.</para>
///
/// <para>⚠️ La SQL scritta a mano con <c>migrationBuilder.Sql(...)</c> non passa di qui in nessuna forma utile:
/// chi la scrive la scrive rieseguibile (un <c>UPDATE ... WHERE</c> che al secondo giro non trova niente, un
/// <c>CREATE OR REPLACE VIEW</c>). La regola è in <c>docs/adr/adr-0007-produzione-persistenza-e-scala.md</c>.</para>
/// </summary>
public sealed class MigrazioniRieseguibili : MySqlMigrationsSqlGenerator
{
    // ⚠️ IMySqlOptions è un'API «interna» di Pomelo (EF1001), ma è l'unico modo di derivarne il generatore. Il
    // pacchetto è fissato nel lock file; se un aggiornamento la cambia, rompe la compilazione e non l'avvio.
#pragma warning disable EF1001
    public MigrazioniRieseguibili(MigrationsSqlGeneratorDependencies dependencies,
        ICommandBatchPreparer commandBatchPreparer, IMySqlOptions options)
        : base(dependencies, commandBatchPreparer, options)
    {
    }
#pragma warning restore EF1001

    public override IReadOnlyList<MigrationCommand> Generate(IReadOnlyList<MigrationOperation> operations,
        IModel? model = null, MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
    {
        var comandi = base.Generate(operations, model, options);
        var risultato = new List<MigrationCommand>(comandi.Count);
        foreach (var c in comandi)
        {
            var testo = RendiRieseguibile(c.CommandText);
            if (ReferenceEquals(testo, c.CommandText))
            {
                risultato.Add(c);
                continue;
            }
            var comando = Dependencies.CommandBuilderFactory.Create().Append(testo).Build();
            risultato.Add(new MigrationCommand(comando, Dependencies.CurrentContext.Context, c.CommandLogger,
                c.TransactionSuppressed));
        }
        return risultato;
    }

    private const string Nome = @"`[^`]+`";

    /// <summary>
    /// Le forme che Pomelo genera e la loro versione rieseguibile. Ancorate all'inizio dell'istruzione: una
    /// parola uguale dentro una stringa o un commento non si tocca.
    /// </summary>
    internal static readonly (Regex Forma, string Diventa)[] Riscritture =
    {
        (new(@"^CREATE TABLE (?!IF NOT EXISTS)"), "CREATE TABLE IF NOT EXISTS "),
        (new(@"^CREATE (UNIQUE )?INDEX (?!IF NOT EXISTS)"), "CREATE $1INDEX IF NOT EXISTS "),
        (new(@"^DROP TABLE (?!IF EXISTS)"), "DROP TABLE IF EXISTS "),
        // ADD seguito dal nome fra apici inversi è una colonna: ADD CONSTRAINT / ADD PRIMARY KEY hanno una parola.
        (new($@"^(ALTER TABLE {Nome}) ADD (?=`)"), "$1 ADD COLUMN IF NOT EXISTS "),
        // Quel che si toglie si toglie anche dalla tabella che al secondo giro non c'è più (rinominata o tolta
        // più avanti nella stessa migrazione): da qui l'IF EXISTS anche sulla tabella, e solo per i DROP. Un ADD
        // su una tabella che manca resta un errore, perché lì la tabella deve esserci.
        (new($@"^ALTER TABLE ({Nome}) DROP (COLUMN|INDEX|FOREIGN KEY) (?!IF EXISTS)"), "ALTER TABLE IF EXISTS $1 DROP $2 IF EXISTS "),
        // ⚠️ Su MariaDB IF NOT EXISTS sta DOPO «FOREIGN KEY» / «UNIQUE», non dopo il nome del vincolo:
        // «ADD CONSTRAINT IF NOT EXISTS `x` FOREIGN KEY» è un errore di sintassi (provato su 11.4.10).
        (new($@"^(ALTER TABLE {Nome} ADD CONSTRAINT {Nome}) (FOREIGN KEY|UNIQUE) (?!IF NOT EXISTS)"),
            "$1 $2 IF NOT EXISTS "),
        (new($@"^(ALTER TABLE {Nome}) RENAME COLUMN (?!IF EXISTS)"), "$1 RENAME COLUMN IF EXISTS "),
        // La tabella rinominata: al secondo giro quella vecchia non c'è più, e senza IF EXISTS è un errore.
        (new($@"^ALTER TABLE ({Nome}) RENAME ({Nome})"), "ALTER TABLE IF EXISTS $1 RENAME $2"),
    };

    /// <summary>
    /// Le istruzioni che si rieseguono già così come sono: niente da riscrivere. Servono al test di guardia per
    /// distinguere «già a posto» da «forma nuova che nessuno ha guardato».
    /// </summary>
    internal static readonly Regex RieseguibiliPerNatura = new(
        $@"^(ALTER TABLE {Nome} MODIFY COLUMN |ALTER TABLE {Nome} ALTER COLUMN {Nome} (SET|DROP) DEFAULT|" +
        @"CREATE OR REPLACE |DROP PROCEDURE IF EXISTS |INSERT INTO `__EFMigrationsHistory`|" +
        @"DELETE FROM `__EFMigrationsHistory`|START TRANSACTION|COMMIT|ALTER DATABASE |" +
        // La SQL a mano che le migrazioni hanno oggi: UPDATE a un valore fisso e DELETE, che al secondo giro
        // non trovano niente da cambiare. Un INSERT su una tabella nostra NON è in elenco, e non per caso.
        @"UPDATE |DELETE FROM )");

    /// <summary>
    /// La stessa istruzione, rieseguibile. Se nessuna forma riconosciuta combacia, torna <b>la stessa istanza</b>
    /// di stringa: chi chiama lo usa per non ricostruire il comando.
    /// </summary>
    public static string RendiRieseguibile(string sql)
    {
        var inizio = sql.Length - sql.TrimStart().Length;
        var corpo = sql[inizio..];
        foreach (var (forma, diventa) in Riscritture)
        {
            if (forma.IsMatch(corpo))
                return sql[..inizio] + forma.Replace(corpo, diventa, 1);
        }
        return sql;
    }

    /// <summary>La forma è già rieseguibile, o lo diventa passando di qui.</summary>
    internal static bool Riconosciuta(string sql)
    {
        var corpo = RendiRieseguibile(sql).TrimStart();
        return RieseguibiliPerNatura.IsMatch(corpo)
            || corpo.Contains(" IF NOT EXISTS ", StringComparison.Ordinal)
            || corpo.Contains(" IF EXISTS ", StringComparison.Ordinal);
    }
}
