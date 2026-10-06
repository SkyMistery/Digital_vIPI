using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Le aerovie a mano nel Lab (lotto «Subito» slice 14d-14f; B4, B14, B15): le etichette calcolate, aggiungere e togliere
/// un'aerovia, coi punti veri dei campioni — una voce sola nelle modifiche, che si annulla tutta insieme.
/// </summary>
public sealed class AerovieAManoNelLabTests : IDisposable
{
    private const string Aerovie = "SectorFiles/Include/IT/AIRWAY/prova14.lairway";

    private static readonly string[] DiPartenza =
    [
        "//Airway tracks",
        "T;ZZ1;ELB;ELB;", "T;ZZ1;PIS;PIS;", "T;ZZ1;CHI;CHI;",
        "",
        "//Airway labels",
    ];

    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;

    public AerovieAManoNelLabTests()
    {
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        _contesto.Services.AddSingleton(_lab);
        _contesto.JSInterop.Mode = JSRuntimeMode.Loose;
        _contesto.JSInterop.Setup<bool>("sectorlab.mappa.crea", _ => true).SetResult(true);
        _albero.Scrivi(Aerovie, string.Join("\r\n", DiPartenza) + "\r\n");
    }

    public void Dispose()
    {
        _contesto.Dispose();
        _albero.Dispose();
    }

