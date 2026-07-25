using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Vipi.Host.Auth;
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

// Modulo login IVAO standalone (scenario C). STACCABILE: attivo solo se VipiAuth:Enabled=true.
// Se attivo, il ClaimsPrincipal lo produce questo modulo e HostIdentityCurrentUserProvider lo legge.
var authEnabled = builder.AddVipiStandaloneAuth();

// Modulo vIPI: un'unica chiamata registra Application, Infrastructure/EF, polling IVAO, opzioni e identità.
// In sviluppo usa l'utente CH fittizio; in produzione l'identità è letta dal login del sito ospitante.
// Se il login IVAO standalone è attivo, esso vince sul dev identity anche in sviluppo (si prova il login vero).
var useDevIdentity = builder.Environment.IsDevelopment() && !authEnabled;
// Guardia di sicurezza (audit D1): mai identità dev fittizia (admin onnipotente) fuori da Development.
Vipi.Hosting.ProductionIdentityGuard.EnsureSafe(builder.Environment.IsDevelopment(), useDevIdentity);
builder.Services.AddVipiModule(builder.Configuration, useDevIdentity: useDevIdentity);

var app = builder.Build();

// Dietro il proxy TLS di Fly.io/Render (TLS al bordo, HTTP interno): fidati di X-Forwarded-Proto/For così
// UseHttpsRedirection non entra in loop e OIDC costruisce il redirect_uri in https. KnownNetworks/Proxies
// svuotati perché l'IP del proxy non è fisso. Innocuo in locale (gli header non arrivano).
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

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

// Auth standalone (scenario C): serve il ClaimsPrincipal alle richieste. Prima di UseVipiModule,
// così lo StaffLoginTrackingMiddleware vede già l'utente autenticato. Montato solo se attivo.
if (authEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapVipiStandaloneAuth();
}

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

// Punto d'ingresso esposto per i test d'integrazione in-process (WebApplicationFactory<Program>).
// I top-level statement generano una classe Program internal: questa partial la rende raggiungibile dai test.
public partial class Program { }
