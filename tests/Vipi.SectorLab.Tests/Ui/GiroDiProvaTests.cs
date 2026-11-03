using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Il kit di prova per gli AOD (carta del 3 novembre 2026): la cartella di lavoro proposta al primo avvio (K2), il Giro
/// di prova acceso dal kit e ricordato (K3, K7), il pannello coi compiti e i tre esiti (K5, K6), «Manda gli esiti» come
/// issue già scritta su GitHub (K8).
/// </summary>
public sealed class GiroDiProvaTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly List<Uri> _aperte = [];
    private string Dati => Path.Combine(_albero.Radice, "dati-del-lab");

    public GiroDiProvaTests() => _contesto.JSInterop.Mode = JSRuntimeMode.Loose;

    public void Dispose()
    {
        _contesto.Dispose();
        _albero.Dispose();
    }

    private SessioneDelLab NuovoLab() => new(Dati) { ApriNelBrowser = _aperte.Add };

    [Fact]
    public void LaCartellaDiLavoroPropostaNasceScritta()
    {
        string proposta = Path.Combine(_albero.Radice, "kit", "lavoro");
        _contesto.Services.AddSingleton(new SessioneDelLab(Dati) { LavoroProposto = proposta });

        var pagina = _contesto.RenderComponent<Home>();

        Assert.Equal(proposta, pagina.Find("[data-campo='cartella-di-lavoro']").GetAttribute("value"));
        Assert.Contains(Path.Combine(proposta, "sector"), pagina.Find("[data-cartelle-del-primo-avvio]").TextContent, StringComparison.Ordinal);
        Assert.False(Directory.Exists(proposta));
    }

    [Fact]
    public async Task SenzaIlKitIlGiroNonCe()
    {
        var lab = NuovoLab();
        Assert.True(await lab.ApriEValidaAsync(_albero.Radice));
        _contesto.Services.AddSingleton(lab);

        var pagina = _contesto.RenderComponent<Home>();

        Assert.False(lab.GiroAcceso);
        Assert.Empty(pagina.FindAll("[data-giro]"));
        Assert.Empty(pagina.FindAll("[data-tasto='giro']"));
    }

    [Fact]
    public async Task IlGiroSiAccendeDalKitERiprendeDopoUnRiavvio()
    {
        var lab = NuovoLab();
        lab.AccendiIlGiro();
        Assert.True(await lab.ApriEValidaAsync(_albero.Radice));
        _contesto.Services.AddSingleton(lab);
        var pagina = _contesto.RenderComponent<Home>();

        Assert.Equal("G1", pagina.Find("[data-compito]").GetAttribute("data-compito"));
        Assert.Contains("0/12", pagina.Find("[data-tasto='giro']").TextContent, StringComparison.Ordinal);

        pagina.Find("[data-tasto='giro-va']").Click();

        pagina.WaitForAssertion(() => Assert.Equal("G2", pagina.Find("[data-compito]").GetAttribute("data-compito")));
        Assert.Contains("1/12", pagina.Find("[data-tasto='giro']").TextContent, StringComparison.Ordinal);
        Assert.Equal("va", pagina.Find("[data-pallino='G1']").GetAttribute("data-esito"));

        // Chiuso dalla ×, si riapre dal tasto in barra.
        pagina.Find("[data-tasto='giro-chiudi']").Click();
        pagina.WaitForAssertion(() => Assert.Empty(pagina.FindAll("[data-giro]")));
        pagina.Find("[data-tasto='giro']").Click();
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-giro]")));

        // Riavviato senza il .cmd (niente --giro-di-prova): il giro c'è, con l'esito, aperto sul primo da fare.
        Assert.Contains("\"Esito\":\"Va\"", File.ReadAllText(Path.Combine(Dati, "giro-di-prova.json")), StringComparison.Ordinal);
        var dopo = new SessioneDelLab(Dati);
        Assert.True(dopo.GiroAcceso);
        Assert.True(dopo.GiroAperto);
        Assert.Equal(EsitoDelCompito.Va, dopo.EsitiDelGiro["G1"].Esito);
        Assert.Equal(1, dopo.CompitoAperto);
    }

    [Fact]
    public async Task NonHoCapitoTieneLaNotaEVaiAPortaAllaPagina()
    {
        var lab = NuovoLab();
        lab.AccendiIlGiro();
        Assert.True(await lab.ApriEValidaAsync(_albero.Radice));
        _contesto.Services.AddSingleton(lab);
        var pagina = _contesto.RenderComponent<Home>();

        // G1 si fa nelle Impostazioni: «Vai a Impostazioni» ci porta.
        pagina.Find("[data-tasto='giro-vai']").Click();
        pagina.WaitForAssertion(() => Assert.Equal(PaginaDelLab.Impostazioni, lab.Pagina));
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-giro-pagina='qui']")));

        pagina.Find("[data-campo='giro-nota']").Input("non trovo dove si scrive il VID");
        pagina.Find("[data-tasto='giro-nonhocapito']").Click();

        var esito = lab.EsitiDelGiro["G1"];
        Assert.Equal(EsitoDelCompito.NonHoCapito, esito.Esito);
        Assert.Equal("non trovo dove si scrive il VID", esito.Nota);
        pagina.WaitForAssertion(() => Assert.Equal("nonhocapito", pagina.Find("[data-pallino='G1']").GetAttribute("data-esito")));
        // Il compito dopo nasce con la nota vuota; tornando a G1 c'è la sua.
        Assert.Equal("", pagina.Find("[data-campo='giro-nota']").GetAttribute("value") ?? "");
        pagina.Find("[data-pallino='G1']").Click();
        pagina.WaitForAssertion(() => Assert.Equal("non trovo dove si scrive il VID", pagina.Find("[data-campo='giro-nota']").GetAttribute("value")));
    }

    [Fact]
    public void LUltimoCompitoESoloUnaNota()
    {
        var lab = NuovoLab();
        lab.AccendiIlGiro();
        var ultimo = GiroDiProva.Compiti[^1];
        Assert.True(ultimo.SoloNota);

        lab.ScriviLaNota(ultimo.Id, "  le aerovie in un altro colore  ");

        Assert.Null(lab.EsitiDelGiro[ultimo.Id].Esito);
        Assert.True(GiroDiProva.Fatto(ultimo, lab.EsitiDelGiro[ultimo.Id]));
        Assert.Equal(1, lab.CompitiFatti);
    }

    [Fact]
    public void MandaGliEsitiChiedeIlVidEPoiApreLaIssue()
    {
        var lab = NuovoLab();
        lab.AccendiIlGiro();
        lab.DaiLEsito("G1", EsitoDelCompito.Va, null);
        lab.DaiLEsito("G4", EsitoDelCompito.NonVa, "lo stand | torna indietro\nal rilascio");

        Assert.False(lab.MandaGliEsiti());
        Assert.Contains("VID", lab.PercheNonMandaGliEsiti, StringComparison.Ordinal);
        Assert.Empty(_aperte);

        Assert.True(lab.CambiaIlVid("123456"));
        Assert.True(lab.MandaGliEsiti(new DateTime(2026, 11, 3, 10, 30, 0, DateTimeKind.Local)));

        var pagina = Assert.Single(_aperte);
        Assert.StartsWith("https://github.com/SkyMistery/it-aurora-sector-test/issues/new?title=", pagina.AbsoluteUri, StringComparison.Ordinal);
        string titolo = Parametro(pagina, "title");
        string testo = Parametro(pagina, "body");
        Assert.Equal("Giro di prova · VID 123456 · 03/11/2026", titolo);
        Assert.Contains("2 compiti su 12: ✅ 1 · ❌ 1 · ❓ 0", testo, StringComparison.Ordinal);
        Assert.Contains("| ✅ | G1 Chi sei e che Aurora usi |  |", testo, StringComparison.Ordinal);
        Assert.Contains("| ❌ | G4 Modificare sulla mappa | lo stand \\| torna indietro<br>al rilascio |", testo, StringComparison.Ordinal);
        Assert.Contains("| · | G5 ", testo, StringComparison.Ordinal);
        Assert.False(lab.EsitiTagliati);
        Assert.Contains(titolo, File.ReadAllText(lab.FileDegliEsitiDelGiro), StringComparison.Ordinal);
    }

    [Fact]
    public void UnTestoLungoSiTagliaEDiceDoveStaIlFile()
    {
        var esiti = GiroDiProva.Compiti.ToDictionary(c => c.Id,
            c => new EsitoDelGiro(c.Id, c.SoloNota ? null : EsitoDelCompito.NonVa, new string('à', 400), DateTime.Now));
        var (titolo, testo) = GiroDiProva.Issue("123456", "1.0.0", "Windows", DateTime.Now, esiti, ["2026-11-03 10:00:00.000 [  1] ERRORE       x"]);

        var (pagina, tagliato) = GiroDiProva.PaginaDellaIssue("o/r", titolo, testo, @"C:\kit\dati\esiti-del-giro.md");

        Assert.True(tagliato);
        string corpo = Parametro(pagina, "body");
        Assert.True(Uri.EscapeDataString(corpo).Length <= GiroDiProva.TettoDelTesto);
        Assert.EndsWith("**Il testo intero è in `C:\\kit\\dati\\esiti-del-giro.md`: trascina il file qui.**\n", corpo, StringComparison.Ordinal);
        Assert.StartsWith(testo[..200], corpo, StringComparison.Ordinal);
    }

    [Fact]
    public void DalRegistroSoloLaPrimaRigaDegliErroriEAlPiuQuindici()
    {
        string cartella = Path.Combine(_albero.Radice, "log");
        Directory.CreateDirectory(cartella);
        var righe = new List<string> { "2026-11-02 09:00:00.000 [  1] avvio        Aurora Sector Lab" };
        for (int i = 0; i < 20; i++)
        {
            righe.Add($"2026-11-02 09:00:{i:00}.000 [  4] ERRORE       mappa: System.InvalidOperationException: numero {i}");
            righe.Add("   at Vipi.SectorLab.Qualcosa()");
        }
        File.WriteAllLines(Path.Combine(cartella, "lab-20261102.txt"), righe);
        File.WriteAllLines(Path.Combine(cartella, "lab-20261103.txt"), ["2026-11-03 08:00:00.000 [  5] ERRORE       giro: l'ultimo"]);

        var errori = GiroDiProva.ErroriDelRegistro(cartella);

        Assert.Equal(GiroDiProva.ErroriNellIssue, errori.Count);
        Assert.EndsWith("giro: l'ultimo", errori[^1], StringComparison.Ordinal);
        Assert.EndsWith("numero 6", errori[0], StringComparison.Ordinal);
        Assert.DoesNotContain(errori, e => e.Contains("   at ", StringComparison.Ordinal));
    }

    [Fact]
    public void ICompitiHannoUnNomeSoloEUnaPaginaCheCe()
    {
        var compiti = GiroDiProva.Compiti;
        Assert.Equal(compiti.Count, compiti.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count());
        foreach (var compito in compiti.Where(c => c.Pagina is not null))
        {
            Assert.True(Enum.IsDefined(compito.Pagina!.Value));
        }
    }

    private static string Parametro(Uri pagina, string nome)
    {
        foreach (string coppia in pagina.Query.TrimStart('?').Split('&'))
        {
            int uguale = coppia.IndexOf('=', StringComparison.Ordinal);
            if (coppia[..uguale] == nome)
                return Uri.UnescapeDataString(coppia[(uguale + 1)..]);
        }

        throw new InvalidOperationException($"Nel link non c'è «{nome}».");
    }
}
