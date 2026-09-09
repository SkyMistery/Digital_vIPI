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
        Assert.Equal("<ul class=\"md-list\"><li>uno</li><li>due</li></ul>", Html("- uno\n- due"));
        Assert.Equal("<ul class=\"md-list\"><li>uno</li></ul>", Html("* uno"));
        Assert.Equal("<ul class=\"md-list\"><li>uno</li></ul>", Html("+ uno"));
    }

    /// <summary>Gli elenchi già scritti a mano coi <c>•</c> nei vSOP diventano elenchi veri senza che
    /// nessuno debba riscriverli.</summary>
    [Fact]
    public void Il_puntino_scritto_a_mano_vale_come_marcatore()
    {
        Assert.Equal("<ul class=\"md-list\"><li>uno</li><li>due</li></ul>", Html("• uno\n• due"));
    }

    [Fact]
    public void Un_elenco_numerato()
    {
        Assert.Equal("<ol class=\"md-list\"><li>uno</li><li>due</li></ol>", Html("1. uno\n2. due"));
        Assert.Equal("<ol class=\"md-list\"><li>uno</li></ol>", Html("1) uno"));
    }

    /// <summary>Chi scrive «3.» sta continuando un elenco interrotto da una tabella o da un'immagine:
    /// ricominciare da 1 gli cambierebbe il documento.</summary>
    [Fact]
    public void Un_elenco_numerato_conserva_il_numero_di_partenza()
    {
        Assert.Equal("<ol class=\"md-list\" start=\"3\"><li>tre</li><li>quattro</li></ol>", Html("3. tre\n4. quattro"));
    }

    [Fact]
    public void Le_voci_portano_il_markup_inline()
    {
        Assert.Equal("<ul class=\"md-list\"><li>a <strong>b</strong></li></ul>", Html("- a **b**"));
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
        Assert.Equal("<ul class=\"md-list\"><li>a</li></ul><ol class=\"md-list\"><li>b</li></ol>", Html("- a\n1. b"));
    }

    [Fact]
    public void Un_elenco_chiude_il_capoverso_che_lo_precede()
    {
        Assert.Equal("<p>testo</p><ul class=\"md-list\"><li>voce</li></ul>", Html("testo\n- voce"));
    }

    /// <summary>Una voce rientrata è una voce come le altre: niente annidamento, è il perimetro.</summary>
    [Fact]
    public void Le_voci_rientrate_non_annidano()
    {
        Assert.Equal("<ul class=\"md-list\"><li>a</li><li>b</li></ul>", Html("- a\n  - b"));
    }

    [Fact]
    public void Vuoto_e_solo_spazi_non_rendono_niente()
    {
        Assert.Equal("", Html(null));
        Assert.Equal("", Html("   \r\n  "));
    }
}
