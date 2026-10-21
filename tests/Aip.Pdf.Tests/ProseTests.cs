using Aip.Pdf.Italia;
using static Aip.Pdf.Tests.Enr4Tests;

namespace Aip.Pdf.Tests;

/// <summary>
/// I testi in prosa (carta «L'AIP intero», passo 4): due colonne, l'italiano a sinistra e l'inglese a destra, su PDF
/// costruiti qui. Dati inventati, senza lettere accentate (il font standard di PdfPig non le ha tutte).
/// </summary>
public sealed class ProseTests
{
    private static (double, double, string)[] Testata(string riferimento) =>
        [(450, 5, riferimento), (52, 17, "AIP - Italia AIRAC effective date 17 APR 2025")];

    [Fact]
    public void DueColonne_IParagrafi_ITitoli_IPuntiElenco()
    {
        var pagina = new Pagina([], [],
        [
            .. Testata("ENR 5.1 - 1"),
            (52, 60, "ENR 5.1 ZONE VIETATE"), (300, 60, "ENR 5.1 PROHIBITED AREAS"),
            (52, 80, "1 Zona vietata"), (300, 80, "1 Prohibited area"),
            (52, 90, "Spazio aereo di dimensioni definite, al di sopra del territorio, entro il"), (300, 90, "An airspace of defined dimensions, above the land areas, within which the"),
            (52, 100, "quale il volo e vietato."), (300, 100, "flight is prohibited."),
            (52, 110, "NOTA"), (300, 110, "REMARK"),
            (52, 120, "A meno che indicato diversamente nella colonna delle note, il volume largo"), (300, 120, "Unless otherwise specified in the remarks column, the volume of airspace"),
            (52, 130, "15 m (per es. linee) vale H24."), (300, 130, "15 m wide (e.g. lines) is H24."),
            (52, 140, "2 PROCEDURE PER IL TRAFFICO"), (300, 140, "2 PROCEDURES FOR GENERAL"),
            (52, 150, "AEREO GENERALE"), (300, 150, "AIR TRAFFIC"),
            (52, 160, "I piloti devono:"), (300, 160, "Pilots shall:"),
            (52, 170, "• evitare le aree;"), (292, 170, "•"), (300, 170, "avoid the areas;"),
        ]);

        var letto = ItalianProse.Read(PdfText.Read(Enr4Tests.Pdf(pagina)), "ENR 5.1");

        var t = Assert.Single(letto.Texts);
        Assert.Equal(("ENR 5.1", new AipPage("ENR 5.1-1", new DateOnly(2025, 4, 17))), (t.Section, t.Page));
        Assert.Equal(string.Join("\n",
            "ENR 5.1 ZONE VIETATE", "1 Zona vietata", "Spazio aereo di dimensioni definite, al di sopra del territorio, entro il quale il volo e vietato.", "NOTA",
            "A meno che indicato diversamente nella colonna delle note, il volume largo 15 m (per es. linee) vale H24.", "2 PROCEDURE PER IL TRAFFICO AEREO GENERALE", "I piloti devono:",
            "• evitare le aree;"), t.Text);
        Assert.Equal(string.Join("\n",
            "ENR 5.1 PROHIBITED AREAS", "1 Prohibited area", "An airspace of defined dimensions, above the land areas, within which the flight is prohibited.", "REMARK",
            "Unless otherwise specified in the remarks column, the volume of airspace 15 m wide (e.g. lines) is H24.", "2 PROCEDURES FOR GENERAL AIR TRAFFIC", "Pilots shall:",
            "• avoid the areas;"), t.English);
        Assert.Empty(letto.Issues);
    }

    [Fact]
    public void IlTestoPrimaDellaTabella_SuPiuPagine()
    {
        var uno = new Pagina([], [],
        [
            .. Testata("ENR 5.5.1 - 1"),
            (52, 60, "1 Generalita"), (300, 60, "1 General"),
            (52, 70, "Le discese si fanno nelle zone."), (300, 70, "Descents are made in the areas."),
        ]);
        var due = new Pagina([40, 200, 300, 420, 560], [200, 260, 320],
        [
            .. Testata("ENR 5.5.1 - 2"),
            (52, 60, "2 Procedure"), (300, 60, "2 Procedures"),
            (52, 70, "Si avvisa l'ente."), (300, 70, "The unit is advised."),
            (45, 210, "ZONA PROVA -CIV-"), (205, 210, "FL80"),
        ]);

        var t = ItalianProse.BeforeTable(PdfText.Read(Enr4Tests.Pdf(uno, due)), "ENR 5.5.1", 4);

        Assert.NotNull(t);
        Assert.Equal("1 Generalita\nLe discese si fanno nelle zone.\n2 Procedure\nSi avvisa l'ente.", t.Text);
        Assert.Equal("1 General\nDescents are made in the areas.\n2 Procedures\nThe unit is advised.", t.English);
        Assert.Equal("ENR 5.5.1-1", t.Page?.Reference);
    }
}
