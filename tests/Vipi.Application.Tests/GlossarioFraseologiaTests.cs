using System.Text.RegularExpressions;
using Vipi.Application.Translation;

namespace Vipi.Application.Tests;

/// <summary>
/// Il glossario di fraseologia: le formule che si dicono in un modo solo (<c>lavori-aperti §Q3</c>).
///
/// <para>
/// ⚠️ <b>Il caso che qui si prova è quello con cui la carta dimostra il rischio.</b> Misurato contro Azure
/// il 27 agosto 2026: «Contatta LIRF_TWR sulla 118.1 e riporta sottovento» tornava «…and <i>bring it back
/// downwind</i>» — grammatica giusta, fraseologia inesistente, e nessuno se ne accorge leggendo. La memoria
/// di traduzione non poteva coprirlo, perché è indicizzata per segmento intero e questa è una formula
/// <b>dentro</b> una frase.
/// </para>
/// </summary>
public class GlossarioFraseologiaTests
{
    private static readonly GlossarioFraseologia Glossario = new(new[]
    {
        new VoceGlossario("riporta sottovento", "report downwind"),
        new VoceGlossario("riporta", "report"),
        new VoceGlossario("il campo", "the airfield"),
    });

    private static TextProtector Protettore(params string[] nomi) => new(nomi, Glossario);

    /// <summary>Un motore che lascia tutto com'è: il giro completo senza rete.</summary>
    private static string Giro(TextProtector p, string testo)
    {
        var protetto = p.Protect(testo);
        Assert.True(TextProtector.TryRestore(protetto.Text, protetto.Tokens, out var tornato));
        return tornato;
    }

    // ---- La formula dentro la frase ------------------------------------------------------------------

    [Fact]
    public void La_formula_torna_nella_nostra_resa()
    {
        Assert.Equal("Poi report downwind", Giro(Protettore(), "Poi riporta sottovento"));
    }

