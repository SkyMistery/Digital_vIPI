using Vipi.SectorLab.Core.PdfAip;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// I due cicli (carta «L'AIP intero e gli emendamenti», passo 8): quale è in vigore il giorno, quale viene dopo, quale è
/// superato e si toglie. Le date sono quelle vere: il 2610 dal 1° ottobre 2026, il 2611 dal 29 ottobre, il 2612 dal 26
/// novembre. Le cartelle nel temporaneo.
/// </summary>
public sealed class CicliDellAipTests : IDisposable
{
    private readonly string _radice = Directory.CreateTempSubdirectory("cicli-").FullName;

    public void Dispose() => Directory.Delete(_radice, recursive: true);

    private string Pdf => Path.Combine(_radice, "pdf");

    private string Espanso => Path.Combine(_radice, "aip-espanso");

    [Fact]
    public void LeDateDeiCicli()
    {
        Assert.Equal(new DateOnly(2026, 10, 29), AipEspanso.CicloInVigore("2611"));
        Assert.Equal(new DateOnly(2026, 11, 26), AipEspanso.CicloInVigore("2612"));
        Assert.Equal("2612", StatoDeiCicli.Dopo("2611"));
        Assert.Equal("2701", StatoDeiCicli.Dopo("2613"));
    }

    // Oggi: in vigore il 2610, che il Lab non ha; i PDF scaricati sono del 2611, e l'emendamento A12/26 ha fatto il 2612.
    [Fact]
    public void Il22Ottobre_SiApreIlSuccessivo()
    {
        var stato = CicliDellAip.Di(["2611", "2612"], new DateOnly(2026, 10, 22));

        Assert.Equal(("2610", (string?)null, "2611"), (stato.DiOggi, stato.InVigore, stato.Successivo));
        Assert.Equal(["2612"], stato.Prossimi);
        Assert.Empty(stato.Superati);
        Assert.Equal("2611", stato.DaAprire);
        Assert.Equal("non ancora in vigore: dal 29/10/2026", stato.Dice("2611"));
    }

    // «Finita quando» del passo 8: il 2611 fino al 25 novembre, il 2612 dal 26; il 2611 da togliere al cambio.
    [Fact]
    public void Il25Novembre_Il2611_Il26_Il2612()
    {
        var prima = CicliDellAip.Di(["2611", "2612"], new DateOnly(2026, 11, 25));
        var dopo = CicliDellAip.Di(["2611", "2612"], new DateOnly(2026, 11, 26));

        Assert.Equal(("2611", "2612"), (prima.DaAprire, prima.Successivo));
        Assert.Empty(prima.DaTogliere);
        Assert.Equal("in vigore dal 29/10/2026", prima.Dice("2611"));
        Assert.Equal(("2612", (string?)null), (dopo.DaAprire, dopo.Successivo));
        Assert.Equal(["2611"], dopo.DaTogliere);
        Assert.Equal("non più in vigore dal 26/11/2026", dopo.Dice("2611"));
    }

    // Un ciclo superato resta finché il Lab non ha quello in vigore: senza, è il solo che c'è e si apre.
    [Fact]
    public void SenzaIlCicloInVigore_IlSuperatoResta()
    {
        var stato = CicliDellAip.Di(["2611"], new DateOnly(2026, 12, 1));

        Assert.Equal(("2612", (string?)null, "2611"), (stato.DiOggi, stato.InVigore, stato.DaAprire));
        Assert.Empty(stato.DaTogliere);
    }

    [Fact]
    public void Togli_LaCartellaDeiPdfEIlModello_SoloDelSuperato()
    {
        foreach (var c in new[] { "2611", "2612" })
        {
            Directory.CreateDirectory(Path.Combine(EmendamentoDellAip.CartellaDelCiclo(Pdf, c), "ENR"));
            File.WriteAllText(Path.Combine(EmendamentoDellAip.CartellaDelCiclo(Pdf, c), "ENR", "LI-ENR 4.4.pdf"), "%PDF");
            Directory.CreateDirectory(Path.Combine(Espanso, c));
            File.WriteAllText(AipEspanso.File(Espanso, c), "{}");
        }
        Assert.Equal(["2611", "2612"], CicliDellAip.Presenti(Pdf, Espanso));

        // Prima del 26 novembre il 2611 è in vigore: non si toglie, e non si tocca niente.
        Assert.NotNull(CicliDellAip.Togli(Pdf, Espanso, "2611", new DateOnly(2026, 11, 25)));
        Assert.NotNull(CicliDellAip.Togli(Pdf, Espanso, "2612", new DateOnly(2026, 11, 26)));
        Assert.Equal(["2611", "2612"], CicliDellAip.Presenti(Pdf, Espanso));

        Assert.Null(CicliDellAip.Togli(Pdf, Espanso, "2611", new DateOnly(2026, 11, 26)));

        Assert.Equal(["2612"], CicliDellAip.Presenti(Pdf, Espanso));
        Assert.False(Directory.Exists(EmendamentoDellAip.CartellaDelCiclo(Pdf, "2611")));
        Assert.True(File.Exists(AipEspanso.File(Espanso, "2612")));
    }

    // Nel repo dell'AIP espanso il ciclo sta sul suo ramo, con la sua storia: si tolgono solo i PDF.
    [Fact]
    public void Togli_NelRepoDellAipEspanso_IlModelloResta()
    {
        foreach (var c in new[] { "2611", "2612" })
        {
            Directory.CreateDirectory(EmendamentoDellAip.CartellaDelCiclo(Pdf, c));
            Directory.CreateDirectory(Path.Combine(Espanso, c));
            File.WriteAllText(AipEspanso.File(Espanso, c), "{}");
        }
        Directory.CreateDirectory(Path.Combine(Espanso, ".git"));

        Assert.Null(CicliDellAip.Togli(Pdf, Espanso, "2611", new DateOnly(2026, 11, 26)));

        Assert.False(Directory.Exists(EmendamentoDellAip.CartellaDelCiclo(Pdf, "2611")));
        Assert.True(File.Exists(AipEspanso.File(Espanso, "2611")));
    }
}
