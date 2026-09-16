using Vipi.Application.Airspace;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Quali pezzi dicono la stessa cosa delle colonne di una riga di catalogo (S11 fase A, carta 15 §4-bis). Ogni caso
/// è una riga che il risolutore di oggi legge in un modo preciso, e i pezzi devono dire <b>quello</b>.
/// </summary>
public class CatalogShapeMirrorTests
{
    private const string Vecchio = "[[1,1],[2,1],[2,2]]";
    private const string Nuovo = "[[1,1],[3,1],[3,3]]";

    private static CatalogShapeRow Riga(
        string? poligono = Nuovo, string? inVigore = null, string? ciclo = null,
        ShapeSource fonte = ShapeSource.Source, bool forzata = false, bool sintetica = false,
        int? basso = null, int? alto = null) =>
        new(poligono, inVigore, ciclo, fonte, forzata, sintetica, basso, alto);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Senza_forma_in_colonna_niente_pezzi(string? poligono)
    {
        Assert.Null(CatalogShapeMirror.Desired(Riga(poligono)));
    }

    [Fact]
    public void Le_quote_si_leggono_come_le_leggeva_il_risolutore()
    {
        var p = CatalogShapeMirror.Desired(Riga(basso: 7_000, alto: 19_500))!.InForce;
        Assert.Equal((7_000, 19_500, "7000", "19500"), (p.BaseFeet, p.TopFeet, p.BaseRaw, p.TopRaw));
        Assert.Equal((AirspaceDatum.Amsl, AirspaceDatum.Amsl), (p.BaseDatum, p.TopDatum));

        var vuote = CatalogShapeMirror.Desired(Riga())!.InForce;
        Assert.Equal((null, null, "GND", "UNL"), (vuote.BaseFeet, vuote.TopFeet, vuote.BaseRaw, vuote.TopRaw));
    }

    [Fact]
    public void Il_cerchio_di_ripiego_e_sintetico_qualunque_fonte_dica_la_colonna()
    {
        Assert.Equal(ShapeSource.Synthetic, CatalogShapeMirror.Desired(Riga(sintetica: true))!.Source);
    }

    /// <summary>Il ramo vecchio delle ATZ scritte in colonna: il risolutore le leggeva come anagrafica, e l'AIP vero sta in archivio.</summary>
    [Fact]
    public void Una_forma_aip_in_colonna_si_legge_come_anagrafica()
    {
        Assert.Equal(ShapeSource.Source, CatalogShapeMirror.Desired(Riga(fonte: ShapeSource.Aip))!.Source);
    }

    [Fact]
    public void Il_sectorfile_differito_da_un_insieme_in_vigore_e_uno_in_attesa()
    {
        var d = CatalogShapeMirror.Desired(Riga(Nuovo, Vecchio, "2611", ShapeSource.Sectorfile, forzata: true))!;

        Assert.Equal(ShapeSource.Sectorfile, d.Source);
        Assert.Equal(Vecchio, d.InForce.PolygonJson);
        Assert.Equal(Nuovo, d.Pending!.PolygonJson);
        Assert.Equal(("2611", true), (d.PendingCycle, d.ForcePublished));
    }

    /// <summary>
    /// ⚠️ I tre casi in cui <c>ShapeAiracGate</c> non differisce mai: niente da mostrare al posto suo, niente ciclo,
    /// fonte che non corre avanti. Lì in vigore c'è la corrente, e un insieme in attesa sarebbe una seconda verità.
    /// </summary>
    [Theory]
    [InlineData(null, "2611", ShapeSource.Sectorfile)]
    [InlineData(Vecchio, null, ShapeSource.Sectorfile)]
    [InlineData(Vecchio, "2611", ShapeSource.Source)]
    public void Dove_il_gate_non_differisce_in_vigore_c_e_la_corrente(string? inVigore, string? ciclo, ShapeSource fonte)
    {
        var d = CatalogShapeMirror.Desired(Riga(Nuovo, inVigore, ciclo, fonte, forzata: true))!;

        Assert.Equal(Nuovo, d.InForce.PolygonJson);
        Assert.Null(d.Pending);
        Assert.Equal(((string?)null, false), (d.PendingCycle, d.ForcePublished));
    }
}
