using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La regola con cui una SID si trova (committente, 1 ottobre 2026): col codice, col nome completo con o senza spazio,
/// col fix o un punto della transition; e la fonia della nota radio della vista live.
/// </summary>
public class CercaProceduraTests
{
    private static AirportSidRowView Riga(string fix, string nome, string transition = "—") =>
        new("06", fix, nome, transition, "—", "—", "—", "—", "—");

    [Theory]
    [InlineData("ALAX7G")]
    [InlineData("alax7g")]
    [InlineData("ALAXI 7G")]
    [InlineData("ALAXI7G")]
    [InlineData("ALAXI")]
    [InlineData("  alaxi  7g ")]
    public void La_SID_si_trova_col_codice_e_col_nome_completo(string cercato) =>
        Assert.True(CercaProcedura.Combacia(Riga("ALAXI", "ALAX7G"), cercato));

    [Fact]
    public void Si_trova_anche_un_punto_della_transition() =>
        Assert.True(CercaProcedura.Combacia(Riga("ALAXI", "ALAX7G", "PONZA"), "ponza"));

    [Theory]
    [InlineData("AGNI7G")]
    [InlineData("ALAXI 7J")]
    public void Un_altra_SID_no(string cercato) =>
        Assert.False(CercaProcedura.Combacia(Riga("ALAXI", "ALAX7G"), cercato));

    [Fact]
    public void Il_nome_completo_e_la_fonia()
    {
        Assert.Equal("ALAXI 7G", CercaProcedura.NomeCompleto(Riga("ALAXI", "ALAX7G")));
        Assert.Equal("ALAXI seven golf", CercaProcedura.Fonia("ALAXI 7G"));
        Assert.Equal("BANAV niner alfa", CercaProcedura.Fonia("BANAV 9A"));
        Assert.Null(CercaProcedura.Fonia("BRL1Z-ARL1K"));
        Assert.Null(CercaProcedura.Fonia("GOLF 1"));
    }
}
