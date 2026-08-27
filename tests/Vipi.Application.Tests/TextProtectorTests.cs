using Vipi.Application.Translation;

namespace Vipi.Application.Tests;

/// <summary>
/// Il protettore: che cosa non si traduce, e soprattutto che cosa <b>non esce di qui</b> (carta
/// <c>2026-08-27-documenti-bilingue.md</c> §3).
///
/// <para>
/// ⚠️ <b>Questi non sono test di qualità della traduzione, sono test di un cancello.</b> Il decisore ha
/// posto un vincolo — i dati pubblici si possono mandare a un servizio esterno, VID e nomi utente mai — e un
/// vincolo del genere o è codice, o è una buona intenzione. La differenza fra le due cose la fanno queste
/// asserzioni e quella su tutto il <c>vipi.db</c> reale in <c>ProtettoreSulDatabaseRealeTests</c>.
/// </para>
/// </summary>
public class TextProtectorTests
{
    private static readonly TextProtector Nudo = new();
    private static readonly TextProtector ConRoster = new(new[] { "Mario Rossi", "Giulia Bianchi" });

    /// <summary>
    /// Il testo con i segnaposto RIMOSSI: e' cio' che il motore tradurrebbe davvero. L'invariante degli
    /// identificatori si esprime qui — non «non compaiono nel testo», che dal 27 agosto 2026 e' falso e
    /// deve esserlo (viaggiano dentro il tag, perche' al motore serve l'ancora), ma «non restano FUORI dai
    /// tag», dove il motore li tradurrebbe.
    /// </summary>
    private static string FuoriDaiSegnaposto(string protetto) =>
        System.Text.RegularExpressions.Regex.Replace(protetto, @"<x id=""\d+""\s*/>|<x id=""\d+""\s*>[^<]*</x>", "@");

    /// <summary>Protegge e ritraduce fingendo un motore che lascia il testo com'è: prova il giro completo.</summary>
    private static string GiroCompleto(TextProtector p, string testo)
    {
        var protetto = p.Protect(testo);
        Assert.True(TextProtector.TryRestore(protetto.Text, protetto.Tokens, out var tornato));
        return tornato;
    }

    // ---- Il cancello sui dati personali --------------------------------------------------------------

