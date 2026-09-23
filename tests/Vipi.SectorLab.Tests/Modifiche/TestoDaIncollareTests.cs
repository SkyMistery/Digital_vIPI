using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Tests.Modifiche;

/// <summary>
/// L'anteprima di «incolla da testo» (chiesta dal committente il 23 settembre): il testo si legge PRIMA di toccare il
/// record, e la lettura è la stessa dell'incolla — quel che l'anteprima mostra è quel che si incolla.
/// </summary>
public sealed class TestoDaIncollareTests : IDisposable
{
    private const string Settore = "SectorFiles/Include/IT/DYNAMIC_SEC/libb_es_ctr.tfl";

    /// <summary>L'esempio del committente della carta F1: un lato, un arco di 17 NM, un punto.</summary>
    private const string TestoAip =
        "44°51'24\" N 008°14'57\" E\n" +
        "then arc of circle in clockwise direction radius 17 NM centred on\n" +
        "44°55'29\" N 007°51'43\" E\n" +
        "till point\n" +
        "44°41'08\" N 008°04'34\" E";

    private readonly AlberoDiProva _albero = new();

    public void Dispose() => _albero.Dispose();

    [Fact]
    public void LAnteprimaContaGliArchiESegnaIlCentro()
    {
        var letto = TestoDaIncollare.Leggi(TestoAip, 1.0);

        Assert.Null(letto.Rifiuto);
        Assert.Equal(1, letto.Archi);
        Assert.Equal(0, letto.Cerchi);
        var centro = Assert.Single(letto.Centri);
        Assert.Equal(44 + (55 / 60.0) + (29 / 3600.0), centro.Lat, 3);
        Assert.True(letto.Punti.Count > 3);
    }

    [Fact]
    public void CambiandoLaDensitaCambiaLaForma()
    {
        // È la ragione dell'anteprima: vedere l'arco farsi più fitto o più rado prima di incollarlo.
        int rado = TestoDaIncollare.Leggi(TestoAip, 0.125).Punti.Count;
        int fitto = TestoDaIncollare.Leggi(TestoAip, 1.0).Punti.Count;

        Assert.True(rado < fitto / 4, $"un punto ogni 8°: {rado}, uno per grado: {fitto}");
    }

    [Theory]
    [InlineData("")]
    [InlineData("niente da leggere qui")]
    public void UnTestoSenzaCoordinateSiRifiuta(string testo)
    {
        var letto = TestoDaIncollare.Leggi(testo, 1.0);

        Assert.NotNull(letto.Rifiuto);
        Assert.Empty(letto.Punti);
    }

    [Fact]
    public void QuelCheSiVedeEQuelCheSiIncolla()
    {
        var file = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!).File[Settore];
        var vertici = ElenchiDiVertici.Uno(file, 0, "Vertices")!;
        bool chiuso = vertici.Scrivi(0) == vertici.Scrivi(vertici.Quanti - 1);
        var letto = TestoDaIncollare.Leggi(TestoAip, 0.5);

        var fatta = Assert.IsType<ModificaDeiVertici>(new ModificheInSospeso().IncollaVertici(file, 0, "Vertices", TestoAip, puntiPerGrado: 0.5));

        // Stessi punti; la forma chiusa ripete il primo in fondo (DensitaEChiusuraTests).
        Assert.Equal(letto.Punti.Count + (chiuso ? 1 : 0), fatta.Dopo);
        var posizioni = vertici.Posizioni().ToList();
        for (int i = 0; i < letto.Punti.Count; i++)
        {
            Assert.Equal(letto.Punti[i].Lat, posizioni[i].LatitudeDeg, 5);
            Assert.Equal(letto.Punti[i].Lon, posizioni[i].LongitudeDeg, 5);
        }
    }
}
