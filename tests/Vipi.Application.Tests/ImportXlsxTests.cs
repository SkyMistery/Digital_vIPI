using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Vipi.Application.Import;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il lettore <c>.xlsx</c> senza pacchetti: uno zip con dentro dell'XML.
///
/// <para>I file di prova si costruiscono qui, a mano, invece di allegarne uno vero: un fixture binario dice
/// che «funziona su quel file», questi dicono <b>quale forma</b> si legge e quale no — che e' la cosa da
/// poter cambiare senza riaprire Excel.</para>
/// </summary>
public class ImportXlsxTests
{
    [Fact]
    public void Le_celle_di_testo_stanno_nelle_stringhe_condivise()
    {
        using var file = Xlsx(
            fogli: new[] { ("Alternati", Foglio("<row><c r=\"A1\" t=\"s\"><v>0</v></c>" +
                                                "<c r=\"B1\" t=\"s\"><v>1</v></c></row>")) },
            condivise: new[] { "AIRPORT", "NAVAIDS" });

        var esito = LettoreXlsx.Leggi(file);

        Assert.Null(esito.Guasto);
        Assert.Equal(FormaGriglia.Xlsx, esito.Griglia.Forma);
        Assert.Equal(new[] { "AIRPORT", "NAVAIDS" }, esito.Griglia.Riga(0));
        Assert.Equal(new[] { "Alternati" }, esito.Fogli);
    }

    /// <summary>⚠️ Un <c>si</c> spezzato in piu' pezzi formattati e' UNA cella: prenderne il primo perderebbe
    /// meta' del contenuto.</summary>
    [Fact]
    public void Una_stringa_spezzata_in_piu_pezzi_resta_una_cella()
    {
        using var file = Xlsx(
            fogli: new[] { ("F", Foglio("<row><c r=\"A1\" t=\"s\"><v>0</v></c></row>")) },
            condivise: new[] { "" },
            condiviseGrezze: "<si><r><t>MNL </t></r><r><t>TAC</t></r></si>");

        Assert.Equal("MNL TAC", LettoreXlsx.Leggi(file).Griglia.Riga(0)[0]);
    }

    [Fact]
    public void I_numeri_arrivano_come_li_scrive_excel()
    {
        using var file = Xlsx(
            fogli: new[] { ("F", Foglio("<row><c r=\"A1\"><v>308</v></c><c r=\"B1\"><v>72.2</v></c></row>")) },
            condivise: new string[0]);

        Assert.Equal(new[] { "308", "72.2" }, LettoreXlsx.Leggi(file).Griglia.Riga(0));
    }

    /// <summary>⚠️ Una cella saltata e' una cella vuota, non una cella che manca: senza il riferimento
    /// <c>r</c> le successive scalerebbero a sinistra.</summary>
    [Fact]
    public void Una_cella_saltata_lascia_il_suo_posto_vuoto()
    {
        using var file = Xlsx(
            fogli: new[] { ("F", Foglio("<row><c r=\"A1\"><v>1</v></c><c r=\"C1\"><v>3</v></c></row>")) },
            condivise: new string[0]);

        Assert.Equal(new[] { "1", "", "3" }, LettoreXlsx.Leggi(file).Griglia.Riga(0));
    }

    /// <summary>⚠️ Un errore di Excel non entra in una SOP: la cella si legge vuota, non «#N/D».</summary>
    [Fact]
    public void Una_cella_d_errore_si_legge_vuota()
    {
        using var file = Xlsx(
            fogli: new[] { ("F", Foglio("<row><c r=\"A1\" t=\"e\"><v>#N/A</v></c>" +
                                        "<c r=\"B1\"><v>2</v></c></row>")) },
            condivise: new string[0]);

        Assert.Equal(new[] { "", "2" }, LettoreXlsx.Leggi(file).Griglia.Riga(0));
    }

    [Fact]
    public void Il_testo_in_linea_si_legge()
    {
        using var file = Xlsx(
            fogli: new[] { ("F", Foglio("<row><c r=\"A1\" t=\"inlineStr\"><is><t>LIBA</t></is></c></row>")) },
            condivise: new string[0]);

        Assert.Equal("LIBA", LettoreXlsx.Leggi(file).Griglia.Riga(0)[0]);
    }

