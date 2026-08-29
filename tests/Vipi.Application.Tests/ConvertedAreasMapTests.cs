using Vipi.Application.Coordinates;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La traduzione «area convertita → settore AoR». Non prova Leaflet — quello è già provato dove vive — ma le
/// tre decisioni che stanno QUI: la chiave della chip, il cerchietto per il punto singolo, e la forma
/// riconvertita che si aggiunge tratteggiata invece di sostituire.
/// </summary>
public class ConvertedAreasMapTests
{
    private static IReadOnlyList<(double Lat, double Lon)> Triangolo() =>
    [
        (42.0, 11.0), (42.5, 11.5), (41.5, 11.5),
    ];

    private static List<(int, CoordinateArea)> Una(IReadOnlyList<(double Lat, double Lon)> punti, string? nome = "R14A") =>
        [(0, new CoordinateArea(nome, punti, AnelloChiuso: true))];

    [Fact]
    public void La_Chiave_Della_Chip_E_L_Indice_Non_Il_Nome()
    {
        // ⚠️ I nomi arrivano da un file qualsiasi e il JS li usa dentro [data-sec="…"]: un numero non ha
        // niente da rompere. Stessa lezione delle aree regolamentate, dove la chiave è l'id IVAO.
        var vista = ConvertedAreasMap.Build(Una(Triangolo(), "LI R300A Amendola bis"), i => "etichetta");

        var settore = Assert.Single(vista.Sectors);
        Assert.Equal("0", settore.Callsign);
        Assert.Equal("etichetta", settore.Label);
        Assert.Single(settore.Polygons);
    }

    [Fact]
    public void Un_Punto_Solo_Diventa_Un_Cerchietto()
    {
        // Senza, chi converte UNA coordinata — il caso più comune di tutti — non vedrebbe mai la mappa.
        var vista = ConvertedAreasMap.Build(Una([(42.0, 11.0)]), i => "p");

        var poligono = Assert.Single(Assert.Single(vista.Sectors).Polygons);
        Assert.True(poligono.Points.Count >= 3);
        Assert.InRange(poligono.CenterLat, 41.99, 42.01);
    }

    [Fact]
    public void Due_Punti_Diventano_Due_Cerchietti()
    {
        var vista = ConvertedAreasMap.Build(Una([(42.0, 11.0), (43.0, 12.0)]), i => "p");

        Assert.Equal(2, Assert.Single(vista.Sectors).Polygons.Count);
    }

    [Fact]
    public void La_Riconvertita_Si_Aggiunge_Tratteggiata_E_Non_Sostituisce()
    {
        var riconv = new Dictionary<int, IReadOnlyList<(double Lat, double Lon)>>
        {
            [0] = Triangolo(),
        };

        var vista = ConvertedAreasMap.Build(Una(Triangolo()), i => "R14A", riconv);

        Assert.Equal(2, vista.Sectors.Count);
        Assert.False(vista.Sectors[0].Dashed);
        Assert.True(vista.Sectors[1].Dashed);
        Assert.Equal("0r", vista.Sectors[1].Callsign);
        // Stesso colore: il confronto è fra due disegni della STESSA area, e due colori direbbero il contrario.
        Assert.Equal(vista.Sectors[0].Color, vista.Sectors[1].Color);
    }

    [Fact]
    public void Aree_Diverse_Prendono_Colori_Diversi()
    {
        List<(int, CoordinateArea)> due =
        [
            (0, new CoordinateArea("A", Triangolo(), true)),
            (1, new CoordinateArea("B", Triangolo(), true)),
        ];

        var vista = ConvertedAreasMap.Build(due, i => $"area{i}");

        Assert.NotEqual(vista.Sectors[0].Color, vista.Sectors[1].Color);
    }

    [Fact]
    public void Senza_Aree_La_Vista_E_Vuota() =>
        Assert.Empty(ConvertedAreasMap.Build([], i => "x").Sectors);

    [Fact]
    public void Il_Json_Dell_Anello_Ha_La_Longitudine_Prima()
    {
        // ⚠️ Regola IVAO regionMapPolygon. Invertirla darebbe un poligono ruotato di 90° che si disegna
        // benissimo, e quindi non se ne lamenterebbe nessuno.
        Assert.Equal("[[11,42],[11.5,42.5]]", AuroraRingJson.Scrivi([(42.0, 11.0), (42.5, 11.5)]));
    }
}
