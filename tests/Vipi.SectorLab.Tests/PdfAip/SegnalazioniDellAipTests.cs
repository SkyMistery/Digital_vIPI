using Aip;
using Aip.Pdf.Italia;
using Vipi.SectorLab.Core.PdfAip;

namespace Vipi.SectorLab.Tests.PdfAip;

// 26 ottobre (committente: «le segnalazioni come si correggono? Almeno quelle che posso correggere io»): le segnalazioni del
// 2611 divise per quel che chiedono, coi dettagli veri della lettura.
public sealed class SegnalazioniDellAipTests
{
    [Theory]
    [InlineData(ImportIssueKind.Procedure, "prosa non letta: AJO: non è una radioassistenza dell'AIP", GruppoDellaSegnalazione.DaSistemare)]
    [InlineData(ImportIssueKind.Procedure, "prosa letta con un'approssimazione: prua 329° disegnata come rotta, senza vento", GruppoDellaSegnalazione.Nota)]
    [InlineData(ImportIssueKind.Procedure, "prosa letta con un'approssimazione: «BCS DNE» è un errore di stampa: letto come «BCS DME»", GruppoDellaSegnalazione.ErroreDellAip)]
    [InlineData(ImportIssueKind.SignificantPoint, "CG753 36°40’23.6”N 011°42’07. 6”E: errore di stampa, letto come 36°40'23.6\"N 011°42'07.6\"E", GruppoDellaSegnalazione.ErroreDellAip)]
    [InlineData(ImportIssueKind.LimitsFromText, "lower vertical limit of overlying controlled airspace / SFC", GruppoDellaSegnalazione.Nota)]
    [InlineData(ImportIssueKind.PageWithoutTable, "", GruppoDellaSegnalazione.Nota)]
    [InlineData(ImportIssueKind.Holding, "senza quota: R", GruppoDellaSegnalazione.DaSistemare)]
    [InlineData(ImportIssueKind.MoreGeometries, "2", GruppoDellaSegnalazione.DaSistemare)]
    [InlineData(ImportIssueKind.Navaid, "senza coordinate", GruppoDellaSegnalazione.DaSistemare)]
    [InlineData(ImportIssueKind.Geometry, "NotClosed", GruppoDellaSegnalazione.DaSistemare)]
    [InlineData(ImportIssueKind.Procedure, "RNP Y RWY17L: IF MMP: distanza «/»", GruppoDellaSegnalazione.DaSistemare)]
    public void IlGruppo(ImportIssueKind tipo, string dettaglio, GruppoDellaSegnalazione atteso) =>
        Assert.Equal(atteso, SegnalazioniDellAip.Gruppo(new ImportIssue(tipo, "X", 1, dettaglio)));

    [Fact]
    public void IlDatoCheManca()
    {
        Assert.Equal((CorrectionKind.Navaid, "AJO"), SegnalazioniDellAip.DatoMancante(new ImportIssue(ImportIssueKind.Procedure, "LIEO Sid TEREZ 8A", 1,
            "prosa non letta: AJO: non è una radioassistenza dell'AIP; la pista non ha una salita iniziale da cui la SID continua")));
        Assert.Equal((CorrectionKind.Point, "XEBAP"), SegnalazioniDellAip.DatoMancante(new ImportIssue(ImportIssueKind.Procedure, "LIXX Star A", 1,
            "prosa non letta: XEBAP: non è un punto né una radioassistenza dell'AIP")));
        // Una parola presa per un nome dentro una salita letta da sola non è un dato che manca.
        Assert.Null(SegnalazioniDellAip.DatoMancante(new ImportIssue(ImportIssueKind.Procedure, "LIMC Sid IBCUC 1M", 1,
            "prosa letta con un'approssimazione: la SID non parte dalla pista e la salita iniziale non si legge (AMSL: non è un punto né una radioassistenza dell'AIP): disegnata da sola")));
    }

    [Fact]
    public void LaVoceDellaSegnalazione()
    {
        VoceDellAip Voce(CorrectionKind? tipo, string chiave) => new("g", chiave, [], [], tipo, chiave);
        IReadOnlyList<VoceDellAip> voci =
        [
            Voce(CorrectionKind.Point, "CG753"),
            Voce(null, "LIEA Star CORSI 3F RWY 02/20"),
            Voce(CorrectionKind.Holding, "JESSY 034 right"),
            Voce(CorrectionKind.Navaid, "GRO DVOR"),
        ];

        Assert.Equal(0, SegnalazioniDellAip.VoceDi(new ImportIssue(ImportIssueKind.SignificantPoint, "CG753", 1), voci));
        Assert.Equal(1, SegnalazioniDellAip.VoceDi(new ImportIssue(ImportIssueKind.Procedure, "LIEA Star CORSI 3F RWY 02/20", 1), voci));
        Assert.Equal(2, SegnalazioniDellAip.VoceDi(new ImportIssue(ImportIssueKind.Holding, "JESSY", 1), voci));
        Assert.Equal(3, SegnalazioniDellAip.VoceDi(new ImportIssue(ImportIssueKind.Navaid, "GRO DVOR", 1), voci));
        Assert.Null(SegnalazioniDellAip.VoceDi(new ImportIssue(ImportIssueKind.PageWithoutTable, null, 1), voci));
        Assert.Null(SegnalazioniDellAip.VoceDi(new ImportIssue(ImportIssueKind.SignificantPoint, "CG75", 1), voci));
    }
}
