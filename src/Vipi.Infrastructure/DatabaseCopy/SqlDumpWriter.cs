using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Vipi.Application.Diagnostics;

namespace Vipi.Infrastructure.DatabaseCopy;

/// <summary>Le righe di testata della copia: chi l'ha scritta, da dove, e che cosa ha lasciato fuori.</summary>
public sealed record DumpHeader(
    string SiteVersion,
    DateTime CreatedUtc,
    string ServerVersion,
    string? LastMigration,
    IReadOnlyList<string> Excluded);

/// <summary>
/// Scrive una copia del database in testo SQL per MariaDB, <b>riga per riga</b> su uno stream che non si
/// riavvolge (la risposta HTTP, o il gzip sopra di lei). Non conosce né la connessione né il provider: riceve
/// tabelle e valori da chi legge (<c>MySqlDumpSource</c>), quindi si prova senza un server.
///
/// <para>🔴 <b>L'ultima riga dice che il file è intero.</b> <c>-- vipi-backup-fine tabelle=… righe=…
/// sha256=…</c>, dove l'impronta copre <b>tutti i byte scritti prima</b>. Un download interrotto non ce l'ha,
/// un file ritoccato non torna: lo controlla <see cref="SqlDumpVerifier"/>.</para>
///
/// <para>Il formato del file è in <c>docs/feature/2026-09-16-copia-del-database.md</c> §2.</para>
/// </summary>
public sealed class SqlDumpWriter : IDisposable
{
    /// <summary>La prima riga: riconosce il file, e ne dice il formato.</summary>
    public const string Magic = "-- vipi-backup formato=1";

    /// <summary>La riga che chiude ogni tabella: <c>-- vipi-tabella `Nome` righe=N</c>.</summary>
    public const string TablePrefix = "-- vipi-tabella ";

    /// <summary>L'ultima riga del file.</summary>
    public const string TrailerPrefix = "-- vipi-backup-fine ";

    /// <summary>
    /// Oltre questa lunghezza l'<c>INSERT</c> si chiude e se ne apre un altro. Un megabyte sta largo sotto il
    /// <c>max_allowed_packet</c> di MariaDB (16 MB di default, sia al server sia al client <c>mariadb</c>);
    /// una riga sola più lunga — un'immagine da 3 MB diventa 6 MB di esadecimale — esce comunque in un
    /// <c>INSERT</c> suo.
    /// </summary>
    internal const int StatementChars = 1024 * 1024;

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Stream _destination;
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly StringBuilder _statement = new();

    private long _bytes;
    private int _tables;
    private long _rows;

    private string? _table;
    private IReadOnlyList<string> _columns = Array.Empty<string>();
    private string _insertHead = "";
    private long _tableRows;
    private bool _finished;

    public SqlDumpWriter(Stream destination) => _destination = destination;

    public async Task WriteHeaderAsync(DumpHeader header, CancellationToken ct = default)
    {
        await LineAsync(Magic, ct);
        await LineAsync("-- vIPI - copia di sicurezza del database", ct);
        await LineAsync($"-- Versione del sito: {OneLine(header.SiteVersion)}", ct);
        await LineAsync($"-- Creata: {header.CreatedUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)}", ct);
        await LineAsync($"-- Server: {OneLine(header.ServerVersion)}", ct);
        await LineAsync($"-- Ultima migrazione: {OneLine(header.LastMigration ?? "(nessuna)")}", ct);
        if (header.Excluded.Count > 0)
            await LineAsync($"-- Escluse di proposito: {string.Join(", ", header.Excluded.Select(OneLine))}", ct);
        await LineAsync("-- Ripristino: gunzip -c <file> | mariadb -u <utente> -p <database>", ct);
        await LineAsync("-- ATTENZIONE: SOSTITUISCE le tabelle che contiene (DROP TABLE IF EXISTS).", ct);
        await LineAsync("", ct);
        await LineAsync("SET NAMES utf8mb4;", ct);
        await LineAsync("SET @VIPI_OLD_FK=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0;", ct);
        await LineAsync("SET @VIPI_OLD_UQ=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0;", ct);
        // NO_AUTO_VALUE_ON_ZERO: una riga con Id 0 deve rientrare con Id 0, non prendere il prossimo contatore.
        // E senza NO_BACKSLASH_ESCAPES, che è la lingua in cui SqlLiteral scrive le stringhe.
        await LineAsync("SET @VIPI_OLD_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO';", ct);
        await LineAsync("SET @VIPI_OLD_TZ=@@TIME_ZONE, TIME_ZONE='+00:00';", ct);
    }

