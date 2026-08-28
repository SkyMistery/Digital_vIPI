using Microsoft.EntityFrameworkCore;
using Vipi.Application.Auth;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Vipi.MilSopLoader;
using Vipi.Domain;

// -----------------------------------------------------------------------------------------------
// Vipi.MilSopLoader — carica un SOP militare TRASCRITTO nel documento del campo (28 agosto 2026).
//
//   dotnet run --project tools/Vipi.MilSopLoader -- --sqlite <file.db> [--icao LIPI] [--apply]
//   dotnet run --project tools/Vipi.MilSopLoader -- --mysql "<conn>"   [--icao LIPI] [--apply]
//
// Senza --apply stampa il piano e si ferma: scrivere dentro il documento di qualcun altro è la cosa
// che va guardata prima di farla.
//
// ⚠️ NON è un lettore di PDF, e non deve diventarlo. Il contenuto è TRASCRITTO a mano — perché va
// anche tradotto in italiano (carta vSOP militari §1d) e perché nei quindici SOP la metà di ciò che
// conta sono FIGURE, che nessun parser porta dentro. Quello che questo strumento fa è mettere una
// trascrizione al posto giusto senza sbagliare chiave, e dire che cosa resta fuori.
//
// ⚠️ E non gira all'avvio, di proposito: è un comando che qualcuno lancia guardando cosa fa.
//
// Carta: docs/feature/2026-08-27-vsop-militari.md (slice 10)
// -----------------------------------------------------------------------------------------------

if (!Argomenti.Prova(args, out var opzioni, out var errore))
{
    if (errore is not null) Console.Error.WriteLine($"Errore: {errore}\n");
    Console.Error.WriteLine(Argomenti.Uso);
    return 1;
}

var builder = new DbContextOptionsBuilder<VipiDbContext>();
if (opzioni.Sqlite is { } file)
{
    if (!File.Exists(file))
    {
        Console.Error.WriteLine($"File SQLite non trovato: {file}");
        return 1;
    }
    builder.UseSqlite($"Data Source={file}");
}
else
{
    builder.UseMySql(opzioni.MySql!, ServerVersion.AutoDetect(opzioni.MySql!));
}

await using var db = new VipiDbContext(builder.Options);

var sop = opzioni.Icao switch
{
    "LIPI" => SopLipi.Costruisci(),
    _ => null,
};

if (sop is null)
{
    Console.Error.WriteLine($"Non c'è una trascrizione per {opzioni.Icao}. Oggi c'è solo LIPI.");
    Console.Error.WriteLine("Trascriverne un altro vuol dire aggiungere un file come SopLipi.cs — e");
    Console.Error.WriteLine("rileggerlo con qualcuno che conosca il campo, che è la parte che conta.");
    return 1;
}

var airac = new AiracService();
var authz = new AutorizzazioneDelloStrumento(opzioni.Autore);
var editing = new EfEditingRepository(db, airac, new EfMediaMaintenance(db));
var militari = new EfMilitaryDocumentService(db, airac, authz, editing, new EfSpecialAreaRepository(db));

Console.WriteLine($"SOP {sop.Icao} — fonte: {sop.Fonte}");
Console.WriteLine($"Sezioni trascritte: {sop.Sezioni.Count}, blocchi: {sop.Sezioni.Sum(s => s.Blocchi.Count)}.");
Console.WriteLine();

var (docId, piano) = await new Caricatore(editing, militari, opzioni.Autore)
    .EseguiAsync(sop, opzioni.Applica);

Console.WriteLine($"Documento {docId}:");
Console.WriteLine();
foreach (var r in piano)
    Console.WriteLine($"  {r.Chiave,-22} {r.BlocchiDaScrivere,2} blocchi   {r.Esito}");

// ⚠️ Una chiave che il profilo non ha è un ERRORE, non una riga di rendiconto: quel contenuto non finisce
// da nessuna parte, e in mezzo a trenta righe non lo nota nessuno. Qui diventa un codice d'uscita.
var perse = piano.Where(r => r.Esito.StartsWith("SALTATA: la chiave", StringComparison.Ordinal)).ToList();
if (perse.Count > 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"{perse.Count} chiavi non esistono nel profilo AirportMil: {string.Join(", ", perse.Select(r => r.Chiave))}");
    Console.Error.WriteLine("La trascrizione va corretta: quel contenuto non è finito da nessuna parte.");
    return 2;
}

Console.WriteLine();
if (!opzioni.Applica)
{
    Console.WriteLine("Prova a vuoto: niente è stato scritto. Rilancia con --apply per eseguire il piano.");
    return 0;
}

Console.WriteLine("Scritto. Il documento è in BOZZA: diventa pubblico solo con una release AIRAC,");
Console.WriteLine("e prima va riletto da chi conosce il campo — la trascrizione è una prima stesura.");
return 0;

/// <summary>
/// L'autorizzazione di uno strumento a riga di comando: chi lo lancia è già entrato nel server.
/// ⚠️ Esiste solo perché i servizi la pretendono, ed è giusto che la pretendano — il gate vero è che
/// questo comando non gira all'avvio e non lo espone nessuna pagina.
/// </summary>
file sealed class AutorizzazioneDelloStrumento : IEditAuthorizationService
{
    private readonly int _vid;
    public AutorizzazioneDelloStrumento(int vid) => _vid = vid;

    public bool IsAdmin => true;
    public VipiRole Role => IsAdmin ? VipiRole.Admin : VipiRole.User;
    public int? CurrentUserId => _vid;
    public string? CurrentName => "MilSopLoader";
    public Task EnsureCanEditAccAsync(string accCode, CancellationToken ct = default) => Task.CompletedTask;
    public Task EnsureCanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.CompletedTask;
    public Task<bool> CanEditAccAsync(string accCode, CancellationToken ct = default) => Task.FromResult(true);
    public Task<bool> CanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.FromResult(true);
    public Task<bool> CanEditAnythingAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task<IReadOnlyList<GrantRow>> ListGrantsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<GrantRow>>(Array.Empty<GrantRow>());
    public Task<int> AddGrantAsync(int userId, string? displayName, string accCode, CancellationToken ct = default) =>
        Task.FromResult(0);
    public Task RevokeGrantAsync(int grantId, CancellationToken ct = default) => Task.CompletedTask;
    public void EnsureAdmin() { }
}
