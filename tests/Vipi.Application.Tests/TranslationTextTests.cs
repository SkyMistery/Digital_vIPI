using System.Text;
using CsCheck;
using Vipi.Application.Translation;

namespace Vipi.Application.Tests;

/// <summary>
/// Il cuore puro della memoria di traduzione (carta <c>2026-08-27-documenti-bilingue.md</c> §1-2).
///
/// <para>
/// ⚠️ <b>Perché questi test contano più di quanto sembri.</b> Questa classe non calcola niente di
/// difficile: decide quali due testi sono <i>la stessa frase</i>. Sbagliarla non produce un errore — produce
/// una cache che non morde: si paga due volte la stessa traduzione, e la correzione fatta su una copia non
/// si vede sull'altra. Un difetto che non si manifesta mai come rosso, solo come bolletta e come confusione.
/// </para>
/// </summary>
public class TranslationTextTests
{
    // I caratteri esotici per codice numerico, per la stessa ragione per cui lo sono nel codice provato:
    // scritti come carattere, a schermo sarebbero indistinguibili dal loro equivalente ASCII.
    private const char Nbsp = (char)0x00A0;
    private const char ApostrofoTipografico = (char)0x2019;
    private const char VirgolettaAperta = (char)0x201C;
    private const char VirgolettaChiusa = (char)0x201D;
    private const char EAccentata = (char)0x00E8;

    private static string StessaFrase(string a, string b) =>
        TranslationText.Hash(a) == TranslationText.Hash(b) ? "uguale" : "DIVERSO";

    // Alfabeto delle proprietà: tutto ciò che la normalizzazione tratta in modo speciale, più qualche
    // carattere ordinario.
    // ⚠️ Non si usa `Gen.String`, che genera anche METÀ COPPIE SURROGATE — stringhe UTF-16 non valide su cui
    // `string.Normalize` LANCIA. Quel rosso non direbbe niente sul nostro codice: solo che il generatore ha
    // pescato un carattere che nessuno può battere a tastiera.
    private static readonly string Alfabeto =
        " \t\r\n.aE1" + Nbsp + ApostrofoTipografico + VirgolettaAperta + VirgolettaChiusa;

    private static readonly Gen<string> TestiPossibili = Gen.Int[0, Alfabeto.Length - 1]
        .Array[0, 40]
        .Select(indici => new string(Array.ConvertAll(indici, i => Alfabeto[i])));

    // ---- Quel che DEVE collassare: grafia, non contenuto -------------------------------------------

    [Fact]
    public void Lo_stile_di_a_capo_non_cambia_la_frase()
    {
        // Lo stesso testo battuto su Windows e incollato da altrove. Se divergessero, ogni frase esisterebbe
        // due volte in memoria a seconda di chi l'ha scritta.
        Assert.Equal("uguale", StessaFrase("Contatta la torre.\r\nRiporta sottovento.",
                                           "Contatta la torre.\nRiporta sottovento."));
        Assert.Equal("uguale", StessaFrase("Contatta la torre.\rRiporta sottovento.",
                                           "Contatta la torre.\nRiporta sottovento."));
    }

    [Fact]
    public void Lo_spazio_unificatore_e_uno_spazio()
    {
        // Il NO-BREAK SPACE lo mettono da soli i programmi di scrittura, e nessuno lo sceglie.
        Assert.Equal("uguale", StessaFrase("FL" + Nbsp + "120 autorizzato", "FL 120 autorizzato"));
    }

    [Fact]
    public void L_apostrofo_tipografico_e_un_apostrofo()
    {
        // L'apostrofo battuto a mano contro quello che la tastiera corregge da sola in "dell'area".
        Assert.Equal("uguale", StessaFrase("Attivazione dell" + ApostrofoTipografico + "area",
                                           "Attivazione dell'area"));
    }

    [Fact]
    public void Le_virgolette_tipografiche_sono_virgolette()
    {
        Assert.Equal("uguale", StessaFrase(
            "Riporta " + VirgolettaAperta + "in finale" + VirgolettaChiusa,
            "Riporta \"in finale\""));
    }

    [Fact]
    public void La_forma_Unicode_composta_e_quella_scomposta_sono_la_stessa_frase()
    {
        // La "e con accento grave" come singolo codepoint (U+00E8) contro "e" + accento combinante
        // (U+0065 U+0300). A schermo sono identiche; senza la normalizzazione NFC avrebbero due hash.
        // ⚠️ La forma scomposta si COSTRUISCE, non si scrive: in un sorgente le due grafie hanno lo stesso
        // disegno, e un editor o uno strumento di copia le ricomporrebbe senza dirlo — un test scritto con
        // due letterali passerebbe anche con la normalizzazione NFC tolta.
        var composta = "L'area " + EAccentata + " attiva";
        var scomposta = composta.Normalize(NormalizationForm.FormD);
        Assert.NotEqual(composta, scomposta);                      // davvero due stringhe diverse
        Assert.Equal("uguale", StessaFrase(composta, scomposta));  // ma una frase sola
    }

