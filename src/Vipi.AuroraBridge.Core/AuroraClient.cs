using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

namespace Vipi.AuroraBridge.Core;

/// <summary>Parametri di connessione al server 3rd-party di Aurora.</summary>
public sealed record AuroraClientOptions(
    string Host = "127.0.0.1",
    int Port = 1130,
    int TimeoutMs = 3000);

/// <summary>Esito di un comando. <see cref="Ok"/> falso quando Aurora risponde <c>@ERR</c> o scade il tempo.</summary>
public sealed record AuroraResponse(bool Ok, string Command, IReadOnlyList<string> Fields, string? Error, string Raw)
{
    public static AuroraResponse Failed(string command, string error) =>
        new(false, command, Array.Empty<string>(), error, "");
}

/// <summary>
/// Client del protocollo 3rd-party di Aurora (TCP 1130, ASCII, campi «;», pacchetti CR/LF).
///
/// Tre vincoli del protocollo, tutti verificati in F0, determinano questa forma:
/// 1. le risposte NON hanno un identificativo di richiesta, si riconoscono solo dall'eco del comando
///    → le richieste vanno **serializzate**: una alla volta, con timeout;
/// 2. esistono messaggi non sollecitati (intercom) che possono arrivare in mezzo → si scartano dalla
///    correlazione e si girano a <see cref="Unsolicited"/>;
/// 3. il «;» separa i campi → un argomento non può contenerlo (viene rifiutato prima dell'invio).
/// </summary>
public sealed class AuroraClient : IAsyncDisposable
{
    private readonly AuroraClientOptions _options;
    private readonly SemaphoreSlim _turn = new(1, 1);
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private Channel<string>? _lines;
    private CancellationTokenSource? _readerCts;

    public AuroraClient(AuroraClientOptions? options = null) => _options = options ?? new AuroraClientOptions();

    /// <summary>Messaggi arrivati senza essere stati chiesti (es. <c>#INTERCOMPHONESTATUS</c>).</summary>
    public event Action<string>? Unsolicited;

    public bool IsConnected => _tcp?.Connected == true;

    /// <summary>Si connette se serve. L'errore tipico è <c>ConnectionRefused</c>: Aurora chiusa, oppure
    /// «3rd Party Software Access» non attivo NELLA SESSIONE IN CORSO (il flag sul profilo non basta).</summary>
    public async Task<bool> EnsureConnectedAsync(CancellationToken ct = default)
    {
        if (IsConnected) return true;

        await DisconnectAsync().ConfigureAwait(false);
        try
        {
            var tcp = new TcpClient();
            // TimeoutMs vale ANCHE per la connessione, non solo per l'attesa della risposta. Senza, un host
            // che non rifiuta ma tace — firewall che scarta i SYN invece di rispondere, macchina spenta con
            // l'IP ancora assegnato — lascia il tool fermo per il timeout del sistema operativo (una ventina
            // di secondi su Windows) mentre l'opzione dice 3000 ms. Con Aurora sulla stessa macchina non
            // capita quasi mai; quando capita, sembra che il tool si sia piantato.
            using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectTimeout.CancelAfter(_options.TimeoutMs);
            await tcp.ConnectAsync(_options.Host, _options.Port, connectTimeout.Token).ConfigureAwait(false);

            _tcp = tcp;
            _stream = tcp.GetStream();
            _lines = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
            _readerCts = new CancellationTokenSource();
            _ = Task.Run(() => ReadLoopAsync(_stream, _lines.Writer, _readerCts.Token), CancellationToken.None);
            return true;
        }
        catch (Exception)
        {
            await DisconnectAsync().ConfigureAwait(false);
            return false;
        }
    }

