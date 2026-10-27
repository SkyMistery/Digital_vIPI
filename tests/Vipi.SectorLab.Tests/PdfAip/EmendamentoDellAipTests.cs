using Aip.Pdf;
using Vipi.SectorLab.Core.PdfAip;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// L'emendamento AIRAC applicato alla cartella dei PDF (carta «L'AIP intero e gli emendamenti», passo 7): la cartella del
/// ciclo nuovo accanto a quella del ciclo prima, che non si tocca; le pagine al loro posto; le sezioni nuove; il controllo
/// col GEN 0.4; e quando si ferma prima di scrivere. Tutto nel temporaneo, PDF costruiti qui.
/// </summary>
public sealed class EmendamentoDellAipTests : IDisposable
{
    private readonly string _radice = Directory.CreateTempSubdirectory("emendamento-").FullName;

    public void Dispose() => Directory.Delete(_radice, recursive: true);

    private string Pdf => Path.Combine(_radice, "pdf");

    private string Amdt => Path.Combine(_radice, "AMDT.pdf");

    // Il 2611: ENR 4.4 di tre pagine, ENR 6.3 con la terza vuota, una sezione che l'emendamento toglie tutta (ENR 5.9).
    private string Il2611()
    {
        var ciclo = EmendamentoDellAip.CartellaDelCiclo(Pdf, "2611");
        var enr = Directory.CreateDirectory(Path.Combine(ciclo, "ENR")).FullName;
        File.WriteAllBytes(Path.Combine(enr, "LI-ENR 4.4.pdf"), PdfAipFinto.Testate("ENR 4.4", "11 JUN 2026", 3));
        File.WriteAllBytes(Path.Combine(enr, "LI-ENR 6.3.pdf"), PdfAipFinto.Testate("ENR 6.3", "03 SEP 2026", 3, 3));
        File.WriteAllBytes(Path.Combine(enr, "LI-ENR 5.9.pdf"), PdfAipFinto.Testate("ENR 5.9", "27 JUL 2023", 1));
        return ciclo;
    }

    private static readonly (string, string)[] Togli =
    [
        ("GEN 0.4 - 1", "29 OCT 2026"), ("ENR 4.4 - 2", "11 JUN 2026"), ("ENR 6.3 - 3", "03 SEP 2026"), ("ENR 5.9 - 1", "27 JUL 2023"),
    ];

    private static readonly (string, string)[] Metti =
    [
        ("ENR 4.4 - 2", "26 NOV 2026"), ("ENR 6.3 - 3", "26 NOV 2026"), ("ENR 2.1.1.4.9 - 1", "26 NOV 2026"),
    ];

    private static readonly string[] Valide =
    [
        "GEN 0.4 - 1 26 NOV 2026", "ENR 2.1.1.4.9 - 1 26 NOV 2026", "ENR 3.1 - 1 26 NOV 2026", "ENR 4.4 - 1 11 JUN 2026",
        "ENR 4.4 - 2 26 NOV 2026", "ENR 4.4 - 3 11 JUN 2026", "ENR 6.3 - 1 03 SEP 2026", "ENR 6.3 - 2 03 SEP 2026",
        "ENR 6.3 - 3 26 NOV 2026",
    ];

