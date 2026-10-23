namespace Aip.Pdf.Tests;

/// <summary>
/// Segnalazioni 2611, B8 (LIET AD 2.12): due tabelle attaccate una sotto l'altra, con colonne diverse. <see cref="Table.FindAll"/>
/// ne fa una sola con le sole colonne lunghe; <see cref="Table.FindAllSplit"/> le riconosce tutte e due.
/// </summary>
public sealed class TableSplitTests
{
    private static PdfPageText Pagina() => new(1, 600, 800, [],
    [
        // I bordi esterni, lunghi quanto le due tabelle.
        new PdfRule(50, 100, 400), new PdfRule(550, 100, 400),
        // La tabella di sopra (100-200): tre righe interne sue.
        new PdfRule(150, 100, 200), new PdfRule(250, 100, 200), new PdfRule(350, 100, 200),
        // Quella di sotto (200-400): una riga interna sua.
        new PdfRule(300, 200, 400),
    ]);

    [Fact]
    public void FindAll_UnaSolaConLeColonneLunghe()
    {
        var g = Assert.Single(Table.FindAll(Pagina(), 300));

        Assert.Equal([50.0, 300, 550], g.Columns);
    }

    [Fact]
    public void FindAllSplit_DueTabelleConLeLoroColonne()
    {
        var griglie = Table.FindAllSplit(Pagina(), 300);

        Assert.Equal(2, griglie.Count);
        Assert.Equal([50.0, 150, 250, 350, 550], griglie[0].Columns);
        Assert.Equal((100.0, 200.0), (griglie[0].Top, griglie[0].Bottom));
        Assert.Equal([50.0, 300, 550], griglie[1].Columns);
        Assert.Equal((200.0, 400.0), (griglie[1].Top, griglie[1].Bottom));
    }
}
