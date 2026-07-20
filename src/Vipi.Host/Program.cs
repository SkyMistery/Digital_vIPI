using Microsoft.AspNetCore.ResponseCompression;
using Vipi.Host.Components;
using Vipi.Hosting;

var builder = WebApplication.CreateBuilder(args);

// File editabile (default globale) della frase di coordinamento. reloadOnChange: l'autore edita senza restart.
builder.Configuration.AddJsonFile("content/coordination-sentence.json", optional: true, reloadOnChange: true);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Compressione asset di testo (CSS/JS/SignalR). NIENTE text/event-stream: la rotta SSE /vsop/live/atc
// usa DisableBuffering() e dev'essere consegnata subito, non compressa/bufferizzata.
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
    o.MimeTypes = new[] { "text/css", "text/javascript", "application/javascript", "application/json", "image/svg+xml", "text/html" };
});

// Modulo vIPI: un'unica chiamata registra Application, Infrastructure/EF, polling IVAO, opzioni e identità.
// In sviluppo usa l'utente CH fittizio; in produzione l'identità è letta dal login del sito ospitante.
builder.Services.AddVipiModule(builder.Configuration, useDevIdentity: builder.Environment.IsDevelopment());

var app = builder.Build();

// Crea/migra il DB del modulo. Nessun seed: i dati reali si inseriscono dall'app (editor/struttura).
app.MigrateVipiDatabase();
// Migrazione A (doc 10 §3f): garantisce una release effettiva per i documenti pubblicati (idempotente).
app.BackfillVipiReleases();
// Retention pubblicazione: pota release Superseded oltre soglia e versioni Archived oltre N (idempotente).
app.PruneVipiReleases();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    // Solo in prod: in dev l'host ascolta su http e il redirect logga un warning inutile.
    app.UseHttpsRedirection();
}

// Compressione prima dei file statici così CSS/JS escono compressi.
app.UseResponseCompression();

app.UseStaticFiles(new StaticFileOptions
{
    // In sviluppo niente cache: CSS/JS aggiornati sono sempre riletti (evita il "vecchio stile" in cache).
    OnPrepareResponse = ctx =>
        ctx.Context.Response.Headers.CacheControl = app.Environment.IsDevelopment()
            ? "no-cache, no-store, must-revalidate"
            : "public,max-age=604800", // 7 giorni in produzione
});
app.UseAntiforgery();

// Middleware del modulo (registrazione login staff nel roster).
app.UseVipiModule();

// Compat: i vecchi URL /sop* (pre-rebuild Round 12) redirigono al nuovo prefisso /vsop*,
// preservando la query string (es. ?icao=LIRF). Tutti gli endpoint reali sono ora su /vsop.
app.MapGet("/sop", (HttpContext ctx) => Results.Redirect($"/vsop{ctx.Request.QueryString}", permanent: true));
app.MapGet("/sop/{*rest}", (HttpContext ctx, string rest) => Results.Redirect($"/vsop/{rest}{ctx.Request.QueryString}", permanent: true));

// Compat: la pagina struttura è stata rinominata in /vsop/admin/sectorstructure.
app.MapGet("/vsop/admin/struttura", (HttpContext ctx) => Results.Redirect($"/vsop/admin/sectorstructure{ctx.Request.QueryString}", permanent: true));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(VipiModuleExtensions.UiAssembly);   // monta la RCL vIPI

// Endpoint del modulo (SSE live ATC).
app.MapVipiModule();

app.Run();