    [Fact]
    public void LaCartellaDelCicloNuovo_LePagineAlLoroPosto_IlControlloColGen04()
    {
        var prima = Il2611();
        File.WriteAllBytes(Amdt, PdfAipFinto.Emendamento("A12/26", "26 NOV 2026", Togli, Metti, Valide, 1));

        var esito = EmendamentoDellAip.Applica(Pdf, Amdt);

        Assert.True(esito.Fatto, string.Join(" | ", esito.Bloccanti));
        Assert.Equal(("A12/26", "2611", "2612"), (esito.Numero, esito.CicloPrima, esito.CicloNuovo));
        Assert.Equal(EmendamentoDellAip.CartellaDelCiclo(Pdf, "2612"), esito.Cartella);
        Assert.Equal((2, 0, 3, 3, 1), (esito.FileRiscritti, esito.FileCopiati, esito.PagineTolte, esito.PagineMesse, esito.PagineFuoriDalleParti));
        Assert.Equal([Path.Combine("ENR", "LI-ENR 2.1.1.4.9.pdf")], esito.FileNuovi);
        Assert.Equal([Path.Combine("ENR", "LI-ENR 5.9.pdf")], esito.FileTolti);

        // Le pagine scritte, rilette: la nuova al posto della vecchia, la vuota dalla posizione, la sezione nuova.
        var dopo = PdfDellAip.Elenco(esito.Cartella!)
            .SelectMany(f => PageIndex.Of(Path.GetFileName(f), PdfText.Read(f)))
            .ToDictionary(p => p.Reference!, p => p.Effective);
        Assert.Equal(new DateOnly(2026, 11, 26), dopo["ENR 4.4-2"]);
        Assert.Equal(new DateOnly(2026, 6, 11), dopo["ENR 4.4-3"]);
        Assert.Null(dopo["ENR 6.3-3"]);
        Assert.Equal(new DateOnly(2026, 11, 26), dopo["ENR 2.1.1.4.9-1"]);
        Assert.False(dopo.ContainsKey("ENR 5.9-1"));
        Assert.Equal(7, dopo.Count);

        // Il GEN 0.4: manca solo la pagina mai scaricata; la vuota non ha data e non conta come diversa.
        Assert.True(esito.Controllato);
        Assert.Equal(["ENR 3.1-1"], esito.Mancano);
        Assert.Empty(esito.DataDiversa);
        Assert.Empty(esito.InPiu);

        // L'emendamento accanto (D2), il ciclo prima com'era, niente a metà.
        Assert.Equal(Path.Combine(esito.Cartella!, "AMDT", "A12-26.pdf"), esito.CopiaDellEmendamento);
        Assert.True(File.Exists(esito.CopiaDellEmendamento));
        Assert.Equal(3, PdfText.Read(Path.Combine(prima, "ENR", "LI-ENR 4.4.pdf")).Count);
        Assert.True(File.Exists(Path.Combine(prima, "ENR", "LI-ENR 5.9.pdf")));
        Assert.False(Directory.Exists(esito.Cartella + ".scrivendo"));
        Assert.Empty(Directory.EnumerateFiles(esito.Cartella!, "*.scrivendo", SearchOption.AllDirectories));
    }

    // Prova A45 (committente): la barra anche per l'emendamento. Le tre fasi nell'ordine, ognuna coi suoi file contati.
    [Fact]
    public void LAvanzamento_TreFasiNellOrdine_ConIFileContati()
    {
        Il2611();
        File.WriteAllBytes(Amdt, PdfAipFinto.Emendamento("A12/26", "26 NOV 2026", Togli, Metti, Valide, 1));
        var riferiti = new List<AvanzamentoDellEmendamento>();

        Assert.True(EmendamentoDellAip.Applica(Pdf, Amdt, new Riferisci(riferiti.Add)).Fatto);

        Assert.Equal([FaseDellEmendamento.LeggeLEmendamento, FaseDellEmendamento.LeggeIlCicloPrima, FaseDellEmendamento.ScriveIlCicloNuovo],
            riferiti.Select(a => a.Fase).Distinct());
        Assert.Equal("AMDT.pdf", riferiti[0].File);
        var letti = riferiti.Where(a => a.Fase == FaseDellEmendamento.LeggeIlCicloPrima).ToList();
        Assert.Equal([0, 1, 2], letti.Select(a => a.Fatti));
        Assert.All(letti, a => Assert.Equal(3, a.Tutti));
        Assert.Equal(Path.Combine("ENR", "LI-ENR 4.4.pdf"), letti[0].File);
        Assert.Equal(3, riferiti.Count(a => a.Fase == FaseDellEmendamento.ScriveIlCicloNuovo));
        Assert.Equal("Leggo i PDF del ciclo prima", letti[0].Cosa);
    }

    private sealed class Riferisci(Action<AvanzamentoDellEmendamento> riferisci) : IProgress<AvanzamentoDellEmendamento>
    {
        public void Report(AvanzamentoDellEmendamento value) => riferisci(value);
    }

    // Banco, 21 ottobre: con una cartella che non ha LICJ, l'emendamento faceva «LI-AD 2 LICJ 2.pdf» con le sole pagine
    // cambiate. Una sezione che la cartella non ha si scrive solo intera.
    [Fact]
    public void UnaSezioneCheLaCartellaNonHaSiScriveSoloIntera()
    {
        Il2611();
        (string, string)[] metti = [.. Metti, ("AD 2 LICJ 2 - 3", "26 NOV 2026")];
        string[] valide = [.. Valide, "AD 2 LICJ 2 - 1 18 APR 2024", "AD 2 LICJ 2 - 3 26 NOV 2026"];
        File.WriteAllBytes(Amdt, PdfAipFinto.Emendamento("A12/26", "26 NOV 2026", Togli, metti, valide, 1));

        var esito = EmendamentoDellAip.Applica(Pdf, Amdt);

        Assert.True(esito.Fatto, string.Join(" | ", esito.Bloccanti));
        Assert.Equal(["AD 2 LICJ 2"], esito.SezioniNuoveNonScritte);
        Assert.Equal([Path.Combine("ENR", "LI-ENR 2.1.1.4.9.pdf")], esito.FileNuovi);
        Assert.Equal(3, esito.PagineMesse);
        Assert.Equal(["AD 2 LICJ 2-1", "AD 2 LICJ 2-3", "ENR 3.1-1"], esito.Mancano);
        Assert.Empty(Directory.EnumerateFiles(esito.Cartella!, "*LICJ*", SearchOption.AllDirectories));
    }

