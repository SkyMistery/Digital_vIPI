using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

// Spike F0 — sonda del protocollo 3rd-party di Aurora (TCP 1130, ASCII, campi ';', pacchetti CR/LF).
// Uso:  dotnet run --project tools/Vipi.AuroraProbe -- [--host H] [--port P] [--gap ms] [--listen s] CMD...
// Ogni CMD viene inviato così com'è (es. "#CONN", "#FP;AZA123"); tutto ciò che arriva viene stampato
// grezzo, con timestamp e millisecondi dall'invio, così si vede anche l'eventuale traffico non sollecitato.

var host = "127.0.0.1";
var port = 1130;
var gapMs = 700;      // attesa dopo ogni comando prima del successivo
var listenS = 2;      // ascolto finale, per catturare risposte tardive o push
var commands = new List<string>();

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--host": host = args[++i]; break;
        case "--port": port = int.Parse(args[++i]); break;
        case "--gap": gapMs = int.Parse(args[++i]); break;
        case "--listen": listenS = int.Parse(args[++i]); break;
        default: commands.Add(args[i]); break;
    }
}

if (commands.Count == 0)
{
    Console.Error.WriteLine("Nessun comando. Esempio: -- \"#CONN\" \"#SELTFC\"");
    return 2;
}

var sw = Stopwatch.StartNew();
void Log(string tag, string text) =>
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{sw.ElapsedMilliseconds,6} ms] {tag} {text}");

using var client = new TcpClient();
try
{
    await client.ConnectAsync(host, port);
}
catch (SocketException ex)
{
    Console.Error.WriteLine($"Connessione a {host}:{port} fallita: {ex.SocketErrorCode} — {ex.Message}");
    Console.Error.WriteLine("Aurora aperta? PVD → Settings (F7) → Other → 3rd Party Software Access = YES?");
    return 1;
}

Log("CONN", $"connesso a {host}:{port}");

var stream = client.GetStream();
using var cts = new CancellationTokenSource();

// Lettore: stampa ogni riga appena arriva. Il protocollo delimita con CR/LF, ma non do per scontato
// che ogni pacchetto arrivi intero: accumulo e taglio sui delimitatori.
var reader = Task.Run(async () =>
{
    var buffer = new byte[8192];
    var acc = new StringBuilder();
    try
    {
        while (!cts.Token.IsCancellationRequested)
        {
            var n = await stream.ReadAsync(buffer, cts.Token);
            if (n == 0) { Log("EOF ", "il server ha chiuso la connessione"); return; }

            acc.Append(Encoding.ASCII.GetString(buffer, 0, n));
            var text = acc.ToString();
            int idx;
            while ((idx = text.IndexOfAny(['\r', '\n'])) >= 0)
            {
                var line = text[..idx];
                text = text[(idx + 1)..].TrimStart('\r', '\n');
                if (line.Length > 0) Log("<<  ", line);
            }
            acc.Clear();
            acc.Append(text);
        }
    }
    catch (OperationCanceledException) { }
    catch (Exception ex) { Log("ERR ", ex.Message); }
});

foreach (var cmd in commands)
{
    var payload = Encoding.ASCII.GetBytes(cmd + "\r\n");
    Log(" >> ", cmd);
    await stream.WriteAsync(payload);
    await stream.FlushAsync();
    await Task.Delay(gapMs);
}

Log("WAIT", $"ascolto per altri {listenS}s…");
await Task.Delay(TimeSpan.FromSeconds(listenS));

cts.Cancel();
await reader;
Log("END ", "fine");
return 0;
