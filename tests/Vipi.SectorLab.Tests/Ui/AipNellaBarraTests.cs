using Aip;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Tests.PdfAip;
using Vipi.SectorLab.Ui.Components.Pages;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.Ui;

/// <summary>
/// Carta F6, slice 0c: il riquadro «AIP» nella barra — la cartella dei PDF, «Prendi i PDF da Download» e «Leggi
/// nell'AIP espanso». La cartella Download è finta, nel temporaneo: il test non tocca mai quella del PC.
/// </summary>
public sealed class AipNellaBarraTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly TestContext _contesto = new();
    private readonly SessioneDelLab _lab;
    private readonly string _fuori = Path.Combine(Path.GetTempPath(), "aip-barra-" + Guid.NewGuid().ToString("N"));

    public AipNellaBarraTests()
    {
        Directory.CreateDirectory(Path.Combine(_fuori, "Downloads"));
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab")) { Download = Path.Combine(_fuori, "Downloads") };
        _contesto.Services.AddSingleton(_lab);
        _contesto.JSInterop.Mode = JSRuntimeMode.Loose;
        _contesto.JSInterop.Setup<bool>("sectorlab.mappa.crea", _ => true).SetResult(true);
        // Riordino delle pagine (R1): i PDF, l'AIP espanso e i confronti stanno nella pagina AIP.
        _lab.ScegliLaPagina(PaginaDelLab.Aip);
    }

    public void Dispose()
    {
        _contesto.Dispose();
        _albero.Dispose();
        Directory.Delete(_fuori, recursive: true);
    }

    [Fact]
    public async Task DaDownloadAllAipEspanso()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pagina = _contesto.RenderComponent<Home>();
        pagina.WaitForAssertion(() => Assert.Equal("— da scegliere", pagina.Find("[data-quanti-pdf]").TextContent.Trim()), TimeSpan.FromSeconds(10));
        Assert.NotNull(pagina.Find("[data-senza-pdf]"));
        // Senza la cartella dei PDF si prende lo stesso l'AIP espanso: serve per guardarlo e confrontarlo (committente, 19 ottobre).
        Assert.NotNull(pagina.Find("[data-tasto=prendi-aip-espanso]"));

        // Le cartelle, come dalle Impostazioni: l'AIP espanso nella cartella dei PDF no.
        var pdf = Path.Combine(_fuori, "AIP");
        Assert.False(_lab.CambiaLaCartellaDeiPdf("relativa"));
        Assert.True(_lab.CambiaLaCartellaDeiPdf(pdf), _lab.Rifiuto);
        Assert.False(_lab.CambiaLAipEspanso(pdf));
        Assert.True(_lab.CambiaLAipEspanso(Path.Combine(_fuori, "espanso")), _lab.Rifiuto);

        // In Download un PDF dell'AIP e uno no: si sposta il primo, l'altro resta.
        await File.WriteAllBytesAsync(Path.Combine(_lab.Download, "LI-ENR 5.1.2.pdf"), PdfAipFinto.Enr51("06 AUG 2026", "LI R901 - Prova Uno"));
        await File.WriteAllTextAsync(Path.Combine(_lab.Download, "fattura.pdf"), "mia");
        pagina.WaitForAssertion(() => Assert.NotNull(pagina.Find("[data-tasto=prendi-da-download]")), TimeSpan.FromSeconds(10));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=prendi-da-download]").Click());

        pagina.WaitForAssertion(() =>
        {
            Assert.Contains("Presi 1: 1 nuovi", pagina.Find("[data-presa]").TextContent, StringComparison.Ordinal);
            Assert.Equal("1 PDF", pagina.Find("[data-quanti-pdf]").TextContent.Trim());
        }, TimeSpan.FromSeconds(10));
        Assert.True(File.Exists(Path.Combine(pdf, "LI-ENR 5.1.2.pdf")));
        Assert.True(File.Exists(Path.Combine(_lab.Download, "fattura.pdf")));

        // Il ciclo proposto è quello di oggi; si scrive quello voluto, e si legge.
        Assert.Equal(_lab.CicloDiOggi, pagina.Find("[data-campo=ciclo]").GetAttribute("value"));
        await pagina.InvokeAsync(() => pagina.Find("[data-campo=ciclo]").Change("2611"));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=leggi-aip]").Click());

        pagina.WaitForAssertion(() =>
        {
            var esito = pagina.Find("[data-esito-aip]").TextContent;
            Assert.Contains("Ciclo 2611", esito, StringComparison.Ordinal);
            Assert.Contains("1 aree", esito, StringComparison.Ordinal);
            Assert.Contains("06/08/2026 ×1", pagina.Find("[data-date-aip]").TextContent, StringComparison.Ordinal);
        }, TimeSpan.FromSeconds(20));

        var doc = AipJson.Deserialize(await File.ReadAllTextAsync(Path.Combine(_fuori, "espanso", "2611", "li.json")));
        Assert.Equal("LI R901", Assert.Single(doc.Airspaces).Designator);

        // Un ciclo scritto male non si legge.
        Assert.False(await _lab.LeggiLAipAsync("26"));
        Assert.Contains("non è un ciclo AIRAC", _lab.Rifiuto, StringComparison.Ordinal);
    }

    // Slice 1b: «Confronta i CTR», dall'AIP espanso al sector aperto; un clic sulla riga apre la voce del sector.
    [Fact]
    public async Task ConfrontaICtr_LaListaEIlClicSullaVoce()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var espanso = Path.Combine(_fuori, "espanso");
        Assert.True(_lab.CambiaLAipEspanso(espanso), _lab.Rifiuto);
        var pagina = _contesto.RenderComponent<Home>();

        // Senza un ciclo letto si dice.
        Assert.False(await _lab.ConfrontaAsync());
        Assert.Contains("non c'è ancora un ciclo", _lab.Rifiuto, StringComparison.Ordinal);

        // Un CTR inventato, in Sardegna: nel sector di prova non c'è; il CTR di Roma del sector non è nell'AIP.
        Directory.CreateDirectory(Path.Combine(espanso, "2611"));
        var ctr = new Airspace
        {
            Section = "ENR 2.1",
            Type = AirspaceType.Ctr,
            Designator = "Prova CTR",
            Upper = new VerticalLimit { Text = "2000ft AMSL" },
            Lower = new VerticalLimit { Text = "SFC" },
            Geometry = new CircleGeometry(new Position(40.0, 9.0), new Distance(5, DistanceUnit.Nm)),
        };
        await File.WriteAllTextAsync(Path.Combine(espanso, "2611", "li.json"), AipJson.Serialize(new AipDocument { State = "LI", Airac = "2611", Airspaces = [ctr] }));

        await pagina.InvokeAsync(() => pagina.Find("[data-pagina=confronti]").Click());
        await pagina.InvokeAsync(() => pagina.Find("[data-sezione=ctr]").Click());

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal("manca nel sector 1 · solo nel sector 1", pagina.Find("[data-riassunto-confronto]").TextContent.Trim());
            Assert.Equal(2, pagina.FindAll("[data-riga]").Count);
        }, TimeSpan.FromSeconds(10));
        await pagina.InvokeAsync(() => pagina.Find("[data-riga][data-esito=SoloNelSector] button.lab-voce").Click());
        Assert.Equal("SectorFiles/Include/IT/lirf.str", _lab.Scelta?.File);
    }

    private static Airspace CtrDiProva(string designatore, Geometry geometria) => new()
    {
        Section = "ENR 2.1",
        Type = AirspaceType.Ctr,
        Designator = designatore,
        Upper = new VerticalLimit { Text = "2000ft AMSL" },
        Lower = new VerticalLimit { Text = "SFC" },
        Geometry = geometria,
    };

    private Task<Bunit.IRenderedComponent<Home>> ConfrontaConAsync(string str, params Airspace[] aip) => ConfrontaConAsync(str, null, "ctr", aip);

    private async Task<Bunit.IRenderedComponent<Home>> ConfrontaConAsync(string str, string? torri, string sezione, params Airspace[] aip)
    {
        _albero.Scrivi("SectorFiles/Include/IT/lirf.str", str);
        if (torri is not null)
            _albero.Scrivi("SectorFiles/Include/IT/DYNAMIC_SEC/twrs.tfl", torri);
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var espanso = Path.Combine(_fuori, "espanso");
        Assert.True(_lab.CambiaLAipEspanso(espanso), _lab.Rifiuto);
        Directory.CreateDirectory(Path.Combine(espanso, "2611"));
        await File.WriteAllTextAsync(Path.Combine(espanso, "2611", "li.json"), AipJson.Serialize(new AipDocument { State = "LI", Airac = "2611", Airspaces = aip }));
        var pagina = _contesto.RenderComponent<Home>();
        // R7: i confronti e i loro gesti stanno nello spazio Confronti.
        await pagina.InvokeAsync(() => pagina.Find("[data-pagina=confronti]").Click());
        await pagina.InvokeAsync(() => pagina.Find($"[data-sezione={sezione}]").Click());
        return pagina;
    }

    // Riordino R3 (D9): la pagina Indice carica da sé lo strato dell'AIP e mette le voci come nell'AIP; un clic apre la
    // scheda della voce accanto.
    [Fact]
    public async Task LIndice_CaricaLAip_ELaVoceSiApreNellaScheda()
    {
        _albero.Scrivi("SectorFiles/Include/IT/lirf.str", "//MENU MAPS\r\n");
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var espanso = Path.Combine(_fuori, "espanso");
        Assert.True(_lab.CambiaLAipEspanso(espanso), _lab.Rifiuto);
        Directory.CreateDirectory(Path.Combine(espanso, "2611"));
        await File.WriteAllTextAsync(Path.Combine(espanso, "2611", "li.json"), AipJson.Serialize(new AipDocument
        {
            State = "LI",
            Airac = "2611",
            Airspaces = [CtrDiProva("Roma CTR", new CircleGeometry(new Position(41.9, 12.5), new Distance(5, DistanceUnit.Nm)))],
            Points = [new SignificantPoint { Section = "ENR 4.4", Designator = "ABRUS", Position = new Position(41.5, 12.25) }],
        }));
        _lab.ScegliLaPagina(PaginaDelLab.AipIndice);

        var pagina = _contesto.RenderComponent<Home>();

        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-parte='ENR 4']")), TimeSpan.FromSeconds(10));
        Assert.Contains("ciclo 2611", pagina.Find("[data-indice-aip] h2").TextContent, StringComparison.Ordinal);
        Assert.Equal(["ENR 2", "ENR 4"], pagina.FindAll("[data-parte]").Select(e => e.GetAttribute("data-parte")));
        Assert.NotEmpty(pagina.FindAll("[data-scheda-aip-vuota]"));

        // Trovare e cliccare nello stesso giro del renderer: la validazione può ridisegnare la pagina fra i due
        // (CI 21-ott, UnknownEventHandlerIdException).
        await pagina.InvokeAsync(() => pagina.FindAll("[data-voce-aip]").Single(e => e.TextContent.Trim() == "ABRUS").Click());

        pagina.WaitForAssertion(() => Assert.Empty(pagina.FindAll("[data-scheda-aip-vuota]")));
        Assert.Equal(1, _lab.SceltaDellAip);
        Assert.Contains("ABRUS", pagina.Markup, StringComparison.Ordinal);
    }

    // Slice 1c: «Sostituisci» su una riga «forma diversa» — la voce riscritta con le zone dell'AIP, la testata e quel che
    // segue restano; il confronto rifatto la dà uguale; un «annulla» la rimette com'era.
    [Fact]
    public async Task Sostituisci_LaVoceCtrPrendeLaFormaDellAip_ESiAnnulla()
    {
        var pagina = await ConfrontaConAsync(
            "//MENU MAPS\r\nLIRF;MAPS;LIRF CTR; ; ;1;\r\nN042.00.00.000;E012.00.00.000;\r\nN042.00.00.000;E012.30.00.000;\r\nN041.48.00.000;E012.30.00.000;\r\nN041.48.00.000;E012.00.00.000;\r\nN042.00.00.000;E012.00.00.000;\r\n\r\nLIRF;MAPS;LIRF ATZ; ; ;5;\r\nN041.50.00.000;E012.10.00.000;\r\nN041.52.00.000;E012.10.00.000;\r\n",
            // Un vertice in più a est, 4 NM fuori dal lato del sector (Malpensa, 18 ottobre); e una seconda zona.
            CtrDiProva("Roma CTR Zona/Zone '1'", new RingGeometry([
                new LineSegment(new Position(42.0, 12.0)), new LineSegment(new Position(42.0, 12.5)), new LineSegment(new Position(41.9, 12.6)),
                new LineSegment(new Position(41.8, 12.5)), new LineSegment(new Position(41.8, 12.0))])),
            CtrDiProva("Roma CTR Zona/Zone '2'", new RingGeometry([
                new LineSegment(new Position(42.0, 12.0)), new LineSegment(new Position(41.8, 12.0)), new LineSegment(new Position(41.9, 11.9))])));
        pagina.WaitForAssertion(() => Assert.Equal("diversa 1", pagina.Find("[data-riassunto-confronto]").TextContent.Trim()), TimeSpan.FromSeconds(10));
        string prima = string.Join("\n", _lab.RigheDiAdesso("SectorFiles/Include/IT/lirf.str"));

        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=sostituisci]").Click());

        pagina.WaitForAssertion(() => Assert.Equal("uguale 1", pagina.Find("[data-riassunto-confronto]").TextContent.Trim()), TimeSpan.FromSeconds(10));
        var righe = _lab.RigheDiAdesso("SectorFiles/Include/IT/lirf.str");
        Assert.Equal(
        [
            "//MENU MAPS", "LIRF;MAPS;LIRF CTR; ; ;1;",
            "//zona 1", "N042.00.00.000;E012.00.00.000;", "N042.00.00.000;E012.30.00.000;", "N041.54.00.000;E012.36.00.000;",
            "N041.48.00.000;E012.30.00.000;", "N041.48.00.000;E012.00.00.000;", "N042.00.00.000;E012.00.00.000;",
            "//zona 2", "N042.00.00.000;E012.00.00.000;<br>", "N041.48.00.000;E012.00.00.000;", "N041.54.00.000;E011.54.00.000;", "N042.00.00.000;E012.00.00.000;",
            "", "LIRF;MAPS;LIRF ATZ; ; ;5;",
        ], righe.Take(16));
        Assert.Contains(_lab.Modifiche.Voci, m => m.File == "SectorFiles/Include/IT/lirf.str");
        Assert.Equal("LIRF LIRF CTR: la forma di Roma CTR dall'AIP 2611", _lab.DaAnnullare);

        _lab.Annulla();

        Assert.Equal(prima, string.Join("\n", _lab.RigheDiAdesso("SectorFiles/Include/IT/lirf.str")));
        Assert.False(_lab.Modifiche.CEQualcosa);
    }

    // Slice 1c: «Porta nel sector» su una riga «manca nel sector» — la voce nuova nello .str dello scalo, prima delle altre
    // del MAPS; lo scalo lo scrive l'AOD.
    [Fact]
    public async Task PortaNelSector_LaVoceNuovaNelloStrDelloScalo()
    {
        var pagina = await ConfrontaConAsync(
            "//MENU MAPS\r\nLIRF;MAPS;LIRF ATZ; ; ;5;\r\nN041.50.00.000;E012.10.00.000;\r\nN041.52.00.000;E012.10.00.000;\r\n",
            CtrDiProva("Roma CTR", new RingGeometry([
                new LineSegment(new Position(42.0, 12.0)), new LineSegment(new Position(42.0, 12.5)), new LineSegment(new Position(41.8, 12.5))])));
        pagina.WaitForAssertion(() => Assert.Equal("manca nel sector 1", pagina.Find("[data-riassunto-confronto]").TextContent.Trim()), TimeSpan.FromSeconds(10));

        // Senza scalo, o con uno che non ha il suo .str, si dice.
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=porta]").Click());
        Assert.Contains("quattro lettere", _lab.Rifiuto, StringComparison.Ordinal);
        await pagina.InvokeAsync(() => pagina.Find("[data-campo=scalo]").Change("lixx"));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=porta]").Click());
        Assert.Contains("non c'è lixx.str", _lab.Rifiuto, StringComparison.Ordinal);

        await pagina.InvokeAsync(() => pagina.Find("[data-campo=scalo]").Change("lirf"));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=porta]").Click());

        pagina.WaitForAssertion(() => Assert.Equal("uguale 1", pagina.Find("[data-riassunto-confronto]").TextContent.Trim()), TimeSpan.FromSeconds(10));
        Assert.Equal(
        [
            "//MENU MAPS", "LIRF;MAPS;LIRF CTR; ; ;1;",
            "N042.00.00.000;E012.00.00.000;", "N042.00.00.000;E012.30.00.000;", "N041.48.00.000;E012.30.00.000;", "N042.00.00.000;E012.00.00.000;",
            "", "LIRF;MAPS;LIRF ATZ; ; ;5;",
        ], _lab.RigheDiAdesso("SectorFiles/Include/IT/lirf.str").Take(8));
        Assert.Equal(("SectorFiles/Include/IT/lirf.str", 0), _lab.Scelta);

        // Una seconda volta no: la voce CTR c'è già.
        Assert.False(_lab.PortaNelSector(new Vipi.SectorLab.Core.PdfAip.DifferenzaDiForma(Vipi.SectorLab.Core.PdfAip.EsitoDelConfronto.MancaNelSector, "Roma CTR", 1, null, null, null, null, null, null, false), "LIRF"));
        Assert.Contains("ha già la voce LIRF CTR", _lab.Rifiuto, StringComparison.Ordinal);
    }

    // Le ATZ in twrs.tfl: il settore della torre riscritto senza ripetere il primo punto; niente «Porta nel sector»; una
    // differenza da errore di stampa (Viterbo, 221 NM) si mostra senza «Sostituisci».
    [Fact]
    public async Task AtzDelleTorri_SostituisciIlSettore_ELErroreDiStampaSiGuarda()
    {
        var pagina = await ConfrontaConAsync(
            "//MENU MAPS\r\nLIRF;MAPS;LIRF ATZ; ; ;5;\r\nN041.50.00.000;E012.10.00.000;\r\nN041.52.00.000;E012.10.00.000;\r\n",
            "//----------LIRR--------------\r\nLIRF_TWR;TWR;1;TWR;1;\r\nN042.00.00.000;E012.00.00.000;\r\nN042.00.00.000;E012.30.00.000;\r\nN041.48.00.000;E012.30.00.000;\r\nN041.48.00.000;E012.00.00.000;\r\n\r\n"
            + "LIRV_TWR;TWR;1;TWR;1;\r\nN042.30.00.000;E012.00.00.000;\r\nN042.30.00.000;E012.10.00.000;\r\nN042.20.00.000;E012.10.00.000;\r\n",
            "atzdelletorri",
            CtrDiProva("ATZ Roma LIRF - RMZ", new RingGeometry([
                new LineSegment(new Position(42.0, 12.0)), new LineSegment(new Position(42.0, 12.5)), new LineSegment(new Position(41.9, 12.6)),
                new LineSegment(new Position(41.8, 12.5)), new LineSegment(new Position(41.8, 12.0))])) with { Type = AirspaceType.Rmz },
            CtrDiProva("ATZ Viterbo LIRV - RMZ", new CircleGeometry(new Position(42.43, 17.06), new Distance(5, DistanceUnit.Nm))) with { Type = AirspaceType.Rmz });
        pagina.WaitForAssertion(() => Assert.Equal("diversa 2", pagina.Find("[data-riassunto-confronto]").TextContent.Trim()), TimeSpan.FromSeconds(10));
        Assert.Single(pagina.FindAll("[data-tasto=sostituisci]"));
        Assert.Single(pagina.FindAll("[data-da-guardare]"));
        Assert.Empty(pagina.FindAll("[data-tasto=porta]"));

        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=sostituisci]").Click());

        pagina.WaitForAssertion(() => Assert.Equal("uguale 1 · diversa 1", pagina.Find("[data-riassunto-confronto]").TextContent.Trim()), TimeSpan.FromSeconds(10));
        Assert.Equal(
        [
            "//----------LIRR--------------", "LIRF_TWR;TWR;1;TWR;1;",
            "N042.00.00.000;E012.00.00.000;", "N042.00.00.000;E012.30.00.000;", "N041.54.00.000;E012.36.00.000;",
            "N041.48.00.000;E012.30.00.000;", "N041.48.00.000;E012.00.00.000;", "", "LIRV_TWR;TWR;1;TWR;1;",
        ], _lab.RigheDiAdesso("SectorFiles/Include/IT/DYNAMIC_SEC/twrs.tfl").Take(9));

        // Viterbo no: si guarda il PDF.
        var viterbo = _lab.UltimoConfronto!.Value.Esiti.Single(e => e.Esito == Vipi.SectorLab.Core.PdfAip.EsitoDelConfronto.FormaDiversa);
        Assert.False(_lab.SostituisciLaVoce(viterbo));
        Assert.Contains("errore di stampa", _lab.Rifiuto, StringComparison.Ordinal);
    }

    // F6, prima della 2b: l'AIP espanso sulla mappa — lo strato in fondo agli Strati coi suoi tipi, la scheda di una voce
    // scelta col clic, e scegliere un record del sector la toglie.
    [Fact]
    public async Task LAipSullaMappa_LoStratoITipiELaScheda()
    {
        var pagina = await ConfrontaConAsync(
            "//MENU MAPS\r\nLIRF;MAPS;LIRF ATZ; ; ;5;\r\nN041.50.00.000;E012.10.00.000;\r\nN041.52.00.000;E012.10.00.000;\r\n",
            CtrDiProva("Roma CTR", new RingGeometry([
                new LineSegment(new Position(42.0, 12.0)), new LineSegment(new Position(42.0, 12.5)), new LineSegment(new Position(41.8, 12.5))])),
            CtrDiProva("ATZ Urbe LIRU - RMZ", new CircleGeometry(new Position(41.95, 12.5), new Distance(3, DistanceUnit.Nm))) with { Type = AirspaceType.Rmz });

        // R7: la casella sta in «Lettura e consegna», nello spazio AIP espanso.
        await pagina.InvokeAsync(() => pagina.Find("[data-pagina=aip]").Click());
        // Il Confronti ha già caricato lo strato: si aspetta che il gestore della casella abbia finito di ricaricarlo.
        await pagina.Find("[data-casella=aip-sulla-mappa]").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true });

        pagina.WaitForAssertion(() =>
        {
            Assert.Contains("ciclo 2611, 2 forme", pagina.Find("[data-aip-strato]").TextContent, StringComparison.Ordinal);
            Assert.True(pagina.Find("[data-strato=aip]").HasAttribute("checked"));
            Assert.Equal(["aip:CTR", "aip:RMZ/TMZ"], pagina.FindAll("[data-generi=aip] input").Select(i => i.GetAttribute("data-genere")));
        }, TimeSpan.FromSeconds(10));

        await pagina.InvokeAsync(() => _lab.Scegli("aip:2611", 0));

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal("Roma CTR", pagina.Find("[data-scheda-aip] .lab-etichetta").TextContent);
            Assert.Contains("2000ft AMSL – SFC", pagina.Find("[data-scheda-aip]").TextContent, StringComparison.Ordinal);
        }, TimeSpan.FromSeconds(10));
        Assert.Null(_lab.Scelta);
        Assert.Equal(("aip:2611", 0), _lab.SceltaSullaMappa);

        // Un record del sector: la scheda dell'AIP se ne va.
        await pagina.InvokeAsync(() => _lab.Scegli("SectorFiles/Include/IT/lirf.str", 0));
        pagina.WaitForAssertion(() => Assert.Empty(pagina.FindAll("[data-scheda-aip]")), TimeSpan.FromSeconds(10));

        // Spenta, lo strato non è più acceso.
        await pagina.InvokeAsync(() => pagina.Find("[data-casella=aip-sulla-mappa]").Change(false));
        Assert.DoesNotContain("aip", _lab.Accesi);
    }

    // Le correzioni dell'AIP espanso, slice 2: dalla scheda di un'area «Correggi» (il modulo), «Ritira la correzione»,
    // «Togli»; dal riquadro «Aggiungi un'area» con la forma incollata. Tutto va nel file delle correzioni e nel modello.
    [Fact]
    public async Task LeCorrezioniDallaSchedaDellArea()
    {
        var pagina = await ConfrontaConAsync("//MENU MAPS\r\n",
            CtrDiProva("Roma CTR", new CircleGeometry(new Position(41.9, 12.5), new Distance(5, DistanceUnit.Nm))));
        var espanso = Path.Combine(_fuori, "espanso");
        var correzioni = Path.Combine(espanso, "correzioni", "li.json");
        AipDocument Modello() => AipJson.Deserialize(File.ReadAllText(Path.Combine(espanso, "2611", "li.json")));
        // R7: la casella sta in «Lettura e consegna», nello spazio AIP espanso.
        await pagina.InvokeAsync(() => pagina.Find("[data-pagina=aip]").Click());
        // Il Confronti ha già caricato lo strato: si aspetta che il gestore della casella abbia finito di ricaricarlo.
        await pagina.Find("[data-casella=aip-sulla-mappa]").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true });
        pagina.WaitForAssertion(() => Assert.NotNull(pagina.Find("[data-aip-strato]")), TimeSpan.FromSeconds(10));
        await pagina.InvokeAsync(() => _lab.Scegli("aip:2611", 0));
        pagina.WaitForAssertion(() => Assert.NotNull(pagina.Find("[data-tasto=correggi-area]")), TimeSpan.FromSeconds(10));

        // Correggi: senza fonte no; con la fonte, il limite nuovo nel modello e la correzione nella scheda.
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=correggi-area]").Click());
        await pagina.InvokeAsync(() => pagina.Find("[data-campo=area-superiore]").Change("2500ft AMSL"));
        await pagina.InvokeAsync(() => pagina.Find("[data-campo=area-perche]").Change("limite sbagliato nel PDF"));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=salva-correzione]").Click());
        pagina.WaitForAssertion(() => Assert.Contains("fonte", pagina.Find("[data-rifiuto-correzione]").TextContent, StringComparison.Ordinal), TimeSpan.FromSeconds(10));
        Assert.False(File.Exists(correzioni));
        await pagina.InvokeAsync(() => pagina.Find("[data-campo=area-fonte]").Change("errore AIP, segnalato a ENAV"));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=salva-correzione]").Click());

        pagina.WaitForAssertion(() =>
        {
            Assert.Contains("Corretta (2", pagina.Find("[data-correzione-aip]").TextContent, StringComparison.Ordinal);
            Assert.Contains("2500ft AMSL – SFC", pagina.Find("[data-scheda-aip]").TextContent, StringComparison.Ordinal);
            Assert.Empty(pagina.FindAll("[data-modulo-area]"));
        }, TimeSpan.FromSeconds(10));
        Assert.Equal("2500ft AMSL", Modello().Airspaces[0].Upper.Text);
        Assert.Equal(CorrectionOp.Change, Assert.Single(AipCorrections.Deserialize(File.ReadAllText(correzioni)).Corrections).Op);
        Assert.StartsWith("Correzione dell'AIP espanso: Roma CTR", _lab.MessaggioDellAip, StringComparison.Ordinal);

        // Ritira: l'area torna com'è nel PDF.
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=ritira-correzione]").Click());
        pagina.WaitForAssertion(() =>
        {
            Assert.Empty(pagina.FindAll("[data-correzione-aip]"));
            Assert.Contains("2000ft AMSL – SFC", pagina.Find("[data-scheda-aip]").TextContent, StringComparison.Ordinal);
        }, TimeSpan.FromSeconds(10));
        Assert.Equal("2000ft AMSL", Modello().Airspaces[0].Upper.Text);

        // Aggiungi un'area dal riquadro, con la forma incollata come la scrive l'AIP.
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=aggiungi-area]").Click());
        await pagina.InvokeAsync(() => pagina.Find("[data-modulo-area=aggiungi] [data-campo=area-identificativo]").Change("LI R999"));
        await pagina.InvokeAsync(() => pagina.Find("[data-modulo-area=aggiungi] [data-campo=area-superiore]").Change("FL 100"));
        await pagina.InvokeAsync(() => pagina.Find("[data-modulo-area=aggiungi] [data-campo=area-inferiore]").Change("GND"));
        await pagina.InvokeAsync(() => pagina.Find("[data-modulo-area=aggiungi] [data-campo=area-forma]").Change("420000N 0120000E - 420000N 0123000E - 414800N 0123000E"));
        await pagina.InvokeAsync(() => pagina.Find("[data-modulo-area=aggiungi] [data-campo=area-perche]").Change("area nuova"));
        await pagina.InvokeAsync(() => pagina.Find("[data-modulo-area=aggiungi] [data-campo=area-fonte]").Change("AIP SUP 123/26"));
        await pagina.InvokeAsync(() => pagina.Find("[data-modulo-area=aggiungi] [data-tasto=salva-correzione]").Click());
        pagina.WaitForAssertion(() =>
        {
            Assert.Contains("ciclo 2611, 2 forme", pagina.Find("[data-aip-strato]").TextContent, StringComparison.Ordinal);
            Assert.Equal("LI R999", pagina.Find("[data-scheda-aip] .lab-etichetta").TextContent);
            Assert.Contains("Aggiunta (", pagina.Find("[data-correzione-aip]").TextContent, StringComparison.Ordinal);
        }, TimeSpan.FromSeconds(10));
        Assert.Equal(["Roma CTR", "LI R999"], Modello().Airspaces.Select(a => a.Key));

        // Togli il CTR (dalla sua scheda): perché e fonte, e non c'è più.
        await pagina.InvokeAsync(() => _lab.Scegli("aip:2611", 0));
        pagina.WaitForAssertion(() => Assert.NotNull(pagina.Find("[data-tasto=togli-area]")), TimeSpan.FromSeconds(10));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=togli-area]").Click());
        await pagina.InvokeAsync(() => pagina.Find("[data-campo=togli-perche]").Change("chiuso"));
        await pagina.InvokeAsync(() => pagina.Find("[data-campo=togli-fonte]").Change("AIC 9/26"));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=conferma-togli]").Click());
        pagina.WaitForAssertion(() => Assert.Contains("ciclo 2611, 1 forme", pagina.Find("[data-aip-strato]").TextContent, StringComparison.Ordinal), TimeSpan.FromSeconds(10));
        Assert.Equal(["LI R999"], Modello().Airspaces.Select(a => a.Key));
        Assert.Equal([CorrectionOp.Change, CorrectionOp.Add, CorrectionOp.Remove],
            AipCorrections.Deserialize(File.ReadAllText(correzioni)).Corrections.Select(c => c.Op));
    }

    // Slice 3: un punto dell'AIP espanso sulla mappa si sceglie e si corregge col modulo generale; «Aggiungi» chiede il tipo.
    [Fact]
    public async Task LeCorrezioniDiUnPunto_DalModuloGenerale()
    {
        var pagina = await ConfrontaConAsync("//MENU MAPS\r\n",
            CtrDiProva("Roma CTR", new CircleGeometry(new Position(41.9, 12.5), new Distance(5, DistanceUnit.Nm))));
        var espanso = Path.Combine(_fuori, "espanso");
        var li = Path.Combine(espanso, "2611", "li.json");
        var conPunto = AipJson.Deserialize(File.ReadAllText(li)) with
        {
            Points = [new SignificantPoint { Section = "ENR 4.4", Designator = "ABRUS", Position = new Position(41.5, 12.25) }],
        };
        // La lettura del confronto appena fatto può tenere il file ancora per un istante (una volta su qualche centinaio,
        // 20 ottobre): si riprova invece di cadere.
        for (var tentativo = 0; ; tentativo++)
        {
            try
            {
                await File.WriteAllTextAsync(li, AipJson.Serialize(conPunto));
                break;
            }
            catch (IOException) when (tentativo < 20)
            {
                await Task.Delay(50);
            }
        }
        // R7: la casella sta in «Lettura e consegna», nello spazio AIP espanso.
        await pagina.InvokeAsync(() => pagina.Find("[data-pagina=aip]").Click());
        // Il Confronti ha già caricato lo strato: si aspetta che il gestore della casella abbia finito di ricaricarlo.
        await pagina.Find("[data-casella=aip-sulla-mappa]").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true });
        pagina.WaitForAssertion(() => Assert.Contains("2 forme", pagina.Find("[data-aip-strato]").TextContent, StringComparison.Ordinal), TimeSpan.FromSeconds(10));

        // Il punto è la seconda voce dello strato, dopo l'area.
        await pagina.InvokeAsync(() => _lab.Scegli("aip:2611", 1));
        pagina.WaitForAssertion(() => Assert.Equal("ABRUS", pagina.Find("[data-scheda-aip] .lab-etichetta").TextContent), TimeSpan.FromSeconds(10));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=correggi-area]").Click());
        Assert.Equal("413000N 0121500E", pagina.Find("[data-modulo-voce=correggi] [data-campo=voce-position]").GetAttribute("value"));
        await pagina.InvokeAsync(() => pagina.Find("[data-campo=voce-position]").Change("413100N 0121500E"));
        await pagina.InvokeAsync(() => pagina.Find("[data-campo=voce-perche]").Change("coordinate sbagliate"));
        await pagina.InvokeAsync(() => pagina.Find("[data-campo=voce-fonte]").Change("errore AIP, segnalato a ENAV"));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=salva-voce]").Click());

        pagina.WaitForAssertion(() =>
        {
            Assert.Contains("Corretta (", pagina.Find("[data-correzione-aip]").TextContent, StringComparison.Ordinal);
            Assert.Empty(pagina.FindAll("[data-modulo-voce]"));
        }, TimeSpan.FromSeconds(10));
        var punto = Assert.Single(Vipi.SectorLab.Core.PdfAip.AipEspanso.Modello(espanso, "2611").Points!);
        Assert.Equal(41.5 + 1 / 60.0, punto.Position.Lat, 6);
        Assert.Equal(CorrectionKind.Point, Assert.Single(AipCorrections.Deserialize(File.ReadAllText(Path.Combine(espanso, "correzioni", "li.json"))).Corrections).Kind);

        // «Aggiungi» chiede il tipo: per un punto il modulo generale.
        await pagina.InvokeAsync(() => pagina.Find("[data-campo=aggiungi-tipo]").Change("Point"));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=aggiungi-area]").Click());
        pagina.WaitForAssertion(() => Assert.Equal("Point", pagina.Find("[data-modulo-voce=aggiungi]").GetAttribute("data-tipo-voce")), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void LeCartelleSiRicordano()
    {
        Assert.True(_lab.CambiaLaCartellaDeiPdf(Path.Combine(_fuori, "AIP")));
        Assert.True(_lab.CambiaLAipEspanso(Path.Combine(_fuori, "espanso")));

        var riaperto = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));

        Assert.Equal(Path.Combine(_fuori, "AIP"), riaperto.CartellaDeiPdf);
        Assert.Equal(Path.Combine(_fuori, "espanso"), riaperto.CartellaDellAipEspanso);
        Assert.True(riaperto.CambiaLaCartellaDeiPdf(""));
        Assert.Null(new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab")).CartellaDeiPdf);
    }
}
