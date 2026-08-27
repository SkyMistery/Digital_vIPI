using System.Text.Json;
using Vipi.Application.Translation;

namespace Vipi.Application.Tests;

/// <summary>
/// Il taglio dei campi editoriali in segmenti (carta <c>2026-08-27-documenti-bilingue.md</c> §2).
///
/// <para>
/// I JSON di prova non sono inventati: sono le forme <b>lette dal <c>vipi.db</c> reale</b> il 27 agosto 2026
/// — <c>{"title":…}</c> e <c>{"columns":…,"unified":…,"rows":[{"cells":[…]}]}</c> — e il caso «LIBB CH / AOD»
/// è una cella vera, che serve a ricordare che dentro una tabella non c'è solo prosa.
/// </para>
/// </summary>
public class TextSegmenterTests
{
    // Una tabella vera, copiata dal database: due colonne, due righe.
    private const string TabellaVera =
        """{"columns":["Item","Value"],"unified":false,"rows":[{"cells":["Review cycle","Bilateral, at least annually"]},{"cells":["Italian signatory","LIBB CH / AOD"]}]}""";

    // ---- Prosa ---------------------------------------------------------------------------------------

    [Fact]
    public void Un_blocco_di_una_frase_e_un_segmento_solo()
    {
        // È il caso normale: sui 72 blocchi con prosa del database reale, 68 non hanno nemmeno un a-capo.
        var segmenti = TextSegmenter.SplitProse("Both areas of responsibility are imported from the IVAO database.");
        Assert.Single(segmenti);
    }

    [Fact]
    public void I_paragrafi_si_separano_e_gli_a_capo_interni_restano_nel_segmento()
    {
        // Un a-capo singolo è un ritorno a capo dentro la stessa frase (MarkdownLite lo rende <br>): tagliare
        // lì spezzerebbe una frase a metà e il motore tradurrebbe due monconi.
        var segmenti = TextSegmenter.SplitProse("Prima riga\nseconda riga\n\nAltro paragrafo");
        Assert.Equal(2, segmenti.Count);
        Assert.Equal("Prima riga\nseconda riga", segmenti[0]);
        Assert.Equal("Altro paragrafo", segmenti[1]);
    }

    [Fact]
    public void Il_vuoto_non_produce_segmenti()
    {
        Assert.Empty(TextSegmenter.SplitProse(null));
        Assert.Empty(TextSegmenter.SplitProse("   \r\n  "));
    }

    [Theory]
    [InlineData("Una frase sola.")]
    [InlineData("Prima.\n\nSeconda.\n\nTerza.")]
    [InlineData("Con **grassetto** e *corsivo*.")]
    [InlineData("Riga\ncon a-capo\n\ne un secondo paragrafo")]
    public void Tagliare_e_rimettere_insieme_ridà_il_normalizzato(string testo)
    {
        // La proprietà che rende sostituibile UN paragrafo alla volta senza perdere il resto. Il confronto è
        // col NORMALIZZATO e non col grezzo: è quella la forma che gira, per scelta (§1).
        Assert.Equal(TranslationText.Normalize(testo),
                     TextSegmenter.JoinProse(TextSegmenter.SplitProse(testo)));
    }

    [Fact]
    public void Un_paragrafo_tradotto_sostituisce_solo_se_stesso()
    {
        var segmenti = TextSegmenter.SplitProse("Contatta la torre.\n\nRiporta sottovento.").ToArray();
        segmenti[0] = "Contact the tower.";
        Assert.Equal("Contact the tower.\n\nRiporta sottovento.", TextSegmenter.JoinProse(segmenti));
    }

    // ---- JSON dei blocchi -----------------------------------------------------------------------------

    [Fact]
    public void Di_una_tabella_escono_intestazioni_e_celle()
    {
        var segmenti = TextSegmenter.SplitJson(TabellaVera);
        Assert.Equal(
            new[] { "Item", "Value", "Review cycle", "Bilateral, at least annually", "Italian signatory", "LIBB CH / AOD" },
            segmenti);
    }

    [Fact]
    public void Il_titolo_di_un_blocco_esce()
    {
        Assert.Equal(new[] { "Reduced coordination" }, TextSegmenter.SplitJson("""{"title":"Reduced coordination"}"""));
    }

    [Fact]
    public void Il_testo_alternativo_di_un_immagine_esce_ma_lo_sha_no()
    {
        var segmenti = TextSegmenter.SplitJson("""{"mediaId":"a1b2c3","alt":"Flusso di rullaggio","width":800,"height":600}""");
        Assert.Equal(new[] { "Flusso di rullaggio" }, segmenti);
    }

    [Fact]
    public void Gli_identificatori_e_gli_interruttori_non_escono_mai()
    {
        // L'elenco è di ciò che SI TRADUCE, non di ciò che si salta: una chiave nuova che nessuno ha
        // classificato resta intatta. Qui `tableId`, `r`, `unified`, `primary`, `star` devono sparire dai
        // segmenti — tradurli romperebbe il documento invece di renderlo bilingue.
        const string json =
            """{"tableId":"cfg-1","unified":true,"rows":[{"r":"16R","primary":true,"star":false,"group":"Partenze","cells":["Testo"]}]}""";
        Assert.Equal(new[] { "Partenze", "Testo" }, TextSegmenter.SplitJson(json));
    }

    [Fact]
    public void Mappare_con_l_identita_non_cambia_il_contenuto()
    {
        // La proprietà che prova che la struttura non si perde per strada. Il confronto è fra i due JSON
        // RILETTI e non fra le due stringhe: la riserializzazione può cambiare spaziatura, e non è un difetto.
        var mappato = TextSegmenter.MapJson(TabellaVera, s => s);
        Assert.Equal(
            JsonSerializer.Serialize(JsonDocument.Parse(TabellaVera).RootElement),
            JsonSerializer.Serialize(JsonDocument.Parse(mappato!).RootElement));
    }

    [Fact]
    public void Mappare_traduce_le_celle_e_lascia_stare_il_resto()
    {
        var tradotto = TextSegmenter.MapJson(TabellaVera, s => s == "Review cycle" ? "Ciclo di revisione" : s);
        Assert.Contains("Ciclo di revisione", tradotto);
        Assert.Contains("\"unified\":false", tradotto);
        Assert.DoesNotContain("Review cycle", tradotto);
        Assert.Contains("LIBB CH / AOD", tradotto);   // non toccata: non era nella mappa
    }

    [Fact]
    public void Un_corpo_che_non_capiamo_non_si_tocca()
    {
        // Un blocco può avere un corpo che questo formato non descrive. Restituirlo intatto è la risposta
        // giusta; protestare renderebbe non pubblicabile un documento per un campo che non riguarda noi.
        const string nonJson = "questo non e' json {{{";
        Assert.Equal(nonJson, TextSegmenter.MapJson(nonJson, _ => "TRADOTTO"));
        Assert.Empty(TextSegmenter.SplitJson(nonJson));
        Assert.Empty(TextSegmenter.SplitJson(null));
    }
}
