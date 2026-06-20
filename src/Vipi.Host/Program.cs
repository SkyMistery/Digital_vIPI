using Microsoft.EntityFrameworkCore;
using Vipi.Application;
using Vipi.Application.Abstractions;
using Vipi.Host;
using Vipi.Host.Components;
using Vipi.Infrastructure;
using Vipi.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Layer vIPI (Clean Architecture). ADR-0001 D2.
builder.Services.AddVipiApplication();
builder.Services.AddVipiInfrastructure(
    builder.Configuration.GetConnectionString("Vipi") ?? "Data Source=vipi.db");

// Adapter di identità: in dev un CH fittizio. In A/B → HostIdentity; in C → OIDC. ADR-0002 D2/D3.
builder.Services.AddScoped<ICurrentUserProvider, DevCurrentUserProvider>();

var app = builder.Build();

// Dev: crea/migra il DB SQLite all'avvio.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VipiDbContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Vipi.Ui.Pages.SopHome).Assembly);  // monta la RCL vIPI

app.Run();