    /// <summary>
    /// ⚠️ L'ordine delle schede lo dicono le relazioni, non i nomi dei file: chi sposta una scheda in Excel
    /// non fa rinominare <c>sheet1.xml</c>. Qui la PRIMA scheda e' il secondo file.
    /// </summary>
    [Fact]
    public void L_ordine_dei_fogli_e_quello_delle_schede_non_dei_file()
    {
        using var file = Xlsx(
            fogli: new[]
            {
                ("Prima", Foglio("<row><c r=\"A1\" t=\"inlineStr\"><is><t>uno</t></is></c></row>")),
                ("Seconda", Foglio("<row><c r=\"A1\" t=\"inlineStr\"><is><t>due</t></is></c></row>")),
            },
            condivise: new string[0],
            ordineSchede: new[] { 1, 0 });

        var esito = LettoreXlsx.Leggi(file);

        Assert.Equal(new[] { "Seconda", "Prima" }, esito.Fogli);
        Assert.Equal("due", esito.Griglia.Riga(0)[0]);
        Assert.Equal("uno", LettoreXlsx.Leggi(Riavvolgi(file), 1).Griglia.Riga(0)[0]);
    }

    [Fact]
    public void Un_file_che_non_e_uno_zip_lo_dice_invece_di_alzare()
    {
        using var finto = new MemoryStream(Encoding.UTF8.GetBytes("non sono uno zip"));

        var esito = LettoreXlsx.Leggi(finto);

        Assert.NotNull(esito.Guasto);
        Assert.False(esito.Griglia.Piena);
    }

    // ---- costruzione dei file di prova -----------------------------------------------------------------

    private static MemoryStream Riavvolgi(MemoryStream s)
    {
        s.Position = 0;
        return s;
    }

    private static string Foglio(string righe) =>
        "<?xml version=\"1.0\"?><worksheet><sheetData>" + righe + "</sheetData></worksheet>";

    /// <summary>
    /// Un xlsx minimo ma vero: cartella di lavoro, relazioni, stringhe condivise e i fogli.
    /// <paramref name="ordineSchede"/> dice in che ordine le schede citano i file (per default lo stesso).
    /// </summary>
    private static MemoryStream Xlsx(
        (string Nome, string Xml)[] fogli,
        IReadOnlyList<string> condivise,
        string? condiviseGrezze = null,
        int[]? ordineSchede = null)
    {
        var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            var ordine = ordineSchede ?? Progressivo(fogli.Length);

            var schede = new StringBuilder();
            var relazioni = new StringBuilder();
            for (var i = 0; i < ordine.Length; i++)
            {
                var f = ordine[i];
                schede.Append($"<sheet name=\"{fogli[f].Nome}\" sheetId=\"{i + 1}\" r:id=\"rId{f + 1}\"/>");
                relazioni.Append($"<Relationship Id=\"rId{f + 1}\" Target=\"worksheets/sheet{f + 1}.xml\"/>");
            }

            Scrivi(zip, "xl/workbook.xml",
                "<workbook xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                "<sheets>" + schede + "</sheets></workbook>");
            Scrivi(zip, "xl/_rels/workbook.xml.rels",
                "<Relationships>" + relazioni + "</Relationships>");

            var si = condiviseGrezze ?? string.Concat(
                System.Linq.Enumerable.Select(condivise, s => "<si><t>" + s + "</t></si>"));
            Scrivi(zip, "xl/sharedStrings.xml", "<sst>" + si + "</sst>");

            for (var i = 0; i < fogli.Length; i++)
                Scrivi(zip, $"xl/worksheets/sheet{i + 1}.xml", fogli[i].Xml);
        }
        memoria.Position = 0;
        return memoria;
    }

    private static int[] Progressivo(int n)
    {
        var v = new int[n];
        for (var i = 0; i < n; i++) v[i] = i;
        return v;
    }

    private static void Scrivi(ZipArchive zip, string percorso, string contenuto)
    {
        var voce = zip.CreateEntry(percorso);
        using var s = voce.Open();
        var byteArray = Encoding.UTF8.GetBytes(contenuto);
        s.Write(byteArray, 0, byteArray.Length);
    }
}
