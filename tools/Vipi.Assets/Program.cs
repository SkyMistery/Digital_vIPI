using Vipi.Assets;

// Prepara la wwwroot del pacchetto. Lo chiama il publish di Vipi.Host (vedi il target VipiOttimizzaAsset
// in Vipi.Host.csproj); a mano si usa così:
//
//     dotnet run --project tools/Vipi.Assets -- <cartella>
//
// ⚠️ NON va lanciato sulla wwwroot dei SORGENTI: riscriverebbe i file del repository togliendo proprio i
// commenti che sono la ragione per cui questo attrezzo esiste. Lavora sull'output di un publish.
//
// ⚠️ Le righe che stampa restano in ASCII: le legge la console di MSBuild, che su Windows non e' UTF-8, e
// un trattino tipografico ci esce come scarabocchio in mezzo al resoconto di un publish riuscito.

if (args.Length != 1)
{
    Console.Error.WriteLine("Uso: Vipi.Assets <cartella-wwwroot>");
    return 2;
}

var cartella = args[0];
if (!Directory.Exists(cartella))
{
    Console.Error.WriteLine($"Vipi.Assets: la cartella '{cartella}' non esiste.");
    return 2;
}

var esito = Ottimizzatore.Esegui(cartella);

foreach (var errore in esito.Errori)
    Console.Error.WriteLine($"Vipi.Assets: NON minificabile - {errore}");

if (esito.Errori.Count > 0)
{
    // Fermarsi, e non proseguire con quei file lasciati com'erano. Un file che il minificatore non riesce a
    // leggere è quasi sempre un file con dentro un errore di sintassi che nessun'altra parte della build
    // guarda: JavaScript e CSS non li compila nessuno. Meglio scoprirlo qui che da una pagina che non
    // risponde, spedita a chi la usa.
    Console.Error.WriteLine(
        $"Vipi.Assets: {esito.Errori.Count} file non minificabili. Il pacchetto NON è stato preparato.");
    return 1;
}

var risparmio = esito.ByteOriginali == 0 ? 0 : 100.0 * (esito.ByteOriginali - esito.ByteCompressi) / esito.ByteOriginali;
Console.WriteLine(
    $"Vipi.Assets: {esito.FileMinificati} file minificati (-{esito.ByteTolti:N0} B), " +
    $"{esito.FileCompressi} precompressi ({esito.ByteOriginali:N0} -> {esito.ByteCompressi:N0} B, -{risparmio:0.0}%).");
return 0;
