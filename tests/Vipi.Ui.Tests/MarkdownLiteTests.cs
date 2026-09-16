using Vipi.Ui;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il formato del testo editoriale: a capo, grassetto, corsivo, sottolineato, elenchi.
///
/// <para>⚠️ Il primo gruppo è una <b>rete su una segnalazione</b> del 9 settembre 2026 — «nella preview di
/// alcuni documenti il testo va tutto su una riga sola», vista in una sezione custom della vIPI di LIMC. Il
/// difetto non si è riprodotto: il renderer l'a capo lo rendeva, e lo rende. Ma non c'era <b>una sola prova</b>
/// che lo dicesse, quindi se torna non si saprebbe ancora dove guardare. Adesso sì.</para>
/// </summary>
public class MarkdownLiteTests
{
    private static string Html(string? markdown) => MarkdownLite.Render(markdown).Value;

    // ---- a capo ------------------------------------------------------------------------------------

    [Fact]
    public void Un_acapo_dentro_un_capoverso_resta_un_acapo()
    {
        Assert.Equal("<p>Test<br>VEst</p>", Html("Test\nVEst"));
    }

    /// <summary>
    /// ⚠️ Il fine riga di Windows. Prima questa era una vera rottura: <c>"\r\n\r\n"</c> non contiene
    /// <c>"\n\n"</c>, quindi due capoversi battuti su Windows uscivano <b>attaccati</b>, in un <c>&lt;p&gt;</c>
    /// solo e con un <c>\r</c> orfano dentro.
    /// </summary>
    [Fact]
    public void I_fine_riga_di_Windows_valgono_come_gli_altri()
    {
        Assert.Equal("<p>uno<br>due</p>", Html("uno\r\ndue"));
        Assert.Equal("<p>uno</p><p>due</p>", Html("uno\r\n\r\ndue"));
        Assert.Equal("<p>uno<br>due</p>", Html("uno\rdue"));
    }

    [Fact]
    public void Una_riga_vuota_apre_un_capoverso()
    {
        Assert.Equal("<p>uno</p><p>due</p>", Html("uno\n\ndue"));
    }

    /// <summary>Tre a capo di fila non fanno un capoverso vuoto: le righe bianche separano e spariscono.</summary>
    [Fact]
    public void Gli_acapo_in_eccesso_non_lasciano_capoversi_vuoti()
    {
        Assert.Equal("<p>uno</p><p>due</p>", Html("uno\n\n\n\ndue"));
        Assert.Equal("<p>uno</p><p>due</p>", Html("uno\n   \ndue"));
    }

    // ---- inline ------------------------------------------------------------------------------------

    [Fact]
    public void Grassetto_corsivo_e_sottolineato()
    {
        Assert.Equal("<p><strong>a</strong></p>", Html("**a**"));
        Assert.Equal("<p><em>a</em></p>", Html("*a*"));
        Assert.Equal("<p><u>a</u></p>", Html("__a__"));
        Assert.Equal("<p><strong>a</strong> e <em>b</em> e <u>c</u></p>", Html("**a** e *b* e __c__"));
    }

    /// <summary>
    /// ⚠️ Un identificatore con due trattini bassi in mezzo <b>non è markup</b>: i bordi del sottolineato
    /// vogliono non-spazio attaccato ai marcatori, e questo è ciò che tiene fuori i nomi tecnici.
    /// </summary>
    [Fact]
    public void I_trattini_bassi_sparsi_non_sottolineano()
    {
        Assert.Equal("<p>a __ b __ c</p>", Html("a __ b __ c"));
        Assert.Equal("<p>QNH_LIMC</p>", Html("QNH_LIMC"));
    }

    /// <summary>
    /// ⚠️ Il difetto che il taglio a righe ha chiuso di sponda: un marcatore spaiato si mangiava tutto fino
    /// al successivo, <b>a capi compresi</b>. Un asterisco dimenticato in cima poteva mettere in corsivo
    /// mezza sezione. Ora al massimo perde la sua riga.
    /// </summary>
    [Fact]
    public void Un_marcatore_spaiato_non_attraversa_le_righe()
    {
        Assert.Equal("<p>*apre<br>chiude*</p>", Html("*apre\nchiude*"));
    }

    [Fact]
    public void Il_testo_resta_encodato()
    {
        Assert.Contains("&lt;script&gt;", Html("<script>"));
        Assert.DoesNotContain("<script>", Html("<script>"));
    }

    // ---- elenchi -----------------------------------------------------------------------------------

    [Fact]
    public void Un_elenco_puntato()
    {
        Assert.Equal("<ul class=\"md-list md-l1\"><li>uno</li><li>due</li></ul>", Html("- uno\n- due"));
        Assert.Equal("<ul class=\"md-list md-l1\"><li>uno</li></ul>", Html("* uno"));
        Assert.Equal("<ul class=\"md-list md-l1\"><li>uno</li></ul>", Html("+ uno"));
    }

