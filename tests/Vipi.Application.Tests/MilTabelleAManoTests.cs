using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le tre tabelle scritte a mano del vSOP militare (carta <c>2026-08-27-vsop-militari.md</c> §12g e §12h):
/// «Nominativi», «Parcheggi» e le attività delle «Aree di lavoro».
///
/// <para>⚠️ Sono <b>diverse</b> dalle altre due: qui non c'è niente da risolvere su un catalogo — il
/// contenuto è il payload, il payload è nel documento, e la release lo fotografa già copiando i blocchi. Per
/// questo restano <c>Editorial</c> mentre «Radioassistenze» e «Aeroporti alternati» sono <c>Derived</c>.</para>
/// </summary>
public class MilTabelleAManoTests
{
    // ---- Colonne fisse, celle libere -------------------------------------------------------------------

    [Fact]
    public void Le_righe_conservano_ordine_e_celle()
    {
        var json = MilTablePayload.Scrivi(MilTablePayload.Nominativi, new[]
        {
            new[] { "13° Gruppo", "IBIS", "IAM 1234", "QRA 01" },
            new[] { "101° Gruppo", "SPARVIERO", "IAM 5678", "" },
        }, 4);

        var righe = MilTablePayload.Leggi(json, 4);
        Assert.Equal(2, righe.Count);
        Assert.Equal("13° Gruppo", righe[0][0]);
        Assert.Equal("SPARVIERO", righe[1][1]);
        Assert.Equal("", righe[1][3]);
    }

    /// <summary>
    /// ⚠️ Le righe si portano al numero di colonne del profilo. Serve il giorno che una colonna si aggiunge o
    /// si toglie: in una tabella HTML una riga più corta dell'intestazione <b>non</b> lascia una cella vuota,
    /// <b>sposta tutto a sinistra</b> — e il dato sembrerebbe sbagliato invece che incompleto.
    /// </summary>
    [Fact]
    public void Una_riga_corta_o_lunga_si_porta_al_numero_di_colonne()
    {
        var json = MilTablePayload.Scrivi(MilTablePayload.Parcheggi, new[] { new[] { "Nord", "1-12" } }, 3);

        var riga = Assert.Single(MilTablePayload.Leggi(json, 3));
        Assert.Equal(3, riga.Count);
        Assert.Equal("", riga[2]);

        // E al contrario: quel che avanza si taglia, invece di sfondare l'intestazione.
        Assert.Equal(2, MilTablePayload.Leggi(json, 2).Single().Count);
    }

    /// <summary>
    /// ⚠️ <b>Una riga vuota è una riga, e si salva.</b> Qui c'era la regola opposta, e la verifica dal vivo
    /// del 30 agosto 2026 ha mostrato che cosa produce: «Aggiungi riga» <b>non aggiungeva niente</b>. La riga
    /// nuova nasce vuota per definizione, il salvataggio la scartava, il ricarico non la trovava, e il tasto
    /// sembrava rotto — senza un errore da nessuna parte.
    /// </summary>
    [Fact]
    public void Una_riga_vuota_e_una_riga_e_si_salva()
    {
        var json = MilTablePayload.Scrivi(MilTablePayload.Parcheggi, new[] { new[] { "", "", "" } }, 3);

        Assert.NotNull(json);
        var riga = Assert.Single(MilTablePayload.Leggi(json, 3));
        Assert.Equal(new[] { "", "", "" }, riga);
    }

