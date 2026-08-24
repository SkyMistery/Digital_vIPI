using System.Linq;
using Vipi.Application.Diagnostics;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// I rilievi sulle shape che arrivano dalla sorgente. Esistono perché `LIRR_TS_CTR` non attribuiva traffico
/// da chissà quanto e se n'è accorto un occhio umano guardando una vista 3D: un settore muto deve dirlo.
/// </summary>
public class ShapeDiSorgenteTests
{
    private const string Anello = "[[11,41],[13,41],[13,43],[11,43]]";
    private const string AnelloDoppio = "[[11,41],[13,41],[13,43],[11,43],[11,41],[13,41],[13,43],[11,43]]";

    private static ConsistencyDataset Dati(params SectorShapeRow[] shapes) => new() { SectorShapes = shapes };

    private static ConsistencyFinding[] Rilievi(params SectorShapeRow[] shapes) =>
        ConsistencyReportService.Analyze(Dati(shapes)).ToArray();

    [Fact]
    public void Un_contorno_ripetuto_si_racconta_col_numero_di_copie()
    {
        var r = Assert.Single(Rilievi(new SectorShapeRow("Settore ACC", "LIRR_TS_CTR", "CTR", AnelloDoppio, false)));

        Assert.Equal("Contorno ripetuto", r.Category);
        Assert.Equal(ConsistencyArea.Sorgente, r.Area);
        Assert.Contains("LIRR_TS_CTR", r.Entity);
        Assert.Contains("2 volte", r.Detail);
        Assert.Equal(new object[] { 2, 8 }, r.DetailArgs);
    }

    [Fact]
    public void Un_anello_normale_non_produce_rilievi()
    {
        Assert.Empty(Rilievi(new SectorShapeRow("Settore ACC", "LIRR_NE_CTR", "CTR", Anello, false)));
    }

    [Fact]
    public void Un_settore_con_volume_ma_senza_poligono_si_segnala()
    {
        var r = Assert.Single(Rilievi(new SectorShapeRow("Postazione", "LIRP_APP", "APP", null, false)));

        Assert.Equal("Settore senza poligono", r.Category);
        Assert.Equal(ConsistencyArea.Sorgente, r.Area);
    }

    [Fact]
    public void Per_DEL_GND_e_ATIS_l_assenza_di_shape_e_la_normalita()
    {
        // Non hanno un volume di spazio aereo: segnalarle vorrebbe dire 25 righe di rumore ogni giorno.
        Assert.Empty(Rilievi(
            new SectorShapeRow("Postazione", "LIRF_GND", "GND", null, false),
            new SectorShapeRow("Postazione", "LIRF_DEL", "DEL", null, false),
            new SectorShapeRow("Postazione", "LIRF_ATIS", "ATIS", null, false)));
    }

    [Fact]
    public void Il_cerchio_sintetico_si_dichiara_come_stima()
    {
        var r = Assert.Single(Rilievi(new SectorShapeRow("Postazione", "LIBD_TWR", "TWR", Anello, IsSynthetic: true)));

        Assert.Equal("Shape sintetica", r.Category);
        Assert.Contains("stima", r.Detail);
    }

    [Fact]
    public void Il_contorno_ripetuto_vince_sul_cerchio_sintetico()
    {
        // Un rilievo per settore: quello che spiega la conseguenza peggiore.
        var r = Assert.Single(Rilievi(new SectorShapeRow("Postazione", "LATI_APP", "APP", AnelloDoppio, IsSynthetic: true)));
        Assert.Equal("Contorno ripetuto", r.Category);
    }

    [Fact]
    public void Senza_shape_in_archivio_il_controllo_non_dice_niente()
    {
        Assert.Empty(ConsistencyReportService.Analyze(new ConsistencyDataset()));
    }
}
