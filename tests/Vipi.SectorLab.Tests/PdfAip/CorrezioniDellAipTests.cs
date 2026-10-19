using Aip;
using Vipi.SectorLab.Core.PdfAip;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Le correzioni dell'AIP espanso, slice 2: i gesti del Lab sulle aree. 🔴 Il patto: quel che un gesto scrive nel modello
/// del ciclo è quel che una «Leggi» coi PDF scriverebbe applicando il file delle correzioni. Dati inventati.
/// </summary>
public sealed class CorrezioniDellAipTests
{
    private static readonly DateOnly Oggi = new(2026, 10, 19);

    private static Airspace Area(string id, double lat, string superiore = "FL 100") => new()
    {
        Section = "ENR 5.1",
        Type = AirspaceType.Restricted,
        Designator = id,
        Upper = new VerticalLimit { Text = superiore, Reference = VerticalReference.FlightLevel, Value = 100, Unit = VerticalUnit.Fl },
        Lower = new VerticalLimit { Text = "GND", Reference = VerticalReference.Ground },
        Geometry = new CircleGeometry(new Position(lat, 13.0), new Distance(3, DistanceUnit.Nm)),
        Page = new AipPage("ENR 5.1.2-1", new DateOnly(2026, 7, 9)),
    };

    private static AipDocument Pdf() => new() { State = "LI", Airac = "2611", Airspaces = [Area("LI R901", 42.0), Area("LI R902", 43.0)] };

    private static AipCorrectionFile Vuoto() => new() { State = "LI" };

    /// <summary>Il patto: il modello del gesto è quello che dà il file delle correzioni applicato ai PDF.</summary>
    private static void ComeUnaLettura(AipDocument pdf, CorrezioneFatta fatta)
    {
        var letto = AipCorrections.Apply(pdf, fatta.Correzioni);
        Assert.Equal(AipJson.Serialize(fatta.Modello), AipJson.Serialize(letto.Document));
        Assert.All(letto.Outcomes, o => Assert.NotEqual(CorrectionOutcomeKind.Invalid, o.Outcome));
    }

    [Fact]
    public void Correggi_IlBeforeEQuelloDelPdf_EUnaLetturaDaLoStessoModello()
    {
        var (nuova, _) = CorrezioniDellAip.Area(ModuloDellArea.Di(Area("LI R901", 42.0)) with { Superiore = "FL 120" }, Pdf().Airspaces[0]);

        var (fatta, perche) = CorrezioniDellAip.Correggi(Pdf(), Vuoto(), "LI R901", nuova!, "limite sbagliato", "AIP SUP 1/26", "2611", Oggi);

        Assert.Null(perche);
        Assert.Equal("FL 120", fatta!.Modello.Airspaces[0].Upper.Text);
        Assert.Equal(120, fatta.Modello.Airspaces[0].Upper.Value);
        var c = Assert.Single(fatta.Correzioni.Corrections);
        Assert.Equal(("2026-10-19-1", CorrectionOp.Change, "LI R901", "2611"), (c.Id, c.Op, c.Key, c.Since));
        Assert.Equal("FL 100", (string)c.Before!["upper"]!["text"]!);
        Assert.Equal("Correzione dell'AIP espanso: LI R901 — limite sbagliato (AIP SUP 1/26)", fatta.Messaggio);
        ComeUnaLettura(Pdf(), fatta);

        // Corretta di nuovo, anche col nome cambiato: il before resta quello del PDF, la prima è superata dalla seconda.
        var (ancora, _) = CorrezioniDellAip.Area(ModuloDellArea.Di(fatta.Modello.Airspaces[0]) with { Identificativo = "LI R901A" }, fatta.Modello.Airspaces[0]);
        var (dopo, _) = CorrezioniDellAip.Correggi(fatta.Modello, fatta.Correzioni, "LI R901", ancora!, "nome", "AIC 3/26", "2611", Oggi);
        Assert.Equal(["2026-10-19-2", null], dopo!.Correzioni.Corrections.Select(x => x.Superseded));
        Assert.Equal("LI R901", dopo.Correzioni.Corrections[1].Key);
        Assert.Equal("FL 100", (string)dopo.Correzioni.Corrections[1].Before!["upper"]!["text"]!);
        Assert.Equal("LI R901A", dopo.Modello.Airspaces[0].Key);
        Assert.Equal("2026-10-19-2", CorrezioniDellAip.Attiva(dopo.Correzioni, "LI R901A")!.Id);
        ComeUnaLettura(Pdf(), dopo);
    }

