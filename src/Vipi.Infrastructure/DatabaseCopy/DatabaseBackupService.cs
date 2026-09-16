using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vipi.Application;
using Vipi.Application.Auth;
using Vipi.Application.Diagnostics;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.DatabaseCopy;

/// <summary>
/// La copia di sicurezza del database: cancello, registro, compressione. Leggere il database lo fa
/// <see cref="IDatabaseDumpSource"/>, scrivere il testo <see cref="SqlDumpWriter"/>.
/// Carta <c>docs/feature/2026-09-16-copia-del-database.md</c>.
/// </summary>
public sealed class DatabaseBackupService : IDatabaseBackup
{
    /// <summary>Tipo di entità nel registro di audit.</summary>
    internal const string Entita = "DatabaseBackup";
    internal const string IdEntita = "database";

    /// <summary>
    /// Una copia alla volta, <b>per processo</b>. Una copia legge tutto il database, e il sito è un processo
    /// solo: due in parallelo (un doppio clic, due schede) raddoppiano il costo senza dare niente in più.
    /// </summary>
    private static int _inCorso;

    private readonly VipiDbContext _db;
    private readonly IEditAuthorizationService _authz;
    private readonly IDatabaseDumpSource? _source;
    private readonly string? _versione;
    private readonly ILogger _log;

    public DatabaseBackupService(VipiDbContext db, IEditAuthorizationService authz,
        IEnumerable<IDatabaseDumpSource> sources, IOptions<VipiChromeOptions> chrome,
        ILogger<DatabaseBackupService>? log = null)
    {
        _db = db;
        _authz = authz;
        _source = sources.FirstOrDefault();
        _versione = chrome.Value.Versione;
        _log = (ILogger?)log ?? NullLogger.Instance;
    }

    public bool IsSupported => _source is not null;

    public string FileName(DateTime utc)
    {
        // «1.28.0 · 4fd8ed7» → «1.28.0-4fd8ed7»: un nome di file senza spazi né punti mediani.
        var v = string.IsNullOrWhiteSpace(_versione)
            ? "sviluppo"
            : string.Join("-", _versione.Split(new[] { ' ', '·' }, StringSplitOptions.RemoveEmptyEntries));
        foreach (var c in Path.GetInvalidFileNameChars()) v = v.Replace(c, '-');
        return $"vipi-copia-{utc:yyyy-MM-dd-HHmm}Z-{v}.sql.gz";
    }

    public async Task<DatabaseBackupSummary> WriteAsync(Stream destination, CancellationToken ct = default)
    {
        _authz.EnsureAdmin();
        if (_source is null)
            throw new InvalidOperationException("La copia del database è disponibile solo su MariaDB/MySQL.");

        if (Interlocked.CompareExchange(ref _inCorso, 1, 0) != 0) throw new DatabaseBackupBusyException();
        try
        {
            var chi = _authz.CurrentUserId ?? 0;
            var creata = DateTime.UtcNow;

            // Tutto quello che può fallire prima di cominciare fallisce QUI, e non è ancora uscito un byte.
            await using var snapshot = await _source.OpenAsync(ct);

            // La richiesta si scrive PRIMA di spedire: se il download si interrompe, il registro dice comunque
            // chi ha portato via che cosa. La riga di fine arriva solo se la copia arriva in fondo.
            AuditScribe.Write(_db, chi, AuditAction.View, Entita, IdEntita,
                new { Fase = "Inizio", Versione = _versione }, creata);
            await _db.SaveChangesAsync(ct);

            var summary = await WriteGzipAsync(snapshot, destination, _versione ?? "sviluppo", creata, ct);

            // ⚠️ «Fine» vuol dire «il sito l'ha spedita tutta», non «il browser l'ha salvata»: quello lo dice solo
            // `verifica` sul file. E il file è già tutto dall'altra parte: se questa riga non si scrive, lo si
            // annota nel log e la copia resta buona — rilanciare qui farebbe troncare un download completo.
            try
            {
                AuditScribe.Write(_db, chi, AuditAction.View, Entita, IdEntita, new
                {
                    Fase = "Fine",
                    Tabelle = summary.Tables,
                    Righe = summary.Rows,
                    Byte = summary.Bytes,
                    IstruzioneMax = summary.LongestStatementBytes,
                    summary.Sha256,
                });
                await _db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception e)
            {
                _log.LogError(e, "Copia del database completata (sha256 {Sha}) ma la riga di fine del registro non si è scritta.", summary.Sha256);
            }
            return summary;
        }
        finally
        {
            Interlocked.Exchange(ref _inCorso, 0);
        }
    }

