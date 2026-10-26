using Aip;
using Aip.Pdf.Italia;
using Vipi.SectorLab.Core.PdfAip;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// 26 ottobre (committente: «quando chiudo e poi riapro, la lista delle cose da sistemare»): l'esito di una lettura si
/// ricorda nei dati del Lab e si rilegge com'era, coi numeri, le date, il rapporto e le segnalazioni. Dati inventati.
/// </summary>
public sealed class LettureRicordateTests : IDisposable
{
    private readonly string _dati = Path.Combine(Path.GetTempPath(), "letture-" + Guid.NewGuid().ToString("N"));
    private readonly string _espanso = Path.Combine(Path.GetTempPath(), "espanso-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dati))
            Directory.Delete(_dati, recursive: true);
    }

    private static EsitoDellAipEspanso Esito() =>
        new("2611", @"D:\espanso\2611\li.json", 851, ["LI-ENR 2.1.pdf", "LI-AD 2 LIBD 1.pdf"], ["LI-ENR 6.pdf"], 870,
            [(new DateOnly(2026, 10, 29), 120), (null, 3)],
            [
                new ProblemaDellImport("LI-AD 2 LIBD 1.pdf", new ImportIssue(ImportIssueKind.Geometry, "LIBD FL100", 4, "NotClosed")),
                new ProblemaDellImport("AD 2 LIEA 5-1", new ImportIssue(ImportIssueKind.Procedure, "LIEA Star CORSI 3F", null, "prosa non letta: AJO: non è una radioassistenza dell'AIP")),
            ],
            ["rotto.pdf: non si apre"])
        {
            Rotte = 29,
            Attese = 413,
            Procedure = 3898,
            Pagine = 7328,
            PagineDallaPosizione = 6,
            Rapporto = [new RapportoDelPdf("LI-ENR 5.6.1.8.pdf", 3, new Dictionary<string, int> { ["aree"] = 12 }, 1, [new ContoAtteso("coordinate", 96, 90)])],
            Cambiate = new PagineCambiate("2610", ["ENR 2.1-1"], ["ENR 2.1-9"], [], Completo: true),
            Correzioni = [new CorrectionOutcome("2026-10-26-1", CorrectionKind.Navaid, "AJO", CorrectionOutcomeKind.Applied)],
        };

    [Fact]
    public void LaLetturaSiRileggeComEra()
    {
        var letta = new DateTime(2026, 10, 26, 21, 55, 0);
        LettureRicordate.Scrivi(_dati, _espanso, Esito(), letta);

        var (esito, quando) = LettureRicordate.Leggi(_dati, _espanso + "\\", "2611")!.Value;

        Assert.Equal(letta, quando);
        Assert.Equal(("2611", 851, 870, 29, 413, 3898, 7328, 6), (esito.Ciclo, esito.Pdf, esito.Aree, esito.Rotte, esito.Attese, esito.Procedure, esito.Pagine, esito.PagineDallaPosizione));
        Assert.Equal(["LI-ENR 2.1.pdf", "LI-AD 2 LIBD 1.pdf"], esito.Letti);
        Assert.Equal([(new DateOnly(2026, 10, 29), 120), ((DateOnly?)null, 3)], esito.Date);
        Assert.Equal(Esito().Problemi, esito.Problemi);
        Assert.Equal(GruppoDellaSegnalazione.DaSistemare, SegnalazioniDellAip.Gruppo(esito.Problemi[0].Problema));
        var rapporto = Assert.Single(esito.Rapporto);
        Assert.Equal((12, new ContoAtteso("coordinate", 96, 90)), (rapporto.Letti["aree"], Assert.Single(rapporto.Conti)));
        Assert.Equal((1, 1), esito.Conti);
        Assert.Equal(["ENR 2.1-9"], esito.Cambiate!.Nuove);
        Assert.Equal("1 applicata", esito.CorrezioniInParole());
        Assert.Equal(["rotto.pdf: non si apre"], esito.Illeggibili);
    }

    [Fact]
    public void DiUnAltroAipEspanso_DiUnAltroCiclo_ORotta_NonSiLegge()
    {
        LettureRicordate.Scrivi(_dati, _espanso, Esito(), DateTime.Now);

        Assert.Null(LettureRicordate.Leggi(_dati, _espanso + "-altro", "2611"));
        Assert.Null(LettureRicordate.Leggi(_dati, _espanso, "2612"));

        File.WriteAllText(LettureRicordate.File(_dati, "2611"), "{ non è json");
        Assert.Null(LettureRicordate.Leggi(_dati, _espanso, "2611"));

        LettureRicordate.Togli(_dati, "2611");
        Assert.False(File.Exists(LettureRicordate.File(_dati, "2611")));
        LettureRicordate.Togli(_dati, "2611");
    }
}