    [Fact]
    public void UnaPaginaDaTogliereConUnAltraDataFermaTutto()
    {
        Il2611();
        (string, string)[] togli = [("GEN 0.4 - 1", "29 OCT 2026"), ("ENR 4.4 - 2", "01 OCT 2026")];
        File.WriteAllBytes(Amdt, PdfAipFinto.Emendamento("A12/26", "26 NOV 2026", togli, Metti[..1], Valide));

        var esito = EmendamentoDellAip.Applica(Pdf, Amdt);

        Assert.False(esito.Fatto);
        Assert.Contains("ENR 4.4-2", Assert.Single(esito.Bloccanti), StringComparison.Ordinal);
        Assert.False(Directory.Exists(EmendamentoDellAip.CartellaDelCiclo(Pdf, "2612")));
    }

    [Fact]
    public void UnaPaginaDaTogliereCheNonCEFermaTutto_UnaSezioneMaiScaricataNo()
    {
        Il2611();
        (string, string)[] togli = [("GEN 0.4 - 1", "29 OCT 2026"), ("ENR 4.4 - 9", "11 JUN 2026"), ("ENR 3.1 - 1", "27 JUL 2023")];
        File.WriteAllBytes(Amdt, PdfAipFinto.Emendamento("A12/26", "26 NOV 2026", togli, Metti[..1], Valide));

        var esito = EmendamentoDellAip.Applica(Pdf, Amdt);

        Assert.Contains("ENR 4.4-9", Assert.Single(esito.Bloccanti), StringComparison.Ordinal);
        Assert.Equal(["ENR 3.1"], esito.SezioniNonScaricate);
    }

    [Fact]
    public void SenzaLaCartellaDelCicloPrimaOConQuellaNuovaGiaFattaSiFerma()
    {
        File.WriteAllBytes(Amdt, PdfAipFinto.Emendamento("A12/26", "26 NOV 2026", Togli, Metti, Valide));
        Directory.CreateDirectory(Pdf);

        Assert.Contains("AIP 2611", Assert.Single(EmendamentoDellAip.Applica(Pdf, Amdt).Bloccanti), StringComparison.Ordinal);

        Il2611();
        Directory.CreateDirectory(EmendamentoDellAip.CartellaDelCiclo(Pdf, "2612"));
        Assert.Contains("AIP 2612", Assert.Single(EmendamentoDellAip.Applica(Pdf, Amdt).Bloccanti), StringComparison.Ordinal);
    }

    [Fact]
    public void UnEmendamentoCheNonEntraAInizioCicloNonEAirac()
    {
        Il2611();
        File.WriteAllBytes(Amdt, PdfAipFinto.Emendamento("A12/26", "27 NOV 2026", Togli, Metti, Valide));

        Assert.Contains(EmendamentoDellAip.Applica(Pdf, Amdt).Bloccanti, b => b.Contains("non è l'inizio di un ciclo AIRAC", StringComparison.Ordinal));
    }

    [Fact]
    public void LaCartellaDaLeggere()
    {
        Directory.CreateDirectory(Pdf);
        Assert.Equal(Pdf, EmendamentoDellAip.CartellaDaLeggere(Pdf, "2611"));

        var ciclo = Il2611();
        Assert.Equal(ciclo, EmendamentoDellAip.CartellaDaLeggere(Pdf, "2611"));
        Assert.Equal(["2611"], EmendamentoDellAip.CicliNellaCartella(Pdf));
        // Con le cartelle dei cicli, un ciclo senza la sua non si legge: si leggerebbero i PDF di un altro.
        Assert.Null(EmendamentoDellAip.CartellaDaLeggere(Pdf, "2612"));
    }

    [Theory]
    [InlineData("ENR 2.1.1.4.9-1", true)]
    [InlineData("ENR 5-1", true)]
    [InlineData("AD 2 LIRF 5-12", true)]
    [InlineData("AD 3 LIBI 1-1", true)]
    [InlineData("ENR 1.4-1", false)]
    [InlineData("ENR 0.6-1", false)]
    [InlineData("GEN 0.4-1", false)]
    [InlineData("AD 1.5-1", false)]
    [InlineData("ENR 21-1", false)]
    public void LePartiTenute(string riferimento, bool tenuta) => Assert.Equal(tenuta, EmendamentoDellAip.Tenuta(riferimento));
}
