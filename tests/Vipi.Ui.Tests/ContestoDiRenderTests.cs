using System.Globalization;
using Vipi.Ui;

namespace Vipi.Ui.Tests;

/// <summary>
/// La frase di contesto che accompagna un guasto di render.
///
/// <para>🔴 <b>La regola che questi test difendono è una sola: la rete non deve poter cadere.</b> Una frase
/// che solleva a sua volta mentre racconta un guasto <b>sostituisce l'errore vero con il proprio</b>, e si
/// perde anche quel poco che si sapeva — che è esattamente il modo di trasformare una diagnosi in un giro a
/// vuoto. Per questo il caso di prova più importante qui è quello con <b>tutto nullo</b>.</para>
///
/// <para>⚠️ E dice anche l'altra metà: che i pezzi ci siano <b>davvero</b>. Una frase di contesto che compila
/// e non nomina niente è peggio di nessuna frase, perché sembra fatta.</para>
/// </summary>
public sealed class ContestoDiRenderTests
{
    [Fact]
    public void Con_tutto_nullo_non_solleva_e_lo_dice()
    {
        var frase = ContestoDiRender.TestataApp(null, null, documentoCaricato: false,
            stazioneMilitareNota: false, nomeMostrato: null);

        Assert.Contains("app=(null)", frase);
        Assert.Contains("acc=(null)", frase);
        Assert.Contains("nome=(null)", frase);
        Assert.Contains("NON caricato", frase);
        Assert.Contains("sconosciuta", frase);
    }

    /// <summary>⚠️ Vuoto e nullo si somigliano a occhio e non sono la stessa cosa: un nome vuoto è un
    /// caricamento a metà, un nome nullo è un campo che non è mai stato scritto.</summary>
    [Fact]
    public void Vuoto_e_nullo_non_si_confondono()
    {
        var vuoto = ContestoDiRender.TestataApp("", "", false, false, "");
        var nullo = ContestoDiRender.TestataApp(null, null, false, false, null);

        Assert.Contains("app=(vuoto)", vuoto);
        Assert.DoesNotContain("(null)", vuoto);
        Assert.Contains("app=(null)", nullo);
        Assert.DoesNotContain("(vuoto)", nullo);
    }

    [Fact]
    public void Coi_valori_veri_li_nomina_tutti()
    {
        var frase = ContestoDiRender.TestataApp("LIBD_CS0_APP", "LIBB", documentoCaricato: true,
            stazioneMilitareNota: true, nomeMostrato: "Bari Avvicinamento");

        Assert.Contains("LIBD_CS0_APP", frase);
        Assert.Contains("LIBB", frase);
        Assert.Contains("Bari Avvicinamento", frase);
        Assert.Contains("caricato", frase);
        Assert.Contains("nota", frase);
    }

    /// <summary>
    /// 🔴 <b>Senza <c>NoInlining</c> la rete non serve a niente</b>, e questo è il pezzo che un riordino
    /// toglierebbe per primo — sembra un attributo di troppo su un metodo di due righe.
    ///
    /// <para>In Release un metodo corto finisce dentro il chiamante e la sua riga sparisce: la prossima
    /// occorrenza tornerebbe a incolpare <c>BuildRenderTree</c> alla riga del chiamante, cioè si sarebbe
    /// scritta una rete per non sapere niente di nuovo. È la ragione per cui tre occorrenze non hanno mai
    /// portato un colpevole.</para>
    /// </summary>
    [Fact]
    public void La_testata_dell_editor_APP_non_si_fa_inlinare()
    {
        var metodo = typeof(Vipi.Ui.Components.Doc.AppSectionsEditor)
            .GetMethod("Testata", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(metodo);
        Assert.True(metodo!.MethodImplementationFlags.HasFlag(System.Reflection.MethodImplAttributes.NoInlining),
            "`Testata()` ha perso `MethodImplOptions.NoInlining`: in Release finirebbe dentro BuildRenderTree e " +
            "la prossima NRE tornerebbe a incolpare la riga del chiamante, che e' il motivo per cui tre " +
            "occorrenze non hanno mai portato un colpevole.");
    }

    /// <summary>
    /// La lingua di lettura c'è sempre, cultura invariante compresa: è la cosa che più probabilmente cambia
    /// fra un render che regge e uno che cade, perché l'editor si ridisegna anche quando si gira la lingua.
    /// </summary>
    [Theory]
    [InlineData("it")]
    [InlineData("en")]
    [InlineData("")]
    public void La_lingua_c_e_sempre(string cultura)
    {
        var prima = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultura);
            var frase = ContestoDiRender.TestataApp("LIBD_CS0_APP", "LIBB", true, true, "x");

            Assert.Contains("lingua di lettura=", frase);
            Assert.Contains(cultura.Length == 0 ? "(invariante)" : cultura, frase);
        }
        finally { CultureInfo.CurrentUICulture = prima; }
    }
}