    /// <summary>
    /// Testata, tabelle e chiusura di una fotografia già aperta, compresse in gzip su <paramref name="destination"/>.
    /// È la copia intera senza cancello né registro: la usa questo servizio, e <c>tools/Vipi.DbBackup scrivi</c>
    /// per l'andata e ritorno della CI — così il file che prova la CI è lo stesso che scarica l'Admin.
    /// </summary>
    public static async Task<DatabaseBackupSummary> WriteGzipAsync(IDumpSnapshot snapshot, Stream destination,
        string siteVersion, DateTime createdUtc, CancellationToken ct = default)
    {
        DatabaseBackupSummary summary;
        // 🔴 Il gzip scrive la SUA chiusura quando si smaltisce, anche mentre un'eccezione risale. Senza il taglio,
        // una copia fallita a metà sarebbe un gzip perfetto che `gunzip -t` promuove e `gunzip | mariadb`
        // esegue per la metà che c'è (revisione del 16 settembre 2026). Col taglio, dopo un guasto nessun byte
        // arriva più a destinazione: il file resta un gzip troncato, e lo dice anche gunzip.
        var taglio = new TaglioStream(destination);
        // ⚠️ `await using` e non `using`: Kestrel rifiuta le scritture sincrone sul corpo della risposta.
        await using (var gz = new GZipStream(taglio, CompressionLevel.Optimal, leaveOpen: true))
        {
            try
            {
                using var writer = new SqlDumpWriter(gz);
                await writer.WriteHeaderAsync(new DumpHeader(
                    siteVersion, createdUtc, snapshot.ServerVersion, snapshot.LastMigration, snapshot.Excluded), ct);
                await snapshot.WriteTablesAsync(writer, ct);
                summary = await writer.FinishAsync(ct);
            }
            catch
            {
                taglio.Taglia();
                throw;
            }
        }
        await destination.FlushAsync(ct);
        return summary;
    }

    public async Task<DatabaseBackupLast?> LastAsync(CancellationToken ct = default)
    {
        var righe = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == Entita)
            .OrderByDescending(a => a.Id)
            .Take(20)
            .Select(a => new { a.Id, a.UserId, a.TimestampUtc, a.DetailsJson })
            .ToListAsync(ct);

        var inizio = righe.FirstOrDefault(r => Fase(r.DetailsJson) == "Inizio");
        if (inizio is null) return null;

        // La fine è la prima riga «Fine» dello stesso VID DOPO quell'inizio: le righe sono più recenti prima.
        var fine = righe.LastOrDefault(r => r.Id > inizio.Id && r.UserId == inizio.UserId && Fase(r.DetailsJson) == "Fine");
        return new DatabaseBackupLast(inizio.TimestampUtc, inizio.UserId, fine is null ? null : Riassunto(fine.DetailsJson));
    }

    private static string? Fase(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var d = JsonDocument.Parse(json);
            return d.RootElement.TryGetProperty("Fase", out var f) ? f.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    private static DatabaseBackupSummary? Riassunto(string? json)
    {
        try
        {
            using var d = JsonDocument.Parse(json!);
            var r = d.RootElement;
            return new DatabaseBackupSummary(
                r.GetProperty("Tabelle").GetInt32(), r.GetProperty("Righe").GetInt64(),
                r.GetProperty("Byte").GetInt64(), r.GetProperty("Sha256").GetString() ?? "",
                r.TryGetProperty("IstruzioneMax", out var im) ? im.GetInt64() : 0);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentNullException)
        {
            return null;
        }
    }
}

/// <summary>
/// Passa le scritture a <paramref name="inner"/> finché qualcuno non chiama <see cref="Taglia"/>; da lì in poi le
/// butta. Serve a impedire che un gzip chiuso durante un guasto aggiunga in coda la sua chiusura regolare.
/// </summary>
internal sealed class TaglioStream(Stream inner) : Stream
{
    private bool _tagliato;

    public void Taglia() => _tagliato = true;

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (!_tagliato) inner.Write(buffer, offset, count);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) =>
        _tagliato ? ValueTask.CompletedTask : inner.WriteAsync(buffer, ct);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
        _tagliato ? Task.CompletedTask : inner.WriteAsync(buffer, offset, count, ct);

    public override void Flush()
    {
        if (!_tagliato) inner.Flush();
    }

    public override Task FlushAsync(CancellationToken ct) =>
        _tagliato ? Task.CompletedTask : inner.FlushAsync(ct);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