    /// <summary>Gli elenchi già scritti a mano coi <c>•</c> nei vSOP diventano elenchi veri senza che
    /// nessuno debba riscriverli.</summary>
    [Fact]
    public void Il_puntino_scritto_a_mano_vale_come_marcatore()
    {
        Assert.Equal("<ul class=\"md-list md-l1\"><li>uno</li><li>due</li></ul>", Html("• uno\n• due"));
    }

    [Fact]
    public void Un_elenco_numerato()
    {
        Assert.Equal("<ol class=\"md-list md-l1\"><li>uno</li><li>due</li></ol>", Html("1. uno\n2. due"));
        Assert.Equal("<ol class=\"md-list md-l1\"><li>uno</li></ol>", Html("1) uno"));
    }

    /// <summary>Chi scrive «3.» sta continuando un elenco interrotto da una tabella o da un'immagine:
    /// ricominciare da 1 gli cambierebbe il documento.</summary>
    [Fact]
    public void Un_elenco_numerato_conserva_il_numero_di_partenza()
    {
        Assert.Equal("<ol class=\"md-list md-l1\" start=\"3\"><li>tre</li><li>quattro</li></ol>", Html("3. tre\n4. quattro"));
    }

    [Fact]
    public void Le_voci_portano_il_markup_inline()
    {
        Assert.Equal("<ul class=\"md-list md-l1\"><li>a <strong>b</strong></li></ul>", Html("- a **b**"));
    }

    /// <summary>
    /// ⚠️ Il conflitto per cui la riga si classifica PRIMA dell'inline: senza, <c>* voce</c> sarebbe finito
    /// nel corsivo. Lo spazio dopo il marcatore è ciò che divide i due casi.
    /// </summary>
    [Fact]
    public void Il_corsivo_a_inizio_riga_non_e_una_voce_di_elenco()
    {
        Assert.Equal("<p><em>corsivo</em></p>", Html("*corsivo*"));
    }

    [Fact]
    public void Puntato_e_numerato_restano_due_elenchi()
    {
        Assert.Equal("<ul class=\"md-list md-l1\"><li>a</li></ul><ol class=\"md-list md-l1\"><li>b</li></ol>", Html("- a\n1. b"));
    }

    [Fact]
    public void Un_elenco_chiude_il_capoverso_che_lo_precede()
    {
        Assert.Equal("<p>testo</p><ul class=\"md-list md-l1\"><li>voce</li></ul>", Html("testo\n- voce"));
    }

    /// <summary>
    /// ⚠️ Il livello lo dicono i TRATTINI, non gli spazi: una voce rientrata a spazi resta del suo livello.
    /// È la regola che rende gli annidati possibili senza l'ambiguità di quanti spazi valga un livello.
    /// </summary>
    [Fact]
    public void Gli_spazi_in_testa_non_cambiano_il_livello()
    {
        Assert.Equal("<ul class=\"md-list md-l1\"><li>a</li><li>b</li></ul>", Html("- a\n    - b"));
    }

    // ---- elenchi annidati (16 settembre 2026) --------------------------------------------------------

    [Fact]
    public void I_trattini_annidano_i_puntati()
    {
        Assert.Equal(
            "<ul class=\"md-list md-l1\"><li>a<ul class=\"md-list md-l2\"><li>b<ul class=\"md-list md-l3\">" +
            "<li>c</li></ul></li><li>d</li></ul></li><li>e</li></ul>",
            Html("- a\n-- b\n--- c\n-- d\n- e"));
    }

    [Fact]
    public void Il_trattino_davanti_al_numero_annida_i_numerati()
    {
        Assert.Equal(
            "<ol class=\"md-list md-l1\"><li>uno<ol class=\"md-list md-l2\"><li>a</li><li>b</li></ol></li>" +
            "<li>due</li></ol>",
            Html("1) uno\n-1) a\n-2) b\n2) due"));
    }

    /// <summary>La richiesta esplicita del committente: un puntato dentro un numerato, e viceversa.</summary>
    [Fact]
    public void Un_puntato_dentro_un_numerato()
    {
        Assert.Equal(
            "<ol class=\"md-list md-l1\"><li>uno<ul class=\"md-list md-l2\"><li>nota</li></ul></li><li>due</li></ol>",
            Html("1) uno\n-- nota\n2) due"));
    }

    [Fact]
    public void Un_numerato_dentro_un_puntato()
    {
        Assert.Equal(
            "<ul class=\"md-list md-l1\"><li>voce<ol class=\"md-list md-l2\"><li>passo</li><li>passo</li></ol></li></ul>",
            Html("- voce\n-1) passo\n-2) passo"));
    }

