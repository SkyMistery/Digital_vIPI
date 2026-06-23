using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Vipi.Application;
using Vipi.Application.Abstractions;
using Vipi.Host;
using Vipi.Host.Components;
using Vipi.Infrastructure;
using Vipi.Infrastructure.Ivao;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Compressione asset di testo (CSS/JS/SignalR). NIENTE text/event-stream: la rotta SSE /sop/live/atc
// usa DisableBuffering() e dev'essere consegnata subito, non compressa/bufferizzata.
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
    o.MimeTypes = new[] { "text/css", "text/javascript", "application/javascript", "application/json", "image/svg+xml", "text/html" };
});

// Layer vIPI (Clean Architecture). ADR-0001 D2.
builder.Services.AddVipiApplication();
builder.Services.AddVipiInfrastructure(
    builder.Configuration.GetConnectionString("Vipi") ?? "Data Source=vipi.db");

// F3: polling IVAO + cache ATC online + hosted service. ADR-0001 D6.
builder.Services.AddVipiIvao(builder.Configuration);

// Identità divisione (Code + prefissi ICAO): basta cambiare la sezione "Division" per passare divisione.
builder.Services.Configure<Vipi.Application.DivisionOptions>(
    builder.Configuration.GetSection(Vipi.Application.DivisionOptions.SectionName));

// Override opzionale dei codici staff admin (pattern completi); se vuoto si derivano da Division.Code.
builder.Services.Configure<Vipi.Application.Auth.AuthOptions>(
    builder.Configuration.GetSection(Vipi.Application.Auth.AuthOptions.SectionName));

// Adapter di identità: in dev un CH fittizio. In A/B → HostIdentity; in C → OIDC. ADR-0002 D2/D3.
builder.Services.AddScoped<ICurrentUserProvider, DevCurrentUserProvider>();

var app = builder.Build();

// Dev: crea/migra il DB SQLite e popola il seed di Roma (struttura + contenuti demo).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VipiDbContext>();
    db.Database.Migrate();
    await RomaStructureSeed.SeedAsync(db);
    await RomaContentSeed.SeedAsync(db);
    await RomaAirportSeed.SeedAsync(db);
    await RomaVloaSeed.SeedAsync(db);
    await RomaTransferSeed.SeedAsync(db);
}

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
    OnPrepareResponse = ctx =>
        ctx.Context.Response.Headers.CacheControl = "public,max-age=604800", // 7 giorni
});
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Vipi.Ui.Pages.SopHome).Assembly);  // monta la RCL vIPI

// F3: transport live SSE. Emette un evento a ogni cambio della cache ATC (+ heartbeat anti-timeout).
// ADR-0003. Read-only, nessun dato sensibile.
app.MapGet("/sop/live/atc", async (HttpContext ctx, OnlineAtcCache cache, CancellationToken ct) =>
{
    ctx.Response.Headers.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";
    ctx.Response.Headers.Connection = "keep-alive";
    // Disabilita il buffering: gli eventi devono raggiungere il browser subito, anche dietro reverse-proxy.
    ctx.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();

    var signal = new SemaphoreSlim(0);
    void OnChanged() => signal.Release();
    cache.Changed += OnChanged;

    async Task EmitAsync()
    {
        var snap = cache.GetCurrent();
        var payload = System.Text.Json.JsonSerializer.Serialize(
            new { asOf = snap.AsOf, count = snap.Callsigns.Count });
        await ctx.Response.WriteAsync($"data: {payload}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);
    }

    try
    {
        await EmitAsync(); // stato corrente subito alla connessione
        while (!ct.IsCancellationRequested)
        {
            if (await signal.WaitAsync(TimeSpan.FromSeconds(25), ct))
                await EmitAsync();
            else
            {
                await ctx.Response.WriteAsync(": ping\n\n", ct); // heartbeat
                await ctx.Response.Body.FlushAsync(ct);
            }
        }
    }
    catch (OperationCanceledException) { /* client disconnesso */ }
    finally { cache.Changed -= OnChanged; }
});

app.Run();
