using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.AgreementsToSections;
using Vipi.Application.Content;
using Vipi.Infrastructure.Persistence;

// -----------------------------------------------------------------------------------------------
// Vipi.AgreementsToSections — converte gli accordi di ferragosto nel modello a SEZIONI (18 agosto 2026).
//
//   dotnet run --project tools/Vipi.AgreementsToSections -- --sqlite <file.db> [--apply]
//   dotnet run --project tools/Vipi.AgreementsToSections -- --mysql "<conn>"   [--apply]
//
// Senza --apply non scrive niente: stampa il piano e si ferma. È il modo in cui va guardato la prima volta.
//
// ⚠️ SEQUENZA — le tre parti vanno in quest'ordine e non in un altro:
//   1. migrazione AgreementSectionsAdditive   (schema nuovo, tutto nullable, niente drop)
//   2. QUESTO TOOL                            (i dati: 40 accordi → 17 coppie, versi ribaltati, gemelle unite)
//   3. migrazione AgreementSectionsFinalize   (NOT NULL, indice unico, via il vecchio)
//
// ⚠️ E NON gira all'avvio, di proposito. Le migrazioni girano PRIMA della manutenzione d'avvio
// (Vipi.Host/Program.cs): una migrazione che droppa più una passata che legge quella tabella nella stessa
// release non trovano niente, scrivono zero, e i dati spariscono SENZA un errore. È la trappola pagata a
// ferragosto, ed è la ragione per cui questo è un comando che qualcuno lancia guardando cosa fa.
//
// Carta: docs/feature/2026-08-18-accordi-a-sezioni.md
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

// ---- 1) Si legge il VECCHIO schema, in SQL grezzo -------------------------------------------------
//
// ⚠️ Non con EF: il DbContext descrive il modello NUOVO, e le tabelle che servono qui — AgreementParties,
// CoordinationAgreements.TrafficKind, AgreementClauses.Direction — per lui non esistono più. Leggerle a mano
// è l'unico modo di vederle, ed è anche la garanzia che questo tool non possa dipendere per sbaglio da
// qualcosa che il modello nuovo gli racconta.

var legacy = await LegacyReader.LeggiAsync(db);
Console.WriteLine($"Letti {legacy.Count} accordi, "
                  + $"{legacy.Sum(a => a.Clauses.Count)} clausole, "
                  + $"{legacy.Sum(a => a.Airports.Count)} aeroporti.");

if (legacy.Count == 0)
{
    Console.WriteLine("Niente da convertire: l'archivio è già a sezioni, oppure è vuoto.");
    return 0;
}

// ---- 2) Il piano: logica PURA, provata senza database ---------------------------------------------

var piano = AgreementsToSections.Plan(legacy);
Rapporto.Stampa(piano, legacy);

if (!piano.CanRun)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("La conversione NON procede: gli accordi qui sopra vanno sistemati a mano prima.");
    Console.Error.WriteLine("Buttarli via perderebbe lavoro editoriale, e inventargli un capo sarebbe scrivere");
    Console.Error.WriteLine("un accordo che nessuno ha concordato.");
    return 1;
}

if (!opzioni.Applica)
{
    Console.WriteLine();
    Console.WriteLine("Prova a vuoto: niente è stato scritto. Rilancia con --apply per eseguire il piano.");
    return 0;
}

// ---- 3) Si scrive ---------------------------------------------------------------------------------

var scritte = await PlanWriter.ApplicaAsync(db, piano);
Console.WriteLine();
Console.WriteLine($"Fatto: {scritte.Agreements} accordi, {scritte.Sections} sezioni, "
                  + $"{scritte.Clauses} clausole riappese, {scritte.Airports} aeroporti riappesi, "
                  + $"{scritte.Deleted} gusci eliminati.");
Console.WriteLine();
Console.WriteLine("Adesso applica la migrazione AgreementSectionsFinalize. Se qualcosa non tornasse,");
Console.WriteLine("il NOT NULL o l'indice unico la fermeranno — ed è il modo giusto di accorgersene.");
return 0;