    /// <summary>
    /// Allo stesso livello, un cambio di tipo chiude un elenco e ne apre un altro — ma DENTRO la stessa voce
    /// di sopra, non fuori: il genitore è ancora lui.
    /// </summary>
    [Fact]
    public void Un_cambio_di_tipo_allo_stesso_livello_resta_nella_stessa_voce()
    {
        Assert.Equal(
            "<ul class=\"md-list md-l1\"><li>a<ul class=\"md-list md-l2\"><li>b</li></ul>" +
            "<ol class=\"md-list md-l2\"><li>c</li></ol></li></ul>",
            Html("- a\n-- b\n-1) c"));
    }

    /// <summary>
    /// ⚠️ Un salto di livello si aggancia al livello subito sotto: un terzo livello senza secondo non ha dove
    /// stare, e inventare una voce vuota metterebbe nel documento un pallino che nessuno ha scritto.
    /// </summary>
    [Fact]
    public void Un_salto_di_livello_scende_di_uno_solo()
    {
        Assert.Equal(
            "<ul class=\"md-list md-l1\"><li>a<ul class=\"md-list md-l2\"><li>b</li></ul></li></ul>",
            Html("- a\n--- b"));
        // E un elenco che comincia al secondo livello comincia dal primo.
        Assert.Equal("<ul class=\"md-list md-l1\"><li>b</li></ul>", Html("-- b"));
    }

    [Fact]
    public void Oltre_il_quinto_livello_si_resta_al_quinto()
    {
        var html = Html("- 1\n-- 2\n--- 3\n---- 4\n----- 5\n------ 6");
        Assert.Contains("md-l5", html);
        Assert.DoesNotContain("md-l6", html);
        Assert.Equal(5, MarkdownLite.LivelliMassimi);
        // La sesta voce è una SORELLA della quinta, non una figlia.
        Assert.EndsWith("<li>5</li><li>6</li></ul></li></ul></li></ul></li></ul></li></ul>", html);
    }

    /// <summary>Il numero di partenza vale per l'elenco annidato come per quello di primo livello.</summary>
    [Fact]
    public void Un_annidato_numerato_conserva_il_numero_di_partenza()
    {
        Assert.Contains("<ol class=\"md-list md-l2\" start=\"4\"><li>d</li>", Html("1) uno\n-4) d"));
    }

    /// <summary>
    /// ⚠️ Lo spazio dopo i trattini è obbligatorio: senza, la riga è testo. È ciò che tiene fuori dagli elenchi
    /// una riga di soli trattini e il «--» usato come lineetta in mezzo a una frase scritta a macchina.
    /// </summary>
    [Fact]
    public void Trattini_senza_spazio_non_sono_una_voce()
    {
        Assert.Equal("<p>---</p>", Html("---"));
        Assert.Equal("<p>--nota</p>", Html("--nota"));
        Assert.Equal("<p>-5 gradi</p>", Html("-5 gradi"));
    }

    /// <summary>Una riga di testo chiude TUTTI i livelli aperti, non solo l'ultimo.</summary>
    [Fact]
    public void Il_testo_dopo_un_annidato_chiude_tutti_i_livelli()
    {
        Assert.Equal(
            "<ul class=\"md-list md-l1\"><li>a<ul class=\"md-list md-l2\"><li>b</li></ul></li></ul><p>testo</p>",
            Html("- a\n-- b\ntesto"));
    }

    /// <summary>
    /// I simboli per livello stanno nel foglio di stile e si agganciano alla classe: se una delle dieci regole
    /// sparisse, quel livello tornerebbe al simbolo del browser — che per un secondo livello puntato è un
    /// cerchio vuoto, cioè il simbolo del TERZO. Un difetto che si legge come giusto.
    /// </summary>
    [Fact]
    public void Il_foglio_ha_un_simbolo_per_ogni_livello_e_tipo()
    {
        var css = File.ReadAllText(FileNellaWwwroot("vipi-theme.css"));
        for (var n = 1; n <= MarkdownLite.LivelliMassimi; n++)
        {
            Assert.Contains($"ol.md-l{n}{{list-style-type:", css, StringComparison.Ordinal);
            Assert.Contains($"ul.md-l{n}{{list-style-type:", css, StringComparison.Ordinal);
        }
        Assert.Contains("ol.md-l1{list-style-type:decimal}", css, StringComparison.Ordinal);
        Assert.Contains("ol.md-l2{list-style-type:lower-alpha}", css, StringComparison.Ordinal);
        Assert.Contains("ol.md-l3{list-style-type:upper-roman}", css, StringComparison.Ordinal);
        Assert.Contains("ul.md-l1{list-style-type:disc}", css, StringComparison.Ordinal);
    }

    private static string FileNellaWwwroot(string nome)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui", "wwwroot", nome);
            if (File.Exists(c)) return c;
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"{nome} non trovato risalendo da {AppContext.BaseDirectory}");
    }

    [Fact]
    public void Vuoto_e_solo_spazi_non_rendono_niente()
    {
        Assert.Equal("", Html(null));
        Assert.Equal("", Html("   \r\n  "));
    }
}
