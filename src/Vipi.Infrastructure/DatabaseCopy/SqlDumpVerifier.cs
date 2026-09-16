using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Vipi.Application.Diagnostics;

namespace Vipi.Infrastructure.DatabaseCopy;

/// <summary>L'esito del controllo di una copia.</summary>
/// <param name="Ok">Vero solo se il file è intero e coerente con la sua riga di chiusura.</param>
/// <param name="Problem">Che cosa non torna, in una frase; null se <paramref name="Ok"/>.</param>
/// <param name="Declared">Quello che dice la riga di chiusura, se c'è.</param>
/// <param name="Found">Quello che si è contato e calcolato leggendo il file.</param>
public sealed record DumpVerification(
    bool Ok,
    string? Problem,
    string? SiteVersion,
    string? CreatedUtc,
    DatabaseBackupSummary? Declared,
    DatabaseBackupSummary Found);

/// <summary>
/// Controlla una copia scritta da <see cref="SqlDumpWriter"/> <b>senza nessun database</b>: che sia intera
/// (c'è la riga di chiusura), che nessun byte sia cambiato (l'impronta torna), e che tabelle e righe contate
/// siano quelle dichiarate.
///
/// <para>⚠️ Non riesegue l'SQL e non lo interpreta: si fida delle righe <c>-- vipi-tabella</c> che lo scrittore
/// mette in fondo a ogni tabella. Quelle sono coperte dall'impronta, quindi non si possono ritoccare senza che
/// il controllo se ne accorga. Che l'SQL si reimporti davvero lo prova la CI contro un MariaDB vero.</para>
/// </summary>
public static partial class SqlDumpVerifier
{
    /// <summary>Apre un file, compresso in gzip o no (lo riconosce dai primi due byte).</summary>
    public static async Task<DumpVerification> VerifyFileAsync(string path, CancellationToken ct = default)
    {
        await using var file = File.OpenRead(path);
        return await VerifyAsync(file, ct);
    }

    /// <summary>
    /// Controlla lo stream. Se è posizionabile e comincia col marcatore gzip, lo decomprime; altrimenti lo legge
    /// così com'è.
    /// </summary>
    public static async Task<DumpVerification> VerifyAsync(Stream input, CancellationToken ct = default)
    {
        if (input.CanSeek && await IsGzipAsync(input, ct))
        {
            await using var gz = new GZipStream(input, CompressionMode.Decompress, leaveOpen: true);
            return await VerifyPlainAsync(gz, ct);
        }
        return await VerifyPlainAsync(input, ct);
    }

    private static async Task<bool> IsGzipAsync(Stream s, CancellationToken ct)
    {
        var start = s.Position;
        var head = new byte[2];
        var n = await s.ReadAsync(head, ct);
        s.Position = start;
        return n == 2 && head[0] == 0x1f && head[1] == 0x8b;
    }

    private static async Task<DumpVerification> VerifyPlainAsync(Stream input, CancellationToken ct)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var pending = new MemoryStream();
        byte[]? held = null;           // l'ultima riga completa: non si sa ancora se è la chiusura
        var first = true;
        string? problem = null;
        string? version = null, created = null;
        var tables = 0;
        long rows = 0, bytes = 0;

        void Consume(byte[] line)
        {
            // La riga trattenuta non era l'ultima: entra nell'impronta e si guarda.
            hash.AppendData(line);
            bytes += line.Length;

            if (first)
            {
                first = false;
                if (Text(line) != SqlDumpWriter.Magic)
                    problem ??= "Non è una copia della vIPI: la prima riga non è quella attesa.";
                return;
            }
            // Solo i commenti si decodificano: un INSERT può pesare megabyte e qui non serve leggerlo.
            if (line.Length < 3 || line[0] != '-' || line[1] != '-' || line[2] != ' ') return;

            var t = Text(line);
            if (t.StartsWith(SqlDumpWriter.TablePrefix, StringComparison.Ordinal))
            {
                var m = RigheDiTabella().Match(t);
                if (!m.Success) { problem ??= $"Riga di tabella illeggibile: {t}"; return; }
                tables++;
                rows += long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            }
            else if (t.StartsWith("-- Versione del sito: ", StringComparison.Ordinal)) version = t[22..];
            else if (t.StartsWith("-- Creata: ", StringComparison.Ordinal)) created = t[11..];
        }

        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            var start = 0;
            for (var i = 0; i < read; i++)
            {
                if (buffer[i] != (byte)'\n') continue;
                pending.Write(buffer, start, i - start + 1);
                start = i + 1;
                if (held is not null) Consume(held);
                held = pending.ToArray();
                pending.SetLength(0);
            }
            pending.Write(buffer, start, read - start);
        }

        var found = () => new DatabaseBackupSummary(tables, rows, bytes,
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());

        // Byte dopo l'ultimo a capo: il file si è interrotto a metà di una riga. Quella trattenuta non era la
        // chiusura, e il pezzo rimasto non può esserlo.
        if (pending.Length > 0)
        {
            if (held is not null) Consume(held);
            return new DumpVerification(false,
                "La copia è INCOMPLETA: il file si interrompe a metà di una riga (download interrotto?).",
                version, created, null, found());
        }
        if (held is null)
            return new DumpVerification(false, "Il file è vuoto.", null, null, null, found());

        var last = Text(held);
        if (!last.StartsWith(SqlDumpWriter.TrailerPrefix, StringComparison.Ordinal))
        {
            Consume(held);
            return new DumpVerification(false,
                "La copia è INCOMPLETA: manca la riga di chiusura (download interrotto, o copia fallita a metà).",
                version, created, null, found());
        }

        var summary = found();
        var mt = Chiusura().Match(last);
        if (!mt.Success)
            return new DumpVerification(false, $"Riga di chiusura illeggibile: {last}", version, created, null, summary);

        var declared = new DatabaseBackupSummary(
            int.Parse(mt.Groups[1].Value, CultureInfo.InvariantCulture),
            long.Parse(mt.Groups[2].Value, CultureInfo.InvariantCulture),
            summary.Bytes,
            mt.Groups[3].Value);

        if (problem is null && declared.Sha256 != summary.Sha256)
            problem = "L'impronta non torna: il file è stato modificato o si è rovinato dopo essere stato scritto.";
        if (problem is null && (declared.Tables != summary.Tables || declared.Rows != summary.Rows))
            problem = $"I conti non tornano: la chiusura dice {declared.Tables} tabelle e {declared.Rows} righe, " +
                      $"il file ne contiene {summary.Tables} e {summary.Rows}.";

        return new DumpVerification(problem is null, problem, version, created, declared, summary);
    }

    private static string Text(byte[] line) =>
        // Anche \r: un file passato da un editor che ha messo i fine riga di Windows deve arrivare a «l'impronta
        // non torna», che è la verità, e non a «non è una copia della vIPI», che manderebbe a cercare altro.
        Encoding.UTF8.GetString(line).TrimEnd('\n', '\r');

    [GeneratedRegex(@"righe=(\d+)$")]
    private static partial Regex RigheDiTabella();

    [GeneratedRegex(@"^-- vipi-backup-fine tabelle=(\d+) righe=(\d+) sha256=([0-9a-f]{64})$")]
    private static partial Regex Chiusura();
}