    /// <summary>Apre una tabella: la cancella e la ricrea com'è sul server.</summary>
    /// <param name="createStatement">Il testo di <c>SHOW CREATE TABLE</c>, senza il punto e virgola.</param>
    /// <param name="columns">I nomi delle colonne, nell'ordine in cui arriveranno i valori.</param>
    public async Task BeginTableAsync(string name, string createStatement, IReadOnlyList<string> columns, CancellationToken ct = default)
    {
        if (_table is not null) throw new InvalidOperationException($"La tabella {_table} non è stata chiusa.");
        _table = name;
        _columns = columns;
        _tableRows = 0;
        _insertHead = $"INSERT INTO {SqlLiteral.Identifier(name)} ({string.Join(",", columns.Select(SqlLiteral.Identifier))}) VALUES ";

        await LineAsync("", ct);
        await LineAsync($"DROP TABLE IF EXISTS {SqlLiteral.Identifier(name)};", ct);
        await LineAsync(createStatement.TrimEnd().TrimEnd(';') + ";", ct);
    }

    public async Task WriteRowAsync(IReadOnlyList<object?> values, CancellationToken ct = default)
    {
        if (_table is null) throw new InvalidOperationException("Nessuna tabella aperta.");
        if (values.Count != _columns.Count)
            throw new InvalidOperationException($"{_table}: {values.Count} valori per {_columns.Count} colonne.");

        _statement.Append(_statement.Length == 0 ? _insertHead : ",");
        _statement.Append('(');
        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0) _statement.Append(',');
            _statement.Append(SqlLiteral.Format(values[i], $"{_table}.{_columns[i]}"));
        }
        _statement.Append(')');
        _tableRows++;

        if (_statement.Length >= StatementChars) await FlushStatementAsync(ct);
    }

    public async Task EndTableAsync(CancellationToken ct = default)
    {
        if (_table is null) throw new InvalidOperationException("Nessuna tabella aperta.");
        await FlushStatementAsync(ct);
        await LineAsync($"{TablePrefix}{SqlLiteral.Identifier(_table)} righe={_tableRows.ToString(CultureInfo.InvariantCulture)}", ct);
        _tables++;
        _rows += _tableRows;
        _table = null;
    }

    /// <summary>Rimette le impostazioni della sessione e scrive la riga di chiusura. Da qui in poi il file è
    /// intero.</summary>
    public async Task<DatabaseBackupSummary> FinishAsync(CancellationToken ct = default)
    {
        if (_table is not null) throw new InvalidOperationException($"La tabella {_table} non è stata chiusa.");
        if (_finished) throw new InvalidOperationException("La copia è già chiusa.");

        await LineAsync("", ct);
        await LineAsync("SET TIME_ZONE=@VIPI_OLD_TZ;", ct);
        await LineAsync("SET SQL_MODE=@VIPI_OLD_MODE;", ct);
        await LineAsync("SET UNIQUE_CHECKS=@VIPI_OLD_UQ;", ct);
        await LineAsync("SET FOREIGN_KEY_CHECKS=@VIPI_OLD_FK;", ct);

        var sha = Convert.ToHexString(_hash.GetHashAndReset()).ToLowerInvariant();
        var summary = new DatabaseBackupSummary(_tables, _rows, _bytes, sha);

        // La riga di chiusura NON entra nell'impronta: l'impronta sta dentro di lei.
        var trailer = Utf8.GetBytes(Trailer(summary) + "\n");
        await _destination.WriteAsync(trailer, ct);
        await _destination.FlushAsync(ct);
        _finished = true;
        return summary;
    }

    /// <summary>Il testo della riga di chiusura (senza a capo). Il verificatore la rilegge con la stessa forma.</summary>
    public static string Trailer(DatabaseBackupSummary s) =>
        string.Create(CultureInfo.InvariantCulture, $"{TrailerPrefix}tabelle={s.Tables} righe={s.Rows} sha256={s.Sha256}");

    private async Task FlushStatementAsync(CancellationToken ct)
    {
        if (_statement.Length == 0) return;
        _statement.Append(';');
        var testo = _statement.ToString();
        _statement.Clear();
        await LineAsync(testo, ct);
    }

    private async Task LineAsync(string line, CancellationToken ct)
    {
        var bytes = Utf8.GetBytes(line + "\n");
        _hash.AppendData(bytes);
        _bytes += bytes.Length;
        await _destination.WriteAsync(bytes, ct);
    }

    /// <summary>Un valore di testata che arriva da fuori non deve poter aprire una riga nuova nel file.</summary>
    private static string OneLine(string s) => s.Replace('\r', ' ').Replace('\n', ' ');

    public void Dispose() => _hash.Dispose();
}
