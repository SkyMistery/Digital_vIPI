using Vipi.Application.Aor;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// «La sorgente mi ha dato qualcosa?» — la domanda che gli upsert devono porsi prima di sovrascrivere una
/// shape. ⚠️ Non è «è valida?»: quella la giudica chi disegna, e ha i suoi ripieghi.
/// </summary>
public class ShapeVuotaTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[]")]            // ← quel che IVAO manda oggi su tutte e 229 le righe italiane
    [InlineData("[ ]")]
    [InlineData("{}")]
    [InlineData("null")]
    public void Un_contenitore_vuoto_e_un_assenza(string? raw) =>
        Assert.True(PolygonGeometry.IsEmptyShape(raw));

    [Theory]
    [InlineData("[[12.5,41.8],[12.6,41.9],[12.7,41.7]]")]   // poligono vero
    [InlineData("[[12.5,41.8]]")]                            // un punto solo: pochi, ma non è un'assenza
    [InlineData("[[]]")]                                     // array che contiene un array vuoto: c'è un elemento
    [InlineData("{\"points\":[]}")]                          // oggetto con una proprietà
    [InlineData("{\"poly\":1}")]                             // forma che il parser non sa leggere: non tocca a noi
    [InlineData("garbage non-json")]
    public void Qualunque_cosa_con_del_contenuto_non_lo_e(string raw) =>
        Assert.False(PolygonGeometry.IsEmptyShape(raw));

    /// <summary>
    /// ⚠️ Il presidio della scelta: la domanda NON è «si disegna?». Un poligono di due punti non si proietta
    /// (<see cref="AorPolygonProjector.Project"/> → null) ma non è un'assenza, e un upsert che lo scartasse si
    /// terrebbe una shape vecchia al posto di quella che la sorgente ha appena mandato.
    /// </summary>
    [Fact]
    public void Non_e_un_validatore()
    {
        const string degenere = "[[12.5,41.8],[12.6,41.9]]";

        Assert.Null(AorPolygonProjector.Project(degenere));       // non si disegna
        Assert.False(PolygonGeometry.IsEmptyShape(degenere));     // ma è comunque qualcosa
    }
}