    /// <summary>Nessuna riga, invece, è null: in archivio «la tabella è vuota» ha una forma sola.</summary>
    [Fact]
    public void Nessuna_riga_si_salva_come_null() =>
        Assert.Null(MilTablePayload.Scrivi(MilTablePayload.Parcheggi, Array.Empty<string[]>(), 3));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("non json")]
    public void Un_payload_illeggibile_da_zero_righe(string? json) =>
        Assert.Empty(MilTablePayload.Leggi(json, 3));

    /// <summary>
    /// ⚠️ Le due sezioni restano <b>editoriali</b> e diventano scheda+blocchi: il contenuto è tutto nel
    /// payload, quindi non c'è nessuna derivazione da congelare — ed è la differenza con le altre due tabelle.
    /// </summary>
    [Theory]
    [InlineData("callsigns")]
    [InlineData("parkings")]
    public void Le_sezioni_a_mano_sono_editoriali_e_tengono_la_prosa(string chiave)
    {
        Assert.Equal(SectionKind.Editorial, SectionCatalog.KindOf(chiave));
        Assert.True(SectionCatalog.IsHostRendered(SectionProfile.AirportMil, chiave));
        Assert.True(SectionCatalog.KeepsOwnBlocks(SectionProfile.AirportMil, chiave));
        Assert.False(SectionCatalog.IsRenderModeToggleable(chiave));
    }

    // ---- Le attività delle aree di lavoro --------------------------------------------------------------

    [Theory]
    [InlineData(MilActivity.AirToAir, "A/A")]
    [InlineData(MilActivity.AirToGround, "A/G")]
    [InlineData(MilActivity.AirToAir | MilActivity.AirToGround, "A/A - A/G")]
    [InlineData(MilActivity.None, "")]
    public void L_attivita_si_scrive_cosi(MilActivity a, string atteso) =>
        Assert.Equal(atteso, MilActivityText.Scrivi(a));

    /// <summary>
    /// ⚠️ L'ordine è quello del CATALOGO, non quello in cui si sono accesi i gettoni: due aree con le stesse
    /// attività devono scriverle uguali, o a schermo sembrano diverse.
    /// </summary>
    [Fact]
    public void Le_attivita_si_leggono_nell_ordine_del_catalogo()
    {
        var a = MilActivity.LowLevel | MilActivity.AirToAir | MilActivity.Cas;

        Assert.Equal("A/A - CAS - LOW LEVEL", MilActivityText.Scrivi(a));
        Assert.Equal("AA-CAS-LOWLEVEL", MilActivityText.Chiave(a));
    }

    /// <summary>
    /// Ogni attività va e torna dalla sua chiave, una per una.
    ///
    /// <para>⚠️ Il test gira sul CATALOGO invece di elencare i casi: un'attività aggiunta all'enum e
    /// dimenticata nel catalogo non si scriverebbe e non si rileggerebbe — silenziosamente, perché a schermo
    /// il suo gettone semplicemente non comparirebbe. Le due liste devono coprirsi, e qui si vede.</para>
    /// </summary>
    [Fact]
    public void Ogni_attivita_va_e_torna_dalla_sua_chiave()
    {
        foreach (var (flag, etichetta, chiave) in MilActivityText.Catalogo)
        {
            Assert.Equal(chiave, MilActivityText.Chiave(flag));
            Assert.Equal(etichetta, MilActivityText.Scrivi(flag));
            Assert.Equal(flag, MilActivityText.Leggi(chiave));
            // La chiave non può contenere il separatore, o rileggerla la spezzerebbe in due pezzi ignoti.
            Assert.DoesNotContain('-', chiave);
            Assert.DoesNotContain(' ', chiave);
        }

        // Il catalogo copre TUTTO l'enum (None a parte) e non ha doppioni: ogni bit una riga, ogni riga un bit.
        var nelCatalogo = MilActivityText.Catalogo.Select(v => v.Flag).ToList();
        var nellEnum = Enum.GetValues<MilActivity>().Where(v => v != MilActivity.None).ToList();
        Assert.Equal(nellEnum.OrderBy(v => (int)v), nelCatalogo.OrderBy(v => (int)v));
        Assert.Equal(nelCatalogo.Count, nelCatalogo.Distinct().Count());
        Assert.Equal(MilActivityText.Catalogo.Count, MilActivityText.Catalogo.Select(v => v.Chiave).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// ⚠️ Quel che è già salvato si rilegge: fino all'8 settembre 2026 le attività erano due sole e nei
    /// documenti c'è scritto <c>AA</c>, <c>AG</c>, <c>AA-AG</c>. L'ordine dei pezzi non conta mai.
    /// </summary>
    [Theory]
    [InlineData("AA", MilActivity.AirToAir)]
    [InlineData("AG", MilActivity.AirToGround)]
    [InlineData("AA-AG", MilActivity.AirToAir | MilActivity.AirToGround)]
    [InlineData("AG-AA", MilActivity.AirToAir | MilActivity.AirToGround)]
    [InlineData("", MilActivity.None)]
    [InlineData(null, MilActivity.None)]
    public void Le_attivita_gia_salvate_si_rileggono(string? chiave, MilActivity atteso) =>
        Assert.Equal(atteso, MilActivityText.Leggi(chiave));

    /// <summary>
    /// ⚠️ Un pezzo che non si conosce si SALTA, non butta via la riga: un documento scritto da una versione
    /// che ha un'attività in più deve leggersi lo stesso, meno quella voce.
    /// </summary>
    [Fact]
    public void Un_attivita_sconosciuta_non_porta_via_le_altre() =>
        Assert.Equal(MilActivity.Cap | MilActivity.Para, MilActivityText.Leggi("CAP-QUELCHESIA-PARA"));

    /// <summary>⚠️ Nel JSON si salva la PAROLA e non il numero dei flag: un documento si legge anche in SQL
    /// davanti a un incidente, e <c>3</c> non dice niente.</summary>
    [Fact]
    public void Nel_json_l_attivita_e_una_parola()
    {
        var json = MilRegulatedPayload.Scrivi(Selezione("A1"), Attivita(("A1", MilActivity.AirToAir | MilActivity.AirToGround)), Note());

        Assert.Contains("\"AA-AG\"", json);
        Assert.DoesNotContain("\"3\"", json);
    }

    /// <summary>
    /// ⚠️ Il cuore della sezione: selezione e attività stanno nello STESSO oggetto, e il lettore condiviso —
    /// quello che usano anche la vIPI ACC e l'APP — continua a leggere la selezione senza sapere niente
    /// delle attività. Se si rompesse, le aree sparirebbero da tre famiglie di documenti.
    /// </summary>
    [Fact]
    public void Il_lettore_condiviso_legge_ancora_la_selezione()
    {
        var json = MilRegulatedPayload.Scrivi(Selezione("A1", "A2"), Attivita(("A1", MilActivity.AirToAir)), Note());

        var sel = RegulatedSelectionJson.Parse(json);
        Assert.Equal(new[] { "A1", "A2" }, sel.OwnIds);
        Assert.Equal(MilActivity.AirToAir, MilRegulatedPayload.LeggiAttivita(json)["A1"]);
    }

    /// <summary>Le attività delle aree <b>non più scelte</b> si scartano: tenerle vorrebbe dire un payload che
    /// cresce a ogni ripensamento, e nessuno che sappia più quali righe contano.</summary>
    [Fact]
    public void L_attivita_di_un_area_tolta_non_si_conserva()
    {
        var json = MilRegulatedPayload.Scrivi(Selezione("A1"),
            Attivita(("A1", MilActivity.AirToAir), ("A2", MilActivity.AirToGround)), Note());

        var attivita = MilRegulatedPayload.LeggiAttivita(json);
        Assert.True(attivita.ContainsKey("A1"));
        Assert.False(attivita.ContainsKey("A2"));
    }

    /// <summary>Le attività si tengono per ID: un'area tolta e rimessa ritrova la sua, e l'ordine delle chip
    /// non c'entra niente.</summary>
    [Fact]
    public void Le_attivita_seguono_l_area_e_non_la_posizione()
    {
        var json = MilRegulatedPayload.Scrivi(Selezione("A1", "A2"),
            Attivita(("A2", MilActivity.AirToGround)), Note());

        var riletto = MilRegulatedPayload.Scrivi(Selezione("A2", "A1"), MilRegulatedPayload.LeggiAttivita(json), Note());

        Assert.Equal(MilActivity.AirToGround, MilRegulatedPayload.LeggiAttivita(riletto)["A2"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("non json")]
    [InlineData("""{"OwnIds":["A1"]}""")]
    public void Senza_attivita_nel_json_non_ce_ne_sono(string? json) =>
        Assert.Empty(MilRegulatedPayload.LeggiAttivita(json));

    // ---- Le note delle aree di lavoro ------------------------------------------------------------------

    /// <summary>La nota va e torna, e sta nello stesso oggetto della selezione e delle attività.</summary>
    [Fact]
    public void La_nota_di_un_area_si_salva_e_si_rilegge()
    {
        var json = MilRegulatedPayload.Scrivi(Selezione("A1", "A2"),
            Attivita(("A1", MilActivity.Cas)), Note(("A1", "Ingresso solo da nord, coordinare con LIBB.")));

        Assert.Equal("Ingresso solo da nord, coordinare con LIBB.", MilRegulatedPayload.LeggiNote(json)["A1"]);
        Assert.Equal(MilActivity.Cas, MilRegulatedPayload.LeggiAttivita(json)["A1"]);
        Assert.Equal(new[] { "A1", "A2" }, RegulatedSelectionJson.Parse(json).OwnIds);
        Assert.False(MilRegulatedPayload.LeggiNote(json).ContainsKey("A2"));
    }

    /// <summary>Come le attività: la nota di un'area non più scelta si scarta, e una nota vuota non si salva.</summary>
    [Fact]
    public void Le_note_vuote_e_quelle_delle_aree_tolte_non_si_conservano()
    {
        var json = MilRegulatedPayload.Scrivi(Selezione("A1", "A3"), Attivita(),
            Note(("A1", "  vale  "), ("A2", "area tolta"), ("A3", "   ")));

        var note = MilRegulatedPayload.LeggiNote(json);
        Assert.Equal("vale", note["A1"]);          // e gli spazi ai bordi si tagliano
        Assert.False(note.ContainsKey("A2"));
        Assert.False(note.ContainsKey("A3"));
    }

    /// <summary>
    /// ⚠️ Il lettore condiviso — quello della vIPI ACC e dell'APP — continua a leggere la selezione senza
    /// sapere niente delle note, esattamente come non sa niente delle attività. Le proprietà sconosciute si
    /// ignorano, ed è quel che tiene un oggetto solo invece di tre blocchi da salvare in fila.
    /// </summary>
    [Fact]
    public void Il_lettore_condiviso_non_si_accorge_delle_note()
    {
        var json = MilRegulatedPayload.Scrivi(Selezione("A1"), Attivita(), Note(("A1", "prosa")));

        Assert.Equal(new[] { "A1" }, RegulatedSelectionJson.Parse(json).OwnIds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("non json")]
    [InlineData("""{"OwnIds":["A1"],"activities":{"A1":"AA"}}""")]
    public void Senza_note_nel_json_non_ce_ne_sono(string? json) =>
        Assert.Empty(MilRegulatedPayload.LeggiNote(json));

    private static RegulatedSelection Selezione(params string[] ids) =>
        new() { OwnAuto = false, OwnIds = ids.ToList() };

    private static Dictionary<string, MilActivity> Attivita(params (string Id, MilActivity A)[] voci) =>
        voci.ToDictionary(v => v.Id, v => v.A);

    private static Dictionary<string, string> Note(params (string Id, string Testo)[] voci) =>
        voci.ToDictionary(v => v.Id, v => v.Testo);
}
