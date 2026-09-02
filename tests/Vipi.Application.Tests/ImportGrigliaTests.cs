using System.Linq;
using Vipi.Application.Import;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il primo stadio dell'import: da un testo o da un file a una <b>griglia di celle</b>, e nient'altro.
///
/// <para>E' una funzione pura perche' e' un <b>giudizio</b> — «questa e' una tabella separata da punto e
/// virgola», «questa riga di trattini e' impaginazione» — e un giudizio va potuto provare e smentire senza un
/// database e senza un browser.</para>
/// </summary>
public class ImportGrigliaTests
{
    // ---- riconoscimento della forma -------------------------------------------------------------------

    [Fact]
    public void Le_tabulazioni_vincono_su_tutto()
    {
        var g = Griglia.Leggi("A\tB\tC\n1\t2\t3");

        Assert.Equal(FormaGriglia.Tabulazioni, g.Forma);
        Assert.Equal(new[] { "A", "B", "C" }, g.Riga(0));
        Assert.Equal(3, g.Colonne);
    }

    [Fact]
    public void Il_punto_e_virgola_fa_un_csv()
    {
        var g = Griglia.Leggi("Nome;Numeri;Usato da\nAlfa;1-4;Squadrone 1");

        Assert.Equal(FormaGriglia.Csv, g.Forma);
        Assert.Equal(new[] { "Alfa", "1-4", "Squadrone 1" }, g.Riga(1));
    }

    [Fact]
    public void Le_virgolette_tengono_dentro_separatore_e_a_capo()
    {
        var g = Griglia.LeggiCsvEsplicito("a;\"uno; due\";c\nd;\"riga\nspezzata\";f", ';');

        Assert.Equal(new[] { "a", "uno; due", "c" }, g.Riga(0));
        Assert.Equal(new[] { "d", "riga\nspezzata", "f" }, g.Riga(1));
    }

    [Fact]
    public void Il_doppio_apice_dentro_le_virgolette_e_un_apice()
    {
        var g = Griglia.LeggiCsvEsplicito("\"detto \"\"cosi\"\"\";b", ';');

        Assert.Equal("detto \"cosi\"", g.Riga(0)[0]);
    }

    /// <summary>
    /// ⚠️ La virgola e' l'ultimo separatore che si prova: una riga sola con due virgole non fa un CSV, o
    /// «MNL TAC, 99Y, 115.25» diventerebbe tre colonne dentro una cella sola.
    /// </summary>
    [Fact]
    public void Una_virgola_sparsa_non_fa_un_csv()
    {
        var g = Griglia.Leggi("LIBA Amendola MNL TAC - 99Y, 115.25\nLIBR Brindisi BRD TAC - 79X 113.20");

        Assert.Equal(FormaGriglia.RigaIntera, g.Forma);
        Assert.Single(g.Riga(0));
    }

    [Fact]
    public void La_virgola_regolare_invece_fa_un_csv()
    {
        var g = Griglia.Leggi("a,b,c\nd,e,f\ng,h,i");

        Assert.Equal(FormaGriglia.Csv, g.Forma);
        Assert.Equal(3, g.Righe.Count);
        Assert.Equal(new[] { "d", "e", "f" }, g.Riga(1));
    }

    // ---- markdown --------------------------------------------------------------------------------------

    [Fact]
    public void La_riga_dei_trattini_e_impaginazione_non_dato()
    {
        var g = Griglia.Leggi("| Nome | Numeri |\n|------|:------:|\n| Alfa | 1-4 |");

        Assert.Equal(FormaGriglia.Markdown, g.Forma);
        Assert.Equal(2, g.Righe.Count);
        Assert.Equal(new[] { "Nome", "Numeri" }, g.Riga(0));
        Assert.Equal(new[] { "Alfa", "1-4" }, g.Riga(1));
    }

    // ---- html ------------------------------------------------------------------------------------------

    [Fact]
    public void Dalla_clipboard_di_excel_le_celle_sono_celle()
    {
        var g = Griglia.Leggi(
            "<html><body><table><tr><th>AIRPORT</th><th>NAVAIDS</th></tr>" +
            "<tr><td>LIBA Amendola</td><td>MNL TAC &ndash; 99Y</td></tr></table></body></html>");

        Assert.Equal(FormaGriglia.Html, g.Forma);
        Assert.Equal(new[] { "AIRPORT", "NAVAIDS" }, g.Riga(0));
        Assert.Equal(new[] { "LIBA Amendola", "MNL TAC - 99Y" }, g.Riga(1));
    }