    [Theory]
    [InlineData("Firmato da VID 123456")]
    [InlineData("Firmato da vid: 1234567")]
    [InlineData("Riferimento 543210 per la pratica")]
    public void Un_VID_non_esce_mai(string testo)
    {
        var protetto = Nudo.Protect(testo);
        Assert.DoesNotContain("123456", protetto.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("1234567", protetto.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("543210", protetto.Text, StringComparison.Ordinal);
        Assert.True(protetto.Safe);
    }

    [Fact]
    public void Un_nome_del_roster_non_esce_mai()
    {
        // Il caso vero misurato: la sezione «validity» è HostAndBlocks, e sotto la scheda derivata resta il
        // FIRMATARIO scritto a mano in un blocco editoriale. Quello a un servizio esterno ci finirebbe.
        var protetto = ConRoster.Protect("Firmatario italiano: Mario Rossi, LIBB CH / AOD");
        Assert.DoesNotContain("Mario", protetto.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Rossi", protetto.Text, StringComparison.OrdinalIgnoreCase);
        Assert.True(protetto.Safe);
    }

    [Fact]
    public void Il_nome_intero_si_protegge_prima_del_pezzo()
    {
        // I nomi si applicano dal più lungo al più corto. Se «Mario» venisse prima, di «Mario Rossi»
        // resterebbe in chiaro il cognome — che è comunque un dato personale.
        var protetto = new TextProtector(new[] { "Mario", "Mario Rossi" }).Protect("Scritto da Mario Rossi.");
        Assert.DoesNotContain("Rossi", protetto.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Un_cognome_dentro_una_parola_non_e_un_cognome()
    {
        // ⚠️ Trovato dal test sul corpus reale, e non era un difetto del test: «Rossi» sta dentro
        // «crossing», che nelle vLOA compare in ogni frase di confine — «traffic crossing the common
        // boundary». Senza la parola intera, uno staffista di cognome Rossi avrebbe fatto uscire i
        // documenti a brandelli, e non per un dato personale ma per una collisione di lettere.
        const string vera = "This LoA covers traffic crossing the common boundary.";
        var protetto = ConRoster.Protect(vera);
        Assert.Empty(protetto.Tokens);
        Assert.Equal(vera, protetto.Text);
        Assert.False(ConRoster.RestaQualcosaDiPersonale(vera));
    }

    [Fact]
    public void Un_cognome_vero_nella_stessa_frase_si_trova_lo_stesso()
    {
        // Il falso positivo non deve fermare la ricerca: dopo «crossing» c'e' un «Rossi» vero.
        var protetto = ConRoster.Protect("Traffic crossing the boundary, firmato Mario Rossi.");
        Assert.Single(protetto.Tokens);
        Assert.Equal("Mario Rossi", protetto.Tokens[0]);
        Assert.Contains("crossing", protetto.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_nome_torna_al_suo_posto_dopo_il_giro()
    {
        Assert.Equal("Firmatario: Mario Rossi", GiroCompleto(ConRoster, "Firmatario: Mario Rossi"));
    }

    [Fact]
    public void Se_resta_qualcosa_di_personale_il_segmento_non_e_sicuro()
    {
        // Fail closed: il chiamante NON deve spedire un segmento non sicuro. Qui si costruisce a mano il
        // caso che il protettore non sa chiudere, per provare che il cancello lo riconosce.
        Assert.True(Nudo.RestaQualcosaDiPersonale("residuo VID 998877"));
        Assert.False(Nudo.RestaQualcosaDiPersonale("Contatta la torre"));
        Assert.True(ConRoster.RestaQualcosaDiPersonale("a cura di Giulia Bianchi"));
    }

    // ---- Gli identificatori --------------------------------------------------------------------------

    [Theory]
    [InlineData("Contatta LIRF_TWR sulla 118.1", new[] { "LIRF_TWR", "118.1" })]
    [InlineData("Passa a LIPP_MIL_CTR", new[] { "LIPP_MIL_CTR" })]
    [InlineData("Sali FL120 e riporta", new[] { "FL120" })]
    [InlineData("Atterra su RWY 16R", new[] { "RWY 16R" })]
    [InlineData("TACAN CH 37X operativo", new[] { "CH 37X" })]
    [InlineData("Imposta SQUAWK 7000 subito", new[] { "SQUAWK 7000" })]
    public void Gli_identificatori_finiscono_DENTRO_un_segnaposto_e_nei_gettoni(string testo, string[] attesi)
    {
        // ⚠️ L'identificatore ORA COMPARE nel testo che parte, dentro il tag, ed e' voluto: misurato contro
        // Azure il 27 agosto 2026, col tag vuoto la frase perde l'ordine delle parole («Contact X on and Y
        // bring it back downwind»). Cio' che NON deve restare e' un identificatore FUORI dai tag, dove il
        // motore lo tradurrebbe.
        var protetto = Nudo.Protect(testo);
        var fuori = FuoriDaiSegnaposto(protetto.Text);
        foreach (var a in attesi)
        {
            Assert.Contains(a, protetto.Tokens);
            Assert.DoesNotContain(a, fuori, StringComparison.Ordinal);
            Assert.Contains($"\">{a}</x>", protetto.Text, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("Contatta LIRF_TWR sulla 118.1")]
    [InlineData("Sorvola QUIESA a FL120, poi RWY 16L")]
    [InlineData("Testo con **grassetto** e *corsivo*")]
    [InlineData("Nessun identificatore qui dentro")]
    public void Il_giro_completo_restituisce_il_testo_di_partenza(string testo) =>
        Assert.Equal(TranslationText.Normalize(testo), GiroCompleto(Nudo, testo));

    [Fact]
    public void I_marcatori_del_grassetto_NON_si_proteggono()
    {
        // ⚠️ Ribaltato il 27 agosto 2026 da una misura contro Azure. Proteggerli spezza la frase in tre e il
        // motore SPOSTA LE PAROLE DENTRO I TAG:
        //   IN  «is initiated <x id="0">**</x>not later than 5 minutes<x id="1">**</x> before…»
        //   OUT «viene <x id="0">avviato **</x>non oltre 5 <x id="1">minuti**</x> prima…»
        // e il ripristino, sostituendo il tag col gettone, cancellava «avviato» e «minuti». Lasciati stare,
        // la stessa frase esce intera: per il motore un asterisco e' testo, non struttura.
        var protetto = Nudo.Protect("Il settore **LIBB** e' attivo");
        Assert.Contains("**", protetto.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("**", protetto.Tokens);
    }

    [Fact]
    public void Se_il_motore_INFILA_una_parola_nel_tag_la_parola_si_TIENE()
    {
        // Caso 2 delle tre derive misurate su Azure: la preposizione appartiene alla traduzione, e il
        // nostro valore e' ancora li'. Buttare la frase perderebbe una parola GIUSTA.
        // Misurato: «sectors bordering <x id="1">LGGG</x>» torna «settori confinanti <x id="1">con LGGG</x>».
        var protetto = Nudo.Protect("settori confinanti LGGG");
        Assert.Equal("LGGG", protetto.Tokens[0]);

        Assert.True(TextProtector.TryRestore("settori confinanti <x id=\"0\">con LGGG</x>", protetto.Tokens, out var r));
        Assert.Equal("settori confinanti con LGGG", r);
    }

    [Fact]
    public void Se_il_motore_CAMBIA_il_valore_la_traduzione_si_butta()
    {
        // Caso 3, ed e' il motivo per cui questo controllo esiste. Misurato: «TKOF AND LDG ... ON RWY 07/25
        // ONLY» e' tornato con «RWY 25» dentro il tag -- Azure ha invertito i numeri. Accettarlo avrebbe
        // scritto una PISTA SBAGLIATA in un documento operativo.
        var protetto = Nudo.Protect("consentito solo su RWY 07");
        Assert.Equal("RWY 07", protetto.Tokens[0]);

        Assert.False(TextProtector.TryRestore("consentito solo su <x id=\"0\">RWY 25</x>", protetto.Tokens, out _));
        // E lo stesso se il valore sparisce del tutto (misurato: «messo LYBA, tornato /»).
        Assert.False(TextProtector.TryRestore("consentito solo su <x id=\"0\">/</x>", protetto.Tokens, out _));
    }

    [Fact]
    public void Uno_spazio_in_piu_non_e_una_parola_persa()
    {
        var protetto = Nudo.Protect("Contatta LIRF_TWR adesso");
        Assert.True(TextProtector.TryRestore("Contact <x id=\"0\"> LIRF_TWR </x>", protetto.Tokens, out var ok));
        Assert.Equal("Contact LIRF_TWR", ok);
    }

    [Fact]
    public void Una_regola_non_puo_entrare_dentro_un_segnaposto_gia_piazzato()
    {
        // ⚠️ Difetto vero, introdotto il 27 agosto 2026 quando gli identificatori hanno cominciato a
        // viaggiare DENTRO il tag: il loro valore diventa testo visibile alle regole successive. Su
        // «Imposta SQUAWK 7000 subito» la regola dello squawk piazza <x id="0">SQUAWK 7000</x>, e quella
        // delle sigle maiuscole vedeva «SQUAWK» li' dentro e lo avvolgeva in un SECONDO tag annidato nel
        // primo. Il testo che parte sarebbe marcatura rotta, e al ritorno il ripristino non ritroverebbe
        // piu' i pezzi. Trovato da un test, non a runtime.
        var protetto = Nudo.Protect("Imposta SQUAWK 7000 subito");
        Assert.Single(protetto.Tokens);
        Assert.Equal("SQUAWK 7000", protetto.Tokens[0]);
        Assert.Equal("Imposta <x id=\"0\">SQUAWK 7000</x> subito", protetto.Text);
        Assert.DoesNotContain("<x id=\"1\"", protetto.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void In_prosa_una_sigla_maiuscola_e_un_identificatore()
    {
        // ⚠️ I nomi dei punti veri non stanno nella forma ICAO a cinque lettere: nei SOP misurati ci sono
        // QUIESA (sei) e RPN1 (tre piu' una cifra). Una regola tarata sui cinque li perdeva entrambi.
        var protetto = Nudo.Protect("Il traffico per LIRF passa da QUIESA e RPN1 in avvicinamento");
        Assert.Contains("LIRF", protetto.Tokens);
        Assert.Contains("QUIESA", protetto.Tokens);
        Assert.Contains("RPN1", protetto.Tokens);
        Assert.Contains("traffico", protetto.Text, StringComparison.Ordinal);   // la prosa resta
    }

    [Fact]
    public void In_una_cella_tutta_maiuscola_nessuna_parola_e_una_sigla()
    {
        // ⚠️ Senza questa guardia, in «REVIEW CYCLE» ogni parola somiglierebbe a un identificatore e non si
        // tradurrebbe piu' niente. La domanda «e' prosa?» si fa sull'ORIGINALE: i segnaposto contengono
        // minuscole, e chiederlo dopo darebbe la risposta sbagliata.
        var protetto = Nudo.Protect("REVIEW CYCLE");
        Assert.Empty(protetto.Tokens);
        Assert.Equal("REVIEW CYCLE", protetto.Text);
    }

    [Fact]
    public void Una_cella_maiuscola_con_una_frequenza_resta_traducibile()
    {
        // Il caso che il difetto d'ordine produceva: protetta la frequenza, il testo acquistava minuscole
        // (dal segnaposto) e da li' in poi REVIEW e CYCLE sparivano dentro altri due segnaposto.
        var protetto = Nudo.Protect("REVIEW CYCLE 126.850");
        Assert.Single(protetto.Tokens);
        Assert.Equal("126.850", protetto.Tokens[0]);
        Assert.Contains("REVIEW CYCLE", protetto.Text, StringComparison.Ordinal);
    }

    // ---- Quando il motore sbaglia --------------------------------------------------------------------

    [Fact]
    public void Se_il_motore_mangia_un_segnaposto_la_traduzione_si_butta()
    {
        // Una frase a cui manca il callsign e' PEGGIO della frase non tradotta: sembra giusta e non lo e'.
        var protetto = Nudo.Protect("Contatta LIRF_TWR sulla 118.1");
        Assert.Equal(2, protetto.Tokens.Count);
        Assert.False(TextProtector.TryRestore("Contact <x id=\"0\"/>", protetto.Tokens, out _));
    }

    [Fact]
    public void Se_il_motore_inventa_un_segnaposto_la_traduzione_si_butta()
    {
        var protetto = Nudo.Protect("Contatta LIRF_TWR");
        Assert.False(TextProtector.TryRestore("Contact <x id=\"7\"/>", protetto.Tokens, out _));
    }

    [Fact]
    public void Un_testo_senza_gettoni_torna_sempre()
    {
        Assert.True(TextProtector.TryRestore("Contact the tower", Array.Empty<string>(), out var r));
        Assert.Equal("Contact the tower", r);
    }
}
