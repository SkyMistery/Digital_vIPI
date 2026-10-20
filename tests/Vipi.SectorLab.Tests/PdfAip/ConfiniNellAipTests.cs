using Aip;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Vipi.SectorLab.Core.PdfAip;
using Vipi.SectorLab.Tests.Ui;
using Vipi.SectorLab.Ui.Servizi;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Riordino R9 (D20): dal sector all'AIP espanso, solo i confini — i tratti che l'AIP nomina senza disegnarli presi da
/// <c>itgeo.geo</c> e scritti come correzione dell'area. Un confine delle Alpi inventato, come in <see cref="ConfiniDelSectorTests"/>.
/// </summary>
public sealed class ConfiniNellAipTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly SessioneDelLab _lab;
    private readonly string _espanso;

    public ConfiniNellAipTests()
    {
        _lab = new SessioneDelLab(Path.Combine(_albero.Radice, "dati-del-lab"));
        _espanso = Path.Combine(_albero.Radice, "espanso");
    }

    public void Dispose() => _albero.Dispose();

    private static readonly IReadOnlyList<Coordinate> Alpi =
        [new(44.0, 7.0), new(45.5, 7.5), new(45.6, 8.5), new(45.05, 8.98)];

    private static Airspace Area(string designatore, AirspaceType tipo, Position da, Position a, Position terzo) => new()
    {
        Section = "ENR 2.1", Type = tipo, Designator = designatore,
        Upper = new VerticalLimit { Text = "FL195" }, Lower = new VerticalLimit { Text = "GND" },
        Geometry = new RingGeometry([new BorderSegment(da, BorderKind.State, "Italian northern geographical border"), new LineSegment(a), new LineSegment(terzo)]),
    };

    // La FIR: il confine dalle Alpi a ovest (44.0, 7.0) a quelle a est (45.05, 8.98), poi a sud. La zona «lontana» ha un
    // confine in Sicilia, dove il sector di prova non ha linee.
    private static Airspace Fir() => Area("PROVA FIR", AirspaceType.Fir, new Position(44.0, 7.0), new Position(45.05, 8.98), new Position(43.0, 8.0));

    private static Airspace Lontana() => Area("LONTANA CTA", AirspaceType.Cta, new Position(37.0, 14.0), new Position(37.5, 15.0), new Position(36.5, 15.0));

    private static Airspace Semplice() => new()
    {
        Section = "ENR 5.1", Type = AirspaceType.Restricted, Designator = "LI R1",
        Upper = new VerticalLimit { Text = "FL95" }, Lower = new VerticalLimit { Text = "GND" },
        Geometry = new CircleGeometry(new Position(42.0, 12.0), new Distance(5, DistanceUnit.Nm)),
    };

    [Fact]
    public void LeRighe_ETracciata_ColTracciatoDelSector()
    {
        var confini = new ConfiniDelSector([Alpi]);
        var modello = new AipDocument { State = "LI", Airspaces = [Fir(), Lontana(), Semplice()] };

        var righe = ConfiniNellAip.Righe(modello, null, confini.Tracciatore);

        Assert.Equal(["LONTANA CTA", "PROVA FIR"], righe.Select(r => r.Chiave));
        Assert.All(righe, r => Assert.Equal(EsitoDelConfronto.SoloNelSector, r.Esito));
        Assert.Equal([false, true], righe.Select(r => r.Tracciabile));
        Assert.StartsWith("«Italian northern geographical border»: 2 punti dal sector", righe[1].Dettagli[0], StringComparison.Ordinal);
        Assert.Contains("non si trova sul tracciato del sector", righe[0].Dettagli[0], StringComparison.Ordinal);
        Assert.All(ConfiniNellAip.Righe(modello, null, null), r => Assert.False(r.Tracciabile));

        var (area, perche) = ConfiniNellAip.Tracciata(Fir(), confini.Tracciatore);
        Assert.Null(perche);
        var tratti = Assert.IsType<RingGeometry>(area!.Geometry).Segments;
        Assert.All(tratti, t => Assert.IsType<LineSegment>(t));
        Assert.Equal([new Position(44.0, 7.0), new Position(45.5, 7.5), new Position(45.6, 8.5), new Position(45.05, 8.98), new Position(43.0, 8.0)],
            tratti.Select(t => t.From));
        Assert.Equal(Fir() with { Geometry = area.Geometry }, area);
        Assert.Contains("non si trova", ConfiniNellAip.Tracciata(Lontana(), confini.Tracciatore).Perche, StringComparison.Ordinal);
        Assert.Contains("non ha tratti", ConfiniNellAip.Tracciata(Semplice(), confini.Tracciatore).Perche, StringComparison.Ordinal);
    }

    private static string Punto(Coordinate c) => Aip.Aurora.AuroraText.Coordinates(new Position(c.LatitudeDeg, c.LongitudeDeg));

    private async Task ApriAsync()
    {
        // Lo sfondo della mappa (lo strato di itgeo.geo): le Alpi, un lato per riga come nel file vero.
        var righe = Alpi.Zip(Alpi.Skip(1), (a, b) => $"{Punto(a)};{Punto(b)};COAST;");
        _albero.Scrivi("SectorFiles/Include/IT/GEO/itgeo.geo", "//Coast Data\r\n" + string.Join("\r\n", righe) + "\r\n");
        Assert.True(await _lab.ApriEValidaAsync(_albero.Radice));
        Assert.True(_lab.CambiaLAipEspanso(_espanso), _lab.Rifiuto);
        Directory.CreateDirectory(Path.Combine(_espanso, "2611"));
        await File.WriteAllTextAsync(Path.Combine(_espanso, "2611", "li.json"),
            AipJson.Serialize(new AipDocument { State = "LI", Airac = "2611", Airspaces = [Fir(), Lontana(), Semplice()] }));
    }

    [Fact]
    public async Task PortaNellAip_UnaCorrezionePerArea_ESiRitira()
    {
        await ApriAsync();
        Assert.True(await _lab.ConfrontaLaSezioneAsync(SezioneDelConfronto.Confini), _lab.Rifiuto);
        var righe = _lab.RigheDellaSezione(SezioneDelConfronto.Confini)!.Value.Righe;
        Assert.Equal([("LONTANA CTA", (string?)null), ("PROVA FIR", "itgeo.geo")], righe.Select(r => (r.Aip!, r.Sector)));

        // La lontana non si traccia: si dice, e la FIR va lo stesso.
        Assert.True(await _lab.PortaIConfiniNellAipAsync(["PROVA FIR", "LONTANA CTA"]), _lab.Rifiuto);
        Assert.Contains("LONTANA CTA: il tratto", _lab.Rifiuto, StringComparison.Ordinal);

        var correzioni = CorrezioniDellAip.Leggi(_espanso, "LI");
        var fatta = Assert.Single(correzioni.Corrections);
        Assert.Equal(("PROVA FIR", CorrectionOp.Change, ConfiniNellAip.Fonte), (fatta.Key, fatta.Op, fatta.Source));
        Assert.Contains("Italian northern geographical border", fatta.Reason, StringComparison.Ordinal);
        var scritto = AipJson.Deserialize(await File.ReadAllTextAsync(Path.Combine(_espanso, "2611", "li.json")));
        var fir = scritto.Airspaces.Single(a => a.Key == "PROVA FIR");
        Assert.DoesNotContain(((RingGeometry)fir.Geometry).Segments, s => s is BorderSegment);
        // Il sector non si tocca.
        Assert.False(_lab.Modifiche.CEQualcosa);

        // La riga della FIR ora è «uguale», con la sua correzione; la lontana resta da portare.
        var dopo = _lab.RigheDellaSezione(SezioneDelConfronto.Confini)!.Value.Righe;
        Assert.Equal([EsitoDelConfronto.SoloNelSector, EsitoDelConfronto.Uguale], dopo.Select(r => r.Esito));
        Assert.Contains(fatta.Id, dopo[1].Dettagli[0], StringComparison.Ordinal);

        // Si ritira come le altre: la FIR torna con la frase dell'AIP.
        Assert.True(await _lab.RitiraLaCorrezioneAsync(fatta.Id), _lab.Rifiuto);
        Assert.True(await _lab.ConfrontaLaSezioneAsync(SezioneDelConfronto.Confini), _lab.Rifiuto);
        Assert.All(_lab.RigheDellaSezione(SezioneDelConfronto.Confini)!.Value.Righe, r => Assert.Equal(EsitoDelConfronto.SoloNelSector, r.Esito));
    }

    [Fact]
    public async Task NelConfronti_PortaNellAip_DallaRiga()
    {
        await ApriAsync();
        using var contesto = new Bunit.TestContext();
        contesto.JSInterop.Mode = JSRuntimeMode.Loose;
        contesto.Services.AddSingleton(_lab);
        var pagina = contesto.RenderComponent<Vipi.SectorLab.Ui.Components.ConfrontiVista>();
        pagina.Find("[data-sezione='confini']").Click();
        pagina.WaitForAssertion(() => Assert.Single(pagina.FindAll("[data-tasto='porta-confine']")));
        Assert.NotNull(pagina.Find("[data-verso-l-aip]"));
        Assert.Empty(pagina.FindAll("[data-tasto='porta-tutti-i-confini']"));   // una sola da portare

        await pagina.InvokeAsync(() => pagina.Find("[data-tasto='porta-confine']").Click());

        pagina.WaitForAssertion(() => Assert.Empty(pagina.FindAll("[data-tasto='porta-confine']")));
        Assert.Single(CorrezioniDellAip.Leggi(_espanso, "LI").Corrections);
        Assert.Empty(pagina.FindAll("[data-confronto-rifiutato]"));
    }
}
