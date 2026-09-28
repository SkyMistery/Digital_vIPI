using Vipi.Host;

// Punto d'ingresso. Deve restare COSÌ CORTO: vedi VipiStartup, che spiega perché ogni riga aggiunta qui è
// una riga che può morire senza lasciare traccia.
//
// L'ordine è l'unica cosa che conta:
//   1. il gancio agli errori fatali, che copre i guasti sugli altri thread;
//   2. il corpo dell'avvio, in un metodo separato, dentro un try — così anche un fallimento di CARICAMENTO
//      TIPI (che avviene alla preparazione del metodo, prima della sua prima riga) diventa un'eccezione
//      gestita invece di una morte muta.
StartupDiagnostics.HookFatalErrors();

try
{
    VipiStartup.Run(args);
}
catch (Exception ex) when (!ArrestoVolutoDaiTest(ex))
{
    // Si scrive e si RILANCIA: il processo deve morire come prima. L'unica cosa che cambia è che adesso
    // lascia detto perché. `throw;` senza argomento conserva lo stack originale.
    StartupDiagnostics.WriteFatal(ex);
    throw;
}

// Gli strumenti che risolvono l'host chiamando questo stesso punto d'ingresso — `dotnet ef` per le migrazioni —
// lo interrompono appena costruito lanciando una HostAbortedException (pubblica da .NET 7; in .NET 6 era la
// StopTheHostException interna, che si riconosceva dal nome). Non è un guasto: senza questo filtro ogni
// `dotnet ef migrations add` lascerebbe un avvio-errore.txt che non descrive niente.
// 🔴 U-235 (revisione totale 3): il commento diceva WebApplicationFactory, che da .NET 7 non lancia niente, e il
// filtro guardava solo il nome vecchio, cioè non scattava mai.
static bool ArrestoVolutoDaiTest(Exception ex) =>
    ex is Microsoft.Extensions.Hosting.HostAbortedException || ex.GetType().Name == "StopTheHostException";

// Punto d'ingresso esposto per i test d'integrazione in-process (WebApplicationFactory<Program>).
// I top-level statement generano una classe Program internal: questa partial la rende raggiungibile dai test.
public partial class Program { }