    [Fact]
    public void Aggiungi_ConLaFormaIncollata_ETogliRitiraLAggiunta()
    {
        var modulo = ModuloDellArea.Vuoto() with
        {
            Identificativo = "LI R999",
            Superiore = "5000 FT AMSL",
            Inferiore = "GND",
            Forma = "420000N 0120000E - 420000N 0123000E - 414800N 0123000E - 420000N 0120000E",
        };
        var (nuova, no) = CorrezioniDellAip.Area(modulo, null);
        Assert.Null(no);
        Assert.Equal(VerticalReference.Msl, nuova!.Upper.Reference);
        Assert.IsType<RingGeometry>(nuova.Geometry);

        var (fatta, _) = CorrezioniDellAip.Aggiungi(Pdf(), Vuoto(), nuova, "nuova area", "AIP SUP 2/26", "2611", Oggi);
        Assert.Equal(["LI R901", "LI R902", "LI R999"], fatta!.Modello.Airspaces.Select(a => a.Key));
        ComeUnaLettura(Pdf(), fatta);

        // Una seconda volta no: c'è già.
        Assert.Contains("C'è già", CorrezioniDellAip.Aggiungi(fatta.Modello, fatta.Correzioni, nuova, "x", "y", "2611", Oggi).Perche, StringComparison.Ordinal);

        // «Togli» su un'area che c'è solo per l'aggiunta: si ritira l'aggiunta, senza perché né fonte.
        var (tolta, perche) = CorrezioniDellAip.Togli(fatta.Modello, fatta.Correzioni, "LI R999", null, null, "2611", Oggi);
        Assert.Null(perche);
        Assert.Equal(CorrezioniDellAip.Ritirata, Assert.Single(tolta!.Correzioni.Corrections).Superseded);
        Assert.Equal(2, tolta.Modello.Airspaces.Count);
        ComeUnaLettura(Pdf(), tolta);
    }

    [Fact]
    public void Togli_ERitira_RimettonoLAreaDelPdf()
    {
        var (tolta, _) = CorrezioniDellAip.Togli(Pdf(), Vuoto(), "LI R901", "chiusa", "AIC 4/26", "2611", Oggi);
        Assert.Equal(["LI R902"], tolta!.Modello.Airspaces.Select(a => a.Key));
        Assert.Equal(CorrectionOp.Remove, Assert.Single(tolta.Correzioni.Corrections).Op);
        ComeUnaLettura(Pdf(), tolta);

        // Rimessa com'era nel PDF: in fondo all'elenco (al suo posto la rimette la prossima «Leggi»).
        var (rimessa, _) = CorrezioniDellAip.Ritira(tolta.Modello, tolta.Correzioni, "2026-10-19-1");
        Assert.Equal(["LI R902", "LI R901"], rimessa!.Modello.Airspaces.Select(a => a.Key));
        Assert.Equal(Pdf().Airspaces[0], rimessa.Modello.Airspaces[1]);
        Assert.Empty(AipCorrections.Apply(Pdf(), rimessa.Correzioni).Outcomes);

        // Una correzione ritirata non si ritira due volte.
        Assert.NotNull(CorrezioniDellAip.Ritira(rimessa.Modello, rimessa.Correzioni, "2026-10-19-1").Perche);
    }

