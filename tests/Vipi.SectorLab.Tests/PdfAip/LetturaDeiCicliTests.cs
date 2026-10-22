using Vipi.SectorLab.Core.PdfAip;
using Vipi.SectorLab.Ui.Servizi;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// La pagina «Lettura e consegna» rifatta (22 ottobre): il ciclo lo dicono le date delle pagine, non il nome della cartella
/// né il giorno di oggi (il 2611 copiato fuori dalla sua cartella si era letto come 2610); i PDF sciolti vanno nella
/// cartella del loro ciclo; la lettura dice a che punto è; una voce apre il suo PDF alla sua pagina.
/// </summary>
public sealed class LetturaDeiCicliTests : IDisposable
{
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "lettura-cicli-" + Guid.NewGuid().ToString("N"));
    private readonly List<Uri> _aperti = [];

    public LetturaDeiCicliTests() => Directory.CreateDirectory(_radice);

    public void Dispose() => Directory.Delete(_radice, recursive: true);

    private string Pdf => Path.Combine(_radice, "pdf");

    private string Espanso => Path.Combine(_radice, "espanso");

    private SessioneDelLab Lab()
    {
        var lab = new SessioneDelLab(Path.Combine(_radice, "dati")) { ApriIlPdfAllaPagina = _aperti.Add };
        Directory.CreateDirectory(Pdf);
        Assert.True(lab.CambiaLaCartellaDeiPdf(Pdf), lab.Rifiuto);
        Assert.True(lab.CambiaLAipEspanso(Espanso), lab.Rifiuto);
        lab.GiornoDeiCicli = () => new DateOnly(2026, 10, 22);
        return lab;
    }

    private void Scrivi(string cartella, string data, string nome = "LI-ENR 5.1.2.pdf")
    {
        Directory.CreateDirectory(cartella);
        File.WriteAllBytes(Path.Combine(cartella, nome), PdfAipFinto.Enr51(data, "LI R901 - Prova Uno"));
    }

    [Fact]
    public void IlCicloDellePagine_ELaPaginaPiuRecente()
    {
        Scrivi(Path.Combine(_radice, "a"), "29 OCT 2026", "LI-ENR 5.1.1.pdf");
        Scrivi(Path.Combine(_radice, "a"), "09 JUL 2026", "LI-ENR 5.1.2.pdf");
        var avanzamenti = new List<AvanzamentoDellaLettura>();

        var lettura = AipEspanso.LeggiIPdf(PdfDellAip.Elenco(Path.Combine(_radice, "a")), new Riferisci(avanzamenti.Add));

        // Una pagina non cambiata tiene la sua data: conta la più recente.
        Assert.Equal(new DateOnly(2026, 10, 29), lettura.UltimaData);
        Assert.Equal("2611", lettura.CicloDellePagine);
        Assert.Equal([(1, 2, "LI-ENR 5.1.1.pdf"), (2, 2, "LI-ENR 5.1.2.pdf")], avanzamenti.Select(a => (a.Fatti, a.Tutti, a.File)));
    }

    [Fact]
    public async Task PagineDiUnCicloDopo_FermanoPrimaDiScrivere()
    {
        var lab = Lab();
        Scrivi(EmendamentoDellAip.CartellaDelCiclo(Pdf, "2611"), "26 NOV 2026");

        Assert.False(await lab.LeggiLAipAsync("2611"));

        Assert.Equal("Le pagine di «AIP 2611» arrivano al 26/11/2026, che è del ciclo 2612: la cartella va chiamata «AIP 2612». Non ho scritto niente.", lab.Rifiuto);
        Assert.False(File.Exists(AipEspanso.File(Espanso, "2611")));
        Assert.Null(lab.Avanzamento);
        Assert.False(lab.StaLeggendoLAip);
    }

    [Fact]
    public async Task NessunaPaginaDelCiclo_SiLeggeESiDice()
    {
        var lab = Lab();
        Scrivi(EmendamentoDellAip.CartellaDelCiclo(Pdf, "2611"), "09 JUL 2026");

        Assert.True(await lab.LeggiLAipAsync("2611"), lab.Rifiuto);

        Assert.True(File.Exists(AipEspanso.File(Espanso, "2611")));
        Assert.Equal("Nessuna pagina è del 2611: la più recente è del 09/07/2026 (2607). Va bene se in questo ciclo i PDF che hai non sono cambiati.", lab.AvvisoDellaLettura);
        var riga = Assert.Single(lab.RigheDeiCicli);
        Assert.Equal(("2611", 1, new DateOnly(2026, 7, 9), true), (riga.Ciclo, riga.Pdf, riga.UltimaData, riga.Scritto is not null));
        Assert.Equal("non ancora in vigore: dal 29/10/2026", riga.Dice);
    }

    [Fact]
    public async Task IPdfSciolti_VannoNellaCartellaDelLoroCiclo()
    {
        var lab = Lab();
        // Come il committente il 22 ottobre: le sottocartelle del 2611 copiate direttamente nella cartella dei PDF.
        Scrivi(Path.Combine(Pdf, "ENR"), "29 OCT 2026");
        File.WriteAllText(Path.Combine(Pdf, "ENR", "mio.txt"), "dell'AOD");
        Scrivi(Path.Combine(Pdf, "AD 2"), "01 OCT 2026", "LI-ENR 5.1.1.pdf");
        Assert.Equal(2, lab.PdfSciolti.Count);

        Assert.True(await lab.LeggiLAipAsync(null), lab.Rifiuto);

        Assert.Equal("2611", lab.UltimoAipEspanso!.Ciclo);
        Assert.Empty(lab.PdfSciolti);
        Assert.True(File.Exists(Path.Combine(Pdf, "AIP 2611", "ENR", "LI-ENR 5.1.2.pdf")));
        Assert.True(File.Exists(Path.Combine(Pdf, "AIP 2611", "AD 2", "LI-ENR 5.1.1.pdf")));
        // La cartella svuotata se ne va; quella con dentro un file dell'AOD resta.
        Assert.False(Directory.Exists(Path.Combine(Pdf, "AD 2")));
        Assert.True(File.Exists(Path.Combine(Pdf, "ENR", "mio.txt")));

        // Altri sciolti dello stesso ciclo: la cartella c'è già, non si sposta niente.
        Scrivi(Path.Combine(Pdf, "ENR"), "29 OCT 2026", "LI-ENR 5.1.3.pdf");
        Assert.False(await lab.LeggiIPdfScioltiAsync());
        Assert.Contains("c'è già la cartella «AIP 2611»", lab.Rifiuto, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(Pdf, "ENR", "LI-ENR 5.1.3.pdf")));
    }

    [Fact]
    public async Task LaPaginaDiUnaVoce_SiApreNelSuoPdf()
    {
        var lab = Lab();
        Scrivi(EmendamentoDellAip.CartellaDelCiclo(Pdf, "2611"), "29 OCT 2026");
        Assert.True(await lab.LeggiLAipAsync("2611"), lab.Rifiuto);
        var riferimento = IndiceDellePagine.Leggi(File.ReadAllText(IndiceDellePagine.File(Espanso, "2611"))).First(p => p.Reference is not null).Reference!;

        Assert.True(lab.ApriLaPaginaDellAip("2611", riferimento), lab.Rifiuto);
        Assert.False(lab.ApriLaPaginaDellAip("2611", "ENR 9.9-1"));
        Assert.False(lab.ApriIlPdf("2611", "LI-ENR 9.pdf"));

        Assert.EndsWith("/AIP%202611/LI-ENR%205.1.2.pdf#page=1", Assert.Single(_aperti).AbsoluteUri, StringComparison.Ordinal);
    }

    private sealed class Riferisci(Action<AvanzamentoDellaLettura> fai) : IProgress<AvanzamentoDellaLettura>
    {
        public void Report(AvanzamentoDellaLettura value) => fai(value);
    }
}
