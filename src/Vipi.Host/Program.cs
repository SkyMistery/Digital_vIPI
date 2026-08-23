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

// WebApplicationFactory<Program> avvia l'host chiamando questo stesso punto d'ingresso e lo interrompe
// lanciando una StopTheHostException (tipo interno di Microsoft.Extensions.Hosting, non referenziabile:
// si riconosce dal nome). Non è un guasto, è il modo normale in cui i test d'integrazione prendono l'host —
// senza questo filtro ogni giro di test lascerebbe un avvio-errore.txt che non descrive niente.
static bool ArrestoVolutoDaiTest(Exception ex) => ex.GetType().Name == "StopTheHostException";

// Punto d'ingresso esposto per i test d'integrazione in-process (WebApplicationFactory<Program>).
// I top-level statement generano una classe Program internal: questa partial la rende raggiungibile dai test.
public partial class Program { }
