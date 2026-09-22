using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vipi.SectorLab.Ui.Server;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Mappa;

/// <summary>
/// La geometria che la mappa si prende con una fetch (carta F3 §3, slice 4), con un Kestrel vero: dietro il cancello
/// come tutto il resto, e nel formato corto che la pagina sa leggere.
/// </summary>
public sealed class MappaDelLabTests : IAsyncLifetime
{
    private const string Segreto = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    private readonly AlberoDiProva _albero = new();
    private WebApplication _app = null!;
    private Uri _base = null!;

    public async Task InitializeAsync()
    {
        // La cartella dei dati sta nell'albero di prova: una corsa in CI non scrive in %LOCALAPPDATA% di chi la lancia.
        _app = ServerDelLab.Crea(new SegretoDelLab(Segreto), typeof(MappaDelLabTests).Assembly.GetName().Name,
            Path.Combine(_albero.Radice, "dati-del-lab"));
        await _app.StartAsync();
        _base = ServerDelLab.Indirizzo(_app);
    }

    public async Task DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
        _albero.Dispose();
    }

    private HttpClient Client() => new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = _base };

    private async Task<JsonElement> Chiedi(string percorso)
    {
        using var client = Client();
        var richiesta = new HttpRequestMessage(HttpMethod.Get, percorso);
        richiesta.Headers.Add("Cookie", $"sectorlab={Segreto}");
        var risposta = await client.SendAsync(richiesta);
        risposta.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await risposta.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private async Task Apri()
    {
        var lab = _app.Services.GetRequiredService<SessioneDelLab>();
        Assert.True(await lab.ApriAsync(_albero.Radice));
    }

    [Theory]
    [InlineData("/mappa/strati")]
    [InlineData("/mappa/strato/sfondo")]
    public async Task SenzaSegretoLaGeometriaNonEsce(string percorso)
    {
        using var client = Client();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(percorso)).StatusCode);
    }

    [Fact]
    public async Task UnoStratoCheNonEsisteE404()
    {
        using var client = Client();
        var richiesta = new HttpRequestMessage(HttpMethod.Get, "/mappa/strato/inventato");
        richiesta.Headers.Add("Cookie", $"sectorlab={Segreto}");
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(richiesta)).StatusCode);
    }

    [Fact]
    public async Task ConLaSessioneChiusaLoStratoEVuoto()
    {
        // Non un errore: la pagina accende una casella e non deve distinguere «non c'è niente» da «è andata male».
        var dati = await Chiedi("/mappa/strato/punti");
        Assert.Empty(dati.GetProperty("f").EnumerateArray());
        Assert.Empty((await Chiedi("/mappa/strati")).EnumerateArray());
    }

    [Fact]
    public async Task LElencoDegliStratiDiceIQuantiSenzaPortareLeCoordinate()
    {
        await Apri();
        var strati = (await Chiedi("/mappa/strati")).EnumerateArray().ToList();

        Assert.Equal(13, strati.Count);
        var punti = strati.Single(s => s.GetProperty("id").GetString() == "punti");
        Assert.True(punti.GetProperty("forme").GetInt32() > 0);
        Assert.True(punti.GetProperty("punti").GetInt32() >= punti.GetProperty("forme").GetInt32());
        Assert.True(strati.Single(s => s.GetProperty("id").GetString() == "sfondo").GetProperty("sfondo").GetBoolean());
    }

    [Fact]
    public async Task UnoStratoPortaFileRecordEtichettaECoordinate()
    {
        await Apri();
        var forme = (await Chiedi("/mappa/strato/punti")).GetProperty("f").EnumerateArray().ToList();

        var fix = forme.First(f => f.GetProperty("e").GetString() == "BC404");
        Assert.EndsWith("NAVAIDS/APT.fix", fix.GetProperty("p").GetString());
        Assert.True(fix.GetProperty("r").GetInt32() >= 0);
        Assert.Equal("p", fix.GetProperty("t").GetString());

        // I punti di un tratto sono numeri in fila, a coppie: un punto solo = due numeri.
        var tratto = fix.GetProperty("c").EnumerateArray().Single();
        Assert.Equal(2, tratto.GetArrayLength());
        Assert.InRange(tratto[0].GetDouble(), 35, 48);
        Assert.InRange(tratto[1].GetDouble(), 6, 20);
    }

    [Fact]
    public async Task LeCoordinateSonoTagliateACinqueDecimali()
    {
        await Apri();
        var numeri = (await Chiedi("/mappa/strato/settori")).GetProperty("f").EnumerateArray()
            .SelectMany(f => f.GetProperty("c").EnumerateArray())
            .SelectMany(t => t.EnumerateArray())
            .Select(n => n.GetDouble())
            .Take(500)
            .ToList();

        Assert.NotEmpty(numeri);
        // Cinque decimali sono circa un metro: sotto, si spedirebbero cifre che il sector non ha.
        Assert.All(numeri, n => Assert.Equal(n, Math.Round(n, MappaDelLab.Decimali)));
    }

    [Fact]
    public async Task UnaFormaCheNonSiDisegnaPortaIlNomeCheNonSiRisolve()
    {
        _albero.Scrivi("SectorFiles/Include/IT/DYNAMIC_SEC/prova.tfl", """
            PROVA_CTR;CTR;1;CTR;1;
            N041.13.55.000;E014.47.19.000;
            NOMEFINTO;NOMEFINTO;

            """);
        await Apri();

        var forme = (await Chiedi("/mappa/strato/settori")).GetProperty("f").EnumerateArray().ToList();
        var rotta = forme.First(f => f.GetProperty("p").GetString()!.EndsWith("prova.tfl", StringComparison.Ordinal));

        Assert.Contains("NOMEFINTO", rotta.GetProperty("x").EnumerateArray().Select(n => n.GetString()));
    }
}