    [Fact]
    public void Il_caso_della_carta_esce_intero()
    {
        // ⚠️ Tre cose in una frase sola, e ognuna con la sua regola: il callsign e la frequenza tornano
        // IDENTICI (sono identificatori), la formula torna DIVERSA (è glossario). È tutta la differenza fra
        // i due segnaposto, provata dove il difetto era stato visto.
        var tornato = Giro(Protettore(), "Contatta LIRF_TWR sulla 118.1 e riporta sottovento");

        Assert.Contains("LIRF_TWR", tornato, StringComparison.Ordinal);
        Assert.Contains("118.1", tornato, StringComparison.Ordinal);
        Assert.Contains("report downwind", tornato, StringComparison.Ordinal);
        Assert.DoesNotContain("sottovento", tornato, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void La_formula_piu_lunga_vince_sulla_piu_corta()
    {
        // Il glossario ha sia «riporta sottovento» sia «riporta». Se scattasse la corta, resterebbe
        // «report sottovento»: mezza formula, e la metà rimasta non la vedrebbe più nessuna regola.
        Assert.Equal("report downwind", Giro(Protettore(), "riporta sottovento"));
    }

    [Fact]
    public void Non_scatta_dentro_una_parola_piu_lunga()
    {
        // «riporta» sta dentro «riportare». Senza la parola intera uscirebbe «reportre», che è una parola
        // inventata in mezzo a una frase — e nessun controllo la segnalerebbe.
        Assert.Equal("Da riportare subito", Giro(Protettore(), "Da riportare subito"));
    }

    [Fact]
    public void Non_distingue_le_maiuscole()
    {
        // ⚠️ La resa entra COM'È SCRITTA nel glossario, anche a inizio frase: è il rovescio del «verbatim»,
        // e va saputo da chi cura la lista. Cercare senza distinguere le maiuscole e rendere distinguendole
        // sarebbe peggio — vorrebbe dire che una formula a inizio frase non scatta affatto.
        Assert.Equal("report downwind, poi chiama", Giro(Protettore(), "Riporta sottovento, poi chiama"));
    }

    // ---- Il contratto opposto del ripristino ---------------------------------------------------------

    [Fact]
    public void Il_motore_che_TRADUCE_dentro_il_tag_non_fa_scartare_la_frase()
    {
        // ⚠️ È la differenza col segnaposto degli identificatori, ed è la ragione per cui i tag sono due.
        // Dentro il <g> parte l'ITALIANO: qualunque cosa torni è «diversa» da quel che va scritto, e col
        // confronto degli identificatori ogni frase con dentro una formula finirebbe fra gli scartati —
        // il glossario spegnerebbe la traduzione invece di migliorarla.
        var protetto = Protettore().Protect("Poi riporta sottovento");
        var dalMotore = protetto.Text.Replace("riporta sottovento", "bring it back downwind", StringComparison.Ordinal);

        Assert.True(TextProtector.TryRestore(dalMotore, protetto.Tokens, out var tornato));
        Assert.Equal("Poi report downwind", tornato);
    }

    [Fact]
    public void Il_motore_che_MANGIA_il_segnaposto_fa_scartare_la_frase()
    {
        // Qui invece il contratto è lo stesso degli identificatori: una frase a cui manca la formula è una
        // frase in cui la fraseologia l'ha scelta il motore, e si butta perché il giro dopo ci riprovi.
        var protetto = Protettore().Protect("Poi riporta sottovento");
        var dalMotore = Regex.Replace(protetto.Text, @"<g[^>]*>.*?</g>", "then downwind");

        Assert.False(TextProtector.TryRestore(dalMotore, protetto.Tokens, out _));
    }

    [Fact]
    public void Il_segnaposto_dice_ai_motori_di_non_tradurlo()
    {
        // `translate="no"` è la funzione nativa di Azure in modalità marcatura, e il nome del tag è quello
        // che DeepL riceve in `ignore_tags`. Se cambiasse qui e non là, si pagherebbero caratteri per una
        // risposta che si butta comunque — e il conto è l'unico posto in cui si vedrebbe.
        var protetto = Protettore().Protect("Poi riporta sottovento");

        Assert.Contains("<g id=\"0\" translate=\"no\">riporta sottovento</g>", protetto.Text, StringComparison.Ordinal);
    }

    // ---- La convivenza con le regole che c'erano già -------------------------------------------------

    [Fact]
    public void Non_ruba_gli_identificatori_alle_regole_che_vengono_dopo()
    {
        var protetto = Protettore().Protect("Contatta LIRF_TWR e riporta sottovento");

        Assert.Contains("<x id=", protetto.Text, StringComparison.Ordinal);   // il callsign è ancora protetto
        Assert.Contains("<g id=", protetto.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void I_dati_personali_restano_intoccabili()
    {
        // Il glossario passa DOPO i nomi, e deve restare così: una formula che ne contenesse un pezzo lo
        // porterebbe fuori dentro la resa.
        var protetto = Protettore("Mario Rossi").Protect("Mario Rossi dice di riportare sottovento");

        Assert.DoesNotContain("Mario", protetto.Text, StringComparison.OrdinalIgnoreCase);
        Assert.True(protetto.Safe);
    }

    [Fact]
    public void Senza_glossario_il_protettore_si_comporta_come_prima()
    {
        var tornato = Giro(new TextProtector(), "Poi riporta sottovento");
        Assert.Equal("Poi riporta sottovento", tornato);
    }

    // ---- Il cancello su che cosa può entrare nel glossario -------------------------------------------

    [Theory]
    [InlineData("contatta LIRF_TWR", "contact the tower")]           // callsign
    [InlineData("passa sulla 118.1", "go to the frequency")]         // frequenza
    [InlineData("sali a FL120", "climb to the level")]               // livello
    [InlineData("libera la 16L", "vacate the runway")]               // pista
    public void Una_formula_con_dentro_un_identificatore_si_rifiuta(string sorgente, string resa)
    {
        // ⚠️ Il perché non è la pignoleria: il glossario passa PRIMA delle regole sugli identificatori,
        // quindi se lo ingoiasse, quell'identificatore resterebbe cablato nella resa — la stessa frequenza
        // in ogni documento che contiene la formula, e nessun errore da nessuna parte.
        Assert.Equal(GlossarioRifiuto.ContieneIdentificatore,
            GlossarioFraseologia.PerchéNonVa(sorgente, resa));
    }

    [Fact]
    public void Una_formula_troppo_corta_si_rifiuta()
    {
        Assert.Equal(GlossarioRifiuto.TroppoCorto, GlossarioFraseologia.PerchéNonVa("su", "up"));
    }

    [Fact]
    public void Una_formula_piu_lunga_della_colonna_si_rifiuta_PRIMA_del_database()
    {
        // Senza questo cancello il rifiuto arriverebbe come eccezione durante il salvataggio: una pagina
        // d'errore a chi stava scrivendo, e senza dire quale dei due campi era troppo lungo.
        var lunga = new string('a', GlossarioFraseologia.LunghezzaMassimaSorgente + 1);
        Assert.Equal(GlossarioRifiuto.TroppoLungo, GlossarioFraseologia.PerchéNonVa(lunga, "whatever"));
    }

    [Fact]
    public void Le_parentesi_angolari_si_rifiutano()
    {
        Assert.Equal(GlossarioRifiuto.ContieneMarcatura,
            GlossarioFraseologia.PerchéNonVa("roma & milano", "rome and milan"));
    }

    [Fact]
    public void Un_duplicato_si_rifiuta_anche_scritto_con_le_maiuscole()
    {
        Assert.Equal(GlossarioRifiuto.Duplicato, GlossarioFraseologia.PerchéNonVa(
            "Riporta Sottovento", "report downwind", new[] { "riporta sottovento" }));
    }

    [Fact]
    public void Tutti_i_semi_passano_il_cancello()
    {
        // ⚠️ Una voce cablata nel codice non deve poter entrare da una porta che a una scritta a mano
        // sarebbe chiusa. Se un giorno un seme non passasse, il seme lo salterebbe in silenzio: questo test
        // lo dice prima, e dice pure QUALE.
        var messe = new List<string>();
        foreach (var voce in GlossarioFraseologia.Semi)
        {
            Assert.True(GlossarioFraseologia.PerchéNonVa(voce.Sorgente, voce.Resa, messe) is null,
                $"il seme «{voce.Sorgente}» non passa il cancello");
            messe.Add(voce.Sorgente);
        }
    }

    [Fact]
    public void I_tre_difetti_misurati_hanno_il_loro_seme()
    {
        // I casi VISTI a schermo: se qualcuno li togliesse dalla lista, tornerebbero «bring it back
        // downwind», «the camp» e «the cocking positions» — e sono gli unici tre di cui sappiamo con
        // certezza che cosa faceva la macchina.
        var sorgenti = GlossarioFraseologia.Semi.Select(v => v.Sorgente).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("riporta sottovento", sorgenti);
        Assert.Contains("il campo", sorgenti);
        Assert.Contains("armamento e disarmo", sorgenti);
    }
}
