using System.Text.RegularExpressions;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// <b>Dove si scrivono i dati dell'aeroporto quando l'unica edizione è il vSOP militare.</b>
///
/// <para>Il guasto, trovato a schermo su LIBG Grottaglie il 2 settembre 2026: le sezioni derivate
/// dell'editor militare mostravano «per cambiarli: editor dell'aeroporto» e quel collegamento era un
/// <b>giro chiuso</b> — su un campo solo militare senza vIPI civile <c>AeroportoEditorPage</c> rimanda
/// indietro all'editor militare, perché <c>EnsureDocumentAsync</c> rifiuterebbe di far nascere il
/// documento. Il clic tornava sulla stessa pagina, senza errore e senza spiegazione, e livelli di
/// transizione, colonne editoriali delle piste e collegamenti di frequenza non avevano <b>nessuna</b>
/// porta di scrittura in tutto il sito.</para>
///
/// <para>⚠️ <b>Perché guardie strutturali.</b> Quel che va difeso non è un comportamento di un componente
/// ma un <b>accordo fra due pagine</b>: una manda, l'altra lascia entrare. Montarle in un banco di prova
/// direbbe che ognuna, da sola, fa quel che dice il suo codice — che è esattamente ciò che era vero anche
/// quando il difetto c'era. Il test che serve è quello che diventa rosso quando una delle due cambia
/// domanda senza l'altra.</para>
///
/// <para>La prova che i dati siano davvero scrivibili senza documento civile è invece un test vero, e sta
/// in <c>EdizioneGiustaPerCampoTests.Su_un_campo_SOLO_militare_i_dati_dell_aeroporto_restano_scrivibili</c>.</para>
/// </summary>
public class DatiDelloScaloMilitareTests
{
    /// <summary>
    /// ⚠️ Il corpo dell'editor militare e' passato dalla PAGINA al COMPONENTE il 3 settembre 2026
    /// (carta 2026-09-03-documenti-uniti.md §5b): lo stesso editor va montato anche nell'editor UNITO.
    /// Le invarianti di §AS — i rimandi, la guardia, il ramo del meteo, i tre editor dello scalo — sono
    /// andate con lui, e questo test le segue. La pagina ora e' un guscio sottile.
    /// </summary>
    private static string Militare() => Sorgente("Components/Doc/MilSectionsEditor.razor");
    /// <inheritdoc cref="Militare"/>
    private static string Aeroporto() => Sorgente("Components/Doc/AirportSectionsEditor.razor");

    /// <summary>
    /// ⚠️ <b>Le due pagine devono fare la STESSA domanda.</b> Chi rimanda («qui non si scrive, vai di là»)
    /// e chi lascia entrare («questa pagina non fa per te, torna indietro») decidono la stessa cosa: se
    /// una dicesse «solo militare» e l'altra «solo militare E senza documento civile», su un campo marcato
    /// solo militare <i>dopo</i> che la sua vIPI civile era nata si tornerebbe al giro chiuso — la guardia
    /// blocca la nascita, non l'apertura.
    /// </summary>
    [Fact]
    public void Chi_rimanda_e_chi_lascia_entrare_chiedono_la_stessa_cosa()
    {
        Assert.Matches(@"SoloMilitare:\s*true,\s*Esiste:\s*false", Militare());
        Assert.Matches(@"IsMilitaryOnly:\s*true,\s*DocumentId:\s*null", Aeroporto());
    }

    /// <summary>
    /// Il rimando all'editor civile esiste ancora — sui campi MISTI è quello giusto, e due editor sullo
    /// stesso dato sarebbero due verità che divergono — ma sta in <b>un posto solo</b>, dietro la guardia.
    /// </summary>
    [Fact]
    public void Il_rimando_all_editor_civile_e_uno_solo_e_passa_dalla_guardia()
    {
        var sorgente = Militare();

        Assert.Single(Regex.Matches(sorgente, @"airports/editor"));
        Assert.Contains("ScaloSenzaCivile ? EditorDelloScalo(s) : NotaEditorCivile", sorgente);
    }

    /// <summary>
    /// ⚠️ <b>Il meteo non è un dato dell'aeroporto.</b> È il METAR/TAF live dal NOAA e non si compila in
    /// nessun editor, nemmeno in quello dell'aeroporto — dove infatti c'è la nota «non c'è nulla da
    /// compilare». Cadendo nel ramo generico mandava a cambiarlo in una pagina che non può cambiarlo, e
    /// questo su <b>tutti</b> i campi militari, non solo sui solo militari.
    /// </summary>
    [Fact]
    public void Il_meteo_ha_il_suo_ramo_e_non_manda_a_cambiarlo_altrove()
    {
        var sorgente = Militare();

        Assert.Matches(@"case ""weather"":", sorgente);
        // La stessa nota dell'editor d'aeroporto, non una seconda stesura della stessa idea.
        Assert.Contains("Ape_WeatherBody", sorgente);
        Assert.Contains("Ape_WeatherBody", Aeroporto());
    }

    /// <summary>
    /// I tre editor sono quelli dell'aeroporto, montati qui: già estratti e indipendenti dalla pagina che
    /// li ospita (doc 14 §3g), quindi non esiste nessuna seconda stesura da tenere allineata.
    /// </summary>
    [Theory]
    [InlineData("AirportTransitionEditor")]
    [InlineData("AirportRunwaysEditor")]
    [InlineData("AirportFrequenciesEditor")]
    public void I_tre_editor_dell_aeroporto_si_montano_anche_qui(string componente)
    {
        Assert.Contains($"<{componente} ", Militare());
        Assert.Contains($"<{componente} ", Aeroporto());
    }

    /// <summary>
    /// ⚠️ <b>La scrittura di questi dati segue il LOCK</b> dal 4 settembre 2026 (carta
    /// 2026-09-04-aeroporto-porta-sola). Prima seguiva il solo ruolo sulla ACC, con la motivazione che «il
    /// lock del vSOP non governa l'anagrafica dell'aeroporto». Non regge: qui l'anagrafica si scrive solo su
    /// un campo <b>senza vIPI civile</b>, e per quel campo il documento militare È il documento dello scalo —
    /// tant'è che la guardia lato service, che ora il lock lo pretende, va a cercarlo proprio lì.
    /// </summary>
    [Fact]
    public void I_dati_dello_scalo_seguono_il_lock_del_documento()
    {
        var sorgente = Militare();

        Assert.DoesNotContain(@"CanEdit=", sorgente);
        foreach (var componente in new[] { "AirportTransitionEditor", "AirportRunwaysEditor", "AirportFrequenciesEditor" })
        {
            // Il montaggio del componente, fino alla sua chiusura: dentro ci dev'essere il lock.
            var montaggio = sorgente[sorgente.IndexOf($"<{componente} ", StringComparison.Ordinal)..];
            montaggio = montaggio[..montaggio.IndexOf("/>", StringComparison.Ordinal)];
            Assert.Contains(@"Editing=""_shell.IsEditing""", montaggio);
        }
    }

    private static string Sorgente(string relativo) => File.ReadAllText(Path.Combine(Radice(), relativo));

    private static string Radice()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui");
            if (Directory.Exists(Path.Combine(c, "Pages"))) return c;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"src/Vipi.Ui non trovata risalendo da {AppContext.BaseDirectory}");
    }
}
