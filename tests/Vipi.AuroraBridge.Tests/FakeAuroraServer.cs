using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Vipi.AuroraBridge.Tests;

/// <summary>
/// Aurora finta: un server TCP che parla lo stesso protocollo (ASCII, «;», CR/LF) e risponde da un copione.
/// Serve a provare il client senza Aurora accesa — inclusi i casi che dal vero sono scomodi da provocare
/// (silenzio, disconnessione, messaggi non sollecitati in mezzo a uno scambio).
/// </summary>
public sealed class FakeAuroraServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, string> _replies = new(StringComparer.OrdinalIgnoreCase);

    public FakeAuroraServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _ = Task.Run(AcceptAsync);
    }

    public int Port { get; }

    /// <summary>Comandi ricevuti, in ordine: serve a verificare che il client non chieda più del necessario.</summary>
    public List<string> Received { get; } = new();

    /// <summary>Righe spedite prima di rispondere: simula i push non sollecitati (intercom).</summary>
    public List<string> PushBeforeReply { get; } = new();

    /// <summary>Comandi ai quali il server non risponde affatto (per provare il timeout).</summary>
    public HashSet<string> Silent { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registra la risposta a un comando. La chiave è la riga intera oppure il solo nome comando.</summary>
    public FakeAuroraServer Reply(string command, string response)
    {
        _replies[command] = response;
        return this;
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                _ = Task.Run(() => ServeAsync(client));
            }
        }
        catch (Exception) { /* listener chiuso */ }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            var stream = client.GetStream();
            var buffer = new byte[4096];
            var acc = new StringBuilder();

            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var read = await stream.ReadAsync(buffer, _cts.Token);
                    if (read == 0) return;

                    acc.Append(Encoding.ASCII.GetString(buffer, 0, read));
                    var text = acc.ToString();
                    int cut;
                    while ((cut = text.IndexOfAny(new[] { '\r', '\n' })) >= 0)
                    {
                        var line = text[..cut];
                        text = text[(cut + 1)..].TrimStart('\r', '\n');
                        if (line.Length > 0) await HandleAsync(stream, line);
                    }
                    acc.Clear();
                    acc.Append(text);
                }
            }
            catch (Exception) { /* client sparito */ }
        }
    }

    private async Task HandleAsync(NetworkStream stream, string line)
    {
        lock (Received) Received.Add(line);

        var name = line.Split(';')[0];
        if (Silent.Contains(name) || Silent.Contains(line)) return;

        foreach (var push in PushBeforeReply) await WriteAsync(stream, push);

        var reply = _replies.TryGetValue(line, out var exact) ? exact
            : _replies.TryGetValue(name, out var byName) ? byName
            : $"@ERR;{name};Unknown command";

        await WriteAsync(stream, reply);
    }

    private static async Task WriteAsync(NetworkStream stream, string payload)
    {
        var bytes = Encoding.ASCII.GetBytes(payload + "\r\n");
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _listener.Stop();
        _cts.Dispose();
    }
}
