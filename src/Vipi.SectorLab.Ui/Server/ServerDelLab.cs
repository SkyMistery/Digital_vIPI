using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vipi.SectorLab.Ui.Components;

namespace Vipi.SectorLab.Ui.Server;

/// <summary>
/// Il server Blazor locale che la finestra del Lab mostra (carta F3 §2.1 e §3). Sta qui e non nel guscio perché i
/// test lo avviano con un Kestrel vero, su Ubuntu. Le trappole di F0 (carta madre §3) sono scritte qui, una per riga,
/// perché nessuna si vede quando manca: la pagina muore senza dire perché.
/// </summary>
public static class ServerDelLab
{
    /// <summary>
    /// Il tetto di SignalR per un messaggio RICEVUTO dalla pagina. Di base è 32 KB e oltre il circuito muore in
    /// silenzio (F0, trappola 3). La geometria non passa di qui (va con una fetch, §3), ma il testo sì: un pezzo
    /// d'AIP incollato nel campo dei vertici arriva facilmente a decine di KB.
    /// </summary>
    internal const long TettoDelMessaggio = 4L * 1024 * 1024;

    /// <param name="segreto">Il segreto di questo avvio: il cancello lo chiede a ogni richiesta.</param>
    /// <param name="nomeApplicazione">L'assieme dell'eseguibile: col suo nome si trova il manifesto degli asset
    /// (<c>&lt;nome&gt;.staticwebassets.endpoints.json</c>). Di base l'assieme d'ingresso.</param>
    public static WebApplication Crea(SegretoDelLab segreto, string? nomeApplicazione = null)
    {
        ArgumentNullException.ThrowIfNull(segreto);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            // 🔴 F0, trappola 2: la radice dei contenuti sarebbe la CARTELLA DI LAVORO del processo. Lanciato da un
            // collegamento, o da un'altra cartella, tutti gli statici andavano a 404.
            ContentRootPath = AppContext.BaseDirectory,
            ApplicationName = nomeApplicazione ?? Assembly.GetEntryAssembly()?.GetName().Name,
            EnvironmentName = Environments.Production,
        });

        // Gli asset delle librerie Razor (questa compresa) stanno nelle loro cartelle finché non si pubblica: questa
        // riga li trova anche lanciando l'app da bin/. Pubblicata non c'è il manifesto di sviluppo e non fa niente.
        builder.WebHost.UseStaticWebAssets();
        // Porta a caso, e SOLO l'interfaccia di loopback: dall'esterno della macchina non si arriva.
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton(segreto);
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.Configure<HubOptions>(o => o.MaximumReceiveMessageSize = TettoDelMessaggio);

        var app = builder.Build();

        // Il cancello per primo: vale anche per gli asset e per /_blazor.
        app.UseMiddleware<Cancello>();
        // 🔴 F0, trappola 1: senza, ogni pagina interattiva risponde 500 e non dice perché.
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

        return app;
    }

    /// <summary>L'indirizzo che il server ha preso all'avvio (la porta è a caso). Dopo <c>StartAsync</c>.</summary>
    public static Uri Indirizzo(WebApplication app)
    {
        var indirizzi = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
        string primo = indirizzi?.FirstOrDefault()
                       ?? throw new InvalidOperationException("Il server del Lab non è in ascolto: StartAsync non è stato chiamato?");
        return new Uri(primo);
    }

    /// <summary>La prima pagina, col segreto: la apre la WebView2, e il cancello lo toglie subito dall'indirizzo.</summary>
    public static Uri Ingresso(WebApplication app)
    {
        var segreto = app.Services.GetRequiredService<SegretoDelLab>();
        return new Uri(Indirizzo(app), $"/?{Cancello.ParametroDelSegreto}={segreto.Valore}");
    }
}