    [Fact]
    public void Spazi_ripetuti_bordi_e_paragrafi_vuoti_non_contano()
    {
        Assert.Equal("uguale", StessaFrase("  Contatta   la\ttorre.  ", "Contatta la torre."));
        Assert.Equal("uguale", StessaFrase("Primo.\n\n\n\n\nSecondo.", "Primo.\n\nSecondo."));
        Assert.Equal("uguale", StessaFrase("Riga con coda.   \nAltra.", "Riga con coda.\nAltra."));
    }

    // ---- Quel che NON deve collassare: contenuto ---------------------------------------------------

    [Fact]
    public void Le_maiuscole_contano()
    {
        // In aviazione distinguono un identificatore da una parola, e un testo tutto maiuscolo si traduce
        // peggio: va visto come un caso a sé, non fuso col suo minuscolo.
        Assert.NotEqual("uguale", StessaFrase("Contatta la torre", "contatta la torre"));
    }

    [Fact]
    public void La_punteggiatura_conta()
    {
        Assert.NotEqual("uguale", StessaFrase("Contatta la torre.", "Contatta la torre?"));
    }

    [Fact]
    public void Un_a_capo_dentro_il_testo_conta()
    {
        // Un a-capo separa due frasi: collassarlo in uno spazio cambierebbe il testo mandato al motore.
        Assert.NotEqual("uguale", StessaFrase("Primo.\nSecondo.", "Primo. Secondo."));
    }

    // ---- Invarianti della chiave -------------------------------------------------------------------

    [Fact]
    public void Il_vuoto_e_una_chiave_sola_e_stabile()
    {
        Assert.Equal("", TranslationText.Normalize(null));
        Assert.Equal("", TranslationText.Normalize("   \r\n  \t "));
        Assert.Equal(TranslationText.Hash(null), TranslationText.Hash("   "));
    }

    [Fact]
    public void L_impronta_e_esadecimale_minuscola_di_64_cifre()
    {
        var h = TranslationText.Hash("Contatta la torre.");
        Assert.Equal(64, h.Length);
        Assert.All(h, c => Assert.True(char.IsAsciiDigit(c) || (c >= 'a' && c <= 'f'), "carattere inatteso: " + c));
    }

    [Fact]
    public void L_impronta_del_grezzo_e_quella_del_normalizzato()
    {
        // Nessun chiamante può sbagliare l'ordine: `Hash` normalizza sempre da sé. Se così non fosse, chi
        // salva il grezzo e chi cerca il normalizzato userebbero due chiavi e non si troverebbero mai.
        const string grezzo = "  Contatta   la torre.\r\n";
        Assert.Equal(TranslationText.Hash(grezzo), TranslationText.Hash(TranslationText.Normalize(grezzo)));
    }

    // ---- Il filtro grossolano ----------------------------------------------------------------------

    [Theory]
    [InlineData("126.850", false)]      // una frequenza
    [InlineData("1 / 2", false)]
    [InlineData("---", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("16R", true)]           // ⚠️ HA una lettera: lo ferma il protettore, non questo cancello
    [InlineData("Contatta la torre", true)]
    public void Il_cancello_grossolano_lascia_passare_solo_cio_che_ha_lettere(string testo, bool atteso) =>
        Assert.Equal(atteso, TranslationText.HasSomethingToTranslate(testo));

    // ---- Proprietà ---------------------------------------------------------------------------------

    [Fact]
    public void Normalizzare_due_volte_non_cambia_niente()
    {
        // Idempotenza. Se cadesse, lo stesso testo salvato due volte avrebbe due hash e la memoria si
        // sdoppierebbe in silenzio. Provata su testi GENERATI e non su esempi scelti: qui il dominio è
        // tutto il testo possibile, e un elenco di esempi ne copre la fetta a cui ha pensato chi scrive.
        TestiPossibili.Sample(s =>
        {
            var una = TranslationText.Normalize(s);
            Assert.Equal(una, TranslationText.Normalize(una));
        });
    }

    [Fact]
    public void Il_normalizzato_non_ha_mai_code_di_riga_ne_bordi()
    {
        TestiPossibili.Sample(s =>
        {
            var n = TranslationText.Normalize(s);
            Assert.Equal(n, n.Trim());
            Assert.DoesNotContain("\r", n, StringComparison.Ordinal);
            Assert.DoesNotContain("\n\n\n", n, StringComparison.Ordinal);
            Assert.DoesNotContain("  ", n, StringComparison.Ordinal);
            Assert.All(n.Split('\n'), riga => Assert.Equal(riga.TrimEnd(), riga));
        });
    }
}