    [Fact]
    public void RitiraUnaCorrezione_LAreaTornaQuellaDelPdf()
    {
        var (nuova, _) = CorrezioniDellAip.Area(ModuloDellArea.Di(Area("LI R902", 43.0)) with { Nome = "PROVA" }, Pdf().Airspaces[1]);
        var (fatta, _) = CorrezioniDellAip.Correggi(Pdf(), Vuoto(), "LI R902", nuova!, "nome mancante", "AIC 5/26", "2611", Oggi);
        Assert.Equal("LI R902 PROVA", fatta!.Modello.Airspaces[1].Key);

        var (ritirata, _) = CorrezioniDellAip.Ritira(fatta.Modello, fatta.Correzioni, "2026-10-19-1");

        Assert.Equal(AipJson.Serialize(Pdf()), AipJson.Serialize(ritirata!.Modello));
        Assert.Null(CorrezioniDellAip.Attiva(ritirata.Correzioni, "LI R902 PROVA"));
    }

    [Theory]
    [InlineData("", "FL 100", "GND", null, "identificativo")]
    [InlineData("LI R1", "", "GND", null, "limiti verticali")]
    [InlineData("LI R1", "FL 100", "GND", "420000N 0120000E - CASA DI MIA NONNA - 414800N 0123000E", "parole che non conosco")]
    [InlineData("LI R1", "FL 100", "GND", "nessuna coordinata", "parole che non conosco")]
    public void IlModuloDiceCheCosaNonVa(string id, string sup, string inf, string? forma, string atteso)
    {
        var modulo = ModuloDellArea.Vuoto() with { Identificativo = id, Superiore = sup, Inferiore = inf, Forma = forma };
        var (area, perche) = CorrezioniDellAip.Area(modulo, Area("LI R1", 42.0));
        Assert.Null(area);
        Assert.Contains(atteso, perche, StringComparison.Ordinal);
    }

    [Fact]
    public void IlModulo_SenzaModificheNonCorregge_ESenzaFormaNonAggiunge()
    {
        var area = Area("LI R1", 42.0);
        Assert.Contains("Non è cambiato niente", CorrezioniDellAip.Area(ModuloDellArea.Di(area), area).Perche, StringComparison.Ordinal);
        Assert.Contains("Manca la forma", CorrezioniDellAip.Area(ModuloDellArea.Di(area), null).Perche, StringComparison.Ordinal);
        // Perché e fonte obbligatori.
        var (nuova, _) = CorrezioniDellAip.Area(ModuloDellArea.Di(area) with { Classe = "G" }, area);
        Assert.Contains("perché", CorrezioniDellAip.Correggi(Pdf(), Vuoto(), "LI R901", nuova!, " ", "x", "2611", Oggi).Perche, StringComparison.Ordinal);
        Assert.Contains("fonte", CorrezioniDellAip.Correggi(Pdf(), Vuoto(), "LI R901", nuova!, "x", null, "2611", Oggi).Perche, StringComparison.Ordinal);
    }

    [Fact]
    public void LIdDelGiornoCresce()
    {
        var file = Vuoto() with
        {
            Corrections =
            [
                new AipCorrection { Id = "2026-10-19-1", Kind = CorrectionKind.Airspace, Key = "A", Op = CorrectionOp.Remove },
                new AipCorrection { Id = "2026-10-19-7", Kind = CorrectionKind.Airspace, Key = "B", Op = CorrectionOp.Remove },
                new AipCorrection { Id = "banco-9", Kind = CorrectionKind.Airspace, Key = "C", Op = CorrectionOp.Remove },
            ],
        };
        Assert.Equal("2026-10-19-8", CorrezioniDellAip.NuovoId(file, Oggi));
        Assert.Equal("2026-10-20-1", CorrezioniDellAip.NuovoId(file, Oggi.AddDays(1)));
    }
}
