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
    private readonly List<Uri> _pdfAperti = [];

    public AipNellaBarraTests()
    {
        Directory.CreateDirectory(Path.Combine(_fuori, "Downloads"));
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab")) { Download = Path.Combine(_fuori, "Downloads"), ApriIlPdfAllaPagina = _pdfAperti.Add };
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
        // Senza il guscio dell'app (qui, sul banco) la finestra per scegliere un file non c'è, e il tasto nemmeno.
        pagina.WaitForAssertion(() => Assert.NotNull(pagina.Find("[data-emendamento]")), TimeSpan.FromSeconds(10));
        Assert.Empty(pagina.FindAll("[data-tasto=sfoglia-amdt]"));

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

        // 22 ottobre: niente campo «Ciclo». Il PDF preso da Download sta fuori dalle cartelle dei cicli: il Lab lo legge, il
        // ciclo lo dicono le date delle pagine (06/08/2026 → 2608), e lo mette nella sua cartella.
        Assert.Empty(pagina.FindAll("[data-campo=ciclo]"));
        pagina.WaitForAssertion(() => Assert.Contains("1 PDF fuori dalle cartelle", pagina.Find("[data-sciolti]").TextContent, StringComparison.Ordinal), TimeSpan.FromSeconds(10));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=leggi-sciolti]").Click());

        pagina.WaitForAssertion(() =>
        {
            var esito = pagina.Find("[data-esito-aip]").TextContent;
            Assert.Contains("ciclo 2608", esito, StringComparison.Ordinal);
            Assert.Equal("1", pagina.Find("[data-da-dove=aree] strong").TextContent);
            Assert.Contains("06/08/2026 ×1", pagina.Find("[data-date-aip]").TextContent, StringComparison.Ordinal);
            Assert.Empty(pagina.FindAll("[data-sciolti]"));
            Assert.NotNull(pagina.Find("[data-ciclo='2608'] [data-tasto=leggi-aip]"));
        }, TimeSpan.FromSeconds(20));
        Assert.True(File.Exists(Path.Combine(pdf, "AIP 2608", "LI-ENR 5.1.2.pdf")));
        Assert.False(File.Exists(Path.Combine(pdf, "LI-ENR 5.1.2.pdf")));

        var doc = AipJson.Deserialize(await File.ReadAllTextAsync(Path.Combine(_fuori, "espanso", "2608", "li.json")));
        Assert.Equal("LI R901", Assert.Single(doc.Airspaces).Designator);

        // Da dove vengono le aree: il PDF, che si apre a pagina 1.
        await pagina.InvokeAsync(() => pagina.Find("[data-da-dove=aree]").Click());
        await pagina.InvokeAsync(() => pagina.Find("[data-da-dove-pdf='LI-ENR 5.1.2.pdf'] button").Click());
        Assert.EndsWith("/AIP%202608/LI-ENR%205.1.2.pdf#page=1", Assert.Single(_pdfAperti).AbsoluteUri, StringComparison.Ordinal);

        // Un ciclo scritto male non si legge.
        Assert.False(await _lab.LeggiLAipAsync("26"));
        Assert.Contains("non è un ciclo AIRAC", _lab.Rifiuto, StringComparison.Ordinal);
    }

    // Prova A45 (committente: «selezionare il file sfogliando con il file explorer»): «Sfoglia…» apre la finestra per un
    // PDF, da Download col campo vuoto e dalla cartella del file col campo pieno, e il file scelto va nel campo.
    [Fact]
    public async Task IlPdfDellEmendamento_SiSceglieConSfoglia()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.True(_lab.CambiaLaCartellaDeiPdf(Path.Combine(_fuori, "AIP")), _lab.Rifiuto);
        var scelto = Path.Combine(_fuori, "Downloads", "AIP 2611", "AMDT.pdf");
        var chiesti = new List<(string Titolo, string? Da, string Filtro)>();
        _lab.ScegliFile = (titolo, da, filtro) =>
        {
            chiesti.Add((titolo, da, filtro));
            return Task.FromResult<string?>(scelto);
        };
        var pagina = _contesto.RenderComponent<Home>();
        pagina.WaitForAssertion(() => Assert.NotNull(pagina.Find("[data-tasto=sfoglia-amdt]")), TimeSpan.FromSeconds(10));
        Assert.True(string.IsNullOrEmpty(pagina.Find("[data-campo=amdt]").GetAttribute("value")));

        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=sfoglia-amdt]").Click());
        Assert.Equal(scelto, pagina.Find("[data-campo=amdt]").GetAttribute("value"));
        Assert.False(pagina.Find("[data-tasto=applica-emendamento]").HasAttribute("disabled"));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=sfoglia-amdt]").Click());

        Assert.Equal([_lab.Download, scelto], chiesti.Select(c => c.Da));
        Assert.Equal("PDF (*.pdf)|*.pdf", chiesti[0].Filtro);
        Assert.Contains("emendamento", chiesti[0].Titolo, StringComparison.Ordinal);
    }

    // Preparando le prove a mano (22 ottobre): delle 542 segnalazioni del 2611 se ne vedevano le prime 200, e «1 non
    // tornano». Ora un tipo scelto le mostra tutte, e il conto è al singolare.
    [Fact]
    public async Task LeSegnalazioniSiScelgonoPerTipo_EUnContoCheNonTornaSiDiceAlSingolare()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.True(_lab.CambiaLaCartellaDeiPdf(Path.Combine(_fuori, "AIP")), _lab.Rifiuto);
        Assert.True(_lab.CambiaLAipEspanso(Path.Combine(_fuori, "espanso")), _lab.Rifiuto);
        var problemi = Enumerable.Range(1, 250).Select(i => new Core.PdfAip.ProblemaDellImport("LI-AD 2 LIRN 6.pdf",
                new Aip.Pdf.Italia.ImportIssue(Aip.Pdf.Italia.ImportIssueKind.Procedure, $"SID {i}", 1, "prosa letta con un'approssimazione: prua 329° disegnata come rotta, senza vento")))
            .Append(new Core.PdfAip.ProblemaDellImport("LI-ENR 3.4.1.pdf", new Aip.Pdf.Italia.ImportIssue(Aip.Pdf.Italia.ImportIssueKind.Holding, "JESSY", 10)))
            .ToList();
        _lab.UltimoAipEspanso = new Core.PdfAip.EsitoDellAipEspanso("2611", "x", 2, ["a.pdf", "b.pdf"], [], 0, [], problemi, [])
        {
            Rapporto = [new("LI-ENR 5.6.1.8.pdf", 3, new Dictionary<string, int>(), 0, [new("coordinate", 96, 90)])],
        };
        var pagina = _contesto.RenderComponent<Home>();

        pagina.WaitForAssertion(() => Assert.Contains("1 controllati, 1 non torna", pagina.Find("[data-conti-aip] summary").TextContent, StringComparison.Ordinal), TimeSpan.FromSeconds(10));
        // 26 ottobre: si apre su «da sistemare», che qui è l'attesa sola; le procedure senza dettaglio sono note della lettura.
        Assert.Contains("JESSY", Assert.Single(pagina.FindAll("[data-problema-aip]")).TextContent, StringComparison.Ordinal);
        Assert.Equal("note della lettura 250", pagina.Find("[data-gruppo-problemi=Nota]").TextContent.Trim());
        await pagina.InvokeAsync(() => pagina.Find("[data-gruppo-problemi='']").Click());
        Assert.Equal(200, pagina.FindAll("[data-problema-aip]").Count);
        Assert.Contains("Le prime 200 di 251", pagina.Find("[data-problemi-troncati]").TextContent, StringComparison.Ordinal);
        // L'attesa, la 251ª, non si vede fra le prime 200: col suo tipo sì.
        Assert.Empty(pagina.FindAll("[data-problema-aip=Holding]"));

        await pagina.InvokeAsync(() => pagina.Find("[data-tipo-problemi=Holding]").Click());
        Assert.Contains("JESSY", Assert.Single(pagina.FindAll("[data-problema-aip]")).TextContent, StringComparison.Ordinal);
        await pagina.InvokeAsync(() => pagina.Find("[data-tipo-problemi=Procedure]").Click());
        Assert.Equal(250, pagina.FindAll("[data-problema-aip=Procedure]").Count);
        Assert.Empty(pagina.FindAll("[data-problemi-troncati]"));
        await pagina.InvokeAsync(() => pagina.Find("[data-tipo-problemi='']").Click());
        Assert.Equal(200, pagina.FindAll("[data-problema-aip]").Count);
    }

    // 26 ottobre (committente: «le segnalazioni come si correggono?», «dove inserisco le coordinate di AJO?»): una segnalazione
    // da sistemare porta alla sua voce dell'Indice, o apre «Aggiungi» con la sigla e il perché già scritti.
    [Fact]
    public async Task LeSegnalazioniDaSistemare_PortanoAllaVoce_EAdAggiungereIlDatoCheManca()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pdf = Path.Combine(_fuori, "AIP");
        Assert.True(_lab.CambiaLaCartellaDeiPdf(pdf), _lab.Rifiuto);
        Assert.True(_lab.CambiaLAipEspanso(Path.Combine(_fuori, "espanso")), _lab.Rifiuto);
        var cartella = Directory.CreateDirectory(Core.PdfAip.EmendamentoDellAip.CartellaDelCiclo(pdf, "2611")).FullName;
        await File.WriteAllBytesAsync(Path.Combine(cartella, "LI-ENR 5.1.2.pdf"), PdfAipFinto.Enr51("06 AUG 2026", "LI R901 - Prova Uno"));
        Assert.True(await _lab.LeggiLAipAsync("2611"), _lab.Rifiuto);
        Assert.Equal("2611", _lab.CicloDiLavoro);
        Assert.True(await _lab.MostraLAipAsync(true), _lab.Rifiuto);
        var area = _lab.StratoDellAip!.Voci.ToList().FindIndex(v => v.Tipo == global::Aip.CorrectionKind.Airspace);
        var chiave = _lab.StratoDellAip.Voci[area].Chiave!;
        Core.PdfAip.ProblemaDellImport Problema(Aip.Pdf.Italia.ImportIssueKind tipo, string chiaveDelProblema, string dettaglio) =>
            new("LI-AD 2 LIEA 5.pdf", new Aip.Pdf.Italia.ImportIssue(tipo, chiaveDelProblema, 1, dettaglio));
        _lab.UltimoAipEspanso = _lab.UltimoAipEspanso! with
        {
            Problemi =
            [
                Problema(Aip.Pdf.Italia.ImportIssueKind.Procedure, "LIEA Star CORSI 3F RWY 02/20", "prosa non letta: AJO: non è una radioassistenza dell'AIP"),
                Problema(Aip.Pdf.Italia.ImportIssueKind.Procedure, "LIEO Star TEREZ 1A", "prosa non letta: AJO: non è una radioassistenza dell'AIP"),
                Problema(Aip.Pdf.Italia.ImportIssueKind.Geometry, chiave, "NotClosed"),
                Problema(Aip.Pdf.Italia.ImportIssueKind.Procedure, "LIRN Sid X 1A", "prosa letta con un'approssimazione: prua 329° disegnata come rotta, senza vento"),
            ],
        };
        _lab.ScegliLaPagina(PaginaDelLab.Aip);
        var pagina = _contesto.RenderComponent<Home>();

        pagina.WaitForAssertion(() => Assert.Equal(3, pagina.FindAll("[data-problema-aip]").Count), TimeSpan.FromSeconds(10));
        Assert.Equal("note della lettura 1", pagina.Find("[data-gruppo-problemi=Nota]").TextContent.Trim());
        Assert.Equal(2, pagina.FindAll("[data-tasto=aggiungi-mancante][data-manca=AJO]").Count);

        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=vai-alla-voce]").Click());
        Assert.Equal(area, _lab.SceltaDellAip);
        Assert.Equal(PaginaDelLab.AipIndice, _lab.Pagina);

        _lab.ScegliLaPagina(PaginaDelLab.Aip);
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-tasto=aggiungi-mancante]")), TimeSpan.FromSeconds(10));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=aggiungi-mancante]").Click());
        pagina.WaitForAssertion(() =>
        {
            Assert.Equal("Navaid", pagina.Find("[data-modulo-voce=aggiungi]").GetAttribute("data-tipo-voce"));
            Assert.Equal("AJO", pagina.Find("[data-campo=voce-designator]").GetAttribute("value"));
            Assert.Equal("stazione nominata dalle procedure di LIEA, LIEO, che l'AIP italiano non ha", pagina.Find("[data-campo=voce-perche]").GetAttribute("value"));
        }, TimeSpan.FromSeconds(10));
        Assert.Null(_lab.AggiuntaRichiesta);
    }

    // 26 ottobre (committente): «Vai alla voce» lascia accesa sulla mappa solo quella voce; una che l'Indice non ha (LIBD
    // FL100: Not closed) o che non si disegna (un'attesa senza il punto nel sector) apre il PDF alla pagina della
    // segnalazione. Poi le voci si riaccendono una per una, o un gruppo dell'Indice tutto insieme.
    [Fact]
    public async Task VaiAllaVoce_LasciaAccesaSoloLei_ESeNonSiVedeApreIlPdf()
    {
        _albero.Scrivi("SectorFiles/Include/IT/lirf.str", "//MENU MAPS\r\n");
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pdf = Path.Combine(_fuori, "AIP");
        var espanso = Path.Combine(_fuori, "espanso");
        Assert.True(_lab.CambiaLaCartellaDeiPdf(pdf), _lab.Rifiuto);
        Assert.True(_lab.CambiaLAipEspanso(espanso), _lab.Rifiuto);
        var cartella = Directory.CreateDirectory(Core.PdfAip.EmendamentoDellAip.CartellaDelCiclo(pdf, "2611")).FullName;
        await File.WriteAllTextAsync(Path.Combine(cartella, "LI-ENR 3.4.1.pdf"), "finto");
        await File.WriteAllTextAsync(Path.Combine(cartella, "LI-AD 2 LIBD 1.pdf"), "finto");
        var roma = CtrDiProva("Roma CTR", new CircleGeometry(new Position(41.9, 12.5), new Distance(5, DistanceUnit.Nm)));
        var attesa = new Holding { Section = "ENR 3.4.1", Fix = "NESSUNO", Minimum = new VerticalLimit { Text = "FL100" }, Turn = TurnDirection.Right, InboundTrack = 90 };
        var abrus = new SignificantPoint { Section = "ENR 4.4", Designator = "ABRUS", Position = new Position(41.5, 12.25) };
        Directory.CreateDirectory(Path.Combine(espanso, "2611"));
        await File.WriteAllTextAsync(Path.Combine(espanso, "2611", "li.json"), AipJson.Serialize(new AipDocument
        {
            State = "LI", Airac = "2611", Airspaces = [roma], Holdings = [attesa], Points = [abrus],
        }));
        Assert.True(await _lab.MostraLAipAsync(true), _lab.Rifiuto);
        Assert.Equal(["CTR", "Attese", "Punti"], _lab.StratoDellAip!.Voci.Select(v => v.Genere));
        Core.PdfAip.ProblemaDellImport Problema(string file, Aip.Pdf.Italia.ImportIssueKind tipo, string chiave, int pagina) =>
            new(file, new Aip.Pdf.Italia.ImportIssue(tipo, chiave, pagina, "NotClosed"));
        var ctr = Problema("LI-ENR 2.1.pdf", Aip.Pdf.Italia.ImportIssueKind.Geometry, _lab.StratoDellAip.Voci[0].Chiave!, 1);
        var nessuno = Problema("LI-ENR 3.4.1.pdf", Aip.Pdf.Italia.ImportIssueKind.Holding, "NESSUNO", 4);
        var libd = Problema("LI-AD 2 LIBD 1.pdf", Aip.Pdf.Italia.ImportIssueKind.Geometry, "LIBD FL100", 7);
        var mappa = _lab.Mappa(SpazioDiLavoro.Aip);
        mappa.Accesi.Add("fix");

        // La voce che si disegna: sola sulla mappa, le coste sì, il resto spento (gli strati del sector e i generi dell'AIP).
        Assert.True(await _lab.VaiAllaSegnalazioneAsync("2611", ctr), _lab.Rifiuto);
        Assert.Equal((PaginaDelLab.AipIndice, 0), (_lab.Pagina, _lab.SceltaDellAip));
        Assert.Equal(["aip", "sfondo"], mappa.Accesi.Order());
        Assert.Equal([true, false, false], Enumerable.Range(0, 3).Select(_lab.VoceDellAipAccesa));
        Assert.Contains("+aip:2611#0", _lab.SpentiDi(SpazioDiLavoro.Aip));
        Assert.Null(_lab.SegnalazioneNelPdf);
        Assert.Empty(_pdfAperti);

        // Un'attesa senza il punto nel sector non si disegna: la voce si apre, e il PDF alla pagina della segnalazione.
        Assert.True(await _lab.VaiAllaSegnalazioneAsync("2611", nessuno), _lab.Rifiuto);
        Assert.Equal(1, _lab.SceltaDellAip);
        Assert.Equal([false, true, false], Enumerable.Range(0, 3).Select(_lab.VoceDellAipAccesa));
        Assert.EndsWith("/AIP%202611/LI-ENR%203.4.1.pdf#page=4", Assert.Single(_pdfAperti).AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains("non si disegna", _lab.SegnalazioneNelPdf, StringComparison.Ordinal);

        // Una voce che l'Indice non ha: il PDF, senza rifiuto.
        Assert.True(await _lab.VaiAllaSegnalazioneAsync("2611", libd), _lab.Rifiuto);
        Assert.EndsWith("/AIP%202611/LI-AD%202%20LIBD%201.pdf#page=7", _pdfAperti[^1].AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains("«LIBD FL100» non è nell'Indice del 2611", _lab.SegnalazioneNelPdf, StringComparison.Ordinal);

        // Scelta nell'Indice, una voce spenta si accende da sola; poi si spegne e si riaccende dalla sua scheda.
        _lab.Scegli("aip:2611", 2);
        Assert.Equal([false, true, true], Enumerable.Range(0, 3).Select(_lab.VoceDellAipAccesa));
        _lab.AccendiLeVociDellAip([2], accese: false);
        Assert.False(_lab.VoceDellAipAccesa(2));

        // Il gruppo dell'Indice: tutto insieme. Prima ENR 2 ha la sua CTR spenta; la casella la accende.
        var pagina = _contesto.RenderComponent<Home>();
        pagina.WaitForAssertion(() => Assert.NotEmpty(pagina.FindAll("[data-accendi-gruppo]")), TimeSpan.FromSeconds(10));
        var gruppoDellaCtr = pagina.Find("[data-parte='ENR 2'] [data-accendi-gruppo]");
        Assert.False(gruppoDellaCtr.HasAttribute("checked"));
        Assert.Equal("Spente sulla mappa: accendile", gruppoDellaCtr.GetAttribute("title"));
        await pagina.InvokeAsync(() => pagina.Find("[data-parte='ENR 2'] [data-accendi-gruppo]").Change(true));
        Assert.Equal([true, true, false], Enumerable.Range(0, 3).Select(_lab.VoceDellAipAccesa));
        pagina.WaitForAssertion(() => Assert.True(pagina.Find("[data-parte='ENR 2'] [data-accendi-gruppo]").HasAttribute("checked")));
        Assert.False(pagina.Find("[data-accendi-voce] input").HasAttribute("checked"));
        await pagina.InvokeAsync(() => pagina.Find("[data-accendi-voce] input").Change(true));
        Assert.True(_lab.VoceDellAipAccesa(2));
        _lab.AccendiLeVociDellAip([2], accese: false);

        // Una correzione rifà lo strato e sposta gli indici (un'area in più prima dell'attesa): le voci restano quelle.
        var milano = CtrDiProva("Milano CTR", new CircleGeometry(new Position(45.5, 9.2), new Distance(5, DistanceUnit.Nm)));
        await File.WriteAllTextAsync(Path.Combine(espanso, "2611", "li.json"), AipJson.Serialize(new AipDocument
        {
            State = "LI", Airac = "2611", Airspaces = [roma, milano], Holdings = [attesa], Points = [abrus],
        }));
        Assert.True(await _lab.MostraLAipAsync(true), _lab.Rifiuto);
        Assert.Equal([true, false, true, false], Enumerable.Range(0, 4).Select(_lab.VoceDellAipAccesa));
        Assert.Contains("aip:2611#3", _lab.SpentiDi(SpazioDiLavoro.Aip));
    }

    // 26 ottobre (committente: «quando chiudo e poi riapro, la lista delle cose da sistemare senza rileggere l'AIP»): la
    // lettura si ricorda nei dati del Lab; riaperto, il Lab mostra quella del ciclo di lavoro, e dice che è ricordata.
    [Fact]
    public async Task LaLetturaDelCicloDiLavoro_SiRicordaDopoUnRiavvio()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pdf = Path.Combine(_fuori, "AIP");
        Assert.True(_lab.CambiaLaCartellaDeiPdf(pdf), _lab.Rifiuto);
        Assert.True(_lab.CambiaLAipEspanso(Path.Combine(_fuori, "espanso")), _lab.Rifiuto);
        var cartella = Directory.CreateDirectory(Core.PdfAip.EmendamentoDellAip.CartellaDelCiclo(pdf, "2611")).FullName;
        await File.WriteAllBytesAsync(Path.Combine(cartella, "LI-ENR 5.1.2.pdf"), PdfAipFinto.Enr51("06 AUG 2026", "LI R901 - Prova Uno"));
        Assert.True(await _lab.LeggiLAipAsync("2611"), _lab.Rifiuto);
        var letto = _lab.UltimoAipEspanso!;
        Assert.False(_lab.LetturaRicordata);

        var dati = Path.Combine(_albero.Radice, "dati-del-lab");
        var riaperto = new SessioneDelLab(dati) { ApriIlPdfAllaPagina = _pdfAperti.Add };
        Assert.True(await riaperto.ApriEValidaAsync(_albero.Radice));
        var ricordato = riaperto.UltimoAipEspanso;

        Assert.NotNull(ricordato);
        Assert.True(riaperto.LetturaRicordata);
        Assert.Equal((letto.Ciclo, letto.Aree), (ricordato.Ciclo, ricordato.Aree));
        Assert.Equal(letto.Letti, ricordato.Letti);
        Assert.Equal(letto.Problemi, ricordato.Problemi);
        Assert.Equal(_lab.LettaIl!.Value, riaperto.LettaIl!.Value, TimeSpan.FromSeconds(1));
        using (var contesto = new TestContext())
        {
            contesto.Services.AddSingleton(riaperto);
            contesto.JSInterop.Mode = JSRuntimeMode.Loose;
            riaperto.ScegliLaPagina(PaginaDelLab.Aip);
            var pagina = contesto.RenderComponent<Home>();
            pagina.WaitForAssertion(() =>
            {
                Assert.Contains("ciclo 2611", pagina.Find("[data-esito-aip]").TextContent, StringComparison.Ordinal);
                Assert.NotNull(pagina.Find("[data-lettura-ricordata]"));
                Assert.Equal("1", pagina.Find("[data-da-dove=aree] strong").TextContent);
            }, TimeSpan.FromSeconds(10));
        }
    }

    // Slice 1b: «Confronta i CTR», dall'AIP espanso al sector aperto; un clic sulla riga apre la voce del sector.
    // Carta «L'AIP intero», passo 8: il 2611 fino al 25 novembre, il 2612 dal 26; il campo, l'indice e i confronti aprono
    // quello in vigore, l'altro si guarda; al cambio il 2611 si toglie, col gesto confermato.
    [Fact]
    public async Task IDueCicli_IlCampo_LIndice_ETogliIlSuperato()
    {
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        var pdf = Path.Combine(_fuori, "AIP");
        Assert.True(_lab.CambiaLaCartellaDeiPdf(pdf), _lab.Rifiuto);
        Assert.True(_lab.CambiaLAipEspanso(Path.Combine(_fuori, "espanso")), _lab.Rifiuto);
        foreach (var (ciclo, nome) in new[] { ("2611", "LI R901 - Prova Uno"), ("2612", "LI R902 - Prova Due") })
        {
            var cartella = Directory.CreateDirectory(Core.PdfAip.EmendamentoDellAip.CartellaDelCiclo(pdf, ciclo)).FullName;
            await File.WriteAllBytesAsync(Path.Combine(cartella, "LI-ENR 5.1.2.pdf"), PdfAipFinto.Enr51("06 AUG 2026", nome));
            Assert.True(await _lab.LeggiLAipAsync(ciclo), _lab.Rifiuto);
        }

        // 26 ottobre (committente): il ciclo di lavoro lo sceglie lui. Due cicli e nessuna scelta: il Lab chiede, non sceglie.
        _lab.GiornoDeiCicli = () => new DateOnly(2026, 11, 25);
        Assert.Null(_lab.CicloDiLavoro);
        Assert.False(await _lab.MostraLAipAsync(true));
        Assert.Contains("Lavora su questo", _lab.Rifiuto, StringComparison.Ordinal);
        Assert.Equal("2611", _lab.CicloDaLeggere);
        Assert.True(await _lab.LavoraSulCicloAsync("2612"), _lab.Rifiuto);
        Assert.True(await _lab.MostraLAipAsync(true), _lab.Rifiuto);
        Assert.Equal(("2612", "2612"), (_lab.StratoDellAip!.Ciclo, _lab.CicloDaLeggere));
        // Cambiando ciclo, la mappa e l'Indice lo seguono.
        Assert.True(await _lab.LavoraSulCicloAsync("2611"), _lab.Rifiuto);
        Assert.Equal("2611", _lab.StratoDellAip!.Ciclo);
        Assert.Empty(_lab.StatoDeiCicliDellAip.DaTogliere);
        Assert.False(await _lab.LavoraSulCicloAsync("2613"));

        // Il 26 entra il 2612, ma si lavora sul 2611 finché il committente non cambia; la scelta resta dopo un riavvio.
        _lab.GiornoDeiCicli = () => new DateOnly(2026, 11, 26);
        Assert.Equal("2611", _lab.CicloDiLavoro);
        Assert.Equal("2611", new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab")).CicloDiLavoro);
        Assert.True(await _lab.LavoraSulCicloAsync("2612"), _lab.Rifiuto);
        Assert.Equal("2612", _lab.StratoDellAip!.Ciclo);
        var pagina = _contesto.RenderComponent<Home>();
        // 22 ottobre: una riga per ciclo al posto del campo; quella su cui si lavora è in evidenza.
        pagina.WaitForAssertion(() =>
        {
            Assert.Equal("non più in vigore dal 26/11/2026", pagina.Find("[data-ciclo='2611'] [data-ciclo-dice]").TextContent);
            Assert.Equal("in vigore dal 26/11/2026", pagina.Find("[data-ciclo='2612'] [data-ciclo-dice]").TextContent);
            Assert.Contains("lab-ciclo-aperto", pagina.Find("li[data-ciclo='2612']").ClassList);
            Assert.Single(pagina.FindAll("[data-ciclo-di-lavoro]"));
            Assert.Equal("Lavora su questo", pagina.Find("[data-ciclo='2611'] [data-tasto=lavora-sul-ciclo]").TextContent.Trim());
            Assert.Empty(pagina.FindAll("[data-ciclo='2612'] [data-tasto=lavora-sul-ciclo]"));
            Assert.Contains("pagine fino al 06/08/2026", pagina.Find("li[data-ciclo='2612']").TextContent, StringComparison.Ordinal);
            Assert.Equal("Rileggi", pagina.Find("[data-ciclo='2612'] [data-tasto=leggi-aip]").TextContent.Trim());
        }, TimeSpan.FromSeconds(10));
        Assert.Empty(pagina.FindAll("[data-ciclo='2612'] [data-tasto=togli-ciclo]"));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=togli-ciclo]").Click());
        Assert.True(Directory.Exists(Core.PdfAip.EmendamentoDellAip.CartellaDelCiclo(pdf, "2611")));
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=togli-ciclo-sicuro]").Click());

        pagina.WaitForAssertion(() => Assert.Empty(pagina.FindAll("[data-tasto=togli-ciclo]")), TimeSpan.FromSeconds(10));
        Assert.False(Directory.Exists(Core.PdfAip.EmendamentoDellAip.CartellaDelCiclo(pdf, "2611")));
        Assert.Equal(["2612"], _lab.CicliConIlModello);
    }

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

    // Carta «Gli aeroporti nel Lab», S1 (27 ottobre): in AD 2 uno scalo è una riga con la copertura delle IAP; il clic apre
    // la pagina dello scalo al posto della scheda; una sua procedura ha sopra la scheda «‹ LIXX» per tornare.
    [Fact]
    public async Task LaPaginaDelloScalo_DallIndice()
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
            Airspaces = [],
            Points = [new SignificantPoint { Section = "AD 2 LIXX", Designator = "UNO", Position = new Position(41.85, 12.26) }],
            Aerodromes =
            [
                new Aerodrome
                {
                    Icao = "LIXX", Name = "PROVA Aeroporto", Arp = new Position(41.8, 12.25), Elevation = "14 FT",
                    Runways = [new Runway { Designator = "16L", Dimensions = "3902 x 60" }, new Runway { Designator = "34R" }],
                    Communications = [new CommunicationFacility("TWR", "Prova Torre", "118.700")],
                    Charts = [new AerodromeChart("ICAO INSTRUMENT APPROACH CHART ILS Z RWY 16L", "AD 2 LIXX 5-3"), new AerodromeChart("ICAO INSTRUMENT APPROACH CHART RNP RWY 16L", "AD 2 LIXX 5-5")],
                },
            ],
            Procedures =
            [
                new Procedure
                {
                    Aerodrome = "LIXX", Kind = ProcedureKind.Approach, Designator = "RNP RWY16L", Runways = ["16L"], Page = new AipPage("AD 2 LIXX 5-5"),
                    Legs = [new ProcedureLeg { PathTerminator = "IF", Fix = "UNO" }],
                },
            ],
        }));
        _lab.ScegliLaPagina(PaginaDelLab.AipIndice);
        var pagina = _contesto.RenderComponent<Home>();

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal("IAP 1/2", pagina.Find("[data-scalo=LIXX] [data-copertura-iap]").TextContent.Trim());
            Assert.Contains("1 scali", pagina.Find("[data-parte='AD 2'] summary").TextContent, StringComparison.Ordinal);
        }, TimeSpan.FromSeconds(10));
        await pagina.InvokeAsync(() => pagina.Find("[data-scalo=LIXX] button").Click());

        pagina.WaitForAssertion(() =>
        {
            Assert.Equal("LIXX", pagina.Find("[data-pagina-scalo]").GetAttribute("data-pagina-scalo"));
            Assert.Contains("14 FT", pagina.Find("[data-testata-scalo]").TextContent, StringComparison.Ordinal);
            Assert.Contains("lab-scelto", pagina.Find("[data-scalo=LIXX]").ClassList);
        }, TimeSpan.FromSeconds(10));
        Assert.Equal(["Piste 1", "Procedure 1", "Carte · IAP 1/2", "Frequenze", "Ground", "Testo AD 2"], pagina.FindAll("[data-scheda-scalo]").Select(b => b.TextContent.Trim()));
        Assert.Equal("16L/Avvicinamenti", pagina.Find("[data-gruppo-scalo]").GetAttribute("data-gruppo-scalo"));
        await pagina.InvokeAsync(() => pagina.Find("[data-scheda-scalo=Piste]").Click());
        Assert.Equal("16L/34R", pagina.Find("[data-pista]").GetAttribute("data-pista"));
        // Le procedure del lato contano anche le carte di avvicinamento (S2: non stanno più nei gruppi).
        Assert.Contains("IAP 1/2", pagina.Find("[data-lato='16L']").TextContent, StringComparison.Ordinal);
        await pagina.InvokeAsync(() => pagina.Find("[data-scheda-scalo=Carte]").Click());
        Assert.Equal(["solo immagine", "in parte · 1 parte, 1 tratto · 1 parte con un punto solo"], pagina.FindAll("[data-carta] td:nth-child(4)").Select(td => td.TextContent.Trim()));
        await pagina.InvokeAsync(() => pagina.Find("[data-scheda-scalo=Frequenze]").Click());
        Assert.Contains("118.700 · Prova Torre", pagina.Find("[data-frequenze-scalo]").TextContent, StringComparison.Ordinal);
        // §2 D: nessun .rw ha LIXX, e lo scalo non ha AD 2.8.
        await pagina.InvokeAsync(() => pagina.Find("[data-scheda-scalo=Ground]").Click());
        Assert.Contains("Nessun .rw ha 16L, 34R", pagina.Find("[data-ground-senza-rw]").TextContent, StringComparison.Ordinal);
        Assert.Contains("AD 2.8 non dice i nomi", pagina.Find("[data-ground-taxiway]").TextContent, StringComparison.Ordinal);
        Assert.Contains("AD 2.24 non ha carte dei parcheggi per LIXX", pagina.Find("[data-ground-note]").TextContent, StringComparison.Ordinal);
        await pagina.InvokeAsync(() => pagina.Find("[data-scheda-scalo=Testo]").Click());
        Assert.NotNull(pagina.Find("[data-testo-scalo] [data-scheda-aip]"));

        // S2: gli avvicinamenti per carta; la carta si apre al posto delle schede, con le parti; una carta solo immagine lo dice.
        await pagina.InvokeAsync(() => pagina.Find("[data-scheda-scalo=Procedure]").Click());
        Assert.Equal(["ILS Z RWY 16L", "RNP RWY 16L"], pagina.FindAll("[data-carta-iap-riga] [data-apri-carta]").Select(b => b.TextContent.Trim()));
        await pagina.InvokeAsync(() => pagina.Find("[data-apri-carta='AD 2 LIXX 5-3']").Click());
        Assert.NotNull(pagina.Find("[data-carta-solo-immagine]"));
        await pagina.InvokeAsync(() => pagina.Find("[data-chiudi-carta]").Click());
        await pagina.InvokeAsync(() => pagina.Find("[data-apri-carta='AD 2 LIXX 5-5']").Click());
        Assert.Equal("InParte", pagina.Find("[data-stato-carta]").GetAttribute("data-stato-carta"));
        Assert.Equal("RNP RWY16L", pagina.Find("[data-parte-carta] [data-voce-aip]").TextContent.Trim());
        // Lo strato è acceso tutto: il tasto spegne le parti, e le riaccende.
        Assert.Equal("Spegni le parti sulla mappa", pagina.Find("[data-tasto=accendi-carta]").TextContent.Trim());
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=accendi-carta]").Click());
        Assert.Equal("Accendi le parti sulla mappa", pagina.Find("[data-tasto=accendi-carta]").TextContent.Trim());
        Assert.Null(pagina.Find("[data-parte-carta] input").GetAttribute("checked"));

        // «Solo lo scalo sulla mappa»: lo scalo, il suo punto e la procedura, accesi da soli.
        await pagina.InvokeAsync(() => pagina.Find("[data-tasto=solo-lo-scalo]").Click());
        Assert.Equal(3, _lab.QuanteVociDellAipAccese(Enumerable.Range(0, _lab.StratoDellAip!.Voci.Count)));

        // Una parte della carta: la sua scheda, con «‹ LIXX» sopra; tornando, la carta è ancora aperta.
        await pagina.InvokeAsync(() => pagina.Find("[data-parte-carta] [data-voce-aip]").Click());
        pagina.WaitForAssertion(() => Assert.Equal("‹ LIXX PROVA Aeroporto", pagina.Find("[data-torna-allo-scalo]").TextContent.Trim()), TimeSpan.FromSeconds(10));
        Assert.Empty(pagina.FindAll("[data-pagina-scalo]"));
        await pagina.InvokeAsync(() => pagina.Find("[data-torna-allo-scalo]").Click());
        pagina.WaitForAssertion(() => Assert.Equal("AD 2 LIXX 5-5", pagina.Find("[data-carta-iap]").GetAttribute("data-carta-iap")), TimeSpan.FromSeconds(10));
        await pagina.InvokeAsync(() => pagina.Find("[data-chiudi-carta]").Click());
        Assert.Empty(pagina.FindAll("[data-carta-iap]"));
        Assert.Contains("lab-scelto", pagina.Find("[data-scheda-scalo=Procedure]").ClassList);
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

        // 22 ottobre: la casella sta nell'Indice, che ha la mappa («Lettura e consegna» è una pagina intera).
        await pagina.InvokeAsync(() => pagina.Find("[data-pagina=aipindice]").Click());
        // Il Confronti ha già caricato lo strato: si aspetta che il gestore della casella abbia finito di ricaricarlo.
        // Trovare e cambiare nello stesso giro del renderer: l'Indice, appena aperto, carica lo strato e si ridisegna
        // (CI 22-ott, UnknownEventHandlerIdException).
        await pagina.InvokeAsync(() => pagina.Find("[data-casella=aip-sulla-mappa]").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true }));

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
        // 22 ottobre: la casella sta nell'Indice, che ha la mappa («Lettura e consegna» è una pagina intera).
        await pagina.InvokeAsync(() => pagina.Find("[data-pagina=aipindice]").Click());
        // Il Confronti ha già caricato lo strato: si aspetta che il gestore della casella abbia finito di ricaricarlo.
        // Trovare e cambiare nello stesso giro del renderer: l'Indice, appena aperto, carica lo strato e si ridisegna
        // (CI 22-ott, UnknownEventHandlerIdException).
        await pagina.InvokeAsync(() => pagina.Find("[data-casella=aip-sulla-mappa]").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true }));
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
        // 22 ottobre: la casella sta nell'Indice, che ha la mappa («Lettura e consegna» è una pagina intera).
        await pagina.InvokeAsync(() => pagina.Find("[data-pagina=aipindice]").Click());
        // Il Confronti ha già caricato lo strato: si aspetta che il gestore della casella abbia finito di ricaricarlo.
        // Trovare e cambiare nello stesso giro del renderer: l'Indice, appena aperto, carica lo strato e si ridisegna
        // (CI 22-ott, UnknownEventHandlerIdException).
        await pagina.InvokeAsync(() => pagina.Find("[data-casella=aip-sulla-mappa]").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true }));
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