    /// <summary>Invia un comando e aspetta la sua risposta. <paramref name="command"/> è il nome (es. «#TRPOS»),
    /// <paramref name="args"/> gli argomenti già ordinati.</summary>
    public async Task<AuroraResponse> SendAsync(string command, CancellationToken ct = default, params string?[] args)
    {
        foreach (var a in args)
        {
            if (a is not null && a.Contains(';'))
                return AuroraResponse.Failed(command, "Argomento con «;»: il protocollo lo userebbe come separatore.");
        }

        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false))
            return AuroraResponse.Failed(command, $"Aurora non raggiungibile su {_options.Host}:{_options.Port}.");

        var payload = args.Length == 0 ? command : command + ";" + string.Join(";", args.Select(a => a ?? ""));

        await _turn.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var stream = _stream;
            var reader = _lines?.Reader;
            if (stream is null || reader is null)
                return AuroraResponse.Failed(command, "Connessione non pronta.");

            // Scarta l'eventuale arretrato: appartiene a uno scambio precedente, non a questo.
            while (reader.TryRead(out var stale)) Dispatch(stale, command, out _);

            var bytes = Encoding.ASCII.GetBytes(payload + "\r\n");
            await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(_options.TimeoutMs);

            try
            {
                while (await reader.WaitToReadAsync(timeout.Token).ConfigureAwait(false))
                {
                    while (reader.TryRead(out var line))
                    {
                        if (Dispatch(line, command, out var response)) return response!;
                    }
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return AuroraResponse.Failed(command, $"Nessuna risposta a {command} entro {_options.TimeoutMs} ms.");
            }

            await DisconnectAsync().ConfigureAwait(false);
            return AuroraResponse.Failed(command, "Aurora ha chiuso la connessione.");
        }
        catch (IOException ex)
        {
            await DisconnectAsync().ConfigureAwait(false);
            return AuroraResponse.Failed(command, ex.Message);
        }
        finally
        {
            _turn.Release();
        }
    }

    /// <summary>Decide se una riga è la risposta al comando in corso. Le risposte fanno ECO al comando
    /// (la wiki dice altro: dà «#CTRL» come esito di #CTRLRWY/#CONN — smentito in F0); gli errori arrivano
    /// come <c>@ERR;&lt;comando&gt;;…;&lt;messaggio&gt;</c>. Tutto il resto è traffico non sollecitato.</summary>
    private bool Dispatch(string line, string command, out AuroraResponse? response)
    {
        response = null;
        if (string.IsNullOrWhiteSpace(line)) return false;

        var fields = line.Split(';');
        var head = fields[0];

        if (head.Equals("@ERR", StringComparison.OrdinalIgnoreCase) || head.StartsWith("$", StringComparison.Ordinal))
        {
            // @ERR;#LBALT;FDX126;250;Traffic not assumed.  →  il comando è il campo 1, il messaggio l'ultimo.
            var failing = fields.Length > 1 ? fields[1] : "";
            if (!failing.Equals(command, StringComparison.OrdinalIgnoreCase)) return false;

            var message = fields.Length > 2 ? fields[^1] : "errore sconosciuto";
            response = new AuroraResponse(false, command, Array.Empty<string>(), message, line);
            return true;
        }

        if (head.Equals(command, StringComparison.OrdinalIgnoreCase))
        {
            response = new AuroraResponse(true, command, fields.Skip(1).ToList(), null, line);
            return true;
        }

        Unsolicited?.Invoke(line);
        return false;
    }

    private static async Task ReadLoopAsync(NetworkStream stream, ChannelWriter<string> writer, CancellationToken ct)
    {
        var buffer = new byte[8192];
        var accumulator = new StringBuilder();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(buffer, ct).ConfigureAwait(false);
                if (read == 0) break;

                accumulator.Append(Encoding.ASCII.GetString(buffer, 0, read));
                var text = accumulator.ToString();
                int cut;
                while ((cut = text.IndexOfAny(new[] { '\r', '\n' })) >= 0)
                {
                    var line = text.Substring(0, cut);
                    text = text.Substring(cut + 1).TrimStart('\r', '\n');
                    if (line.Length > 0) writer.TryWrite(line);
                }
                accumulator.Clear();
                accumulator.Append(text);
            }
        }
        catch (Exception) { /* connessione caduta: la segnala il prossimo invio */ }
        finally { writer.TryComplete(); }
    }

    private async Task DisconnectAsync()
    {
        try { _readerCts?.Cancel(); } catch { /* già chiuso */ }
        _readerCts?.Dispose();
        _readerCts = null;

        if (_stream is not null) await _stream.DisposeAsync().ConfigureAwait(false);
        _stream = null;

        _tcp?.Dispose();
        _tcp = null;
        _lines = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _turn.Dispose();
    }
}