    private async Task Apri()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        // I punti sono VOR dei campioni; senza soglia ogni tratto ha la sua etichetta, quanto è lungo non conta.
        Assert.True(_lab.CambiaLaSogliaDelleEtichette("0"));
    }

    [Fact]
    public async Task LaSogliaSiCambiaSiRicordaESiRifiutaSeNonEUnNumero()
    {
        Assert.Equal(EtichetteDelleAerovie.SogliaDiBaseNm, _lab.SogliaDelleEtichette);
        await Apri();

        Assert.True(_lab.CambiaLaSogliaDelleEtichette("12,5"));
        Assert.Equal(12.5, new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab")).SogliaDelleEtichette);
        Assert.False(_lab.CambiaLaSogliaDelleEtichette("dieci"));
        Assert.Contains("miglia", _lab.Rifiuto, StringComparison.Ordinal);
        Assert.True(_lab.CambiaLaSogliaDelleEtichette(""));
        Assert.Equal(EtichetteDelleAerovie.SogliaDiBaseNm, _lab.SogliaDelleEtichette);
    }

    [Fact]
    public async Task SistemaLeEtichetteScriveQuelleCheMancanoInUnaVoceSola()
    {
        await Apri();

        var piano = _lab.PianoDelleEtichetteDi(Aerovie)!;
        Assert.Equal(2, piano.DaAggiungere.Count);
        Assert.True(_lab.SistemaLeEtichette(Aerovie));

        var righe = _lab.RigheDiAdesso(Aerovie);
        Assert.Equal(DiPartenza, righe.Take(DiPartenza.Length));
        Assert.Equal(2, righe.Skip(DiPartenza.Length).Count(r => r.StartsWith("L;ZZ1;", StringComparison.Ordinal)));
        Assert.Equal(1, _lab.Modifiche.Quante);
        Assert.True(_lab.PianoDelleEtichetteDi(Aerovie)!.Vuoto);
        Assert.False(_lab.SistemaLeEtichette(Aerovie));

        // Annullato, il file torna quello dell'apertura.
        _lab.Annulla();
        Assert.Equal(DiPartenza, _lab.RigheDiAdesso(Aerovie));
    }

    [Fact]
    public async Task UnAeroviaAggiuntaHaBloccoTrattiEdEtichetteESiAnnullaInUnColpo()
    {
        await Apri();

        Assert.True(_lab.AggiungiUnAerovia(Aerovie, "ZZ2", "ost tea sor"));

        var righe = _lab.RigheDiAdesso(Aerovie).ToList();
        int inizio = righe.IndexOf("//@\"ZZ2\" locked=si");
        Assert.Equal(DiPartenza.ToList().IndexOf("T;ZZ1;CHI;CHI;") + 1, inizio);
        Assert.Equal(
            ["//@START", "//@@\"OST\" dir=both", "T;ZZ2;OST;OST;", "//@@\"TEA\" dir=both", "T;ZZ2;TEA;TEA;", "T;ZZ2;SOR;SOR;", "//@END \"ZZ2\""],
            righe.Skip(inizio + 1).Take(7));
        Assert.Equal(2, righe.Count(r => r.StartsWith("L;ZZ2;", StringComparison.Ordinal)));
        // Le etichette della ZZ1, che mancavano già, non le ha toccate: chi aggiunge un'aerovia non mette mano alle altre.
        Assert.DoesNotContain(righe, r => r.StartsWith("L;ZZ1;", StringComparison.Ordinal));

        // Il record nuovo è scelto, coi suoi tratti «nei due versi» e segnato a mano.
        Assert.Equal("ZZ2", _lab.AeroviaDi(Aerovie, _lab.Scelta!.Value.Record));
        Assert.Equal(["both", "both"], _lab.TrattiDellAeroviaDi(Aerovie, _lab.Scelta.Value.Record).Select(t => t.Verso));
        // I tratti dicono il verso e non le quote: l'avviso che chiede di scriverle.
        var senzaQuote = TrattiDelleAerovie.Problemi(_lab.Sessione!).ToList();
        Assert.Equal(2, senzaQuote.Count);
        Assert.All(senzaQuote, p => Assert.Equal(Regola.TrattoSenzaQuote, p.Regola));
        Assert.Equal("T;ZZ2;OST;OST;", senzaQuote[0].Testo);

        _lab.Annulla();
        Assert.Equal(DiPartenza, _lab.RigheDiAdesso(Aerovie));

        // Un nome che c'è già, o un punto che non esiste: rifiutata col perché, e il file non cambia.
        Assert.False(_lab.AggiungiUnAerovia(Aerovie, "ZZ1", "OST TEA"));
        Assert.Contains("c'è già", _lab.Rifiuto, StringComparison.Ordinal);
        Assert.False(_lab.AggiungiUnAerovia(Aerovie, "ZZ3", "OST QQQQQ"));
        Assert.Contains("QQQQQ", _lab.Rifiuto, StringComparison.Ordinal);
        Assert.Equal(DiPartenza, _lab.RigheDiAdesso(Aerovie));
    }

    [Fact]
    public async Task ToltaUnAeroviaIlFileNonLaHaPiu()
    {
        await Apri();
        Assert.True(_lab.AggiungiUnAerovia(Aerovie, "ZZ2", "OST TEA SOR"));

        Assert.NotNull(_lab.Scelta);
        Assert.True(_lab.TogliLAerovia(Aerovie, "ZZ2"));

        // Il record scelto era suo: tolta lei, non resta scelto quello che ne ha preso il numero.
        Assert.Null(_lab.Scelta);

        Assert.Equal(DiPartenza, _lab.RigheDiAdesso(Aerovie));
        Assert.False(_lab.TogliLAerovia(Aerovie, "ZZ9"));
        Assert.Contains("non c'è", _lab.Rifiuto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ITreGestiStannoNellaSchedaDiUnAerovia()
    {
        await Apri();
        var pagina = _contesto.RenderComponent<Home>();
        await pagina.InvokeAsync(() => _lab.Scegli(Aerovie, 0));

        pagina.WaitForAssertion(() => Assert.Equal("2+0+0", pagina.Find("[data-piano-etichette]").GetAttribute("data-piano-etichette")));
        Assert.Equal("ZZ1", pagina.Find("[data-togli-aerovia]").GetAttribute("data-togli-aerovia"));
        pagina.Find("[data-sistema-etichette]").Click();
        pagina.WaitForAssertion(() => Assert.Equal("0+0+0", pagina.Find("[data-piano-etichette]").GetAttribute("data-piano-etichette")));

        pagina.Find("[data-nome-aerovia]").Change("ZZ2");
        pagina.Find("[data-punti-aerovia]").Change("OST TEA");
        pagina.Find("[data-aggiungi-aerovia]").Click();
        pagina.WaitForAssertion(() => Assert.Contains("T;ZZ2;TEA;TEA;", _lab.RigheDiAdesso(Aerovie)));
    }
}
