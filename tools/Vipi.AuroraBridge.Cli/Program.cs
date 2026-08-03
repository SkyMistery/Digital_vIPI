using Vipi.AuroraBridge.Contracts;
using Vipi.AuroraBridge.Core;

// vIPI Aurora Bridge — verifica a riga di comando (F2). Non è il prodotto finale: la shell è F3.
//
//   dotnet run --project tools/Vipi.AuroraBridge.Cli -- [--site URL] [--watch] [--write N]
//
// Senza argomenti fa un giro solo e stampa i candidati. --write N scrive il candidato N-esimo
// (1-based) nell'etichetta quota: è un'azione esplicita, mai automatica.

var site = "http://127.0.0.1:5034";
var watch = false;
int? writeIndex = null;
var clear = false;
string? owner = null;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--site": site = args[++i]; break;
        case "--watch": watch = true; break;
        case "--write": writeIndex = int.Parse(args[++i]); break;
        case "--clear": clear = true; break;
        case "--owner": owner = args[++i]; break;
        case "--help" or "-h":
            Console.WriteLine("Uso: [--site URL] [--owner CALLSIGN] [--watch] [--write N] [--clear]");
            return 0;
    }
}

await using var client = new AuroraClient();
using var api = new VipiApiClient(new VipiApiOptions(BaseAddress: site));
var orchestrator = new BridgeOrchestrator(new AuroraSession(client), api, ownerOverride: owner);

using var stopping = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopping.Cancel(); };

if (watch)
{
    orchestrator.StateChanged += Render;
    Console.WriteLine("In ascolto sulla selezione di Aurora. Ctrl+C per uscire.\n");
    await orchestrator.RunAsync(stopping.Token);
    return 0;
}

var state = await orchestrator.RefreshAsync(force: true, stopping.Token);
Render(state);

if (clear)
{
    var result = await orchestrator.ClearAsync(stopping.Token);
    Console.WriteLine(result.Ok ? "\nEtichetta cancellata." : $"\nScrittura rifiutata: {result.Error}");
    return result.Ok ? 0 : 1;
}

if (writeIndex is int n)
{
    var candidates = state.Proposal?.Candidates;
    if (candidates is null || n < 1 || n > candidates.Count)
    {
        Console.Error.WriteLine($"Candidato {n} inesistente.");
        return 2;
    }

    var chosen = candidates[n - 1];
    Console.WriteLine($"\nScrivo «{chosen.AuroraValue}» su {state.SelectedTraffic}…");
    var result = await orchestrator.WriteAsync(chosen, stopping.Token);
    Console.WriteLine(result.Ok ? "Scritto. (Il tag si aggiorna al prossimo giro radar.)" : $"Rifiutata: {result.Error}");
    return result.Ok ? 0 : 1;
}

return 0;

static void Render(BridgeState s)
{
    Console.WriteLine($"Aurora: {(s.AuroraConnected ? "connessa" : "NON connessa")}" +
                      (s.OwnerCallsign is null ? "" : $"   postazione: {s.OwnerCallsign}"));

    if (s.Notice is not null) Console.WriteLine($"Nota: {s.Notice}");
    if (s.SelectedTraffic is null) { Console.WriteLine(); return; }

    var fp = s.FlightPlan;
    Console.WriteLine($"Traffico: {s.SelectedTraffic}   {fp?.Departure}→{fp?.Arrival}   " +
                      $"crociera {fp?.CruisingAltitudeRaw}   quota {s.Position?.AltitudeFt} ft   " +
                      $"{(s.TrafficAssumed ? "ASSUNTO" : "non assunto (scrittura non consentita)")}");

    var proposal = s.Proposal;
    if (proposal is null) { Console.WriteLine("Nessuna proposta.\n"); return; }

    if (s.ProposalFromCache) Console.WriteLine("⚠ proposta dalla CACHE locale, non dal sito");
    foreach (var w in proposal.Warnings) Console.WriteLine($"⚠ {w}");

    var i = 1;
    foreach (var c in proposal.Candidates)
    {
        Console.WriteLine($"  [{i++}] {c.Cop,-10} {c.Level.Text,-22} → {c.ResolvedHandler,-14} " +
                          $"scrivi «{c.AuroraValue ?? "—"}»  ({c.Score:0.000})");
        Console.WriteLine($"      {string.Join(" · ", c.Reasons)}");
    }
    Console.WriteLine();
}