    /// <summary>⚠️ Una cella su due colonne diventa la cella piu' una vuota: senza, le celle successive
    /// scalerebbero a sinistra e il dato sembrerebbe sbagliato invece che unito.</summary>
    [Fact]
    public void Il_colspan_si_espande_in_celle_vuote()
    {
        var g = Griglia.Leggi("<table><tr><td colspan=\"2\">unita</td><td>c</td></tr></table>");

        Assert.Equal(new[] { "unita", "", "c" }, g.Riga(0));
    }

    [Fact]
    public void Le_entita_e_le_interruzioni_si_sciolgono()
    {
        var g = Griglia.Leggi("<table><tr><td>a&amp;b<br>c</td><td>308&#176;</td></tr></table>");

        Assert.Equal(new[] { "a&b c", "308°" }, g.Riga(0));
    }

    [Fact]
    public void Senza_tabella_l_html_non_da_niente()
    {
        Assert.False(TabellaHtml.Leggi("<p>nessuna tabella</p>").Piena);
    }

    // ---- larghezza fissa -------------------------------------------------------------------------------

    [Fact]
    public void I_tagli_suggeriti_stanno_dove_tutte_le_righe_hanno_spazio()
    {
        const string testo = "LIBA  Amendola\nLIBR  Brindisi";

        Assert.Equal(new[] { 6 }, TestoTabellare.TagliSuggeriti(testo));
    }

    [Fact]
    public void I_tagli_a_mano_producono_le_colonne()
    {
        var g = Griglia.LeggiLarghezzaFissa("LIBA  Amendola\nLIBR  Brindisi", new[] { 6 });

        Assert.Equal(FormaGriglia.LarghezzaFissa, g.Forma);
        Assert.Equal(new[] { "LIBA", "Amendola" }, g.Riga(0));
        Assert.Equal(new[] { "LIBR", "Brindisi" }, g.Riga(1));
    }

    /// <summary>Una riga piu' corta del taglio da' una cella vuota: e' quel che si vede trascinando la
    /// maniglia, non un errore da rifiutare.</summary>
    [Fact]
    public void Un_taglio_oltre_la_riga_da_una_cella_vuota()
    {
        var g = Griglia.LeggiLarghezzaFissa("ab", new[] { 10 });

        Assert.Equal(new[] { "ab", "" }, g.Riga(0));
    }

    // ---- pulizie ---------------------------------------------------------------------------------------

    /// <summary>Nelle cinque righe d'esempio degli «Aeroporti alternati» convivono il trattino lungo e
    /// quello normale, e una riga ha un doppio spazio.</summary>
    [Fact]
    public void I_trattini_lunghi_e_gli_spazi_ripetuti_si_appianano()
    {
        Assert.Equal("MNL TAC - 99Y 115.25",
            TestoTabellare.NormalizzaSegni("MNL TAC – 99Y  115.25"));
        Assert.Equal("a b", TestoTabellare.NormalizzaSegni("a\u00A0b"));
    }

    /// <summary>⚠️ Le righe restano irregolari: pareggiarle qui nasconderebbe che la lettura ha perso un
    /// pezzo. Le pareggia la specifica che sa quante colonne vuole.</summary>
    [Fact]
    public void Le_righe_restano_irregolari()
    {
        var g = Griglia.Leggi("a\tb\tc\nd\te");

        Assert.Equal(3, g.Riga(0).Count);
        Assert.Equal(2, g.Riga(1).Count);
    }

    [Fact]
    public void Un_testo_vuoto_non_e_un_errore()
    {
        Assert.False(Griglia.Leggi(null).Piena);
        Assert.False(Griglia.Leggi("   \n  ").Piena);
        Assert.Equal(FormaGriglia.Vuota, Griglia.Leggi("").Forma);
    }

    [Fact]
    public void Senza_la_prima_riga_restano_i_dati()
    {
        var g = Griglia.Leggi("H1\tH2\na\tb").SenzaPrima();

        Assert.Single(g.Righe);
        Assert.Equal(new[] { "a", "b" }, g.Riga(0));
    }
}
