using Vipi.Application.Stats;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Che cosa accetta il campo «cerca un controllore».
///
/// <para>⚠️ I casi che contano non sono il numero nudo — quello funziona da sé — ma i tre modi in cui un
/// VID arriva davvero: incollato da un indirizzo, con l'etichetta davanti, o con lo spazio delle migliaia
/// preso da un foglio di calcolo.</para>
/// </summary>
public class VidInputTests
{
    [Theory]
    [InlineData("704798", 704798)]
    [InlineData("  704798  ", 704798)]
    [InlineData("VID 704798", 704798)]
    [InlineData("vid:704798", 704798)]
    [InlineData("704 798", 704798)]                                    // incollato da un foglio di calcolo
    [InlineData("https://ivao.aero/Member.aspx?Id=704798", 704798)]    // incollato dal profilo
    [InlineData("Carmine (704798)", 704798)]                           // com'è scritto il nickname IVAO
    [InlineData("12345", 12345)]                                       // cinque cifre: il minimo
    public void Riconosce_il_vid_comunque_sia_scritto(string scritto, int atteso) =>
        Assert.Equal(atteso, VidInput.Parse(scritto));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Mario Rossi")]
    [InlineData("1234")]                       // quattro cifre non sono un VID
    [InlineData("123456789")]                  // nove: è l'identificativo di qualcos'altro
    public void Quel_che_non_e_un_vid_resta_niente(string? scritto) =>
        Assert.Null(VidInput.Parse(scritto));

    /// <summary>
    /// ⚠️ In <c>Member.aspx?Id=704798</c> ci sono cifre anche prima del VID: se si prendesse la prima
    /// sequenza <b>qualunque</b> invece della prima sequenza <b>lunga abbastanza</b>, si aprirebbe il
    /// profilo di un altro — e nessuno lo noterebbe, perché una pagina si aprirebbe lo stesso.
    /// </summary>
    [Fact]
    public void Le_cifre_corte_dentro_l_indirizzo_non_ingannano() =>
        Assert.Equal(704798, VidInput.Parse("https://ivao.aero/v2/Member.aspx?p=3&Id=704798"));
}
